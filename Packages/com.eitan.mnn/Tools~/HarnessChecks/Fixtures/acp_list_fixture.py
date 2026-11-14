#!/usr/bin/env python3
"""Independent ACP peer: account-wide list deliberately includes unowned sessions."""
import fcntl,json,os,sys,time,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_LIST_ROOT']);ROOT.mkdir(exist_ok=True);variant=sys.argv[1];sid=None

def record(name,data):
 with (ROOT/name).open('a') as f:
  fcntl.flock(f,fcntl.LOCK_EX);f.write(json.dumps(data)+'\n');f.flush()
def send(data):
 record('wire.jsonl',dict(direction='out',message=data,pid=os.getpid(),variant=variant));print(json.dumps(data),flush=True)
def reply(req,result):send(dict(jsonrpc='2.0',id=req['id'],result=result))
for line in sys.stdin:
 req=json.loads(line);record('wire.jsonl',dict(direction='in',message=req,pid=os.getpid(),variant=variant));m=req.get('method');p=req.get('params',{})
 if m=='initialize':
  caps=dict(loadSession=True,sessionCapabilities={} if variant=='unsupported' else dict(list={}))
  reply(req,dict(protocolVersion=1,agentCapabilities=caps,agentInfo=dict(name='independent-list-peer',version='1')))
 elif m=='session/new':
  sid=uuid.uuid4().hex;data=dict(sessionId=sid,cwd=p['cwd'],title='Owned native '+sid,updatedAt='2026-11-09T01:00:00Z');(ROOT/(sid+'.json')).write_text(json.dumps(data));record('new.jsonl',data);reply(req,dict(sessionId=sid))
 elif m in ['session/resume','session/load']:
  sid=p['sessionId'];record('loads.jsonl',dict(method=m,sid=sid));reply(req,{})
 elif m=='session/prompt':
  record('prompts.jsonl',dict(sid=p['sessionId'],prompt=p['prompt']));time.sleep(.2)
  send(dict(jsonrpc='2.0',method='session/update',params=dict(sessionId=p['sessionId'],update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='new-task-result')))))
  reply(req,dict(stopReason='end_turn'))
 elif m=='session/list':
  record('lists.jsonl',dict(pid=os.getpid(),variant=variant,params=p))
  if variant=='slow':time.sleep(2)
  if variant=='malformed':reply(req,dict(sessions={}));continue
  if variant=='loop':reply(req,dict(sessions=[],nextCursor=p.get('cursor') or 'loop'));continue
  if not p.get('cursor'):
   reply(req,dict(sessions=[dict(sessionId='foreign-private',cwd=p['cwd'],title='OTHER OWNER SECRET')],nextCursor='x'*3000 if variant=='wide' else 'opaque page +/=?'));continue
  sessions=[json.loads(f.read_text()) for f in ROOT.glob('*.json') if f.name not in ['host.json']]
  if variant=='wrong-cwd':sessions=[dict(v,cwd='/different-workspace') for v in sessions]
  if variant=='oversize':sessions=[dict(sessionId='x'+str(i),cwd=p['cwd']) for i in range(101)]
  reply(req,dict(sessions=sessions))
 elif 'id' in req:send(dict(jsonrpc='2.0',id=req['id'],error=dict(code=-32601,message='not implemented')))
