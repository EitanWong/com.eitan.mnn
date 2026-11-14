"""Independent ACP/MSP peer. Initialization never creates a native session."""
import json,os,sys,threading,time,uuid
from pathlib import Path
root=Path(os.environ['READINESS_PEER']);mode=sys.argv[1];write=threading.Lock();cancel=threading.Event()
def record(f):
 with write:
  with (root/'events.jsonl').open('a') as o:o.write(json.dumps(dict(pid=os.getpid(),mode=mode,**f))+'\n')
def emit(**v):
 with write:print(json.dumps(dict(jsonrpc='2.0',**v)),flush=True)
def prompt(f):
 cancel.wait(25);emit(id=f['id'],result=dict(stopReason='cancelled'))
for line in sys.stdin:
 f=json.loads(line);record(f);m=f.get('method');p=f.get('params',{})
 if m=='initialize':
  if mode=='exit':raise SystemExit(7)
  if mode=='hang':time.sleep(60)
  if mode=='hold':
   while not (root/'release-hold').exists():time.sleep(.03)
  if mode=='msp':emit(id=f['id'],result=dict(schema=dict(version=1,fingerprint='sha256:c94c3d1f2f51e6424074b9ec43eac3eedcf532d3dcd77351ec1383a87d3049c6'),sessionDurability='durable'))
  elif mode=='invalid':emit(id=f['id'],result=dict(protocolVersion=99,agentCapabilities={}))
  else:emit(id=f['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,sessionCapabilities=dict(resume={},list={}),promptCapabilities=dict(image=True,audio=False,embeddedContext=True),mcpCapabilities=dict(http=mode!='no-http',sse=False)),agentInfo=dict(name='independent-readiness',version='1'),_meta=dict(steering=dict(supported=True))))
 elif m=='initialized':pass
 elif m=='session/new':emit(id=f['id'],result=dict(sessionId='native-'+uuid.uuid4().hex))
 elif m=='session/prompt':threading.Thread(target=prompt,args=(f,),daemon=True).start()
 elif m=='session/cancel':cancel.set()
 else:raise AssertionError(m)
