#!/usr/bin/env python3
"""Check queued holds, full active listings and result delivery through a real independent Host."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    out = args.output.resolve()
    if 'TestArtifacts~' not in out.parts:
        parser.error('Keep outputs under TestArtifacts~.')
    out.mkdir(parents=True, exist_ok=True)
    token, other = uuid.uuid4().hex * 2, uuid.uuid4().hex * 2
    env = dict(os.environ, SIRIAI_CONTROL_TOKEN=token, SIRIAI_CONTROL_OTHER_TOKEN=other)
    records = []
    process = log = None

    def command(cmd):
        result = subprocess.run(list(map(str, cmd)), capture_output=True, text=True, env=env, timeout=120)
        records.append(dict(command=list(map(str, cmd)), stdout=result.stdout, stderr=result.stderr, exit_code=result.returncode))
        print(result.stdout + result.stderr, end='', flush=True)
        if result.returncode:
            raise AssertionError('Command failed with exit ' + str(result.returncode))
        return result.stdout

    def record(message):
        records.append(dict(check=message, result='PASS'))
        print('PASS ' + message, flush=True)

    def call(path, data=None, identity=token, expected=200):
        request = urllib.request.Request(url + path, None if data is None else json.dumps(data).encode(),
            headers={'Authorization': 'Bearer ' + identity, 'Content-Type': 'application/json'})
        try:
            with urllib.request.urlopen(request, timeout=8) as response:
                status, raw = response.status, response.read()
        except urllib.error.HTTPError as error:
            status, raw = error.code, error.read()
        assert status == expected, (path, status, raw.decode())
        return json.loads(raw) if raw else None

    def wait(probe, seconds=12):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            value = probe()
            if value:
                return value
            time.sleep(.03)
        raise AssertionError('Timed out waiting for observed task state')

    def start():
        nonlocal process, log
        log = (out / ('host-' + uuid.uuid4().hex + '.log')).open('w')
        process = subprocess.Popen(['dotnet', str(host), '--config', str(config)], env=env, stdout=log, stderr=log)
        def ready():
            assert process.poll() is None, 'Host exited early'
            try:
                return call('/v1/tasks') is not None
            except urllib.error.URLError:
                return False
        wait(ready)

    def stop():
        nonlocal process, log
        if process is not None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
            process = None
            log.close()

    def submit(objective, conversation='controls'):
        return call('/v1/tasks', dict(ConversationId=conversation, SubmissionKey=uuid.uuid4().hex,
            HarnessId='fixture', WorkspaceKey='test', Objective=objective), expected=202)

    try:
        command(['dotnet', 'build', PACKAGE_ROOT / 'Tools~/HarnessHost/SiriAI.HarnessHost.csproj', '--nologo'])
        host = PROJECT_ROOT / 'TestArtifacts~/SiriAIHarness/Host/bin/Debug/net8.0/SiriAI.HarnessHost.dll'
        consumer = out / 'ControlConsumer.exe'
        command(['mcs', '-langversion:7.2', '-r:System.Runtime.Serialization', '-r:System.Net.Http',
            '-out:' + str(consumer), *sorted((PACKAGE_ROOT / 'Runtime/Core/Language/Agent/Harnesses').glob('*.cs')),
            Path(__file__).parent / 'Fixtures/TaskControlConsumer.cs'])
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
        url = 'http://127.0.0.1:' + str(port)
        config = out / 'host.json'
        config.write_text(json.dumps(dict(Listen=url, DataDirectory=str(out / ('data-' + uuid.uuid4().hex)), MaximumConcurrent=1,
            Identities=[dict(OwnerId='alice', TokenEnvironmentVariable='SIRIAI_CONTROL_TOKEN'),
                dict(OwnerId='bob', TokenEnvironmentVariable='SIRIAI_CONTROL_OTHER_TOKEN')], Workspaces=dict(test=str(out)),
            Harnesses=[dict(Id='fixture', Protocol='acp', Command=sys.executable,
                Arguments=['-u', str(Path(__file__).parent / 'Fixtures/acp_fixture.py')])]), indent=2))
        start()
        first = submit('permission')
        wait(lambda: call('/v1/tasks/' + first['Id'])['WaitingFor'] == 2)
        queued = [submit('queued-' + str(index), 'controls-' + str(index)) for index in range(12)]
        held = queued[0]['Id']
        text = command(['mono', consumer, url, held, queued[-1]['Id']])
        assert 'PASS all 13 active tasks' in text
        call('/v1/tasks/' + held + '/resume', {}, identity=other, expected=404)
        call('/v1/tasks/' + first['Id'] + '/pause', {}, expected=422)
        record('owner isolation and running-task suspension rejection')
        pending = call('/v1/tasks/' + first['Id'])
        call('/v1/tasks/' + first['Id'] + '/respond', dict(RequestId=pending['RequestId'], Text='Allow once', Allow=True))
        wait(lambda: all(task['State'] in (3, 5, 7) for task in call('/v1/tasks')))
        assert call('/v1/tasks/' + held)['State'] == 7
        record('other tasks finish while paused work never starts')
        stop(); start()
        assert call('/v1/tasks/' + held)['State'] == 7
        record('paused state survives an independent Host restart')
        call('/v1/tasks/' + held + '/resume', {})
        resumed = wait(lambda: (lambda task: task if task['State'] == 3 else None)(call('/v1/tasks/' + held)))
        assert resumed['Result'] == 'fixture result: queued-0' and resumed['ResultPending']
        call('/v1/tasks/' + held + '/resume', {}, expected=409)
        record('resume keeps original task identity and produces a pending foreground result without replay')
        task = call('/v1/results/claim', dict(ConversationId=resumed['ConversationId'], ClientId='control-display', LeaseSeconds=30))
        assert task is not None and task['ResultPending'] and task['LeaseId']
        call('/v1/tasks/' + task['Id'] + '/receipt', dict(LeaseId=task['LeaseId'], Receipt=4))
        assert not call('/v1/tasks/' + task['Id'])['ResultPending']
        record('completion requires an actual text display receipt before leaving the result outbox')
    finally:
        stop()
        (out / 'results.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
