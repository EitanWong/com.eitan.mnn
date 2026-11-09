#!/usr/bin/env python3
"""Real C# host against an independent durable MSP process, no vendor/model."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse,json,os,shutil,socket,subprocess,sys,time,urllib.error,urllib.request,uuid
from pathlib import Path
SAMPLE=PACKAGE_ROOT;ROOT=PROJECT_ROOT
p=argparse.ArgumentParser();p.add_argument('--output',required=True,type=Path);p.add_argument('--mode',default='durable');p.add_argument('--host',type=Path,default=ROOT/'TestArtifacts~/SiriAIHarness/Host/bin/Debug/net8.0/SiriAI.HarnessHost.dll');a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir()
(RUN/'other-workspace').mkdir()
TOKEN=uuid.uuid4().hex*2;OTHER=uuid.uuid4().hex*2;KEY=uuid.uuid4().hex
ENV=dict(os.environ,SIRIAI_MSP_CHECK_TOKEN=TOKEN,SIRIAI_MSP_OTHER_TOKEN=OTHER,META_API_KEY=KEY,MUSE_FIXTURE_EXPECTED_KEY=KEY,MUSE_FIXTURE_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));PORT=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(PORT);CONFIG=RUN/'host.json';REG=RUN/'registration'
FIXTURE=SAMPLE/'Tools~/HarnessChecks/Fixtures/msp_fixture.py';FIXTURE.chmod(0o755)
CONFIG.write_text(json.dumps(dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_MSP_CHECK_TOKEN'),dict(OwnerId='bob',TokenEnvironmentVariable='SIRIAI_MSP_OTHER_TOKEN')],Workspaces=dict(test=str(RUN),other=str(RUN/'other-workspace')),Harnesses=[dict(Id='muse',Backend='muse',Command=str(FIXTURE),Arguments=['serve',a.mode],Model='fixture-msp-model',PermissionMode='full' if a.mode in ['full','unconfirmed-full'] else 'native',TimeoutMilliseconds=100 if a.mode in ['timeout','slow-ack'] else 300000)])) )
records=[];passed=[];process=None;log=None
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def cli(args,expected=0):
 cmd=['dotnet',str(a.host),*args];r=subprocess.run(cmd,env=ENV,text=True,capture_output=True,timeout=35);records.append(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));(OUT/'commands.json').write_text(json.dumps(records,indent=2))
 assert r.returncode==expected,records[-1]
 assert all(k not in r.stdout+r.stderr for k in [TOKEN,OTHER,KEY]);return r
def call(path,body=None,token=TOKEN,expected=200):
 req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=15) as r:status,raw=r.status,r.read()
 except urllib.error.HTTPError as e:status,raw=e.code,e.read()
 assert status==expected,(path,status,raw);assert all(k.encode() not in raw for k in [TOKEN,OTHER,KEY]);return json.loads(raw) if raw else None
def wait(test,timeout=14):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  value=test()
  if value:return value
  time.sleep(.03)
 raise AssertionError('Timed out waiting for MSP behavior')
def start():
 global process,log
 log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');env={k:v for k,v in os.environ.items() if k not in ENV or not (k.startswith('SIRIAI_MSP_') or k.startswith('MUSE_') or k=='META_API_KEY')}
 process=subprocess.Popen(['dotnet',str(a.host),'--service','run','--directory',str(REG)],env=env,stdout=log,stderr=log)
 def ready():
  if process.poll() is not None:raise AssertionError('host exited '+str(process.returncode))
  try:return call('/v1/harnesses')
  except urllib.error.URLError:return None
 return wait(ready)
def stop(kill=False):
 global process
 if process is not None:
  process.kill() if kill else process.terminate()
  try:process.wait(timeout=10)
  except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
  process=None;log.close()
def wire():return [json.loads(x) for x in (PEER/'wire.jsonl').read_text().splitlines()] if (PEER/'wire.jsonl').exists() else []
def submit(text,key=None,token=TOKEN,attachments=None,workspace='test'):return call('/v1/tasks',dict(ConversationId='phone',SubmissionKey=key or uuid.uuid4().hex,HarnessId='muse',WorkspaceKey=workspace,Objective=text,Attachments=attachments),token=token,expected=202)
def terminal(task):
 v=call('/v1/tasks/'+task['Id']);return v if v['State'] in [3,4,5,6] else None
try:
 cli(['--service','prepare','--id','msp-test','--config',str(CONFIG),'--runtime',str(Path(shutil.which('dotnet')).resolve()),'--directory',str(REG)])
 catalog=start();assert catalog['configured'][0]['backend']=='muse' and catalog['configured'][0]['capabilities']==31,catalog
 assert catalog['configured'][0]['cancellationScope']=='msp-turn' and not wire()
 ok('named MSP backend prepares/starts with private scoped credentials and advertises native controls without startup work')
 if a.mode!='durable':
  objective='permission-stages' if a.mode=='full' else 'timeout' if a.mode=='timeout' else 'lost-ack' if a.mode=='slow-ack' else 'crash' if a.mode=='ephemeral' else 'hello'
  item=submit(objective)
  if a.mode=='ephemeral':
   wait(lambda:call('/v1/tasks/'+item['Id']).get('BackendState'));wait(lambda:any(v['objective']=='crash' for v in json.loads((PEER/'state.json').read_text())['turns'].values()));stop(kill=True);start()
  value=wait(lambda:terminal(item))
  expected=6 if a.mode in ['missing-schema','unconfirmed-full','slow-ack','ephemeral'] else 4 if a.mode=='timeout' else 3
  assert value['State']==expected,value
  if a.mode=='schema-mismatch':assert 'schema fingerprint differs' in value['Activity']
  if a.mode in ['missing-schema','unconfirmed-full']:assert not any(x['frame'].get('method')=='turn/start' for x in wire())
  if a.mode=='full':assert all(x['frame']['params']['choiceId']=='once' for x in wire() if x['frame'].get('method')=='approval/decide')
  if a.mode=='ephemeral':assert not any(x['frame'].get('method')=='session/resume' for x in wire())
  ok(a.mode+' preserves observed failure, permission or durability semantics')
  assert not (PEER/'errors.txt').exists(),(PEER/'errors.txt').read_text()
  sys.exit(0)
 first=submit('slow','intent');assert first['State'] in [0,1];assert submit('slow','intent')['Id']==first['Id'];call('/v1/tasks/'+first['Id'],token=OTHER,expected=404)
 value=wait(lambda:terminal(first));assert value['State']==3 and value['Result']=='fixture MSP answer: slow',value
 starts=[x for x in wire() if x['direction']=='client' and x['frame'].get('method')=='turn/start'];assert len(starts)==1
 ok('durable nonblocking admission, initialized handshake and one acknowledged turn complete independently of foreground connection')
 second=submit('before-ack');value=wait(lambda:terminal(second));assert value['State']==3 and value['Result']=='fixture MSP answer: before-ack',value
 assert len([x for x in wire() if x['frame'].get('method')=='session/start'])==1
 ok('same owner/workspace reuses session and handles terminal/item events arriving before turn acknowledgement')
 bob=submit('bob-task',token=OTHER)
 def bob_finished():
  value=call('/v1/tasks/'+bob['Id'],token=OTHER);return value if value['State']==3 else None
 wait(bob_finished);alternate=submit('other-workspace',workspace='other');assert wait(lambda:terminal(alternate))['State']==3
 assert len(json.loads((PEER/'state.json').read_text())['sessions'])==3
 call('/v1/tasks/'+bob['Id'],token=TOKEN,expected=404)
 ok('native conversation sessions separate owners and workspaces without forwarding host owner or foreground history')
 for objective,decision,expected in [('permission',True,'once'),('permission-deny',False,'deny')]:
  item=submit(objective)
  def pending():
   value=call('/v1/tasks/'+item['Id']);return value if value['WaitingFor']==2 else None
  prompt=wait(pending);call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId='stale',Text='Allow once',Allow=True),expected=409)
  call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Allow once',Allow=decision),token=OTHER,expected=404)
  call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Allow once',Allow=decision))
  value=wait(lambda:terminal(item));assert value['State']==3 and value['Result']=='permission '+expected,value
 ok('native approval request/receipt deduplicates, preserves offered once-only choices and fences owner/stale UI replies')
 item=submit('permission-stages')
 def permission():
  value=call('/v1/tasks/'+item['Id']);return value if value['WaitingFor']==2 else None
 first_prompt=wait(permission);call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=first_prompt['RequestId'],Text='Allow once',Allow=True))
 next_prompt=wait(lambda:(lambda v:v if v and v['RequestId']!=first_prompt['RequestId'] else None)(permission()))
 call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=first_prompt['RequestId'],Text='Allow once',Allow=True),expected=409)
 call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=next_prompt['RequestId'],Text='Allow once',Allow=True));assert wait(lambda:terminal(item))['State']==3
 ok('each native approval stage has a separate foreground request identity and confirms its exact requirement')
 for objective,reply in [('input','Reports'),('multi-input','Reports and Final')]:
  item=submit(objective)
  def prompt():
   value=call('/v1/tasks/'+item['Id']);return value if value['WaitingFor']==1 else None
  question=wait(prompt);call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=question['RequestId'],Text=reply,Allow=False));assert wait(lambda:terminal(item))['State']==3
 ok('native user input uses offered single choice or multi-question clarification commands without treating answers as permission grants')
 item=submit('input')
 def input_pending():
  value=call('/v1/tasks/'+item['Id']);return value if value['WaitingFor']==1 else None
 prompt=wait(input_pending);call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Please stop asking',Allow=False,InputAction=2));assert wait(lambda:terminal(item))['State']==3
 ok('typed input cancellation is separate from the permission Allow flag used by the existing Unity reply button')
 item=submit('steer');wait(lambda:any(v['objective']=='steer' for v in json.loads((PEER/'state.json').read_text())['turns'].values()))
 call('/v1/tasks/'+item['Id']+'/update',dict(Text='Focus on report'));assert wait(lambda:terminal(item))['Result']=='steered result'
 ok('mid-turn steering addresses the confirmed turn identity and preserves the same task')
 item=submit('image',attachments=[dict(Kind=3,Payload='aW1hZ2U=',MediaType='image/png')]);assert wait(lambda:terminal(item))['State']==3
 count=len(wire());call('/v1/tasks',dict(ConversationId='phone',SubmissionKey='bad-attachment',HarnessId='muse',WorkspaceKey='test',Objective='bad',Attachments=[dict(Kind=0,Payload='text')]),expected=422);assert len(wire())==count
 ok('inline image attachments project to native input and unsupported content is rejected before admission')
 item=submit('result-revisions');assert wait(lambda:terminal(item))['Result']=='final text'
 item=submit('gap');assert wait(lambda:terminal(item))['Result']=='gap recovered result'
 item=submit('gap-stalled');assert wait(lambda:terminal(item))['State']==6
 item=submit('failure');assert wait(lambda:terminal(item))['State']==4
 ok('final item revisions own text; native view paging fills gaps, while stalled paging and failed turns never produce false completed results')
 item=submit('cancel');wait(lambda:any(v['objective']=='cancel' for v in json.loads((PEER/'state.json').read_text())['turns'].values()))
 cancelled=call('/v1/tasks/'+item['Id']+'/cancel',{});assert cancelled['State']==5 and cancelled['Result'],cancelled
 ok('cancellation addresses acknowledged turn identity and waits for terminal confirmation rather than command acknowledgement')
 stop();start();assert call('/v1/tasks/'+first['Id'])['Result']=='fixture MSP answer: slow'
 restored=submit('after-restart');value=wait(lambda:terminal(restored));assert value['State']==3,value
 assert len([x for x in wire() if x['frame'].get('method')=='session/start'])==3
 assert any(x['frame'].get('method')=='session/resume' for x in wire())
 ok('completed results and owner workspace session identity survive host restart')
 item=submit('crash')
 def acknowledged_turn():
  state=json.loads(call('/v1/tasks/'+item['Id']).get('BackendState') or '{}')
  return state if state.get('TurnId') else None
 # The next case covers a lost acknowledgement; this case requires its durable checkpoint.
 acknowledged=wait(acknowledged_turn)
 assert json.loads((PEER/'state.json').read_text())['turns'][acknowledged['TurnId']]['objective']=='crash'
 count=len([x for x in wire() if x['frame'].get('method')=='turn/start']);stop(kill=True);start();value=wait(lambda:terminal(item));assert value['State']==3,value
 assert len([x for x in wire() if x['frame'].get('method')=='turn/start'])==count
 ok('durable active turn resumes after host crash without submitting the task again')
 item=submit('lost-ack');wait(lambda:any(v['objective']=='lost-ack' for v in json.loads((PEER/'state.json').read_text())['turns'].values()));stop(kill=True);start();value=wait(lambda:terminal(item));assert value['State']==3,value
 frames=[x['frame']['params'] for x in wire() if x['frame'].get('method')=='turn/start' and x['frame']['params']['displayText']=='lost-ack'];assert len(frames)==2 and frames[0]==frames[1]
 ok('ambiguous turn acknowledgement replays identical MSP commandId and parameters instead of creating a new native turn')
 for objective,waiting,reply in [('permission',2,'Allow once'),('input',1,'Reports')]:
  item=submit(objective)
  def pending():
   value=call('/v1/tasks/'+item['Id']);return value if value['WaitingFor']==waiting else None
  before=wait(pending);stop(kill=True);start();after=wait(pending)
  assert after['RequestId']==before['RequestId']
  call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=after['RequestId'],Text=reply,Allow=True));assert wait(lambda:terminal(item))['State']==3
 ok('pending approval and user input are reconstructed from native listPending after host crash without resubmitting the turn')
 assert not (PEER/'errors.txt').exists(),(PEER/'errors.txt').read_text()
finally:
 stop();(OUT/'wire.json').write_text(json.dumps(wire(),indent=2,ensure_ascii=False));(OUT/'results.json').write_text(json.dumps(dict(passed=passed,run=str(RUN),vendor_inference=False),indent=2))
