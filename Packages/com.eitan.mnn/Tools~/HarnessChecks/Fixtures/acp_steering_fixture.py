import json,os,sys,threading,time,uuid
from pathlib import Path
MODE=sys.argv[1];ROOT=Path(os.environ['SIRIAI_STEER_ROOT']);ROOT.mkdir(parents=True,exist_ok=True);LOCK=threading.RLock();sessions={};pending={}
def record(direction,frame):
 with LOCK:
  with (ROOT/'wire.jsonl').open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),direction=direction,frame=frame))+'\n')
def emit(**frame):
 frame=dict(jsonrpc='2.0',**frame)
 with LOCK:record('agent',frame);print(json.dumps(frame),flush=True)
def activity(sid,text):emit(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='tool_call',toolCallId='working',title=text,status='in_progress')))
def request(method,params):
 rid=uuid.uuid4().hex;event=threading.Event();value=dict(event=event);pending[rid]=value;emit(id=rid,method=method,params=params)
 assert event.wait(25),'No client response';pending.pop(rid);return value['frame']
def prompt(frame):
 try:
  p=frame['params'];sid=p['sessionId'];state=sessions[sid];blocks=p['prompt']
  if blocks[0].get('text','').startswith('{'):state.update(prompts=[],updates=[],effects=0,promptRequired=False)
  state['prompts'].append(blocks);first=len(state['prompts'])==1
  if first:
   state['case']=json.loads(blocks[0]['text']);state['effects']+=1
  else:state['updates'] += [b['text'] for b in blocks[1:] if b.get('type')=='text']
  state['cancel']=threading.Event();activity(sid,'working')
  if first and MODE=='pull':
   wrong=request('craft/drainMidTurnQueue',dict(sessionId='foreign'));assert wrong.get('error',{}).get('code')==-32602
   while len(state['updates'])<state['case'].get('updates',1) and not state['cancel'].is_set():
    answer=request('craft/drainMidTurnQueue',dict(sessionId=sid));assert 'result' in answer,answer
    for item in answer['result']['items']:state['updates'] += [b['text'] for b in item['content']]
    time.sleep(.03)
  end=time.monotonic()+40
  while first and len(state['updates'])<state['case'].get('updates',1) and not state['cancel'].is_set() and not state.get('promptRequired'):
   assert time.monotonic()<end;time.sleep(.01)
  if state['cancel'].is_set():
   if MODE=='ignore-cancel':time.sleep(35)
   else:emit(id=frame['id'],result=dict(stopReason='cancelled'));return
  if not state.get('promptRequired') or not first:
   text=json.dumps(dict(updates=state['updates'],prompts=len(state['prompts']),sideEffects=state['effects']),ensure_ascii=False)
   emit(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text))))
  emit(id=frame['id'],result=dict(stopReason='end_turn'))
 except Exception:
  import traceback
  with (ROOT/'errors.txt').open('a') as f:f.write(traceback.format_exc())
  raise
for line in sys.stdin:
 frame=json.loads(line);record('client',frame);m=frame.get('method');p=frame.get('params',{})
 if m=='initialize':emit(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True),agentInfo=dict(name='independent-steering-agent',version='1'),_meta=dict(steering=dict(supported=MODE in ['extension','required','unknown','reject','lost','error']))))
 elif m=='session/new':
  sid=uuid.uuid4().hex;sessions[sid]=dict(prompts=[],updates=[],effects=0);emit(id=frame['id'],result=dict(sessionId=sid))
 elif m=='session/prompt':threading.Thread(target=prompt,args=(frame,),daemon=True).start()
 elif m=='_session/steering':
  assert MODE in ['extension','required','unknown','reject','lost','error'];state=sessions[p['sessionId']];assert p['_meta']==dict(steering=dict(idleBehavior='promptRequired'))
  if MODE=='reject':emit(id=frame['id'],error=dict(code=-32601,message='explicitly unsupported'))
  elif MODE=='required':state['promptRequired']=True;emit(id=frame['id'],result=dict(outcome='promptRequired'))
  elif MODE=='error':state['updates'] += [b['text'] for b in p['prompt'][1:]];emit(id=frame['id'],error=dict(code=-32603,message='execution may already have happened'))
  elif MODE=='unknown':emit(id=frame['id'],result=dict(outcome='startedNewTurn'))
  else:
   state['updates'] += [b['text'] for b in p['prompt'][1:]]
   if MODE!='lost':emit(id=frame['id'],result=dict(outcome='injected'))
 elif m=='session/cancel':
  state=sessions[p['sessionId']];state['cancel'].set()
 elif frame.get('id') in pending:
  value=pending[frame['id']];value['frame']=frame;value['event'].set()
 else:raise AssertionError('Unexpected '+str(frame))
