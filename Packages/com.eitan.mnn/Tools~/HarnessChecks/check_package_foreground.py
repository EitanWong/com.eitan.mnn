#!/usr/bin/env python3
"""Run package-owned frontend tools/feedback/observation against a real independent Host and ACP fixture."""
import argparse
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import time
import urllib.error
import urllib.request
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', required=True, type=Path)
    parser.add_argument('--dotnet', required=True, type=Path)
    parser.add_argument('--runtime', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    output = args.output.resolve()
    if 'TestArtifacts~' not in output.parts:
        parser.error('Outputs must remain under TestArtifacts~/.')
    output.mkdir(parents=True, exist_ok=True)
    run = output / ('run-' + uuid.uuid4().hex)
    package = run / 'Packages/com.eitan.mnn'
    source = args.source_root.resolve() / 'Packages/com.eitan.mnn'
    for relative in ['Runtime/Core/Language/Agent/Harnesses', 'Tools~/HarnessCore', 'Tools~/HarnessHost']:
        if (source / relative).exists():
            shutil.copytree(source / relative, package / relative)
    consumer = run / 'Consumer'
    consumer.mkdir(parents=True)
    checks = Path(__file__).parent
    shutil.copyfile(checks / 'Fixtures/ForegroundConsumer/Program.cs', consumer / 'Program.cs')
    shutil.copyfile(checks / 'Fixtures/PackageConsumer/Consumer.csproj', consumer / 'Consumer.csproj')
    assert not (run / 'Assets').exists()
    token = uuid.uuid4().hex * 2
    env = dict(os.environ, MNN_PACKAGE_FOREGROUND_TOKEN=token, DOTNET_CLI_HOME=str(output / 'cli'),
               DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    records = []
    host = None
    log = None

    def command(cmd):
        started = time.monotonic()
        result = subprocess.run(cmd, cwd=run, env=env, capture_output=True, text=True, timeout=150)
        assert token not in result.stdout + result.stderr
        record = dict(command=cmd, stdout=result.stdout, stderr=result.stderr, exit_code=result.returncode,
                      elapsed_seconds=time.monotonic() - started)
        records.append(record)
        (output / 'commands.json').write_text(json.dumps(records, indent=2) + '\n')
        print(result.stdout + result.stderr, end='', flush=True)
        return result

    try:
        build = command([str(args.dotnet), 'build', str(consumer / 'Consumer.csproj'), '--nologo', '--no-incremental',
                         '-p:HarnessPackageRoot=' + str(package), '-p:HarnessCoreArtifactsRoot=' + str(run / 'CoreBuild') + '/'])
        if build.returncode:
            return build.returncode
        build = command([str(args.dotnet), 'build', str(package / 'Tools~/HarnessHost/SiriAI.HarnessHost.csproj'),
                         '--nologo', '--no-incremental', '-p:HarnessArtifactsRoot=' + str(run / 'HostBuild') + '/'])
        if build.returncode:
            return build.returncode
        fixture = run / 'independent-acp.py'
        shutil.copyfile(checks / 'Fixtures/acp_fixture.py', fixture)
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0))
            url = 'http://127.0.0.1:' + str(sock.getsockname()[1])
        config = run / 'host.json'
        import sys
        config.write_text(json.dumps(dict(Listen=url, DataDirectory=str(run / 'data'),
            Identities=[dict(OwnerId='owner', TokenEnvironmentVariable='MNN_PACKAGE_FOREGROUND_TOKEN')],
            Workspaces=dict(test=str(run)), Harnesses=[dict(Id='acp', Protocol='acp', Command=sys.executable,
            Arguments=['-u', str(fixture)])])))
        log = (run / 'host.log').open('w')
        runtime = args.runtime or Path(shutil.which('dotnet') or str(args.dotnet))
        host = subprocess.Popen([str(runtime), str(run / 'HostBuild/bin/Debug/net8.0/SiriAI.HarnessHost.dll'),
            '--config', str(config)], cwd=run, env=env, stdout=log, stderr=log)
        deadline = time.monotonic() + 15
        while True:
            assert host.poll() is None, 'Host exited: ' + str(host.returncode)
            request = urllib.request.Request(url + '/v1/harnesses', headers={'Authorization':'Bearer ' + token})
            try:
                with urllib.request.urlopen(request, timeout=2) as response:
                    assert response.status == 200
                break
            except urllib.error.URLError:
                if time.monotonic() > deadline:
                    raise AssertionError('Host did not start')
                time.sleep(.03)
        result = command([str(args.dotnet), str(consumer / 'bin/Debug/net10.0/Consumer.dll'), url, str(run / 'journal')])
        if result.returncode:
            return result.returncode
        assert result.stdout.count('PASS ') == 9
        (output / 'results.json').write_text(json.dumps(dict(run=str(run), package_only=True,
            assets_directory_exists=False, passed=9, real_vendor=False, physical_playback=False), indent=2) + '\n')
        return 0
    finally:
        if host:
            host.terminate()
            try:
                host.wait(timeout=10)
            except subprocess.TimeoutExpired:
                host.kill()
                host.wait(timeout=5)
        if log:
            log.close()


if __name__ == '__main__':
    raise SystemExit(main())
