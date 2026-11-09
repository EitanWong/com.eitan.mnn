#!/usr/bin/env python3
"""Independent native coordinator that calls the actual HTTP MCP endpoint."""
import json, os, sys, threading, time, urllib.request, uuid
from pathlib import Path

ROOT = Path(os.environ['SIRIAI_COORD_STEERING_ROOT'])
MODE = sys.argv[1]
WRITE = threading.Lock()
sessions, active, permissions = {}, {}, {}
initialized = set()

def record(name, value):
    with (ROOT / name).open('a') as f:
        f.write(json.dumps(dict(pid=os.getpid(), **value), ensure_ascii=False) + '\n')

def emit(frame):
    frame = dict(jsonrpc='2.0', **frame)
    with WRITE:
        record('wire.jsonl', dict(direction='agent', frame=frame))
        print(json.dumps(frame), flush=True)

def rpc(server, method, parameters):
    frame = dict(jsonrpc='2.0', id=uuid.uuid4().hex, method=method, params=parameters)
    headers = {h['name']: h['value'] for h in server['headers']}
    headers.update({'Content-Type': 'application/json', 'Accept': 'application/json,text/event-stream'})
    with urllib.request.urlopen(urllib.request.Request(server['url'], json.dumps(frame).encode(), headers=headers), timeout=20) as response:
        reply = json.loads(response.read())
    record('mcp-wire.jsonl', dict(request=frame, response=reply))
    return reply['result']

def mcp(server, name, arguments):
    return rpc(server, 'tools/call', dict(name=name, arguments=arguments))

def prompt(frame):
    sid = frame['params']['sessionId']
    state = dict(cancel=threading.Event(), injected=threading.Event(), text=None)
    active[sid] = state
    try:
        objective = frame['params']['prompt'][0]['text']
        record('events.jsonl', dict(kind='prompt', sid=sid, mode=MODE, objective=objective))
        if MODE == 'coordinator':
            turn = json.loads(objective)
            assert turn['kind'] == 'siriai-coordinator-turn'
            record('turns.jsonl', dict(turn=turn))
            server = sessions[sid][0]
            if sid not in initialized:
                rpc(server, 'initialize', dict(protocolVersion='2025-06-18', capabilities={}, clientInfo=dict(name='independent-coordinator-steering', version='1')))
                assert len(rpc(server, 'tools/list', {})['tools']) == 5
                initialized.add(sid)
            if turn['phase'] == 'initial':
                answer = mcp(server, 'siriai_session_start', dict(turn_id=turn['turn_id'], request_id='initial-child', prompt=turn['objective'], title='Owned child'))
                assert not answer['isError'], answer
                record('children.jsonl', dict(parent=turn['run_id'], child=json.loads(answer['content'][0]['text'])))
                text = 'Child admitted; work is still running.'
            elif turn['phase'] == 'update':
                if turn['instruction'] == 'permission-update':
                    request_id = 'permission-' + uuid.uuid4().hex
                    permission = dict(ready=threading.Event(), response=None)
                    permissions[request_id] = permission
                    emit(dict(id=request_id, method='session/request_permission', params=dict(sessionId=sid, toolCall=dict(toolCallId='update-permission', title='Approve routing'), options=[dict(optionId='allow', name='Allow', kind='allow_once'), dict(optionId='deny', name='Deny', kind='reject_once')])))
                    assert permission['ready'].wait(20)
                    assert permission['response']['result']['outcome'] == dict(outcome='selected', optionId='allow'), permission['response']
                if turn['instruction'] == 'forbidden-start':
                    answer = mcp(server, 'siriai_session_start', dict(turn_id=turn['turn_id'], request_id='forbidden', prompt='must not run'))
                    assert answer['isError'], answer
                elif turn['instruction'] == 'early-end':
                    target = turn['targets'][0]
                    threading.Thread(target=mcp, args=(server, 'siriai_session_send', dict(turn_id=turn['turn_id'], request_id=turn['instruction_id'], session_id=target['session_id'], prompt=turn['instruction'])), daemon=True).start()
                    while not any(json.loads(line).get('text') == 'early-end' for line in (ROOT / 'events.jsonl').read_text().splitlines()):
                        time.sleep(.02)
                    answer = dict(isError=False, content=[])
                else:
                    target = turn['targets'][0]
                    if turn['instruction'] == 'hold-routing':
                        while not (ROOT / 'release-routing').exists():
                            time.sleep(.02)
                    if turn['instruction'] == 'wrong-target':
                        target = json.loads((ROOT / 'foreign-target.json').read_text())
                    answer = mcp(server, 'siriai_session_send', dict(turn_id=turn['turn_id'], request_id=turn['instruction_id'], session_id=target['session_id'], prompt=turn['instruction']))
                text = json.dumps(answer)
            else:
                assert turn['phase'] == 'result'
                forbidden = mcp(server, 'siriai_session_start', dict(turn_id=turn['turn_id'], request_id='result-must-not-delegate', prompt='must not run'))
                assert forbidden['isError'], forbidden
                text = 'collected:' + '|'.join(t['result'] or t['status'] for t in turn['targets'])
        else:
            if objective == 'immediate':
                text = 'worker:immediate'
            else:
                while not state['cancel'].is_set():
                    if state['injected'].is_set() and (ROOT / ('release-' + objective)).exists():
                        break
                    time.sleep(.02)
                if state['cancel'].is_set() and objective != 'ignore-cancel':
                    emit(dict(id=frame['id'], result=dict(stopReason='cancelled')))
                    return
                if objective == 'ignore-cancel':
                    threading.Event().wait(60)
                    return
                text = 'worker:' + objective + ':' + state['text']
        emit(dict(method='session/update', params=dict(sessionId=sid, update=dict(sessionUpdate='agent_message_chunk', content=dict(type='text', text=text)))))
        emit(dict(id=frame['id'], result=dict(stopReason='end_turn')))
    except Exception:
        import traceback
        record('errors.jsonl', dict(error=traceback.format_exc()))
        emit(dict(id=frame['id'], error=dict(code=-32000, message='independent coordinator fixture failed')))

