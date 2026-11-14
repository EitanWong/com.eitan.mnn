#!/usr/bin/env python3
"""Independent ACP input peer; exact requests/decisions and completion notifications are observable."""
import json,os,sys,threading,time,urllib.request,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_INPUT_MODES_ROOT']);MODE=sys.argv[1] if len(sys.argv)>1 else 'direct';WRITE=threading.Lock();RECORD=threading.Lock();pending={};sessions={};turns={}
def record(name,value):
 with RECORD,(ROOT/name).open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),mode=MODE,**value),ensure_ascii=False)+'\n')
def emit(**frame):
 frame=dict(jsonrpc='2.0',**frame)
 with WRITE:record('wire.jsonl',dict(direction='agent',frame=frame));print(json.dumps(frame),flush=True)
def notify(sid,text):emit(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text))))
def complete_watch(native_id,stop):
 while not stop.is_set():
  marker=ROOT/('complete-'+native_id)
  if marker.exists():
   value=marker.read_text();marker.unlink();emit(method='elicitation/complete',params=dict(elicitationId=value or native_id))
  time.sleep(.01)
def ask(sid,spec,cancel):
 identifier='native-'+uuid.uuid4().hex;event=threading.Event();item=dict(event=event,reply=None);pending[identifier]=item
 payload=dict(sessionId=sid,message=spec.get('message','Native input request'))
 if not spec.get('omit_mode'):payload['mode']=spec.get('mode','url')
 if spec.get('mode','url')=='url' and not spec.get('omit_mode'):
  payload.update(url=spec.get('url','https://example.invalid/auth'),elicitationId=spec.get('elicitationId','flow-'+uuid.uuid4().hex))
  if spec.get('omit_id'):payload.pop('elicitationId')
 if 'schema' in spec:payload['requestedSchema']=spec['schema']
 if spec.get('wrong_session'):payload['sessionId']='foreign-session'
 if spec.get('request_scope'):payload.pop('sessionId');payload['requestId']='unrelated-request'
 if spec.get('mode')=='permission':payload=dict(sessionId=sid,toolCall=dict(toolCallId=identifier,title='Allow explicit action?'),options=[dict(kind='allow_once',optionId='allow',name='Allow'),dict(kind='reject_once',optionId='deny',name='Deny')])
 record('requests.jsonl',dict(sid=sid,id=identifier,payload=payload));emit(id=identifier,method='session/request_permission' if spec.get('mode')=='permission' else 'elicitation/create',params=payload)
 if payload.get('mode')=='url' and 'elicitationId' in payload:threading.Thread(target=complete_watch,args=(payload['elicitationId'],event),daemon=True).start()
 while not event.wait(.01):
  if cancel.is_set():break
 pending.pop(identifier,None)
 result=item['reply'] if item['reply'] is not None else dict(error=dict(code=-32800,message='Cancelled peer wait'))
 record('decisions.jsonl',dict(sid=sid,id=identifier,reply=result));return result

def mcp(server,name,args):
 frame=dict(jsonrpc='2.0',id=uuid.uuid4().hex,method='tools/call',params=dict(name=name,arguments=args));headers={x['name']:x['value'] for x in server['headers']};headers.update({'Content-Type':'application/json','Accept':'application/json,text/event-stream'})
 with urllib.request.urlopen(urllib.request.Request(server['url'],json.dumps(frame).encode(),headers=headers),timeout=20) as response:reply=json.loads(response.read())
 record('mcp-wire.jsonl',dict(request=frame,response=reply));assert not reply['result'].get('isError',False),reply;return json.loads(reply['result']['content'][0]['text'])
def prompt(frame):
 sid=frame['params']['sessionId'];cancel=threading.Event();turns[sid]=[frame['id'],cancel]
 try:
  raw=frame['params']['prompt'][0]['text'];case=json.loads(raw);record('prompts.jsonl',dict(sid=sid,objective=raw));turn=None
  if MODE=='coordinator':
   turn=case
   if turn['phase']=='initial':sessions[sid]['case']=json.loads(turn['objective'])
   case=sessions[sid]['case'];record('turns.jsonl',dict(sid=sid,turn=turn))
  requests=case.get('requests',[]) if turn is None or turn['phase']=='initial' else case.get('result_requests',[])
  results=[None]*len(requests)
  threads=[threading.Thread(target=lambda i=i,spec=spec:results.__setitem__(i,ask(sid,spec,cancel))) for i,spec in enumerate(requests)]
  for thread in threads:thread.start()
  emit(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='tool_call',toolCallId='input-progress',title='Native activity while waiting',kind='other',status='in_progress')))
  for thread in threads:thread.join(40)
  assert all(v is not None for v in results),results
  if turn and turn['phase']=='initial' and not cancel.is_set():
   child=mcp(sessions[sid]['servers'][0],'siriai_session_start',dict(turn_id=turn['turn_id'],request_id='child',prompt=json.dumps(case.get('child',{}))));record('children.jsonl',dict(parent=turn['run_id'],child=child))
  delay=case.get('after_answer_delay',0)
  if delay:cancel.wait(delay)
  if not cancel.is_set():notify(sid,json.dumps(results,ensure_ascii=False))
  if turns.get(sid,[None])[0]==frame['id']:emit(id=frame['id'],result=dict(stopReason='cancelled' if cancel.is_set() else 'end_turn'))
 except Exception:
  import traceback
  record('errors.jsonl',dict(error=traceback.format_exc()));emit(id=frame['id'],error=dict(code=-32000,message='Independent input modes peer failed'))
 finally:
  if turns.get(sid,[None])[0]==frame['id']:turns.pop(sid,None)
for line in sys.stdin:
 frame=json.loads(line);record('wire.jsonl',dict(direction='client',frame=frame));method=frame.get('method');p=frame.get('params',{})
 if method=='initialize':
  record('capabilities.jsonl',dict(capabilities=p['clientCapabilities']));emit(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,mcpCapabilities=dict(http=True)),agentInfo=dict(name='independent-input-modes',version='1')))
 elif method in ['session/new','session/load']:
  sid=p.get('sessionId') or uuid.uuid4().hex;sessions[sid]=dict(servers=p['mcpServers'],case=None);emit(id=frame['id'],result=dict(sessionId=sid) if method=='session/new' else {})
 elif method=='session/prompt':threading.Thread(target=prompt,args=(frame,),daemon=True).start()
 elif method=='session/cancel':
  if p['sessionId'] in turns:turns[p['sessionId']][1].set()
 elif method is None and 'id' in frame:
  item=pending.get(frame['id'])
  if item:item['reply']=dict(result=frame['result']) if 'result' in frame else dict(error=frame['error']);item['event'].set()
 else:raise AssertionError(method)
