#!/usr/bin/env python3
"""Independent native ACP elicitation peer; never imports the Host implementation."""
import json,os,sys,threading,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_INPUT_ROOT']);ROOT.mkdir(exist_ok=True)
lock=threading.Lock();pending={};turns={};sessions=set()
def record(direction,frame):
 with (ROOT/'wire.jsonl').open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),direction=direction,frame=frame))+'\n')
def emit(**frame):
 frame=dict(jsonrpc='2.0',**frame)
 with lock:record('agent',frame);print(json.dumps(frame),flush=True)
def finish(prompt,sid,result):
 if turns.get(sid)!=prompt:return
 emit(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=json.dumps(result,ensure_ascii=False)))))
 emit(id=prompt,result=dict(stopReason='end_turn'));turns.pop(sid,None)
def schema():
 return dict(type='object',properties={
  'name':dict(type='string',title='Name',minLength=2,maxLength=20,pattern='^[^0-9]+$'),
  'count':dict(type='integer',minimum=1,maximum=9,default=2),
  'ratio':dict(type='number',minimum=0,maximum=1,default=.5),
  'enabled':dict(type='boolean',default=False),
  'color':dict(type='string',oneOf=[dict(const='red',title='Red'),dict(const='blue',title='Blue')],default='blue'),
  'tags':dict(type='array',items=dict(anyOf=[dict(const='中',title='Chinese'),dict(const='a',title='A')]),minItems=1,maxItems=2,default=['中']),
  'note':dict(type='string',description='Optional')},required=['name','count','ratio','enabled','color','tags'])
try:
 for line in sys.stdin:
  frame=json.loads(line);record('client',frame);method=frame.get('method');p=frame.get('params',{})
  if method=='initialize':
   (ROOT/('capabilities-'+str(os.getpid())+'.json')).write_text(json.dumps(p['clientCapabilities']))
   emit(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True),agentInfo=dict(name='independent-elicitation-agent',version='1')))
  elif method in ['session/new','session/load']:
   sid=p.get('sessionId') or 'input-'+uuid.uuid4().hex;sessions.add(sid)
   emit(id=frame['id'],result=dict(sessionId=sid) if method=='session/new' else {})
  elif method=='session/prompt':
   sid=p['sessionId'];case=json.loads(p['prompt'][0]['text']);turns[sid]=frame['id']
   payload=dict(sessionId=sid,mode='form',message='Choose settings',requestedSchema=case.get('schema',schema()))
   if case.get('mode'):payload['mode']=case['mode']
   if case.get('wrong_session'):payload['sessionId']='unrelated-session'
   if payload['mode']=='url':payload=dict(sessionId=sid,mode='url',message='Sign in',url='https://example.com/auth',elicitationId='url-1')
   count=case.get('count',1)
   for i in range(count):
    request='native-'+uuid.uuid4().hex;pending[request]=dict(prompt=frame['id'],sid=sid,index=i,count=count,results=[],delay=case.get('after_answer_delay',0))
    emit(id=request,method='elicitation/create',params=payload)
   emit(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='tool_call',toolCallId='input-tool',title='Progress while asking',kind='other',status='in_progress')))
  elif method=='session/cancel':
   sid=p['sessionId'];prompt=turns.pop(sid,None)
   if prompt is not None:emit(id=prompt,result=dict(stopReason='cancelled'))
  elif method is None and 'id' in frame:
   item=pending.pop(frame['id'],None)
   if item:
    result=frame.get('result',dict(error=frame.get('error')))
    with (ROOT/'decisions.jsonl').open('a') as f:f.write(json.dumps(dict(sid=item['sid'],result=result))+'\n')
    if item['sid'] in turns:
     left=[v for v in pending.values() if v['prompt']==item['prompt']]
     if left:left[0]['results'].extend(item['results']+[result])
     elif item['delay']:threading.Timer(item['delay'],finish,args=(item['prompt'],item['sid'],item['results']+[result])).start()
     else:finish(item['prompt'],item['sid'],item['results']+[result])
  else:raise AssertionError(method)
except Exception:
 import traceback
 (ROOT/('error-'+str(os.getpid())+'.txt')).write_text(traceback.format_exc());raise
