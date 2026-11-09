#!/usr/bin/env python3
"""Independent ACP peer for typed native steering and original coordinator runs."""
import json,os,sys,threading,time,urllib.request,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_INSTRUCTION_CONTENT_ROOT']);MODE=sys.argv[1];WRITE=threading.Lock();RECORD=threading.Lock();sessions={};states={};pending={}
def record(name,value):
    with RECORD,(ROOT/name).open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),mode=MODE,**value),ensure_ascii=False)+'\n')
def emit(value):
    frame=dict(jsonrpc='2.0',**value)
    with WRITE:record('wire.jsonl',dict(direction='agent',frame=frame));print(json.dumps(frame),flush=True)
def request(method,params):
    rid=uuid.uuid4().hex;entry=dict(ready=threading.Event());pending[rid]=entry;emit(dict(id=rid,method=method,params=params));assert entry['ready'].wait(20);pending.pop(rid);return entry['reply']
def mcp(server,name,args):
    frame=dict(jsonrpc='2.0',id=uuid.uuid4().hex,method='tools/call',params=dict(name=name,arguments=args));headers={h['name']:h['value'] for h in server['headers']};headers.update({'Content-Type':'application/json','Accept':'application/json,text/event-stream'})
    with urllib.request.urlopen(urllib.request.Request(server['url'],json.dumps(frame).encode(),headers=headers),timeout=20) as response:reply=json.loads(response.read())
    record('mcp-wire.jsonl',dict(request=frame,response=reply));return reply['result']
def prompt(frame):
    p=frame['params'];sid=p['sessionId'];blocks=p['prompt'];follow=blocks[0]['text'].startswith('用户更新了当前工作的要求')
    state=states.get(sid) if follow else None
    if state is None:state=dict(objective=blocks[0]['text'],updates=[],effects=1,ready=threading.Event());states[sid]=state
    state['cancel']=threading.Event();record('prompts.jsonl',dict(sid=sid,blocks=blocks,follow=follow,objective=state['objective']))
    try:
        if MODE=='coordinator':
            turn=json.loads(blocks[0]['text']);record('turns.jsonl',dict(sid=sid,turn=turn,blocks=blocks));server=sessions[sid][0]
            if turn['phase']=='initial':
                reply=mcp(server,'siriai_session_start',dict(turn_id=turn['turn_id'],request_id='child',prompt=turn['objective']));assert not reply['isError'],reply
                record('children.jsonl',dict(parent=turn['run_id'],child=json.loads(reply['content'][0]['text'])));text='admitted'
            elif turn['phase']=='update':
                if turn['instruction']=='hold-routing':
                    while not (ROOT/'release-routing').exists() and not state['cancel'].is_set():time.sleep(.02)
                reply=mcp(server,'siriai_session_send',dict(turn_id=turn['turn_id'],request_id=turn['instruction_id'],session_id=turn['targets'][0]['session_id'],prompt=turn['instruction']));text=json.dumps(reply)
            else:text='collected:'+'|'.join(v['result'] or v['status'] for v in turn['targets'])
        else:
            if follow:state['updates'].append(blocks[1:]);record('updates.jsonl',dict(sid=sid,blocks=blocks[1:],route='followup',objective=state['objective']));state['ready'].set();emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='tool_call',toolCallId='followup',title='Input accepted',status='in_progress'))))
            if MODE=='pull' and not follow:
                while not state['cancel'].is_set() and not state['ready'].is_set():
                    reply=request('craft/drainMidTurnQueue',dict(sessionId=sid));assert 'result' in reply,reply
                    for item in reply['result']['items']:
                        state['updates'].append(item['content']);record('updates.jsonl',dict(sid=sid,blocks=item['content'],route='pull',objective=state['objective']));state['ready'].set()
                    time.sleep(.02)
            while not (ROOT/('release-'+state['objective'])).exists() and not state['cancel'].is_set():time.sleep(.02)
            if state['cancel'].is_set():emit(dict(id=frame['id'],result=dict(stopReason='cancelled')));return
            if state.get('required') and not follow:emit(dict(id=frame['id'],result=dict(stopReason='end_turn')));return
            text=json.dumps(dict(updates=state['updates'],effects=state['effects']),ensure_ascii=False)
        emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text)))))
        emit(dict(id=frame['id'],result=dict(stopReason='end_turn')))
    except Exception:
        import traceback
        record('errors.jsonl',dict(error=traceback.format_exc()));emit(dict(id=frame['id'],error=dict(code=-32000,message='independent instruction fixture failed')))
for line in sys.stdin:
    frame=json.loads(line);record('wire.jsonl',dict(direction='client',frame=frame));method=frame.get('method');p=frame.get('params',{})
    if method=='initialize':
        caps=json.loads((ROOT/'capabilities.json').read_text()).get(MODE,{})
        emit(dict(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,mcpCapabilities=dict(http=True),promptCapabilities=caps),agentInfo=dict(name='independent-instruction-content',version='1'),_meta=dict(steering=dict(supported=MODE not in ['pull','restart'])))))
    elif method in ['session/new','session/load','session/resume']:
        sid=p.get('sessionId') or uuid.uuid4().hex;sessions[sid]=p['mcpServers'];record('sessions.jsonl',dict(sid=sid,cwd=p['cwd']));emit(dict(id=frame['id'],result=dict(sessionId=sid) if method=='session/new' else {}))
    elif method=='session/prompt':threading.Thread(target=prompt,args=(frame,),daemon=True).start()
    elif method=='_session/steering':
        state=states[p['sessionId']];blocks=p['prompt'][1:];text=blocks[0]['text'];record('updates.jsonl',dict(sid=p['sessionId'],blocks=blocks,route='extension',objective=state['objective']))
        if text=='reject':emit(dict(id=frame['id'],error=dict(code=-32602,message='unsupported content')))
        elif text=='unknown':emit(dict(id=frame['id'],result=dict(outcome='startedNewTurn')))
        elif text=='required':
            state['required']=True
            emit(dict(id=frame['id'],result=dict(outcome='promptRequired')));(ROOT/('release-'+state['objective'])).touch()
        elif text=='lost':pass
        else:state['updates'].append(blocks);state['ready'].set();emit(dict(id=frame['id'],result=dict(outcome='injected')))
    elif method=='session/cancel':
        if p['sessionId'] in states:states[p['sessionId']]['cancel'].set()
    elif frame.get('id') in pending:entry=pending[frame['id']];entry['reply']=frame;entry['ready'].set()
    else:raise AssertionError(method)
