#!/usr/bin/env python3
"""Independent native ACP/MCP peer for attachment ownership, negotiation and crash behavior."""
import hashlib,json,os,sys,threading,time,urllib.request,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_COORD_ATTACHMENTS_ROOT']);MODE=sys.argv[1];WRITE=threading.Lock();RECORD=threading.Lock();sessions={};active={}
def record(name,value):
    with RECORD,(ROOT/name).open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),**value),ensure_ascii=False)+'\n')
def emit(value):
    frame=dict(jsonrpc='2.0',**value)
    with WRITE:record('wire.jsonl',dict(direction='agent',frame=frame));print(json.dumps(frame),flush=True)
def digest(blocks):return hashlib.sha256(json.dumps(blocks,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def mcp(server,name,args):
    frame=dict(jsonrpc='2.0',id=uuid.uuid4().hex,method='tools/call',params=dict(name=name,arguments=args));headers={h['name']:h['value'] for h in server['headers']};headers.update({'Content-Type':'application/json','Accept':'application/json,text/event-stream'})
    with urllib.request.urlopen(urllib.request.Request(server['url'],json.dumps(frame).encode(),headers=headers),timeout=20) as r:reply=json.loads(r.read())
    record('mcp-wire.jsonl',dict(request=frame,response=reply));return reply['result']
def prompt(frame):
    sid=frame['params']['sessionId'];state=dict(cancel=threading.Event(),injected=None);active[sid]=state
    try:
        blocks=frame['params']['prompt'];objective=blocks[0]['text'];record('prompts.jsonl',dict(mode=MODE,sid=sid,objective=objective,blocks=blocks))
        if MODE=='coordinator':
            turn=json.loads(objective);record('turns.jsonl',dict(sid=sid,turn=turn));server=sessions[sid][0]
            if turn['phase']=='initial':
                while turn['objective'].startswith('hold-admission') and not (ROOT/('release-admission-'+turn['run_id'])).exists() and not state['cancel'].is_set():time.sleep(.02)
                if state['cancel'].is_set():emit(dict(id=frame['id'],result=dict(stopReason='cancelled')));return
                answer=mcp(server,'siriai_session_start',dict(turn_id=turn['turn_id'],request_id='same-child',prompt=turn['objective']))
                assert not answer['isError'],answer;child=json.loads(answer['content'][0]['text']);record('children.jsonl',dict(parent=turn['run_id'],child=child))
                retry=mcp(server,'siriai_session_start',dict(turn_id=turn['turn_id'],request_id='same-child',prompt=turn['objective']));assert json.loads(retry['content'][0]['text'])['delegation_id']==child['delegation_id']
                text='admitted'
            elif turn['phase']=='update':
                assert len(blocks)==1,'original attachments replayed into update'
                answer=mcp(server,'siriai_session_send',dict(turn_id=turn['turn_id'],request_id=turn['instruction_id'],session_id=turn['targets'][0]['session_id'],prompt=turn['instruction']));assert not answer['isError'],answer;text='confirmed'
            else:
                assert len(blocks)==1,'original attachments replayed into result'
                text='collected:'+'|'.join(t['result'] or t['status'] for t in turn['targets'])
        else:
            while objective.startswith('hold-') and not (ROOT/('release-worker-'+objective)).exists() and not state['cancel'].is_set():time.sleep(.02)
            text='native-content:'+digest(blocks[1:])+(':'+state['injected'] if state['injected'] else '')
        if state['cancel'].is_set():emit(dict(id=frame['id'],result=dict(stopReason='cancelled')));return
        emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text)))))
        emit(dict(id=frame['id'],result=dict(stopReason='end_turn')))
    except Exception:
        import traceback
        record('errors.jsonl',dict(error=traceback.format_exc()));emit(dict(id=frame['id'],error=dict(code=-32000,message='independent attachment fixture failed')))
for line in sys.stdin:
    frame=json.loads(line);record('wire.jsonl',dict(direction='client',frame=frame));method=frame.get('method');params=frame.get('params',{})
    if method=='initialize':
        capabilities=json.loads((ROOT/'capabilities.json').read_text())[MODE]
        emit(dict(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,promptCapabilities=capabilities,mcpCapabilities=dict(http=True)),agentInfo=dict(name='independent-attachments',version='1'),_meta=dict(steering=dict(supported=True)))))
    elif method in ['session/new','session/load','session/resume']:
        sid=params.get('sessionId') or uuid.uuid4().hex;sessions[sid]=params['mcpServers'];record('sessions.jsonl',dict(mode=MODE,sid=sid,cwd=params['cwd'],method=method));emit(dict(id=frame['id'],result=dict(sessionId=sid) if method=='session/new' else {}))
    elif method=='session/prompt':threading.Thread(target=prompt,args=(frame,),daemon=True).start()
    elif method=='_session/steering':
        blocks=params['prompt'];record('injections.jsonl',dict(sid=params['sessionId'],blocks=blocks));active[params['sessionId']]['injected']=blocks[-1]['text'];emit(dict(id=frame['id'],result=dict(outcome='injected')))
    elif method=='session/cancel':
        if params['sessionId'] in active:active[params['sessionId']]['cancel'].set()
    else:raise AssertionError(method)
