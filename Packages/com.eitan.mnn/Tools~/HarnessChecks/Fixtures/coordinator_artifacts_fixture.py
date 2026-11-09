#!/usr/bin/env python3
"""Independent ACP peers: observable native outputs, durable children and result gates."""
import json, os, sys, threading, time, urllib.request, uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_COORD_ARTIFACT_ROOT']);MODE=sys.argv[1];WRITE=threading.Lock();RECORD=threading.Lock();sessions={};active={}
def record(name,value):
    with RECORD,(ROOT/name).open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),mode=MODE,**value),ensure_ascii=False)+'\n')
def emit(value):
    frame=dict(jsonrpc='2.0',**value)
    with WRITE:record('wire.jsonl',dict(direction='agent',frame=frame));print(json.dumps(frame),flush=True)
def update(sid,content):emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=content))))
def mcp(server,name,args):
    frame=dict(jsonrpc='2.0',id=uuid.uuid4().hex,method='tools/call',params=dict(name=name,arguments=args));headers={h['name']:h['value'] for h in server['headers']};headers.update({'Content-Type':'application/json','Accept':'application/json,text/event-stream'})
    with urllib.request.urlopen(urllib.request.Request(server['url'],json.dumps(frame).encode(),headers=headers),timeout=20) as r:reply=json.loads(r.read())
    record('mcp-wire.jsonl',dict(request=frame,response=reply));assert not reply['result'].get('isError',False),reply
    return json.loads(reply['result']['content'][0]['text'])
def gate(name,cancel):
    while not (ROOT/('release-'+name)).exists() and not cancel.is_set():time.sleep(.02)
def prompt(frame):
    sid=frame['params']['sessionId'];cancel=threading.Event();active[sid]=cancel
    try:
        objective=frame['params']['prompt'][0]['text'];plans=json.loads((ROOT/'plans.json').read_text());reason='end_turn'
        record('prompts.jsonl',dict(sid=sid,objective=objective))
        if MODE=='coordinator':
            turn=json.loads(objective);record('turns.jsonl',dict(sid=sid,turn=turn));name=turn.get('objective') or sessions[sid][1];plan=plans[name]
            if turn['phase']=='initial':
                sessions[sid][1]=name
                for index,child in enumerate(plan['children']):
                    args=dict(turn_id=turn['turn_id'],request_id='child-'+str(index),prompt=child)
                    value=mcp(sessions[sid][0][0],'siriai_session_start',args);record('children.jsonl',dict(parent=turn['run_id'],child=value))
                    assert mcp(sessions[sid][0][0],'siriai_session_start',args)['delegation_id']==value['delegation_id']
                blocks=plan.get('initial_blocks',[]);text='admitted'
            elif turn['phase']=='result':
                if plan.get('hold_result'):gate('result-'+turn['run_id'],cancel)
                blocks=plan.get('result_blocks',[]);text='collected:'+name;reason=plan.get('result_reason','end_turn')
            elif turn['phase']=='update':
                target=next(v for v in turn['targets'] if v['status']=='completed')
                value=mcp(sessions[sid][0][0],'siriai_session_send',dict(turn_id=turn['turn_id'],request_id=turn['instruction_id'],session_id=target['session_id'],prompt=turn['instruction']))
                record('children.jsonl',dict(parent=turn['run_id'],child=value,continuation=True));blocks=[];text='continued'
            else:raise AssertionError('Unexpected update')
        else:
            plan=plans[objective];blocks=plan['blocks'];text=plan.get('text','worker:'+objective);reason=plan.get('reason','end_turn')
        for block in blocks:update(sid,block)
        if MODE=='worker' and plan.get('hold'):gate(objective,cancel)
        if cancel.is_set():emit(dict(id=frame['id'],result=dict(stopReason='cancelled')));return
        update(sid,dict(type='text',text=text));emit(dict(id=frame['id'],result=dict(stopReason=reason)))
    except Exception:
        import traceback
        record('errors.jsonl',dict(error=traceback.format_exc()));emit(dict(id=frame['id'],error=dict(code=-32000,message='Independent artifact peer failed')))
for line in sys.stdin:
    frame=json.loads(line);record('wire.jsonl',dict(direction='client',frame=frame));method=frame.get('method');params=frame.get('params',{})
    if method=='initialize':emit(dict(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,mcpCapabilities=dict(http=True)),agentInfo=dict(name='independent-coordinator-artifacts',version='1'))))
    elif method in ['session/new','session/load']:
        sid=params.get('sessionId') or uuid.uuid4().hex;sessions[sid]=[params['mcpServers'],None];emit(dict(id=frame['id'],result=dict(sessionId=sid) if method=='session/new' else {}))
    elif method=='session/prompt':threading.Thread(target=prompt,args=(frame,),daemon=True).start()
    elif method=='session/cancel':
        if params['sessionId'] in active:active[params['sessionId']].set()
    else:raise AssertionError(method)
