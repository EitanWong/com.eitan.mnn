#!/usr/bin/env python3
"""Observe installed CLI versions through the real Host and its public HTTP contract."""
from pathlib import Path
import argparse
import json
import os
import signal
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--host', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
run = args.output.resolve() / ('run-' + uuid.uuid4().hex)
run.mkdir(parents=True)
token, bob = uuid.uuid4().hex * 2, uuid.uuid4().hex * 2
env = dict(os.environ, VERSION_HOST_TOKEN=token, VERSION_BOB_TOKEN=bob, PRIVATE_PROVIDER_TOKEN='private-not-forwarded',
           VERSION_TEST_FORWARD='explicit-test-forward')
requirements = {'qwen': '0.21.6', 'opencode': '1.18.0', 'minimax': '0.3.7', 'kimi': '0.31.0',
                'deepseek': '0.1.5', 'pi': '0.80.4'}
harnesses, events, records, passed = [], {}, [], []
fixture = Path(__file__).parent / 'Fixtures/backend_version_fixture.py'


def create(identity, backend, version, mode='stdout', exit=0):
    folder = run / ('tool with spaces ' + identity)
    folder.mkdir()
    executable = folder / (backend + '.py')
    executable.write_text('#!' + sys.executable + '\n' + fixture.read_text().split('\n', 1)[1])
    executable.chmod(0o755)
    event = folder / 'events.jsonl'
    executable.with_suffix('.json').write_text(json.dumps(dict(events=str(event), version=version, mode=mode, exit=exit)))
    events[identity] = event
    harness = dict(Id=identity, Backend=backend, Command=str(executable), ForwardEnvironment=['VERSION_TEST_FORWARD'])
    if backend == 'pi':
        native = folder / 'native pi.py'
        native.write_bytes(executable.read_bytes())
        native.chmod(0o755)
        native.with_suffix('.json').write_bytes(executable.with_suffix('.json').read_bytes())
        harness['NativeCommand'] = str(native)
    harnesses.append(harness)


for backend, minimum in requirements.items():
    parts = minimum.split('.')
    old = '.'.join(parts[:-1] + [str(int(parts[-1]) - 1)]) if int(parts[-1]) else str(int(parts[0])) + '.' + str(int(parts[1]) - 1) + '.99'
    create(backend + '-old', backend, old)
    create(backend + '-ok', backend, backend + ' ' + minimum + '-rc.3\nextra ignored output')
create('stderr', 'qwen', 'qwen 9.10.11', 'stderr')
create('invalid', 'qwen', 'not-a-version')
create('later-line', 'qwen', 'not-a-version\nqwen 99.99.99')
create('invalid-utf8', 'qwen', '', 'invalid-utf8')
create('empty', 'qwen', '')
create('exit', 'qwen', '99.99.99', exit=7)
create('huge', 'qwen', '9' * 200 + '.1.1')
create('hang', 'qwen', '', 'hang')
create('flood', 'qwen', '', 'flood')
create('no-minimum', 'hermes', '')
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]
url = 'http://127.0.0.1:' + str(port)
configuration = run / 'host.json'
configuration.write_text(json.dumps(dict(Listen=url, DataDirectory=str(run / 'data'),
    Identities=[dict(OwnerId='alice', TokenEnvironmentVariable='VERSION_HOST_TOKEN'), dict(OwnerId='bob', TokenEnvironmentVariable='VERSION_BOB_TOKEN')],
    Workspaces=dict(project=str(run)), Harnesses=harnesses)))
log = (run / 'host.log').open('w')
host = subprocess.Popen(['dotnet', str(args.host), '--config', str(configuration)], env=env, stdout=log, stderr=log)


