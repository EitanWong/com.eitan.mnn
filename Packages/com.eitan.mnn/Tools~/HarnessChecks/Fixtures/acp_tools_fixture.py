#!/usr/bin/env python3
import fcntl,json,os,sys,time,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_TOOL_ROOT']);ROOT.mkdir(exist_ok=True);sid=None

def record(name,data):
 with (ROOT/name).open('a') as f:fcntl.flock(f,fcntl.LOCK_EX);f.write(json.dumps(data)+'\n');f.flush()
def send(data):record('wire.jsonl',dict(direction='out',message=data,pid=os.getpid()));print(json.dumps(data),flush=True)
def reply(req,data):send(dict(jsonrpc='2.0',id=req['id'],result=data))
def update(value,session=None):send(dict(jsonrpc='2.0',method='session/update',params=dict(sessionId=session or sid,update=value)))
for line in sys.stdin:
 req=json.loads(line);record('wire.jsonl',dict(direction='in',message=req,pid=os.getpid()));m=req.get('method');p=req.get('params',{})
 if m=='initialize':reply(req,dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,sessionCapabilities=dict(resume={})),agentInfo=dict(name='tool-delta-peer',version='1')))
 elif m=='session/new':sid=uuid.uuid4().hex;record('new.jsonl',dict(sid=sid,cwd=p['cwd']));reply(req,dict(sessionId=sid))
 elif m in ['session/resume','session/load']:sid=p['sessionId'];reply(req,{})
 elif m=='session/prompt':
  sid=p['sessionId'];case=json.loads(p['prompt'][0]['text']);record('prompts.jsonl',dict(sid=sid,case=case))
  if case.get('mode')=='bulk':
   for i in range(40):update(dict(sessionUpdate='tool_call',toolCallId='bulk-'+str(i),title='Bulk '+str(i),status='completed',rawOutput={'data':'值"\\'*1000}))
  elif case.get('mode')=='oversize':
   update(dict(sessionUpdate='tool_call',toolCallId='big',title='Large native tool',status='in_progress',rawInput={'data':'x'*12000}))
   update(dict(sessionUpdate='tool_call_update',toolCallId='big',status='completed',rawOutput={'answer':42}))
  elif case.get('mode')=='escaped':
   update(dict(sessionUpdate='tool_call',toolCallId='\\'*4096,title='escaped',status='completed',rawInput={'v':'\\'*3000},rawOutput={'v':'\\'*3000},content=[dict(type='content',content=dict(type='text',text='\\'*3000))]))
  elif case.get('mode')=='invalid':update(dict(sessionUpdate='tool_call_update',status='completed'))
  elif case.get('mode')!='empty':
   update(dict(sessionUpdate='tool_call',toolCallId='native-1',title='<b>Inspect work</b>',kind='read',status='in_progress',rawInput={'path':'original.txt'},content=[dict(type='content',content=dict(type='text',text='old content'))],locations=[dict(path='/workspace/original.txt',line=3)]))
   time.sleep(.25);update(dict(sessionUpdate='tool_call_update',toolCallId='native-1',rawOutput={'progress':1}))
   update(dict(sessionUpdate='tool_call',toolCallId='native-2',title='Independent tool',kind='execute',status='pending',rawInput={'command':'not executed by client'}))
   update(dict(sessionUpdate='tool_call_update',toolCallId='orphan',status='in_progress',rawInput=None))
   time.sleep(.25);update(dict(sessionUpdate='tool_call_update',toolCallId='native-1',title=None,kind=None,content=[dict(type='diff',path='/workspace/original.txt',oldText='before',newText='after')],locations=[],rawOutput={'answer':42},status='completed'))
   update(dict(sessionUpdate='tool_call_update',toolCallId='native-2',status='failed',content=[dict(type='content',content=dict(type='text',text='native failure'))]))
   update(dict(sessionUpdate='tool_call_update',toolCallId='orphan',title='Late title',status='completed'))
   update(dict(sessionUpdate='tool_call',toolCallId='foreign',title='FOREIGN SECRET',status='completed'),session='not-owned')
   if case.get('mode')=='long':update(dict(sessionUpdate='tool_call_update',toolCallId='native-1',rawOutput={'text':'y'*5000+'EXACT END'}))
  update(dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='fresh result')));reply(req,dict(stopReason='end_turn'))
