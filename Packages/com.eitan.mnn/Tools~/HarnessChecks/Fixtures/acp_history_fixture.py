#!/usr/bin/env python3
import fcntl,json,os,sys,time,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_HISTORY_ROOT']);ROOT.mkdir(exist_ok=True);variant=sys.argv[1];sid=None

def record(name,data):
 with (ROOT/name).open('a') as f:fcntl.flock(f,fcntl.LOCK_EX);f.write(json.dumps(data)+'\n');f.flush()
def send(data):record('wire.jsonl',dict(direction='out',message=data,pid=os.getpid(),variant=variant));print(json.dumps(data),flush=True)
def reply(req,data):send(dict(jsonrpc='2.0',id=req['id'],result=data))
def update(sid,entry):send(dict(jsonrpc='2.0',method='session/update',params=dict(sessionId=sid,update=entry)))
for line in sys.stdin:
 req=json.loads(line);record('wire.jsonl',dict(direction='in',message=req,pid=os.getpid(),variant=variant));m=req.get('method');p=req.get('params',{})
 if m=='initialize':reply(req,dict(protocolVersion=1,agentCapabilities=dict(loadSession=variant!='resume-only',sessionCapabilities=dict(resume={},list={})),agentInfo=dict(name='independent-history-peer',version='1')))
 elif m=='session/new':
  sid=uuid.uuid4().hex;(ROOT/(sid+'.json')).write_text(json.dumps(dict(cwd=p['cwd'])));record('new.jsonl',dict(sid=sid,cwd=p['cwd']));reply(req,dict(sessionId=sid))
 elif m=='session/resume':sid=p['sessionId'];record('resumes.jsonl',p);reply(req,{})
 elif m=='session/prompt':
  sid=p['sessionId'];text=p['prompt'][0]['text'];record('prompts.jsonl',dict(sid=sid,text=text));time.sleep(1 if text=='hold' else .1)
  result='fresh:'+text
  for kind,value in [('user_message_chunk',text),('agent_message_chunk',result)]:
   entry=dict(sessionUpdate=kind,messageId=uuid.uuid4().hex,content=dict(type='text',text=value));record(sid+'.history',entry)
   if kind=='agent_message_chunk':update(sid,entry)
  reply(req,dict(stopReason='end_turn'))
 elif m=='session/load':
  sid=p['sessionId'];record('loads.jsonl',dict(sid=sid,pid=os.getpid(),variant=variant,params=p));assert p['cwd']==json.loads((ROOT/(sid+'.json')).read_text())['cwd']
  if variant=='slow':time.sleep(2)
  update('foreign-session',dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='FOREIGN SECRET')))
  entries=[json.loads(x) for x in (ROOT/(sid+'.history')).read_text().splitlines()] if variant!='empty' else []
  if variant=='bulk':entries=[dict(sessionUpdate='agent_message_chunk',messageId='m'+str(i),content=dict(type='text',text='entry'+str(i)+' '+'x'*1000)) for i in range(400)]
  if variant=='oversized':entries=[dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='x'*70000))]+entries
  for entry in entries:update(sid,entry)
  if variant not in ['empty','bulk']:
   update(sid,dict(sessionUpdate='plan',entries=[dict(content='historical plan',priority='high',status='completed')]))
   update(sid,dict(sessionUpdate='tool_call',toolCallId='call-1',title='<b>historical tool</b>',status='in_progress',rawInput=dict(path='old.txt')))
   update(sid,dict(sessionUpdate='tool_call_update',toolCallId='call-1',status='completed',content=[dict(type='content',content=dict(type='text',text='historical tool output'))]))
   update(sid,dict(sessionUpdate='agent_message_chunk',messageId='binary',content=dict(type='image',data='aGk=',mimeType='image/png')))
  if variant=='disconnect':os._exit(7)
  if variant=='bad-update':update(sid,dict(sessionUpdate='agent_message_chunk',content='malformed'))
  if variant=='callbacks':
   send(dict(jsonrpc='2.0',id='forbidden-permission',method='session/request_permission',params=dict(sessionId=sid,toolCall=dict(toolCallId='forbidden',title='Must not execute'),options=[dict(optionId='yes',name='Allow',kind='allow_once')])))
   denied=json.loads(sys.stdin.readline());record('wire.jsonl',dict(direction='in',message=denied,pid=os.getpid(),variant=variant));assert denied.get('error',{}).get('code')==-32601
  reply(req,{})
  # This live-only notification follows the load response in the same process/write turn.
  update(sid,dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='AFTER LOAD RESPONSE')))
 elif m=='session/list':reply(req,dict(sessions=[]))
 elif m is not None and 'id' in req:send(dict(jsonrpc='2.0',id=req['id'],error=dict(code=-32601,message='not implemented')))
