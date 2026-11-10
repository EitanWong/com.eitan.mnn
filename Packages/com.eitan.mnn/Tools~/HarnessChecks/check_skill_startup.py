from pathlib import Path
import argparse, json, os, socket, subprocess, sys, time, urllib.error, urllib.request, uuid

p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
O=a.output.resolve()/('startup-'+uuid.uuid4().hex);O.mkdir();cases=[];passed=[]
mapping={'qwen':'qwen-code','claude':'claude-code','codex':'codex','opencode':'opencode','kimi':'kimi-code-cli',
    'openclaw':'openclaw','qoder':'qoder','hermes':'hermes-agent','codebuddy':'codebuddy','pi':'pi'}
directories={'qwen-code':'.qwen/skills','claude-code':'.claude/skills','codex':'.agents/skills','opencode':'.agents/skills',
    'kimi-code-cli':'.agents/skills','openclaw':'.openclaw/skills','qoder':'.qoder/skills','hermes-agent':'.hermes/skills',
    'codebuddy':'.codebuddy/skills','pi':'.pi/agent/skills'}
def wait(probe,seconds=12):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  value=probe()
  if value:return value
  time.sleep(.03)
 raise AssertionError('Timed out awaiting independent startup fixture')
def ok(text):passed.append(text);print('PASS '+text,flush=True)
class Case:
 def __init__(self,backend='qwen',startup=True):
  self.root=O/uuid.uuid4().hex;self.root.mkdir();self.home=self.root/'home';self.home.mkdir();self.records=[];self.host=None;self.logs=[]
  fixture=(Path(__file__).parent/'Fixtures/skill_startup_fixture.py').read_text().split('\n',1)[1]
  for name,mode in [('installer with spaces','installer'),('backend','backend'),('native','backend')]:
   path=self.root/(name+'.py');path.write_text('#!'+sys.executable+'\n'+fixture);path.chmod(0o755);path.with_suffix('.json').write_text(json.dumps(dict(root=str(self.root),mode=mode)))
  with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
  self.url='http://127.0.0.1:'+str(port);self.token=uuid.uuid4().hex*2;self.env=dict(os.environ,STARTUP_HOST_TOKEN=self.token)
  harness=dict(Id='work',Backend=backend,Command=str(self.root/'backend.py'),NativeCommand=str(self.root/'native.py'))
  self.data=dict(Listen=self.url,DataDirectory=str(self.root/'data'),MaximumConcurrent=1,
   Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='STARTUP_HOST_TOKEN')],Workspaces=dict(project=str(self.root)),Harnesses=[harness],
   Skills=dict(Command=str(self.root/'installer with spaces.py'),AllowedOwners=['alice'],WorkingDirectory=str(self.root),TimeoutMilliseconds=5000))
  if startup:self.data['Skills']['Startup']=dict(OwnerId='alice',HomeDirectory=str(self.home))
  self.config=self.root/'host.json';cases.append(self)
 def lock(self,values):
  path=self.home/'.agents/.skill-lock.json';path.parent.mkdir(exist_ok=True);path.write_text(json.dumps(dict(skills=values)))
 def seed(self,installer,name):
  path=self.home/directories[installer]/name/'SKILL.md';path.parent.mkdir(parents=True,exist_ok=True);path.write_text('already installed')
 def events(self):return [json.loads(v) for v in (self.root/'events.jsonl').read_text().splitlines()] if (self.root/'events.jsonl').exists() else []
 def installers(self):return [e for e in self.events() if e['kind']=='installer-start']
 def start(self,ready=True):
  self.config.write_text(json.dumps(self.data));log=self.root/('host-'+uuid.uuid4().hex+'.log');stream=log.open('w');self.logs.append((log,stream));self.host=subprocess.Popen(['dotnet',str(a.host),'--config',str(self.config)],env=self.env,stdout=stream,stderr=stream)
  if ready:
   def probe():
    assert self.host.poll() is None,log.read_text()
    try:return self.call('/v1/health')
    except urllib.error.URLError:return None
   return wait(probe)
 def stop(self,hard=False):
  if self.host and self.host.poll() is None:
   self.host.kill() if hard else self.host.terminate();self.host.wait(timeout=8)
  for _,stream in self.logs:stream.flush()
 def call(self,path,body=None,expected=200):
  req=urllib.request.Request(self.url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+self.token,'Content-Type':'application/json'})
  try:
   with urllib.request.urlopen(req,timeout=3) as response:code,data=response.status,response.read()
  except urllib.error.HTTPError as error:code,data=error.code,error.read()
  self.records.append(dict(path=path,input=body,status=code,stdout=data.decode(),stderr=''))
  assert code==expected,(path,code,data);assert self.token.encode() not in data
  return json.loads(data) if data else None
 def submit(self,objective='inspect-skills',conversation='startup'):
  return self.call('/v1/tasks',dict(ConversationId=conversation,SubmissionKey=uuid.uuid4().hex,HarnessId='work',WorkspaceKey='project',Objective=objective),202)
 def done(self,task):return wait(lambda:(lambda t:t if t['State']>=3 else None)(self.call('/v1/tasks/'+task['Id'])))
 def management(self,operation,**kwargs):return self.call('/v1/skills/operations',dict(SubmissionKey=uuid.uuid4().hex,Operation=operation,**kwargs),202)
 def kill_children(self):
  for event in self.events():
   if event['kind'] not in ['installer-start','backend-scan']:continue
   p=subprocess.run(['ps','-p',str(event['pid']),'-o','command='],capture_output=True,text=True)
   if str(self.root) in p.stdout:
    try:os.kill(event['pid'],9)
    except ProcessLookupError:pass

