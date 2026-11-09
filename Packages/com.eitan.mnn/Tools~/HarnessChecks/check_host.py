#!/usr/bin/env python3
"""Exercise the real .NET host over HTTP with a deterministic external ACP process."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import json
import os
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
from pathlib import Path

SAMPLE = PACKAGE_ROOT
ROOT = PROJECT_ROOT
OUT = ROOT / 'TestArtifacts~/SiriAIHarness/HostChecks'
OUT.mkdir(parents=True, exist_ok=True)
TOKEN = uuid.uuid4().hex + uuid.uuid4().hex
OTHER_TOKEN = uuid.uuid4().hex + uuid.uuid4().hex
ENV = dict(os.environ, SIRIAI_HOST_CHECK_TOKEN=TOKEN, SIRIAI_HOST_OTHER_TOKEN=OTHER_TOKEN)
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    PORT = sock.getsockname()[1]
URL = f'http://127.0.0.1:{PORT}'
CONFIG = OUT / 'host.json'
DATA = OUT / ('data-' + uuid.uuid4().hex)
CONFIG.write_text(json.dumps(dict(
    Listen=URL, DataDirectory=str(DATA), MaximumConcurrent=2,
    Identities=[dict(OwnerId='alice', TokenEnvironmentVariable='SIRIAI_HOST_CHECK_TOKEN'),
                dict(OwnerId='bob', TokenEnvironmentVariable='SIRIAI_HOST_OTHER_TOKEN')],
    Workspaces=dict(test=str(OUT)), Harnesses=[dict(Id='fixture', Protocol='acp', Command=sys.executable,
        Arguments=['-u', str(SAMPLE / 'Tools~/HarnessChecks/Fixtures/acp_fixture.py')])]), indent=2))
HOST = ROOT / 'TestArtifacts~/SiriAIHarness/Host/bin/Debug/net8.0/SiriAI.HarnessHost.dll'
records = []
process = None
log = None


def call(path, data=None, token=TOKEN, expected=200):
    body = None if data is None else json.dumps(data).encode()
    request = urllib.request.Request(URL + path, body, headers={
        'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            status, raw = response.status, response.read()
    except urllib.error.HTTPError as error:
        status, raw = error.code, error.read()
    assert status == expected, (path, status, raw)
    return json.loads(raw) if raw else None


def wait_for(test, timeout=10):
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        value = test()
        if value:
            return value
        time.sleep(0.03)
    raise AssertionError('Timed out waiting for observed host state')


def start():
    global process, log
    log = (OUT / ('host-' + uuid.uuid4().hex + '.log')).open('w')
    process = subprocess.Popen(['dotnet', str(HOST), '--config', str(CONFIG)], env=ENV, stdout=log, stderr=log)
    def ready():
        if process.poll() is not None:
            raise AssertionError('Host exited: ' + str(process.returncode))
        try:
            return call('/v1/tasks') is not None
        except urllib.error.URLError:
            return False
    wait_for(ready)


def stop():
    global process
    if process is not None:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
        process = None
        log.close()


def submit(text, key):
    return call('/v1/tasks', dict(ConversationId='phone-chat', SubmissionKey=key, HarnessId='fixture',
                                WorkspaceKey='test', Objective=text), expected=202)


def completed(task_id):
    task = call('/v1/tasks/' + task_id)
    return task if task['State'] in [3, 4, 5, 6] else None


def passed(message):
    records.append(dict(result='PASS', behavior=message))
    print('PASS ' + message, flush=True)


try:
    start()
    call('/v1/tasks', token='invalid', expected=401)
    call('/v1/tasks', dict(ConversationId='phone-chat', SubmissionKey='unsupported-attachments', HarnessId='fixture',
        WorkspaceKey='test', Objective='Inspect the input', Attachments=[dict(Kind=0, Payload='attachment')]), expected=422)
    assert call('/v1/tasks') == []
    passed('unsupported ACP input attachments are rejected before admission or backend execution')
    first = submit('slow', 'phone-call')
    assert first['State'] in [0, 1]
    assert call('/v1/tasks/' + first['Id'])['State'] == 1
    assert submit('slow', 'phone-call')['Id'] == first['Id']
    call('/v1/tasks/' + first['Id'], token=OTHER_TOKEN, expected=404)
    passed('nonblocking HTTP admission, duplicate suppression and owner authorization')
    # The requesting connection is now gone. No request token is kept by the task.
    result = wait_for(lambda: completed(first['Id']))
    assert result['State'] == 3 and result['Result'] == 'fixture result: slow', result
    passed('external ACP process completes after the submitting client disconnects')
    client = subprocess.run(['mono', str(ROOT / 'TestArtifacts~/SiriAIHarness/Checks/HarnessChecks.exe'), '--remote', URL],
                            env=ENV, text=True, capture_output=True, timeout=15)
    assert client.returncode == 0, client.stdout + client.stderr
    passed('Unity-compatible C# remote client reads actual host task DTOs')
    stop()
    start()
    assert call('/v1/tasks/' + first['Id'])['Result'] == result['Result']
    claim = call('/v1/results/claim', dict(ConversationId='phone-chat', ClientId='new-phone', LeaseSeconds=60))
    assert claim['Id'] == first['Id']
    call('/v1/tasks/' + first['Id'] + '/renew', dict(LeaseId='wrong-lease', LeaseSeconds=120), expected=404)
    renewed = call('/v1/tasks/' + first['Id'] + '/renew', dict(LeaseId=claim['LeaseId'], LeaseSeconds=120))
    assert renewed['LeaseId'] == claim['LeaseId'] and renewed['LeaseUntilUtcTicks'] > claim['LeaseUntilUtcTicks']
    passed('HTTP delivery lease renewal preserves identity and rejects a mismatched lease')
    call('/v1/tasks/' + first['Id'] + '/receipt', dict(LeaseId=claim['LeaseId'], Receipt=1))
    call('/v1/results/claim', dict(ConversationId='phone-chat', ClientId='desktop', LeaseSeconds=60), expected=204)
    passed('host restart retains completed result and playback-start prevents automatic replay')
    task = submit('permission', 'permission-call')
    def pending():
        current = call('/v1/tasks/' + task['Id'])
        return current if current['WaitingFor'] == 2 else None
    question = wait_for(pending)
    call('/v1/tasks/' + task['Id'] + '/respond', dict(RequestId='stale', Text='yes', Allow=True), expected=409)
    call('/v1/tasks/' + task['Id'] + '/respond', dict(RequestId=question['RequestId'], Text='allow once', Allow=True))
    result = wait_for(lambda: completed(task['Id']))
    assert result['Result'] == 'fixture result: approved', result
    passed('bidirectional ACP permission routed by exact request identity')
    task = submit('slow', 'cancel-call')
    result = call('/v1/tasks/' + task['Id'] + '/cancel', {})
    assert result['State'] == 5, result
    passed('ACP cancel waits for backend cancelled stop reason')
    task = submit('slow', 'restart-call')
    stop()
    start()
    assert call('/v1/tasks/' + task['Id'])['State'] == 6
    passed('host interruption is explicit and never blindly replays work')
finally:
    stop()
    (OUT / 'results.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n')
