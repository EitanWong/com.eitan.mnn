#!/usr/bin/env python3
"""Independent ACP peer exercising actual C# Host/MCP conversation boundaries."""
import json, os, sys, threading, time, urllib.request, uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_COORD_CONVERSATIONS_ROOT']); MODE=sys.argv[1]
WRITE=threading.Lock(); RECORD=threading.Lock(); sessions={}; active={}
def record(name,value):
    with RECORD, (ROOT/name).open('a') as f: f.write(json.dumps(dict(pid=os.getpid(),**value),ensure_ascii=False)+'\n')
def emit(value):
    frame=dict(jsonrpc='2.0',**value)
    with WRITE:
        record('wire.jsonl',dict(direction='agent',frame=frame)); print(json.dumps(frame),flush=True)
def mcp(server,name,args):
    frame=dict(jsonrpc='2.0',id=uuid.uuid4().hex,method='tools/call',params=dict(name=name,arguments=args))
    headers={h['name']:h['value'] for h in server['headers']}; headers.update({'Content-Type':'application/json','Accept':'application/json,text/event-stream'})
    with urllib.request.urlopen(urllib.request.Request(server['url'],json.dumps(frame).encode(),headers=headers),timeout=20) as r: reply=json.loads(r.read())
    record('mcp-wire.jsonl',dict(request=frame,response=reply)); return reply['result']
def prompt(frame):
    sid=frame['params']['sessionId']; state=dict(cancel=threading.Event(),injected=None); active[sid]=state
    try:
        objective=frame['params']['prompt'][0]['text'];record('prompts.jsonl',dict(sid=sid,mode=MODE,objective=objective))
        if MODE=='coordinator':
            turn=json.loads(objective);record('turns.jsonl',dict(sid=sid,turn=turn));server=sessions[sid][0]
            if turn['phase']=='initial':
                answer=mcp(server,'siriai_session_start',dict(turn_id=turn['turn_id'],request_id='same-child-intent',prompt=turn['objective']))
                assert not answer['isError'],answer
                record('children.jsonl',dict(parent=turn['run_id'],child=json.loads(answer['content'][0]['text'])))
                while turn['objective'].startswith('hold-') and not (ROOT/('release-initial-'+turn['run_id'])).exists() and not state['cancel'].is_set(): time.sleep(.02)
                text='admitted'
            elif turn['phase']=='update':
                answer=mcp(server,'siriai_session_send',dict(turn_id=turn['turn_id'],request_id=turn['instruction_id'],session_id=turn['targets'][0]['session_id'],prompt=turn['instruction']))
                assert not answer['isError'],answer;text='confirmed'
            else: text='collected:'+'|'.join(t['result'] or t['status'] for t in turn['targets'])
        else:
            while objective.startswith('hold-') and not (ROOT/('release-worker-'+objective)).exists() and not state['cancel'].is_set(): time.sleep(.02)
            text='worker:'+objective+(':'+state['injected'] if state['injected'] else '')
        if state['cancel'].is_set(): emit(dict(id=frame['id'],result=dict(stopReason='cancelled')));return
        emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text)))))
        emit(dict(id=frame['id'],result=dict(stopReason='end_turn')))
    except Exception:
        import traceback
        record('errors.jsonl',dict(error=traceback.format_exc()));emit(dict(id=frame['id'],error=dict(code=-32000,message='conversation fixture failed')))
for line in sys.stdin:
    frame=json.loads(line);record('wire.jsonl',dict(direction='client',frame=frame));method=frame.get('method');params=frame.get('params',{})
    if method=='initialize': emit(dict(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,mcpCapabilities=dict(http=True)),agentInfo=dict(name='independent-conversations',version='1'),_meta=dict(steering=dict(supported=True)))))
    elif method in ['session/new','session/load','session/resume']:
        sid=params.get('sessionId') or uuid.uuid4().hex;sessions[sid]=params['mcpServers'];record('sessions.jsonl',dict(sid=sid,cwd=params['cwd'],mode=MODE,method=method))
        emit(dict(id=frame['id'],result=dict(sessionId=sid) if method=='session/new' else {}))
    elif method=='session/prompt': threading.Thread(target=prompt,args=(frame,),daemon=True).start()
    elif method=='session/cancel':
        if params['sessionId'] in active: active[params['sessionId']]['cancel'].set()
    elif method=='_session/steering':
        state=active[params['sessionId']];state['injected']=params['prompt'][-1]['text'];record('injections.jsonl',dict(sid=params['sessionId'],text=state['injected']))
        emit(dict(id=frame['id'],result=dict(outcome='injected')))
    else: raise AssertionError(method)
