from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse,json,os,socket,subprocess,sys,time,urllib.request,urllib.error,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/uuid.uuid4().hex;RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();SAMPLE=PACKAGE_ROOT
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
url='http://127.0.0.1:'+str(port);token=uuid.uuid4().hex*2;env=dict(os.environ,SIRIAI_STEER_TOKEN=token,SIRIAI_STEER_BOB=uuid.uuid4().hex*2,SIRIAI_STEER_ROOT=str(PEER));modes=['extension','pull','restart','required','unknown','reject','ignore-cancel','lost','error'];config=dict(Listen=url,DataDirectory=str(RUN/'data'),MaximumConcurrent=4,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_STEER_TOKEN'),dict(OwnerId='bob',TokenEnvironmentVariable='SIRIAI_STEER_BOB')],Workspaces=dict(test=str(RUN)),Harnesses=[dict(Id=m,Protocol='acp',Command=sys.executable,Arguments=['-u',str(SAMPLE/'Tools~/HarnessChecks/Fixtures/acp_steering_fixture.py'),m],ForwardEnvironment=['SIRIAI_STEER_ROOT']) for m in modes]);CONFIG=RUN/'host.json';CONFIG.write_text(json.dumps(config));log=(RUN/'host.log').open('w');process=None;passed=[]
def http(path,body=None,status=200,authorization=None):
 request=urllib.request.Request(url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(authorization or token),'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(request,timeout=35) as response:code,raw=response.status,response.read()
 except urllib.error.HTTPError as e:code,raw=e.code,e.read()
 assert code==status,(path,code,raw);return json.loads(raw) if raw else None
def wait(fn):
 end=time.monotonic()+35
 while time.monotonic()<end:
  value=fn()
  if value:return value
  time.sleep(.03)
 raise AssertionError('Timeout')
def submit(mode,updates=1):
 task=http('/v1/tasks',dict(ConversationId=uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId=mode,WorkspaceKey='test',Objective=json.dumps(dict(updates=updates))),202)
 wait(lambda:http('/v1/tasks/'+task['Id']).get('Activity')=='working')
 if mode=='pull':wait(lambda:any(json.loads(l)['frame'].get('method')=='craft/drainMidTurnQueue' for l in (PEER/'wire.jsonl').read_text().splitlines()))
 return task
def terminal(task):
 value=http('/v1/tasks/'+task['Id']);return value if value['State'] in [3,4,5,6] and all(i['State']!=0 for i in value.get('Instructions',[])) else None
def update(task,text='新的要求',identity=None,status=200):return http('/v1/tasks/'+task['Id']+'/update',dict(RequestId=identity or uuid.uuid4().hex,Text=text),status)
def ok(text):passed.append(text);print('PASS '+text,flush=True)
try:
 process=subprocess.Popen(['dotnet',str(a.host),'--config',str(CONFIG)],env=env,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None
  try:return http('/v1/harnesses')
  except urllib.error.URLError:return None
 wait(ready)
 for mode in ['extension','pull','restart','required']:
  task=submit(mode);update(task);done=wait(lambda:terminal(task));assert done['State']==3,done
  result=json.loads(done['Result']);assert result['sideEffects']==1 and result['updates']==['新的要求'] and result['prompts']==(1 if mode in ['extension','pull'] else 2),done
  assert done['Instructions'][0]['State']==1,done
  ok('native '+mode+' delivers exact instruction in same session without replaying original side effects')
 task=submit('extension',2);identity=uuid.uuid4().hex
 update(task,'first',identity);update(task,'first',identity);update(task,'different',identity,status=409);update(task,'second')
 done=wait(lambda:terminal(task));assert json.loads(done['Result'])['updates']==['first','second'] and len(done['Instructions'])==2
 update(task,'first',identity);update(task,'late',status=409)
 ok('durable instruction identity deduplicates retries after delivery and completion; conflicting reuse and new late updates are rejected')
 from concurrent.futures import ThreadPoolExecutor
 task=submit('extension');identity=uuid.uuid4().hex
 with ThreadPoolExecutor(max_workers=8) as pool:list(pool.map(lambda _:update(task,'once',identity),range(8)))
 done=wait(lambda:terminal(task));assert json.loads(done['Result'])['updates']==['once'] and len(done['Instructions'])==1
 ok('concurrent retries share one durable admission and one native injection')
 task=submit('unknown');updated=update(task);assert updated['Instructions'][0]['State']==3;done=wait(lambda:terminal(task));assert done['State']==6,done
 ok('startedNewTurn and unknown delivery outcomes become uncertain without replay or replacement prompt')
 task=submit('error');updated=update(task);assert updated['Instructions'][0]['State']==3 and wait(lambda:terminal(task))['State']==6
 ok('native execution errors are uncertain rather than falsely rejected or automatically retried')
 task=submit('reject');updated=update(task);assert updated['Instructions'][0]['State']==2 and updated['State']==1
 http('/v1/tasks/'+task['Id']+'/cancel',{});assert wait(lambda:terminal(task))['State']==5
 ok('explicit native rejection is distinct from uncertain delivery and leaves original work cancellable')
 task=submit('ignore-cancel');updated=update(task);assert updated['Instructions'][0]['State']==3;assert wait(lambda:terminal(task))['State']==6
 ok('unconfirmed cancellation boundary never starts a replacement prompt')
 task=submit('extension',30)
 for i in range(16):update(task,'instruction '+str(i))
 update(task,'over budget',status=400);assert len(http('/v1/tasks/'+task['Id'])['Instructions'])==16
 http('/v1/tasks/'+task['Id']+'/cancel',{});assert wait(lambda:terminal(task))['State']==5
 ok('instruction capacity is explicit and bounded; cancellation ends original task without replay')
 task=submit('lost');identity=uuid.uuid4().hex
 before=time.monotonic();admitted=http('/v1/tasks/'+task['Id']+'/update',dict(RequestId=identity,Text='lost acknowledgement',AdmitOnly=True));assert time.monotonic()-before<2
 assert admitted['Instructions'][0]['State']==0
 wait(lambda:any(json.loads(l)['frame'].get('method')=='_session/steering' and json.loads(l)['frame']['params']['prompt'][-1]['text']=='lost acknowledgement' for l in (PEER/'wire.jsonl').read_text().splitlines()))
 process.kill();process.wait();process=subprocess.Popen(['dotnet',str(a.host),'--config',str(CONFIG)],env=env,stdout=log,stderr=log);wait(ready)
 done=wait(lambda:terminal(task));assert done['State']==6 and done['Instructions'][0]['State']==3
 update(task,'lost acknowledgement',identity);time.sleep(.1)
 task=submit('restart');update(task);assert wait(lambda:terminal(task))['State']==3
 ok('admission survives client lifetime; Host crash recovers unconfirmed instruction without retry and independent new work remains usable')

 task=submit('extension',2);identity=uuid.uuid4().hex
 update(task,'ok',identity)
 http('/v1/tasks/'+task['Id']+'/update',dict(RequestId='foreign',Text='foreign'),404,authorization=env['SIRIAI_STEER_BOB'])
 http('/v1/tasks/'+task['Id'],status=404,authorization=env['SIRIAI_STEER_BOB'])
 update(task,'x'*32769,status=400)
 foreign='00000000000000000000000000000000';http('/v1/tasks/'+foreign+'/update',dict(RequestId='x',Text='foreign'),404)
 update(task,'finish');assert wait(lambda:terminal(task))['State']==3
 frames=[json.loads(line) for line in (PEER/'wire.jsonl').read_text().splitlines()]
 lost=[e for e in frames if e['frame'].get('method')=='_session/steering' and e['frame']['params']['prompt'][-1]['text']=='lost acknowledgement'];assert len(lost)==1
 ok('invalid boundaries do not admit extra work; wire confirms crashed instruction was sent once')

finally:
 if process is not None:
  process.terminate()
  try:process.wait(timeout=8)
  except subprocess.TimeoutExpired:process.kill();process.wait()
 log.close()
 if (PEER/'wire.jsonl').exists():(OUT/'wire.json').write_text(json.dumps([json.loads(l) for l in (PEER/'wire.jsonl').read_text().splitlines()],ensure_ascii=False,indent=2)+'\n')
 (OUT/'results.json').write_text(json.dumps(passed,indent=2)+'\n')
