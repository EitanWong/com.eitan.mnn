#!/usr/bin/env python3
"""Exercise the actual service CLI; native checks use a temporary per-user job."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse
import fcntl
import json
import os
import plistlib
import shutil
import socket
import stat
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
import xml.etree.ElementTree as ET
from pathlib import Path

SAMPLE = PACKAGE_ROOT
ROOT = PROJECT_ROOT
parser = argparse.ArgumentParser()
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--host', type=Path, default=ROOT / 'TestArtifacts~/SiriAIHarness/Host/bin/Debug/net8.0/SiriAI.HarnessHost.dll')
parser.add_argument('--native', action='store_true')
parser.add_argument('--probe', action='store_true')
parser.add_argument('--direct', action='store_true')
parser.add_argument('--client', type=Path)
args = parser.parse_args()
OUT = args.output.resolve()
OUT.mkdir(parents=True, exist_ok=True)
runtime_path = Path(shutil.which('dotnet')).resolve()
real_runtime = runtime_path.parent.parent / 'libexec/dotnet'
DOTNET = str(real_runtime if real_runtime.is_file() else runtime_path)
TOKEN, OTHER = uuid.uuid4().hex * 2, uuid.uuid4().hex * 2
ENV = dict(os.environ, SIRIAI_SERVICE_TOKEN=TOKEN, SIRIAI_SERVICE_OTHER=OTHER)
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    PORT = sock.getsockname()[1]
URL = 'http://127.0.0.1:' + str(PORT)
RUN = OUT / ('run-' + uuid.uuid4().hex)
RUN.mkdir()
CONFIG = RUN / 'input.json'
CONFIG.write_text(json.dumps(dict(Listen=URL, DataDirectory='data', MaximumConcurrent=2,
    Identities=[dict(OwnerId='alice', TokenEnvironmentVariable='SIRIAI_SERVICE_TOKEN'),
                dict(OwnerId='bob', TokenEnvironmentVariable='SIRIAI_SERVICE_OTHER')],
    Workspaces=dict(test='.'), Harnesses=[dict(Id='fixture', Protocol='acp', Command=sys.executable,
    Arguments=['-u', str(SAMPLE / 'Tools~/HarnessChecks/Fixtures/acp_fixture.py')])]), indent=2))
RECORDS, PASSED = [], []
REG = RUN / 'mac 空间 $ percent%'
LABEL = None
DIRECT_PROCESS = None
DIRECT_LOG = None


def cli(command, directory=REG, extra=(), expected=0, env=ENV):
    command = [DOTNET, str(args.host), '--service', command, '--directory', str(directory), *extra]
    result = subprocess.run(command, text=True, capture_output=True, env=env, timeout=50)
    for secret in [TOKEN, OTHER]:
        assert secret not in result.stdout + result.stderr, 'credential leaked to command output'
    RECORDS.append(dict(command=command, stdout=result.stdout, stderr=result.stderr, exit_code=result.returncode))
    (OUT / 'commands.json').write_text(json.dumps(RECORDS, indent=2, ensure_ascii=False))
    assert result.returncode == expected, RECORDS[-1]
    return json.loads(result.stdout) if expected == 0 else result.stderr


def prepare(directory=REG, platform='macos', config=CONFIG, expected=0):
    return cli('prepare', directory, ['--id', 'desktop-test', '--config', str(config), '--runtime', DOTNET,
        '--platform', platform], expected)


def passed(text):
    PASSED.append(text)
    print('PASS ' + text, flush=True)


def http(path, data=None, token=TOKEN, expected=200):
    request = urllib.request.Request(URL + path, None if data is None else json.dumps(data).encode(),
        headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(request, timeout=5) as response:
            code, data = response.status, response.read()
    except urllib.error.HTTPError as error:
        code, data = error.code, error.read()
    assert code == expected, (path, code, data)
    return json.loads(data) if data else None


def wait_for(test):
    end = time.monotonic() + 15
    while time.monotonic() < end:
        result = test()
        if result:
            return result
        time.sleep(.05)
    raise AssertionError('Timed out waiting for task')


try:
    first = prepare()
    LABEL = first['Label']
    passed('prepare produces a service registration without launching a host')
    if args.client:
        for verb in ['prepare', 'status']:
            command = ['mono', str(args.client), '--service-client', DOTNET, str(args.host), str(REG), verb]
            if verb == 'prepare': command.append(str(CONFIG))
            result = subprocess.run(command, text=True, capture_output=True, timeout=50, env=ENV)
            RECORDS.append(dict(command=command, stdout=result.stdout, stderr=result.stderr, exit_code=result.returncode))
            assert result.returncode == 0, RECORDS[-1]
            assert TOKEN not in result.stdout + result.stderr and OTHER not in result.stdout + result.stderr
            value = json.loads(result.stdout)
            assert value['Label'] == LABEL
        passed('Unity-compatible C# process client prepares and reads actual CLI with Unicode/spaces/metacharacters')
    if not args.probe:
        snapshot = {p.name: p.read_bytes() for p in REG.iterdir() if p.name != 'prepare.lock'}
        prepare()
        assert snapshot == {p.name: p.read_bytes() for p in REG.iterdir() if p.name != 'prepare.lock'}
        config = json.loads((REG / 'host.json').read_text())
        assert config['DataDirectory'] == str(RUN / 'data') and config['Workspaces']['test'] == str(RUN)
        assert stat.S_IMODE(REG.stat().st_mode) == 0o700
        assert all(stat.S_IMODE(p.stat().st_mode) == 0o600 for p in REG.iterdir())
        definition = plistlib.loads((REG / 'host.plist').read_bytes())
        assert definition['ProgramArguments'] == [DOTNET, str(args.host), '--service', 'run', '--directory', str(REG)]
        assert TOKEN not in (REG / 'host.plist').read_text() + (REG / 'registration.json').read_text() + (REG / 'host.json').read_text()
        passed('prepare is byte-idempotent, resolves relative paths and stores secrets only in private credentials')
        # Simulate a process dying between atomic file writes, retaining the registration identity.
        for filename in ['host.json', 'host.plist', 'credentials.json']:
            (REG / filename).unlink()
        prepare()
        assert snapshot == {p.name: p.read_bytes() for p in REG.iterdir() if p.name != 'prepare.lock'}
        passed('prepare repairs an interrupted write from the same saved registration identity')
        with (REG / 'prepare.lock').open('r+') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            prepare(expected=2)
        assert snapshot == {p.name: p.read_bytes() for p in REG.iterdir() if p.name != 'prepare.lock'}
        passed('another writer holding the registration lock refuses concurrent preparation without changing files')
        linux = RUN / 'linux $path %percent 空间'
        linux_info = prepare(linux, 'linux')
        unit = (linux / (linux_info['Label'] + '.service')).read_text()
        assert 'WorkingDirectory="' + str(linux).replace('%', '%%') + '"' in unit, unit
        assert str(linux).replace('$', '$$').replace('%', '%%') in unit
        windows = RUN / 'windows space'
        prepare(windows, 'windows')
        tree = ET.parse(windows / 'host.xml')
        ns = {'t': 'http://schemas.microsoft.com/windows/2004/02/mit/task'}
        assert tree.find('.//t:Command', ns).text == DOTNET
        assert tree.find('.//t:WorkingDirectory', ns).text == str(windows)
        assert tree.find('.//t:ExecutionTimeLimit', ns).text == 'PT0S'
        passed('three platform definitions parse and preserve literal paths (generator checks only)')
        bad = RUN / 'invalid.json'
        value = json.loads(CONFIG.read_text()); value['MaximumConcurrent'] = 0
        bad.write_text(json.dumps(value))
        prepare(RUN / 'invalid', config=bad, expected=2)
        clean = dict(ENV); clean.pop('SIRIAI_SERVICE_TOKEN')
        cli('prepare', RUN / 'missing-secret', ['--id', 'missing', '--config', str(CONFIG), '--runtime', DOTNET], expected=2, env=clean)
        for filename in ['host.json', 'host.plist']:
            path = REG / filename; original = path.read_bytes()
            path.write_bytes(original + b' ')
            cli('status', expected=2)
            path.write_bytes(original)
        credentials = REG / 'credentials.json'
        credentials.chmod(0o644); cli('status', expected=2); credentials.chmod(0o600)
        original = credentials.read_bytes(); credentials.unlink()
        linked = RUN / 'linked.json'; linked.write_bytes(original); linked.chmod(0o600)
        credentials.symlink_to(linked); cli('status', expected=2); credentials.unlink()
        credentials.write_bytes(original); credentials.chmod(0o600)
        passed('invalid configuration, missing token, tampered definitions/config and exposed/symlink secrets are rejected')
        if args.native or args.direct:
            if args.direct:
                DIRECT_LOG = (RUN / 'direct-host.log').open('w')
                DIRECT_PROCESS = subprocess.Popen([DOTNET, str(args.host), '--service', 'run', '--directory', str(REG)],
                    env={k: v for k, v in os.environ.items() if not k.startswith('SIRIAI_SERVICE_')}, stdout=DIRECT_LOG, stderr=DIRECT_LOG)
                wait_for(lambda: cli('status')['EndpointState'] == 'Ready')
            assert sys.platform == 'darwin', 'native acceptance currently targets macOS'
            state = cli('status'); assert not state['ManagerLoaded'] and state['EndpointState'] == ('Ready' if args.direct else 'Stopped')
            state = cli('start'); health = state['Health']
            assert state['EndpointState'] == 'Ready' and state['ManagerLoaded'] == args.native and health['OwnerId'] == 'alice'
            assert cli('start')['Health']['ProcessId'] == health['ProcessId']
            http('/v1/health', token='wrong', expected=401)
            assert http('/v1/health', token=OTHER)['OwnerId'] == 'bob'
            assert http('/v1/health')['ServiceLabel'] == LABEL
            passed(('real launchd' if args.native else 'direct registered process') + ' confirms authenticated host identity and repeated start preserves PID')
            submit = dict(ConversationId='service-chat', SubmissionKey=uuid.uuid4().hex, HarnessId='fixture', WorkspaceKey='test', Objective='slow')
            script = "import json,os,urllib.request; r=urllib.request.Request(os.environ['CHECK_URL']+'/v1/tasks',os.environ['CHECK_BODY'].encode(),headers={'Authorization':'Bearer '+os.environ['SIRIAI_SERVICE_TOKEN'],'Content-Type':'application/json'}); print(urllib.request.urlopen(r).read().decode())"
            client = subprocess.run([sys.executable, '-c', script], env=dict(ENV, CHECK_URL=URL, CHECK_BODY=json.dumps(submit)), text=True, capture_output=True, timeout=10)
            RECORDS.append(dict(command=['python3', 'short-lived HTTP client'], stdout=client.stdout, stderr=client.stderr, exit_code=client.returncode))
            assert client.returncode == 0, client.stderr
            task = json.loads(client.stdout)
            assert task['State'] in [0, 1]
            def complete():
                value = http('/v1/tasks/' + task['Id'])
                return value if value['State'] == 3 else None
            result = wait_for(complete)
            assert result['Result'] == 'fixture result: slow'
            http('/v1/tasks/' + task['Id'], token=OTHER, expected=404)
            passed('task completes after the submitting process exits and remains owner isolated')
            # A conflicting registration must not start or stop the actual owner of the endpoint.
            conflict = RUN / 'conflict'
            prepare(conflict)
            assert cli('status', conflict)['EndpointState'] == 'Conflict'
            cli('start', conflict, expected=2)
            cli('stop', conflict)
            assert http('/v1/health')['ProcessId'] == health['ProcessId']
            passed('endpoint conflict refuses admission and does not stop the other service')
            changed = json.loads(CONFIG.read_text()); changed['MaximumConcurrent'] = 3
            modified_config = RUN / 'modified.json'; modified_config.write_text(json.dumps(changed))
            if args.native:
                prepare(config=modified_config, expected=2)
            else:
                DIRECT_PROCESS.terminate(); DIRECT_PROCESS.wait(timeout=10); DIRECT_LOG.close()
                DIRECT_PROCESS = None
            state = cli('stop'); assert not state['ManagerLoaded'] and state['EndpointState'] == 'Stopped'
            assert cli('stop')['EndpointState'] == 'Stopped'
            if args.direct:
                DIRECT_LOG = (RUN / 'direct-host-restart.log').open('w')
                DIRECT_PROCESS = subprocess.Popen([DOTNET, str(args.host), '--service', 'run', '--directory', str(REG)],
                    stdout=DIRECT_LOG, stderr=DIRECT_LOG)
                wait_for(lambda: cli('status')['EndpointState'] == 'Ready')
            new = cli('start')['Health']
            assert new['InstanceId'] != health['InstanceId']
            assert http('/v1/tasks/' + task['Id'])['Result'] == result['Result']
            passed('stop is idempotent and a new process reloads durable results' + ('; loaded configuration is protected' if args.native else '; no native-manager lifecycle claim'))
except Exception:
    if args.native and LABEL:
        diagnostic = subprocess.run(['/bin/launchctl', 'print', 'gui/' + str(os.getuid()) + '/' + LABEL], text=True, capture_output=True)
        RECORDS.append(dict(command=diagnostic.args, stdout=diagnostic.stdout, stderr=diagnostic.stderr, exit_code=diagnostic.returncode))
    raise
finally:
    if DIRECT_PROCESS is not None:
        DIRECT_PROCESS.terminate(); DIRECT_PROCESS.wait(timeout=10); DIRECT_LOG.close()
    if args.native and LABEL:
        cleanup = subprocess.run(['/bin/launchctl', 'bootout', 'gui/' + str(os.getuid()) + '/' + LABEL], text=True, capture_output=True)
        RECORDS.append(dict(command=cleanup.args, stdout=cleanup.stdout, stderr=cleanup.stderr, exit_code=cleanup.returncode))
        # bootout returns before launchd has removed the temporary job.
        deadline = time.monotonic() + 20
        while True:
            check = subprocess.run(['/bin/launchctl', 'print', 'gui/' + str(os.getuid()) + '/' + LABEL], text=True, capture_output=True)
            if check.returncode != 0 or time.monotonic() >= deadline:
                break
            time.sleep(.1)
        RECORDS.append(dict(command=check.args, stdout=check.stdout, stderr=check.stderr, exit_code=check.returncode))
        (OUT / 'commands.json').write_text(json.dumps(RECORDS, indent=2, ensure_ascii=False))
        assert check.returncode != 0, 'temporary job left loaded'
        assert not (Path.home() / 'Library/LaunchAgents' / (LABEL + '.plist')).exists()
        passed('temporary launchd job removed; no login service installed')
    (OUT / 'commands.json').write_text(json.dumps(RECORDS, indent=2, ensure_ascii=False))
    (OUT / 'results.json').write_text(json.dumps(dict(passed=PASSED, native=args.native, run=str(RUN)), indent=2, ensure_ascii=False))
