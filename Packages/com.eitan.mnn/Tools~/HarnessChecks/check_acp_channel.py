#!/usr/bin/env python3
"""Exercise the actual C# Host using independent ACP and MCP processes."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse,json,os,socket,subprocess,sys,time,urllib.error,urllib.request,uuid
from pathlib import Path
SAMPLE=PACKAGE_ROOT
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();(PEER/'capability.json').write_text('true');(PEER/'value.txt').write_text('native-channel-value')
TOKEN=uuid.uuid4().hex*2;SECRET=uuid.uuid4().hex*2;(PEER/'secret.txt').write_text(SECRET);(RUN/'other').mkdir();env=dict(os.environ,SIRIAI_CHANNEL_TOKEN=TOKEN,SIRIAI_CHANNEL_SECRET=SECRET,SIRIAI_CHANNEL_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
url='http://127.0.0.1:'+str(port);fixture=SAMPLE/'Tools~/HarnessChecks/Fixtures/acp_channel_fixture.py';config=dict(Listen=url,DataDirectory=str(RUN/'data'),Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_CHANNEL_TOKEN')],Workspaces=dict(test=str(RUN),other=str(RUN/'other')),Harnesses=[dict(Id='acp',Protocol='acp',Command=sys.executable,Arguments=['-u',str(fixture),'agent'],ForwardEnvironment=['SIRIAI_CHANNEL_ROOT'],AcpMcpServers=[dict(Name='channel-tools',Type='acp',Command=str(Path(sys.executable).resolve()),Arguments=['-u',str(fixture),'tool'],Environment=[dict(Name='SIRIAI_CHANNEL_ROOT',Value=str(PEER)),dict(Name='TOOL_SECRET',ValueEnvironmentVariable='SIRIAI_CHANNEL_SECRET')])])]);path=RUN/'host.json';path.write_text(json.dumps(config));records=[];passed=[];process=None;log=None

def ok(value):passed.append(value);print('PASS '+value,flush=True)
def wait(fn,timeout=30):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  value=fn()
  if value:return value
  time.sleep(.03)
 raise AssertionError('Timed out waiting for in-channel MCP behavior')
def call(path,body=None,status=200):
 req=urllib.request.Request(url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+TOKEN,'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=10) as response:code,raw=response.status,response.read()
 except urllib.error.HTTPError as e:code,raw=e.code,e.read()
 assert code==status,(code,raw);assert TOKEN.encode() not in raw and SECRET.encode() not in raw;return json.loads(raw) if raw else None
def prepare():
 cmd=['dotnet',str(a.host),'--service','prepare','--id','acp-channel','--config',str(path),'--runtime',str(Path('/opt/homebrew/bin/dotnet').resolve()),'--directory',str(RUN/'registration')];r=subprocess.run(cmd,text=True,capture_output=True,env=env,timeout=40);records.append(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));assert TOKEN not in r.stdout+r.stderr and SECRET not in r.stdout+r.stderr;assert r.returncode==0,records[-1]
def start():
 global process,log
 log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--service','run','--directory',str(RUN/'registration')],env=os.environ,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None,'Host exited'
  try:return call('/v1/harnesses')
  except urllib.error.URLError:return None
 return wait(ready)
def stop():
 global process
 if process:
  if process.poll() is None:process.terminate()
  try:process.wait(timeout=10)
  except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
  process=None;log.close()
def submit(objective,conversation='phone',workspace='test'):return call('/v1/tasks',dict(ConversationId=conversation,SubmissionKey=uuid.uuid4().hex,HarnessId='acp',WorkspaceKey=workspace,Objective=objective),202)
def terminal(task):
 task=call('/v1/tasks/'+task['Id']);return task if task['State'] in [3,4,5,6] else None
def events():return [json.loads(s) for s in (PEER/'events.jsonl').read_text().splitlines()] if (PEER/'events.jsonl').exists() else []
def methods():return [json.loads(s)['frame'] for s in (PEER/'wire.jsonl').read_text().splitlines() if json.loads(s)['direction']=='client' and json.loads(s)['frame'].get('method') in ['session/new','session/load','session/resume','session/prompt']]
def providers_stopped():
 for event in events():
  if event['kind']!='provider-start':continue
  try:os.kill(event['pid'],0);return False
  except ProcessLookupError:pass
 return True
try:
 prepare();start();task=submit('normal');value=wait(lambda:terminal(task));assert value['State']==3 and value['Result']=='channel-native-ok',value
 ok('actual native Agent connects through ACP, invokes concurrent typed-ID MCP requests, handles reverse sampling/notification and preserves native tool errors')
 wait(providers_stopped)
 assert any(e['kind']=='agent-notification' for e in events())
 sid=methods()[-1]['params']['sessionId']
 stop();start()
 second=submit('normal');value=wait(lambda:terminal(second));assert value['State']==3 and value['Result']=='channel-native-ok',value
 assert methods()[-1]['params']['sessionId']==sid and len([f for f in methods() if f['method']=='session/new'])==1
 stop();(PEER/'resume').touch();start();third=submit('normal');value=wait(lambda:terminal(third));assert value['State']==3 and methods()[-1]['params']['sessionId']==sid and any(f['method']=='session/resume' for f in methods()),value
 ok('Host restarts load and resume the same native session with fresh channel providers and no serialized credentials/process handles')
 for objective in ['crash','oversize']:
  task=submit(objective);value=wait(lambda:terminal(task));assert value['State']==3 and value['Result']=='provider-failed-without-replay',value
  ok(objective+' provider failure reaches the Agent as a channel error without replaying a tool request')
 task=submit('cancel');wait(lambda:any(e['kind']=='tool-start' and e['name']=='delay' for e in events()));call('/v1/tasks/'+task['Id']+'/cancel',{});value=wait(lambda:terminal(task));assert value['State']==5,value
 assert any(e['kind']=='cancel-notification' and e['requestId']=='cancel-me' for e in events())
 ok('native cancellation forwards the exact inner request ID, closes its provider and confirms Cancelled through the native prompt result')
 wait(providers_stopped);assert len([e for e in events() if e['kind']=='crash-side-effect'])==1
 task=submit('limits');value=wait(lambda:terminal(task));assert value['State']==3 and value['Result']=='channel-native-ok',value;wait(providers_stopped)
 ok('eight distinct channel connections are bounded, escaped oversized input is rejected and disconnect reaps every actual provider process')
 held=submit('hold');wait(lambda:(PEER/'held-cid.txt').exists());other=submit('foreign','other','other');value=wait(lambda:terminal(other));assert value['State']==3,value
 (PEER/'release').touch();assert wait(lambda:terminal(held))['State']==3;wait(providers_stopped)
 ok('simultaneously live native sessions cannot address each others connection IDs even when server descriptors share the same configured identity')
 stop()
 for capability in [False,'true']:
  (PEER/'capability.json').write_text(json.dumps(capability));start();count=len(events());requests=len(methods());task=submit('normal','unsupported-'+str(capability));value=wait(lambda:terminal(task));assert value['State']==4 and 'MCP' in value['Result'],value;assert len(events())==count and len(methods())==requests;stop()
 (PEER/'capability.json').write_text('true');start();task=submit('normal');assert wait(lambda:terminal(task))['State']==3;wait(providers_stopped)
 ok('missing/nonboolean channel capability sends no session/tool request and restoring capability preserves the existing native conversation')
 task=submit('idle-exit');value=wait(lambda:terminal(task));assert value['State']==3,value;wait(providers_stopped)
 ok('native Agent exit after a confirmed prompt immediately reaps its still-connected provider without waiting for another task or Host shutdown')
 for file in [RUN/'registration/host.json',*(RUN/'data').glob('*.json')]:assert SECRET.encode() not in file.read_bytes() and TOKEN.encode() not in file.read_bytes(),file
 assert not list(PEER.glob('errors-*.txt'))
 ok('channel-only credentials stay in the private service snapshot/provider environment and never enter native descriptors or public/durable task/session data')
finally:
 stop();wire=[json.loads(s) for s in (PEER/'wire.jsonl').read_text().splitlines()] if (PEER/'wire.jsonl').exists() else []
 (OUT/'results.json').write_text(json.dumps(passed,indent=2)+'\n');(OUT/'wire.json').write_text(json.dumps(wire,indent=2)+'\n');(OUT/'commands.json').write_text(json.dumps(records,indent=2)+'\n')
