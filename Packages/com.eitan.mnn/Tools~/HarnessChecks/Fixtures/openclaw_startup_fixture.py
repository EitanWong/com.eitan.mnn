#!/usr/bin/env python3
"""Independent OpenClaw-shaped ACP peer; requires an actually listening TCP gateway."""
import json,os,socket,sys,threading,time,urllib.parse,uuid
from pathlib import Path
root=Path(os.environ['OPENCLAW_STARTUP_TEST_ROOT']);root.mkdir(parents=True,exist_ok=True)
lock=threading.Lock()
def event(kind,**fields):
 with (root/'wire.jsonl').open('a') as out:out.write(json.dumps(dict(kind=kind,pid=os.getpid(),**fields))+'\n')
def emit(value):
 with lock:print(json.dumps(value),flush=True)
event('spawn',argv=sys.argv[1:]);args=sys.argv[1:];assert args[0]=='acp' and args[1]=='--url' and args[3]=='--verbose',args
address=urllib.parse.urlparse(args[2]);sid='session-'+uuid.uuid4().hex
for line in sys.stdin:
 frame=json.loads(line);method=frame.get('method');event('request',frame=frame)
 if method=='initialize':
  try:
   with socket.create_connection((address.hostname,address.port),timeout=.2):pass
   if (root/'reject').exists():raise OSError('fixture TLS/auth/routing rejection')
  except OSError:
   emit(dict(jsonrpc='2.0',id=frame['id'],error=dict(code=-32000,message='gateway unavailable or rejected')));continue
  while (root/'hold-initialize').exists():time.sleep(.02)
  emit(dict(jsonrpc='2.0',id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True))))
 elif method in ['session/new','session/load']:
  sid=frame.get('params',{}).get('sessionId',sid);emit(dict(jsonrpc='2.0',id=frame['id'],result=dict(sessionId=sid)))
 elif method=='session/prompt':
  text=frame['params']['prompt'][0]['text']
  if text=='hold':continue
  emit(dict(jsonrpc='2.0',method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='startup result: '+text)))))
  emit(dict(jsonrpc='2.0',id=frame['id'],result=dict(stopReason='end_turn')))
