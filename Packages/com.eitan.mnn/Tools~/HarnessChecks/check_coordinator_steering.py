#!/usr/bin/env python3
"""Verify original-run native coordinator routing, not an in-process mock."""
import argparse, concurrent.futures, json, os, socket, subprocess, sys, time, urllib.error, urllib.request, uuid
from pathlib import Path

p = argparse.ArgumentParser(); p.add_argument('--host', type=Path, required=True); p.add_argument('--output', type=Path, required=True)
a = p.parse_args(); OUT = a.output.resolve(); OUT.mkdir(parents=True, exist_ok=True)
RUN = OUT / ('run-' + uuid.uuid4().hex); RUN.mkdir(); PEER = RUN / 'peer'; PEER.mkdir()
fixture = Path(__file__).parent / 'Fixtures/coordinator_steering_fixture.py'
secrets = {key: uuid.uuid4().hex * 2 for key in ['COORD_HOST_TOKEN', 'COORD_BOB_TOKEN', 'COORD_SCOPE_TOKEN', 'COORD_OTHER_SCOPE_TOKEN', 'COORD_BOB_SCOPE_TOKEN', 'COORD_QUEUED_SCOPE_TOKEN']}
env = dict(os.environ, **secrets, SIRIAI_COORD_STEERING_ROOT=str(PEER))
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
URL = 'http://127.0.0.1:' + str(port)
scopes = [dict(Id='run', OwnerId='alice', ConversationId='phone', WorkspaceKey='test', HarnessId='worker', TokenEnvironmentVariable='COORD_SCOPE_TOKEN'),
          dict(Id='legacy', OwnerId='alice', ConversationId='other-phone', WorkspaceKey='test', HarnessId='worker', TokenEnvironmentVariable='COORD_OTHER_SCOPE_TOKEN'),
          dict(Id='bob', OwnerId='bob', ConversationId='bob-phone', WorkspaceKey='test', HarnessId='worker', TokenEnvironmentVariable='COORD_BOB_SCOPE_TOKEN'),
          dict(Id='queued', OwnerId='alice', ConversationId='queued-phone', WorkspaceKey='test', HarnessId='worker', TokenEnvironmentVariable='COORD_QUEUED_SCOPE_TOKEN')]
config = dict(Listen=URL, DataDirectory=str(RUN / 'data'), MaximumConcurrent=1, Identities=[dict(OwnerId='alice', TokenEnvironmentVariable='COORD_HOST_TOKEN'), dict(OwnerId='bob', TokenEnvironmentVariable='COORD_BOB_TOKEN')], Workspaces=dict(test=str(RUN)), CoordinatorMcpScopes=scopes,
              Harnesses=[dict(Id=mode, Protocol='acp', Command=sys.executable, Arguments=['-u', str(fixture), mode], ForwardEnvironment=['SIRIAI_COORD_STEERING_ROOT'], TimeoutMilliseconds=0,
                            **(dict(CoordinatorScopeId='run', AcpMcpServers=[dict(Name='coordinator', Type='http', Url=URL + '/mcp/coordinator', Headers=[dict(Name='Authorization', ValueEnvironmentVariable='COORD_SCOPE_TOKEN', Prefix='Bearer ')])]) if mode == 'coordinator' else {})) for mode in ['coordinator', 'worker']])
queued_profile = json.loads(json.dumps(config['Harnesses'][0]))
queued_profile.update(Id='coordinator-queued', CoordinatorScopeId='queued')
queued_profile['AcpMcpServers'][0]['Headers'][0]['ValueEnvironmentVariable'] = 'COORD_QUEUED_SCOPE_TOKEN'
config['Harnesses'].append(queued_profile)
CONFIG = RUN / 'host.json'; CONFIG.write_text(json.dumps(config)); passed = []; process = None; log = None; wire = []

def ok(text):
    passed.append(text); print('PASS ' + text, flush=True)

def wait(fn, seconds=25):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        value = fn()
        if value: return value
        time.sleep(.025)
    raise AssertionError('Timed out waiting for original coordinator run')