def call(path, body=None, expected=200, credential=token):
    request = urllib.request.Request(url + path, None if body is None else json.dumps(body).encode(),
        headers={'Authorization': 'Bearer ' + credential, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(request, timeout=15) as response:
            status, data = response.status, response.read()
    except urllib.error.HTTPError as error:
        status, data = error.code, error.read()
    records.append(dict(path=path, input=body, status=status, stdout=data.decode()))
    assert status == expected, (status, data)
    assert token not in data.decode() and bob not in data.decode()
    return json.loads(data) if data else None


def wait(check, seconds=15):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        result = check()
        if result:
            return result
        time.sleep(.03)
    raise AssertionError('Expected version/protocol observation did not arrive')


def observed(identity):
    p = events[identity]
    return [json.loads(line) for line in p.read_text().splitlines()] if p.exists() else []


def probe(identity):
    return call('/v1/harnesses/' + identity + '/readiness', dict(WorkspaceKey='project'))


def ok(text):
    passed.append(text)
    print('PASS ' + text, flush=True)


try:
    def ready():
        assert host.poll() is None, (run / 'host.log').read_text()
        try:
            return call('/v1/harnesses')
        except urllib.error.URLError:
            return None
    wait(ready)
    for identity in events:
        assert call('/v1/harnesses/' + identity + '/readiness?workspace=project')['State'] == 'unknown'
        assert not observed(identity)
    ok('catalog and cached readiness are inventory-only and execute no CLI commands')
    for backend, minimum in requirements.items():
        identity = backend + '-old'
        result = probe(identity)
        assert result['State'] == 'unavailable' and result['Evidence'] == 'backend_version_too_old', 'MINIMUM_VERSION_MISSING: old ' + backend + ' incorrectly accepted: ' + json.dumps(result)
        assert result['BackendVersion']['Minimum'] == minimum and result['BackendVersion']['State'] == 'too_old'
        assert result['AcpCapabilities'] is None
        assert [item['mode'] for item in observed(identity) if 'mode' in item] == ['version']
        assert all('method' not in item for item in observed(identity))
        identity = backend + '-ok'
        result = probe(identity)
        assert result['State'] == 'protocol_verified' and result['Evidence'] == 'acp_initialize', result
        assert result['BackendVersion'] == dict(Version=minimum, Minimum=minimum, State='compatible'), result
        seen = observed(identity)
        assert [item['mode'] for item in seen if 'mode' in item] == ['version', 'protocol'], seen
        assert [item['method'] for item in seen if 'method' in item] == ['initialize']
        assert all(not item.get('host_token_leaked') and not item.get('unrelated_secret_leaked') for item in seen)
        assert all(item['forwarded'] == 'explicit-test-forward' for item in seen if 'mode' in item)
        ok(backend + ': exact upstream minimum, compatible prerelease tuple, too-old refusal before handshake; correct native executable and scoped environment')
    installations = {item['backend']: item for item in call('/v1/harnesses')['installations']}
    assert all(installations[backend]['minimumVersion'] == minimum for backend, minimum in requirements.items())
    ok('inventory exposes the exact static minimums without running installed tools')
    result = probe('stderr')
    assert result['State'] == 'protocol_verified' and result['BackendVersion']['Version'] == '9.10.11'
    ok('successful stderr-only version is parsed without exposing arbitrary process output')
    for identity in ['invalid-utf8', 'invalid', 'later-line', 'empty', 'exit', 'huge', 'hang', 'flood']:
        start = time.monotonic()
        result = probe(identity)
        assert result['State'] == 'unavailable' and result['Evidence'] == 'backend_version_unconfirmed', result
        assert result['BackendVersion'] == dict(Version=None, Minimum='0.21.6', State='unconfirmed'), result
        assert time.monotonic() - start < 7
        assert all('method' not in item for item in observed(identity))
        for item in observed(identity):
            for name in ['pid', 'child_pid']:
                if name in item:
                    def gone():
                        try:
                            os.kill(item[name], 0)
                            return False
                        except ProcessLookupError:
                            return True
                    wait(gone, 3)
        ok(identity + ': bounded failure is unconfirmed, no protocol start and no surviving process tree')
    result = probe('no-minimum')
    assert result['State'] == 'protocol_verified' and result.get('BackendVersion') is None
    assert [item['mode'] for item in observed('no-minimum') if 'mode' in item] == ['protocol']
    ok('backend without an upstream minimum retains its existing protocol-only readiness contract')
    assert call('/v1/harnesses/qwen-ok/readiness?workspace=project', credential=bob)['State'] == 'unknown'
    assert not call('/v1/tasks')
    count = sum(len(observed(identity)) for identity in events)
    call('/v1/harnesses/missing/readiness', dict(WorkspaceKey='project'), 404)
    call('/v1/harnesses/qwen-ok/readiness', dict(WorkspaceKey='bad'), 400)
    call('/v1/harnesses/qwen-ok/readiness', dict(WorkspaceKey='project'), 401, 'invalid')
    assert count == sum(len(observed(identity)) for identity in events)
    ok('version evidence is owner scoped and authentication/configuration validation precedes child processes')
finally:
    host.terminate()
    try:
        host.wait(timeout=10)
    except subprocess.TimeoutExpired:
        host.kill()
        host.wait(timeout=5)
    log.close()
    (run / 'http.json').write_text(json.dumps(records, indent=2) + '\n')
    (run / 'observed.json').write_text(json.dumps({key: observed(key) for key in events}, indent=2) + '\n')
    (args.output / 'results.json').write_text(json.dumps(dict(run=str(run), passed=passed, requirements=requirements), indent=2) + '\n')
