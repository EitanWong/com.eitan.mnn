"""Independent ACP peer; persistent native contexts are intentionally not managed aliases."""
import json,os,sys,threading,time,uuid
from pathlib import Path
root=Path(os.environ['NATIVE_PROJECT_ROOT']);write=threading.Lock();cancels={}
def emit(**f):
 with write:print(json.dumps(dict(jsonrpc='2.0',**f)),flush=True)
def record(**v):
 with write:
  with (root/'events.jsonl').open('a') as f:f.write(json.dumps(v)+'\n')
def prompt(f):
 p=f['params'];sid=p['sessionId'];text=p['prompt'][0]['text'];cancel=threading.Event();cancels[sid]=cancel
 path=root/('history-'+sid+'.json');history=json.loads(path.read_text()) if path.exists() else []
 record(method='prompt',sid=sid,text=text,prior=len(history))
 if text in ['wait','crash']:
  while not cancel.wait(.02) and not (root/('release-'+text)).exists():pass
 if cancel.is_set():emit(id=f['id'],result=dict(stopReason='cancelled'));return
 history.append(text);path.write_text(json.dumps(history))
 emit(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='worker:'+text+'|prior='+str(len(history)-1)))))
 emit(id=f['id'],result=dict(stopReason='end_turn'))
for line in sys.stdin:
 f=json.loads(line);m=f.get('method');p=f.get('params',{})
 record(method=m,params=p)
 if m=='initialize':emit(id=f['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,sessionCapabilities=dict(resume={})),agentInfo=dict(name='native-project-peer',version='1')))
 elif m in ['session/new','session/load','session/resume']:
  sid=p.get('sessionId') or 'native-'+uuid.uuid4().hex+('' if (root/'short-next').exists() else 'x'*130)
  if m=='session/new' and (root/'reuse-next').exists():sid=(root/'reuse-next').read_text();(root/'reuse-next').unlink()
  (root/'short-next').unlink(missing_ok=True)
  emit(id=f['id'],result=dict(sessionId=sid) if m=='session/new' else {})
 elif m=='session/prompt':threading.Thread(target=prompt,args=(f,),daemon=True).start()
 elif m=='session/cancel':cancels[p['sessionId']].set()
 else:raise AssertionError(m)