for line in sys.stdin:
    frame = json.loads(line)
    record('wire.jsonl', dict(direction='client', frame=frame))
    method, params = frame.get('method'), frame.get('params', {})
    if method is None and frame.get('id') in permissions:
        permission = permissions[frame['id']]
        permission['response'] = frame
        permission['ready'].set()
    elif method == 'initialize':
        emit(dict(id=frame['id'], result=dict(protocolVersion=1, agentCapabilities=dict(loadSession=True, mcpCapabilities=dict(http=True)), agentInfo=dict(name='independent-coordinator-steering', version='1'), _meta=dict(steering=dict(supported=True)))))
    elif method in ['session/new', 'session/load', 'session/resume']:
        sid = params.get('sessionId') or uuid.uuid4().hex
        sessions[sid] = params['mcpServers']
        emit(dict(id=frame['id'], result=dict(sessionId=sid) if method == 'session/new' else {}))
    elif method == 'session/prompt':
        threading.Thread(target=prompt, args=(frame,), daemon=True).start()
    elif method == '_session/steering':
        sid = params['sessionId']; state = active[sid]
        instruction = params['prompt'][-1]['text']
        record('events.jsonl', dict(kind='injection', sid=sid, text=instruction))
        if instruction == 'early-end':
            while not (ROOT / 'release-native-ack').exists():
                time.sleep(.02)
        if instruction == 'crash-pending':
            pass
        elif instruction == 'uncertain':
            emit(dict(id=frame['id'], result=dict(outcome='startedNewTurn')))
        elif instruction == 'reject':
            emit(dict(id=frame['id'], error=dict(code=-32602, message='explicitly rejected')))
        else:
            state['text'] = instruction
            state['injected'].set()
            emit(dict(id=frame['id'], result=dict(outcome='injected')))
    elif method == 'session/cancel':
        if params['sessionId'] in active:
            active[params['sessionId']]['cancel'].set()
    else:
        raise AssertionError(method)
