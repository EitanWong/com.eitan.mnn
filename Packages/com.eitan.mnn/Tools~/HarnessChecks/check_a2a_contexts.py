#!/usr/bin/env python3
"""Real Host HTTP/SSE checks for Qwen's opt-in A2A owner context continuity."""
from pathlib import Path
import json
import os
import socket
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid

sys.dont_write_bytecode = True
import check_remote_protocols as common

OUT = Path(os.environ['SIRIAI_PROTOCOL_CHECK_OUTPUT']) / 'A2aContextChecks'
OUT.mkdir(parents=True, exist_ok=True)
common.OUT = OUT
records = []


def passed(behavior):
    records.append(dict(result='PASS', behavior=behavior))
    print('PASS ' + behavior, flush=True)


def check(version, binding, streaming=False):
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    url = 'http://127.0.0.1:' + str(port)
    name = version + '-' + binding + ('-stream' if streaming else '-poll')
    log = (OUT / (name + '-peer.log')).open('w')
    peer = subprocess.Popen([sys.executable, '-u', str(Path(__file__).parent / 'Fixtures/a2a_context_fixture.py'),
                             str(port), version, binding, 'stream' if streaming else 'poll'], stdout=log, stderr=log)
    host = common.Host(name, [dict(Id='backend', Protocol='a2a', Endpoint=url, ReuseContext=True, PollMilliseconds=30)])
    settings = json.loads(host.config.read_text())
    settings['MaximumConcurrent'] = 4
    settings['Workspaces'].update({str(i): str(host.path / ('workspace-' + str(i))) for i in range(4)})
    for path in settings['Workspaces'].values():
        Path(path).mkdir(exist_ok=True)
    settings['Identities'] += [dict(OwnerId='other', TokenEnvironmentVariable='SIRIAI_OTHER_TOKEN')]
    common.ENV['SIRIAI_OTHER_TOKEN'] = 'other-' + common.TOKEN
    for index in range(105):
        variable = 'SIRIAI_CONTEXT_TOKEN_' + str(index)
        common.ENV[variable] = 'owner-' + str(index) + '-' + common.TOKEN
        settings['Identities'].append(dict(OwnerId='owner-' + str(index), TokenEnvironmentVariable=variable))
    host.config.write_text(json.dumps(settings))

    def wire():
        try:
            with urllib.request.urlopen(url + '/__state', timeout=2) as response:
                return json.load(response)
        except OSError:
            return None

    def submit(text, isolated=False, owner=None, lane='test', key=None):
        payload = dict(ConversationId='front-' + lane, SubmissionKey=key or uuid.uuid4().hex,
                       HarnessId='backend', WorkspaceKey=lane, Objective=text, IsolatedContext=isolated)
        if owner is None:
            return host.call('/v1/tasks', payload, 202)
        request = urllib.request.Request(host.url + '/v1/tasks', json.dumps(payload).encode(),
                                         headers={'Authorization': 'Bearer ' + owner + '-' + common.TOKEN, 'Content-Type': 'application/json'})
        with urllib.request.urlopen(request, timeout=10) as response:
            assert response.status == 202
            return json.load(response)

    def finish(item):
        return common.wait(lambda: (lambda t: t if t['State'] in [3, 4, 5, 6] else None)(host.task(item['Id'])))

    try:
        common.wait(wire)
        host.start()
        first = finish(submit('first'))
        second = finish(submit('second'))
        sent = wire()['requests']
        assert first['State'] == second['State'] == 3, (first, second)
        assert sent[1]['message'].get('contextId') == sent[0]['context'], 'A2A_REUSE_MISSING: second task lost the server-issued owner context'
        assert sent[0]['id'] != sent[1]['id']
        assert all(not item['message'].get('taskId') for item in sent)
        isolated = finish(submit('isolated', True))
        third = finish(submit('third'))
        sent = wire()['requests']
        assert isolated['IsolatedContext'] is True
        assert not sent[2]['message'].get('contextId')
        assert sent[3]['message']['contextId'] == sent[0]['context']
        owner = submit('other-owner', owner='other')
        common.wait(lambda: len(wire()['requests']) == 5)
        assert not wire()['requests'][4]['message'].get('contextId')
        assert owner['OwnerId'] == 'other'
        key = 'stable-context-flag'
        fixed = submit('idempotent', True, key=key)
        assert submit('idempotent', True, key=key)['Id'] == fixed['Id']
        host.call('/v1/tasks', dict(ConversationId='front-test', SubmissionKey=key, HarnessId='backend',
                                  WorkspaceKey='test', Objective='idempotent', IsolatedContext=False), 400)
        finish(fixed)
        passed(name + ': owner-only reuse, fresh tasks, isolated bypass and idempotent identity')
        message = finish(submit('message'))
        assert message['State'] == 3 and message['Result'] == 'message'
        mismatch = finish(submit('mismatch'))
        assert mismatch['State'] == 6, mismatch
        after = finish(submit('after-mismatch'))
        assert after['State'] == 3 and wire()['requests'][-1]['message']['contextId'] == sent[0]['context']
        passed(name + ': direct messages reuse context; changed server identity cannot poison mapping')
        # Restart clears the optional memory cache; completed tasks/results and isolation stay durable.
        host.stop(kill=True)
        host.start()
        assert host.task(isolated['Id'])['IsolatedContext'] is True
        fresh = finish(submit('fresh-after-restart'))
        assert fresh['State'] == 3 and not wire()['requests'][-1]['message'].get('contextId')
        passed(name + ': host restart preserves exact results/isolated flag and resets optional owner cache')
        host.stop(kill=True)
        host.start()
        absent = finish(submit('no-context'))
        assert absent['State'] == 3
        base_count = len(wire()['requests'])
        immediate = finish(submit('message'))
        reused = finish(submit('after-message'))
        sent = wire()['requests']
        assert immediate['State'] == reused['State'] == 3
        assert not sent[base_count]['message'].get('contextId')
        assert sent[base_count + 1]['message']['contextId'] == sent[base_count]['context']
        passed(name + ': absent context releases discovery; direct Message assigns a reusable server context')
        host.stop(kill=True)
        host.start()
        count = len(wire()['requests'])
        discovering = submit('discover', lane='0')
        common.wait(lambda: len(wire()['requests']) == count + 1)
        cancelled = submit('cancel-context-waiter', lane='1')
        common.wait(lambda: host.task(cancelled['Id'])['State'] == 1)
        time.sleep(.1)
        assert len(wire()['requests']) == count + 1, 'Concurrent first requests forked the owner context'
        response = host.call('/v1/tasks/' + cancelled['Id'] + '/cancel', {})
        assert response['State'] == 5, response
        waiting = submit('waiting-for-context', lane='2')
        common.wait(lambda: host.task(waiting['Id'])['State'] == 1)
        with urllib.request.urlopen(url + '/__release', timeout=2):
            pass
        result = finish(waiting)
        assert result['State'] == 3
        assert host.task(discovering['Id'])['State'] == 1, 'Waiting tasks must resume at context discovery, before the first task completes'
        sent = wire()['requests']
        assert len(sent) == count + 2
        assert sent[-1]['message']['contextId'] == sent[-2]['context']
        passed(name + ': concurrent discovery waits only for context; cancelled waiter sends no remote work')
        recovering = discovering
        if version == '1.0' and binding == 'HTTP+JSON' and not streaming:
            with urllib.request.urlopen(url + '/__finish', timeout=2):
                pass
            assert finish(discovering)['State'] == 3
            with urllib.request.urlopen(url + '/__reset', timeout=2):
                pass
            recovering = submit('input', lane='0')
            prompt = common.wait(lambda: (lambda t: t if t['WaitingFor'] == 1 else None)(host.task(recovering['Id'])))
            host.call('/v1/tasks/' + recovering['Id'] + '/respond', dict(RequestId=prompt['RequestId'], Text='answer'))
            assert host.task(recovering['Id'])['State'] == 1
            for index in range(105):
                previous = len(wire()['requests'])
                item = submit('owner-work', owner='owner-' + str(index), lane='3')
                common.wait(lambda: len(wire()['requests']) == previous + 1)
                def owner_result():
                    request = urllib.request.Request(host.url + '/v1/tasks/' + item['Id'],
                        headers={'Authorization': 'Bearer owner-' + str(index) + '-' + common.TOKEN})
                    with urllib.request.urlopen(request, timeout=2) as response:
                        task = json.load(response)
                    if task['State'] in [3, 4, 5, 6]:
                        assert task['State'] == 3, task
                        return task
                common.wait(owner_result)
            last_context = wire()['requests'][-1]['context']
            previous = len(wire()['requests'])
            submit('recent-owner', owner='owner-104', lane='3')
            common.wait(lambda: len(wire()['requests']) == previous + 1)
            assert wire()['requests'][-1]['message']['contextId'] == last_context
            previous += 1
            submit('evicted-owner', owner='owner-0', lane='3')
            common.wait(lambda: len(wire()['requests']) == previous + 1)
            assert not wire()['requests'][-1]['message'].get('contextId')
            active = finish(submit('active-owner', lane='1'))
            assert active['State'] == 3 and wire()['requests'][-1]['message'].get('contextId') == sent[-2]['context'], 'INPUT_CONTEXT_RELEASED: answered task lost its live owner context during cache eviction'
            passed(name + ': 105 owners evict inactive oldest context, preserve recent and running answered-input owner')
        remote_id = json.loads(host.task(recovering['Id'])['BackendState'])['RemoteId']
        count = len(wire()['requests'])
        host.stop(kill=True)
        host.start()
        restored = common.wait(lambda: (lambda t: t if t['State'] == 1 else None)(host.task(recovering['Id'])))
        assert json.loads(restored['BackendState'])['RemoteId'] == remote_id
        assert len(wire()['requests']) == count
        with urllib.request.urlopen(url + '/__finish', timeout=2):
            pass
        assert finish(recovering)['State'] == 3
        assert len(wire()['requests']) == count
        passed(name + ': confirmed reused-context task recovers across Host crash without new admission')
        if not streaming:
            host.stop(kill=True)
            host.start()
            with urllib.request.urlopen(url + '/__reset', timeout=2):
                pass
            count = len(wire()['requests'])
            failed = submit('discover-fail', lane='0')
            common.wait(lambda: len(wire()['requests']) == count + 1)
            waiting = submit('after-failed-discovery', lane='1')
            common.wait(lambda: host.task(waiting['Id'])['State'] == 1)
            time.sleep(.1)
            assert len(wire()['requests']) == count + 1
            with urllib.request.urlopen(url + '/__release', timeout=2):
                pass
            assert finish(failed)['State'] == 6
            assert finish(waiting)['State'] == 3
            assert len(wire()['requests']) == count + 2
            assert not wire()['requests'][-1]['message'].get('contextId')
            passed(name + ': failed ambiguous discovery releases waiters without replaying the original work')
            delegated = common.Host('delegated-' + name, [dict(Id='backend', Protocol='http', Endpoint=host.url,
                RemoteHarnessId='backend', RemoteWorkspace='test', TokenEnvironmentVariable='SIRIAI_REMOTE_CHECK_TOKEN',
                RemoteCapabilities=19, PollMilliseconds=30)])
            try:
                delegated.start()
                item = delegated.call('/v1/tasks', dict(ConversationId='mobile', SubmissionKey='isolated-chain',
                    HarnessId='backend', WorkspaceKey='test', Objective='isolated-http-chain', IsolatedContext=True), 202)
                result = common.wait(lambda: (lambda t: t if t['State'] in [3, 4, 5, 6] else None)(delegated.task(item['Id'])))
                assert result['State'] == 3 and result['IsolatedContext'] is True
                assert not wire()['requests'][-1]['message'].get('contextId')
                child = next(t for t in host.call('/v1/tasks') if t['Objective'] == 'isolated-http-chain')
                assert child['IsolatedContext'] is True
                passed(name + ': HTTP delegation preserves isolated context through both durable Hosts')
            finally:
                delegated.stop()
        assert not wire()['errors'], wire()['errors']
    finally:
        (OUT / (name + '-wire.json')).write_text(json.dumps(wire(), indent=2) + '\n')
        host.stop()
        peer.terminate()
        peer.wait(timeout=5)
        log.close()


try:
    for version in ['1.0', '0.3']:
        for binding in ['JSONRPC', 'HTTP+JSON']:
            check(version, binding)
            check(version, binding, True)
finally:
    (OUT / 'results.json').write_text(json.dumps(records, indent=2) + '\n')