try:
 c=Case();c.lock(dict(review=dict(source='owner/repo?a=1&b=2'),pdf=dict(source='owner/repo?a=1&b=2'),existing=dict(source='old/repo')));c.seed('qwen-code','existing');c.start()
 events=c.installers();operations=c.call('/v1/skills/operations')
 assert len(events)==1,('SKILL_STARTUP_MISSING' if not operations else 'SKILL_STARTUP_EXECUTION_FAILED',operations)
 assert events[0]['argv']==['-y','skills@1.5.22','add','owner/repo?a=1&b=2','--skill','review','--skill','pdf','-g','--copy','-y','-a','qwen-code']
 result=c.done(c.submit());assert result['State']==3 and json.loads(result['Result'])['.qwen/skills']==['existing','pdf','review']
 assert c.call('/v1/skills/operations')[0]['State']==3
 assert not any(e.get('host_token_leaked') for e in events)
 ok('grouped missing names use literal source argv and exist before the first actual backend skill scan')
 c.stop();count=len(c.installers());c.start();assert len(c.installers())==count
 c.call('/v1/skills/catalog');c.call('/v1/harnesses');assert len(c.installers())==count
 ok('restart with complete files and passive inventory start no additional installer')
 c.stop()

 for backend,installer in mapping.items():
  c=Case(backend);c.lock(dict(review=dict(sourceUrl='owner/repo')));c.start();assert len(c.installers())==1
  assert c.installers()[0]['argv'][-2:]==['-a',installer]
  assert (c.home/directories[installer]/'review/SKILL.md').is_file();c.stop()
 ok('all ten pinned backend targets use their exact global directories, including the universal directory')

 c=Case();c.lock(dict(stale=dict(source='failed-source'),good=dict(source='valid-source')));c.start()
 ops=c.call('/v1/skills/operations');assert sorted(t['State'] for t in ops)==[3,4],ops
 assert (c.home/'.qwen/skills/good/SKILL.md').exists() and not (c.home/'.qwen/skills/stale/SKILL.md').exists()
 assert 'remove' in c.logs[-1][0].read_text().lower();c.stop()
 ok('one source failure remains failed, reports removal guidance and does not prevent other sources or Host readiness')

 c=Case();c.lock(dict(phantom=dict(source='no-files')));c.start();ops=c.call('/v1/skills/operations');assert len(ops)==1 and ops[0]['State']==4,ops;c.stop()
 ok('installer exit zero without the expected SKILL.md cannot be reported as successful synchronization')

 for backend,enabled in [('qwen',False),('deepseek',True)]:
  c=Case(backend,enabled);c.lock(dict(review=dict(source='owner/repo')));c.start();assert not c.installers();c.stop()
 ok('startup is opt-in and unmapped backends never trigger installer execution')

 for content in [None,'{broken',json.dumps(dict(skills=dict(orphan={},**{'../escape':dict(source='owner/repo'),'bad-option':dict(source='--all')})))]:
  c=Case()
  if content is not None:
   path=c.home/'.agents/.skill-lock.json';path.parent.mkdir();path.write_text(content)
  c.start();assert not c.installers();c.stop();assert not (c.home.parent/'escape').exists()
 ok('missing or malformed locks and invalid names or sources never create installer side effects or path traversal')

 c=Case();c.lock({('skill'+str(i)):dict(source='owner/repo') for i in range(70)})
 c.data['Harnesses'].append(dict(c.data['Harnesses'][0],Id='duplicate-qwen'));c.start()
 assert len(c.installers())==2 and all(len([x for x in e['argv'] if x=='--skill'])<=64 for e in c.installers())
 assert len(list((c.home/'.qwen/skills').glob('*/SKILL.md')))==70;c.stop()
 ok('more than64 missing skills are fully synchronized in bounded batches and duplicate backend configurations do not reinstall')

 for source in ['hang-source','flood-source']:
  c=Case();c.data['Skills']['TimeoutMilliseconds']=2000;c.lock(dict(problem=dict(source=source),good=dict(source='valid-source')));c.start()
  assert sorted(t['State'] for t in c.call('/v1/skills/operations'))==[3,4]
  assert (c.home/'.qwen/skills/good/SKILL.md').exists();c.stop()
 ok('startup inherits real timeout and bounded-output failures, cleans the local process and continues the next source')

 c=Case();c.data['Skills']['TimeoutMilliseconds']=10000;c.lock(dict(problem=dict(source='hang-source')));c.start(ready=False)
 wait(lambda:c.installers());installer_pid=c.installers()[0]['pid'];c.stop();assert c.host.returncode==130
 def absent():
  try:os.kill(installer_pid,0);return False
  except ProcessLookupError:return True
 wait(absent,3)
 ok('SIGTERM during startup waits for installer cleanup, exits130 and never starts the main Host')

 c=Case();c.lock(dict(review=dict(source='owner/repo')));health=c.start();count=len(c.installers())
 (c.home/'.qwen/skills/review/SKILL.md').unlink();log=c.root/'duplicate-host.log'
 with log.open('w') as stream:
  duplicate=subprocess.Popen(['dotnet',str(a.host),'--config',str(c.config)],env=c.env,stdout=stream,stderr=stream)
  duplicate.wait(timeout=8)
 assert duplicate.returncode!=0 and len(c.installers())==count
 assert c.call('/v1/health')['InstanceId']==health['InstanceId'];c.stop()
 ok('a competing Host is rejected by the main storage lease before any startup installer can run')

 c=Case(startup=False);c.lock(dict(review=dict(source='owner/repo')));c.start();first=c.submit('hold-backend')
 wait(lambda:any(e['kind']=='backend-prompt' and e['objective']=='hold-backend' for e in c.events()))
 queued=c.submit(conversation='independent-queued');assert c.call('/v1/tasks/'+queued['Id'])['State']==0;c.stop(hard=True);c.kill_children()
 c.data['Skills']['Startup']=dict(OwnerId='alice',HomeDirectory=str(c.home));c.start();done=c.done(queued)
 assert done['State']==3 and json.loads(done['Result'])['.qwen/skills']==['review']
 assert c.call('/v1/tasks/'+first['Id'])['State']==6
 rows=c.events();install_end=next(i for i,e in enumerate(rows) if e['kind']=='installer-end')
 assert any(e['kind']=='backend-scan' and e['files']['.qwen/skills']==['review'] for e in rows[install_end+1:])
 assert sum(e['kind']=='backend-prompt' and e['objective']=='inspect-skills' for e in rows)==1;c.stop()
 ok('startup synchronization precedes recovery and first skill scan of a genuinely queued main task after Host crash')

 c=Case(startup=False);c.lock(dict(toremove=dict(source='owner/repo')));c.start()
 first=c.management('source-list',Source='hold-source');wait(lambda:c.installers())
 queued=c.management('remove',Names=['toremove']);assert c.call('/v1/skills/operations/'+queued['Id'])['State']==0
 c.stop(hard=True);c.kill_children();c.data['Skills']['Startup']=dict(OwnerId='alice',HomeDirectory=str(c.home));c.start()
 assert c.call('/v1/skills/operations/'+queued['Id'])['State']==3
 assert c.call('/v1/skills/operations/'+first['Id'])['State']==6
 assert len(c.installers())==2 and c.installers()[-1]['argv'][2:]==['remove','toremove','-g','-y']
 assert json.loads((c.home/'.agents/.skill-lock.json').read_text())['skills']=={};c.stop()
 ok('recovered management removal finishes before startup reads the lock, without resurrecting removed skills or adding another writer')

 c=Case();c.lock(dict(first=dict(source='rewrite-lock'),second=dict(source='next-source')));c.start()
 assert len(c.installers())==2 and all(t['State']==3 for t in c.call('/v1/skills/operations'))
 assert json.loads((c.home/'.agents/.skill-lock.json').read_text())['installerGeneration']==1;c.stop()
 ok('each source refreshes its accepted lock hash after installer metadata changes, so later sources are not silently skipped')

 for startup in [dict(OwnerId='unknown',HomeDirectory='.'),dict(OwnerId='alice',HomeDirectory='/missing-siriai-startup-home')]:
  c=Case();c.data['Skills']['Startup']=startup;c.start(ready=False)
  wait(lambda:c.host.poll() is not None);assert c.host.returncode!=0 and not c.installers();c.stop()
 ok('invalid startup owner or inspection home is rejected before any process starts')
finally:
 for c in cases:
  c.stop()
  c.kill_children()
  (c.root/'http.json').write_text(json.dumps(c.records,indent=2)+'\n')
  for _,stream in c.logs:stream.close()
 (O/'results.json').write_text(json.dumps(dict(passed=passed,groups=len(passed)),indent=2)+'\n')
