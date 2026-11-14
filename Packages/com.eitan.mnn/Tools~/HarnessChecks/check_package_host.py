#!/usr/bin/env python3
"""Copy only package sources, build the real Host, and check an external ACP task over HTTP."""
import argparse
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--dotnet', required=True, type=Path)
    parser.add_argument('--runtime', type=Path, help='Runtime with .NET8 installed; defaults to dotnet on PATH.')
    args = parser.parse_args()
    runtime = args.runtime or Path(shutil.which('dotnet') or str(args.dotnet))
    output = args.output.resolve()
    if 'TestArtifacts~' not in output.parts:
        parser.error('Outputs must remain under TestArtifacts~/.')
    output.mkdir(parents=True, exist_ok=True)
    run = output / ('run-' + uuid.uuid4().hex)
    package = run / 'Packages/com.eitan.mnn'
    source = args.source_root.resolve() / 'Packages/com.eitan.mnn'
    package.mkdir(parents=True)
    for relative in ['Runtime/Core/Language/Agent/Harnesses', 'Tools~/HarnessCore', 'Tools~/HarnessHost']:
        if (source / relative).exists():
            shutil.copytree(source / relative, package / relative)
    assert not (run / 'Assets').exists()
    token = uuid.uuid4().hex * 2
    environment = dict(os.environ, DOTNET_CLI_HOME=str(output / 'cli'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
                       DOTNET_GENERATE_ASPNET_CERTIFICATE='false', MNN_PACKAGE_HOST_TOKEN=token)
    records = []
    passed = []
    host = None
    log = None

    def ok(message):
        passed.append(message)
        print('PASS ' + message, flush=True)

    try:
        command = [str(args.dotnet), 'build', str(package / 'Tools~/HarnessHost/SiriAI.HarnessHost.csproj'),
                   '--nologo', '--no-incremental', '-p:HarnessArtifactsRoot=' + str(run / 'Build') + '/']
        build = subprocess.run(command, env=environment, cwd=run, text=True, capture_output=True, timeout=120)
        records.append(dict(command=command, stdout=build.stdout, stderr=build.stderr, exit_code=build.returncode))
        print(build.stdout + build.stderr, end='', flush=True)
        if build.returncode:
            return build.returncode
        fixture = run / 'independent-acp.py'
        shutil.copyfile(Path(__file__).parent / 'Fixtures/acp_fixture.py', fixture)
        workspace = run / 'workspace'
        workspace.mkdir()
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0))
            url = 'http://127.0.0.1:' + str(sock.getsockname()[1])
        config = run / 'host.json'
        config.write_text(json.dumps(dict(Listen=url, DataDirectory=str(run / 'data'),
            Identities=[dict(OwnerId='owner', TokenEnvironmentVariable='MNN_PACKAGE_HOST_TOKEN')],
            Workspaces=dict(test=str(workspace)), Harnesses=[dict(Id='acp', Protocol='acp', Command=sys.executable,
            Arguments=['-u', str(fixture)])])))

        def call(path, body=None, expected=200):
            request = urllib.request.Request(url + path, None if body is None else json.dumps(body).encode(),
                headers={'Authorization':'Bearer ' + token, 'Content-Type':'application/json'})
            try:
                with urllib.request.urlopen(request, timeout=5) as response:
                    status, raw = response.status, response.read()
            except urllib.error.HTTPError as error:
                status, raw = error.code, error.read()
            records.append(dict(path=path, input=body, status=status, output=raw.decode()))
            assert status == expected and token not in raw.decode(), (status, raw)
            return json.loads(raw) if raw else None

        def wait(probe):
            deadline = time.monotonic() + 15
            while time.monotonic() < deadline:
                value = probe()
                if value:
                    return value
                time.sleep(.03)
            raise AssertionError('Package Host behavior timed out')

        def start():
            nonlocal host, log
            log = (run / ('host-' + uuid.uuid4().hex + '.log')).open('w')
            host = subprocess.Popen([str(runtime), str(run / 'Build/bin/Debug/net8.0/SiriAI.HarnessHost.dll'),
                '--config', str(config)], env=environment, cwd=run, stdout=log, stderr=log)
            def ready():
                assert host.poll() is None, 'Host exited: ' + str(host.returncode)
                try:
                    return call('/v1/harnesses')
                except urllib.error.URLError:
                    return None
            wait(ready)

        def stop():
            nonlocal host
            if host:
                host.terminate()
                try:
                    host.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    host.kill()
                    host.wait(timeout=5)
                host = None
                log.close()

        start()
        ok('package-only Host builds and starts without any SiriAI application sources')
        task = call('/v1/tasks', dict(ConversationId='phone', SubmissionKey='stable', HarnessId='acp',
                    WorkspaceKey='test', Objective='slow'), 202)
        assert task['State'] in [0, 1]
        # No foreground HTTP connection remains open during the independent external process execution.
        def completed():
            value = call('/v1/tasks/' + task['Id'])
            return value if value['State'] in [3, 4, 5, 6] else None
        result = wait(completed)
        assert result['State'] == 3 and result['Result'] == 'fixture result: slow', result
        ok('external ACP task completes after foreground disconnect with its exact backend result')
        stop()
        start()
        assert call('/v1/tasks/' + task['Id'])['Result'] == result['Result']
        claim = call('/v1/results/claim', dict(ConversationId='phone', ClientId='reconnected-phone', LeaseSeconds=60))
        assert claim['Id'] == task['Id'] and claim['Result'] == result['Result']
        call('/v1/tasks/' + task['Id'] + '/receipt', dict(LeaseId=claim['LeaseId'], Receipt=4))
        stop()
        start()
        call('/v1/results/claim', dict(ConversationId='phone', ClientId='phone-again', LeaseSeconds=60), 204)
        ok('Host restart delivers exact result to foreground and persists its presentation receipt')
        stop()
        return 0
    finally:
        if host:
            host.kill()
            host.wait(timeout=5)
            log.close()
        (output / 'commands.json').write_text(json.dumps(records, indent=2) + '\n')
        (output / 'results.json').write_text(json.dumps(dict(run=str(run), passed=passed,
            package_only=True, assets_directory_exists=False), indent=2) + '\n')


if __name__ == '__main__':
    raise SystemExit(main())