def http(path, body=None, token=secrets['COORD_HOST_TOKEN'], status=200):
    request = urllib.request.Request(URL + path, None if body is None else json.dumps(body).encode(), headers={'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json', 'Accept': 'application/json,text/event-stream'})
    try:
        with urllib.request.urlopen(request, timeout=25) as response: code, data = response.status, response.read()
    except urllib.error.HTTPError as error: code, data = error.code, error.read()
    assert code == status, (path, code, data)
    assert not any(secret.encode() in data for secret in secrets.values())
    return json.loads(data) if data else None

def mcp(name, args, token=secrets['COORD_SCOPE_TOKEN'], error=False):
    frame = dict(jsonrpc='2.0', id=uuid.uuid4().hex, method='tools/call', params=dict(name=name, arguments=args))
    reply = http('/mcp/coordinator', frame, token)
    wire.append(dict(request=frame, response=reply))
    assert reply['result'].get('isError', False) == error, reply
    return json.loads(reply['result']['content'][0]['text'])

def start():
    global process, log
    log = (RUN / ('host-' + uuid.uuid4().hex + '.log')).open('w')
    process = subprocess.Popen(['dotnet', str(a.host), '--config', str(CONFIG)], env=env, stdout=log, stderr=log)
    def ready():
        assert process.poll() is None, 'Host exited'
        try: return http('/v1/harnesses')
        except urllib.error.URLError: return None
    return wait(ready)

def stop(kill=False):
    global process
    if process:
        if process.poll() is None: process.kill() if kill else process.terminate()
        try: process.wait(timeout=10)
        except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=5)
        process = None; log.close()

def task(value): return http('/v1/tasks/' + (value['Id'] if 'Id' in value else value['delegation_id']))
def terminal(value):
    t = task(value); return t if t['State'] in [3, 4, 5, 6] else None
def rows(name):
    path = PEER / name
    return [json.loads(line) for line in path.read_text().splitlines()] if path.exists() else []
def submit(objective): return http('/v1/tasks', dict(ConversationId='phone', SubmissionKey=uuid.uuid4().hex, HarnessId='coordinator', WorkspaceKey='test', Objective=objective), status=202)
def child(parent):
    matches = [e['child'] for e in rows('children.jsonl') if e['parent'] == parent['Id']]
    return matches[-1] if matches else None
def update(parent, text, identity):
    http('/v1/tasks/' + parent['Id'] + '/update', dict(RequestId=identity, Text=text, AdmitOnly=True))
def delivered(parent, identity, state=1):
    found = [v for v in task(parent).get('Instructions', []) if v['Id'] == identity]
    return found[0] if found and found[0]['State'] == state else None

try:
    start()
    old = mcp('siriai_session_start', dict(request_id='old-start', prompt='legacy-wait'), token=secrets['COORD_OTHER_SCOPE_TOKEN'])
    wait(lambda: task(old)['State'] == 1)
    before = len(http('/v1/tasks'))
    mcp('siriai_session_send', dict(request_id='busy-send', session_id=old['session_id'], prompt='must not queue'), token=secrets['COORD_OTHER_SCOPE_TOKEN'], error=True)
    assert len(http('/v1/tasks')) == before
    http('/v1/tasks/' + old['delegation_id'] + '/cancel', {})
    ok('ordinary busy session_send rejects without creating a replacement or queued task')

    previous = submit('immediate'); previous_child = wait(lambda: child(previous))
    assert wait(lambda: terminal(previous))['State'] == 3
    (PEER / 'foreign-target.json').write_text(json.dumps(previous_child))
    # This checker has read the earlier result; persist that presentation beyond a lease timeout.
    earlier = http('/v1/results/claim', dict(ConversationId='phone', ClientId='previous-check', LeaseSeconds=60))
    assert earlier['Id'] == previous['Id']
    http('/v1/tasks/' + previous['Id'] + '/receipt', dict(LeaseId=earlier['LeaseId'], Receipt=4))
    parent = submit('normal'); owned = wait(lambda: child(parent))
    wait(lambda: task(owned)['State'] == 1)
    assert task(parent)['State'] == 1 and not task(parent)['ResultPending']
    assert task(owned)['ResultConversationId'] != 'phone'
    ok('original coordinator stays running while its child executes with MaximumConcurrent1; admission text is not final work result')

    first_turn = next(e['turn'] for e in rows('turns.jsonl') if e['turn']['run_id'] == parent['Id'])
    mcp('siriai_session_send', dict(turn_id=first_turn['turn_id'], request_id='late', session_id=owned['session_id'], prompt='stale'), error=True)
    mcp('siriai_session_start', dict(request_id='missing-turn', prompt='must not run'), error=True)
    ok('ended turn capabilities and missing run context cannot create or redirect delegated work')

    update(parent, 'hold-routing', 'hold-routing')
    live_turn = wait(lambda: next((e['turn'] for e in rows('turns.jsonl') if e['turn']['run_id'] == parent['Id'] and e['turn'].get('instruction') == 'hold-routing'), None))
    mcp('siriai_session_send', dict(turn_id=live_turn['turn_id'], request_id='cross-owner', session_id=owned['session_id'], prompt='must not run'), token=secrets['COORD_BOB_SCOPE_TOKEN'], error=True)
    http('/v1/tasks/' + parent['Id'] + '/update', dict(RequestId='cross-owner', Text='must not run'), token=secrets['COORD_BOB_TOKEN'], status=404)
    (PEER / 'release-routing').touch(); wait(lambda: delivered(parent, 'hold-routing'))
    update(parent, 'wrong-target', 'wrong-target'); wait(lambda: delivered(parent, 'wrong-target', 2))
    assert task(previous_child)['State'] == 3
    assert not any(v['Objective'] == 'wrong-target' for v in http('/v1/tasks'))
    ok('a live turn capability cannot cross owner grants or steer a different original run in the same grant')

    update(parent, 'permission-update', 'permission-update')
    pending = wait(lambda: task(parent) if task(parent)['WaitingFor'] == 2 else None)
    http('/v1/tasks/' + parent['Id'] + '/respond', dict(RequestId=pending['RequestId'], Text='approved', Allow=True))
    wait(lambda: delivered(parent, 'permission-update'))
    ok('native permission during an update round reaches the original foreground task and answers the exact native turn')

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        list(pool.map(lambda _: update(parent, '新的要求', 'change-one'), range(4)))
    wait(lambda: delivered(parent, 'change-one'))
    assert len([v for v in rows('events.jsonl') if v['kind'] == 'injection' and v['text'] == '新的要求']) == 1
    assert task(owned)['Id'] == owned['delegation_id'] and len([v for v in http('/v1/tasks') if v['HarnessId'] == 'worker' and v['ConversationId'] == owned['session_id']]) == 1
    ok('foreground update routes through a new coordinator update turn to the exact original child once')

    update(parent, 'forbidden-start', 'forbidden'); wait(lambda: delivered(parent, 'forbidden', 2))
    assert not any(v['Objective'] == 'must not run' for v in http('/v1/tasks'))
    (PEER / 'release-normal').touch()
    done = wait(lambda: terminal(parent)); assert done['State'] == 3 and done['Result'] == 'collected:worker:normal:新的要求', done
    claimed = http('/v1/results/claim', dict(ConversationId='phone', ClientId='checker', LeaseSeconds=60))
    assert claimed['Id'] == parent['Id'], claimed
    update(parent, '新的要求', 'change-one')
    http('/v1/tasks/' + parent['Id'] + '/update', dict(RequestId='change-one', Text='conflicting intent'), status=409)
    assert len([v for v in rows('events.jsonl') if v['kind'] == 'injection' and v['text'] == '新的要求']) == 1
    ok('update and result rounds cannot spawn unrelated work; only the collected parent result is delivered to the foreground')

    for objective, instruction, expected in [('uncertain', 'uncertain', 3), ('reject', 'reject', 2)]:
        current = submit(objective); current_child = wait(lambda: child(current)); wait(lambda: task(current_child)['State'] == 1)
        update(current, instruction, 'attempt'); wait(lambda: delivered(current, 'attempt', expected))
        update(current, instruction, 'attempt'); time.sleep(.1)
        assert len([v for v in rows('events.jsonl') if v['kind'] == 'injection' and v['text'] == instruction]) == 1
        http('/v1/tasks/' + current['Id'] + '/cancel', {})
        assert wait(lambda: terminal(current))['State'] == (6 if instruction == 'uncertain' else 5)
    ok('child rejection and uncertain delivery remain distinct in the original durable instruction and retries never replay them')

    early = submit('early-end'); early_child = wait(lambda: child(early)); wait(lambda: task(early_child)['State'] == 1)
    update(early, 'early-end', 'early-end')
    wait(lambda: any(e['kind'] == 'injection' and e['text'] == 'early-end' for e in rows('events.jsonl')))
    time.sleep(.15)
    assert delivered(early, 'early-end', 0), task(early)
    (PEER / 'release-native-ack').touch(); wait(lambda: delivered(early, 'early-end', 3))
    http('/v1/tasks/' + early['Id'] + '/cancel', {})
    assert wait(lambda: terminal(early))['State'] == 5
    ok('a coordinator ending before its tool response drains admitted effects and records uncertainty instead of falsely rejecting delivery')

    ignored = submit('ignore-cancel'); ignored_child = wait(lambda: child(ignored)); wait(lambda: task(ignored_child)['State'] == 1)
    http('/v1/tasks/' + ignored['Id'] + '/cancel', {})
    assert wait(lambda: terminal(ignored))['State'] == 6
    ok('unconfirmed child cancellation interrupts the parent instead of falsely reporting confirmed cancellation')
    stop(); start()

    crashed = submit('crash'); crashed_child = wait(lambda: child(crashed)); wait(lambda: task(crashed_child)['State'] == 1)
    update(crashed, 'crash-pending', 'pending-at-crash')
    wait(lambda: any(e['kind'] == 'injection' and e['text'] == 'crash-pending' for e in rows('events.jsonl')))
    rebound = submit('immediate')
    assert task(rebound)['State'] == 0
    queued_allowed = http('/v1/tasks', dict(ConversationId='queued-phone', SubmissionKey=uuid.uuid4().hex, HarnessId='coordinator-queued', WorkspaceKey='test', Objective='immediate'), status=202)
    assert task(queued_allowed)['State'] == 0
    old_turns = len(rows('turns.jsonl')); old_prompts = len(rows('events.jsonl'))
    stop(kill=True)
    config['CoordinatorMcpScopes'][0]['ConversationId'] = 'different-phone'
    config['CoordinatorMcpScopes'][0]['Id'] = 'rebound'
    config['Harnesses'][0]['CoordinatorScopeId'] = 'rebound'
    CONFIG.write_text(json.dumps(config)); start()
    assert wait(lambda: terminal(crashed))['State'] == 6 and wait(lambda: terminal(crashed_child))['State'] == 6
    assert delivered(crashed, 'pending-at-crash', 3)
    assert task(crashed_child)['Instructions'][0]['State'] == 3
    assert wait(lambda: terminal(rebound))['State'] == 6, task(rebound)
    assert not any(e['turn']['run_id'] == rebound['Id'] for e in rows('turns.jsonl')[old_turns:])
    assert not any(e['turn']['run_id'] == crashed['Id'] for e in rows('turns.jsonl')[old_turns:])
    assert len([e for e in rows('events.jsonl') if e['kind'] == 'injection' and e['text'] == 'crash-pending']) == 1
    ok('Host crash invalidates original run ownership and interrupts both unknown executions without replay')
    ok('queued coordinator work revalidates a changed scope before execution and cannot inherit a different foreground grant')
    resumed = wait(lambda: terminal(queued_allowed)); assert resumed['State'] == 3 and resumed['Result'] == 'collected:worker:immediate', resumed
    ok('unchanged queued coordinator work starts only after its own HTTP MCP endpoint is ready and completes once')

    assert not rows('errors.jsonl'), rows('errors.jsonl')
    (OUT / 'host-config-redacted.json').write_text(json.dumps(config, indent=2))
finally:
    stop()
    (OUT / 'results.json').write_text(json.dumps(passed, indent=2) + '\n')
    (OUT / 'mcp-wire.json').write_text(json.dumps(wire + rows('mcp-wire.jsonl'), indent=2) + '\n')
    (OUT / 'wire.json').write_text(json.dumps(rows('wire.jsonl'), indent=2) + '\n')
    (OUT / 'events.json').write_text(json.dumps(rows('events.jsonl'), indent=2) + '\n')
