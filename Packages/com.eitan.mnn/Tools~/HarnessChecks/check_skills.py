from pathlib import Path
import argparse,json,os,socket,subprocess,sys,time,urllib.request,urllib.error,uuid
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();O=a.output.resolve()/('skills-'+uuid.uuid4().hex);O.mkdir();root=O/'fixture';root.mkdir();exe=root/'npx with spaces.py';exe.write_text('#!'+sys.executable+'\n'+(Path(__file__).parent/'Fixtures/skills_fixture.py').read_text().split('\n',1)[1]);exe.chmod(0o755);exe.with_suffix('.json').write_text(json.dumps(dict(root=str(root))))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
url='http://127.0.0.1:'+str(port);token=uuid.uuid4().hex*2;bob=uuid.uuid4().hex*2;env=dict(os.environ,SKILL_HOST_TOKEN=token,SKILL_BOB_TOKEN=bob,PRIVATE_SKILL_SECRET='never-forward',SKILL_FORWARD='allowed')
config=O/'host.json';config.write_text(json.dumps(dict(Listen=url,DataDirectory=str(O/'data'),Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SKILL_HOST_TOKEN'),dict(OwnerId='bob',TokenEnvironmentVariable='SKILL_BOB_TOKEN')],Workspaces=dict(project=str(O)),Harnesses=[],Skills=dict(Command=str(exe),AllowedOwners=['alice'],WorkingDirectory=str(O),ForwardEnvironment=['SKILL_FORWARD'],TimeoutMilliseconds=3000))))
records=[];passed=[];host=None;log=None

def start():
 global host,log
 log=(O/('host-'+uuid.uuid4().hex+'.log')).open('w');host=subprocess.Popen(['dotnet',str(a.host),'--config',str(config)],env=env,stdout=log,stderr=log)
 end=time.monotonic()+15
 while True:
  assert host.poll() is None,'Host failed to start'
  try:call('/v1/health');return
  except urllib.error.URLError:
   if time.monotonic()>end:raise
   time.sleep(.03)
def stop(kill=False):
 if host and host.poll() is None:
  host.kill() if kill else host.terminate();host.wait(timeout=10)
 if log:log.close()
def call(path,body=None,expected=200,credential=None):
 req=urllib.request.Request(url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(credential or token),'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=10) as response:code,data=response.status,response.read()
 except urllib.error.HTTPError as error:code,data=error.code,error.read()
 records.append(dict(path=path,input=body,status=code,output=data.decode()))
 assert code==expected,('SKILLS_LIBRARY_MISSING' if path=='/v1/skills/catalog' else path,code,data)
 assert token.encode() not in data and bob.encode() not in data
 return json.loads(data) if data else None
def events():return [json.loads(x) for x in (root/'events.jsonl').read_text().splitlines()] if (root/'events.jsonl').exists() else []
def submit(operation,**kwargs):return call('/v1/skills/operations',dict(SubmissionKey=uuid.uuid4().hex,Operation=operation,**kwargs),202)
def result(task):
 end=time.monotonic()+15
 while time.monotonic()<end:
  current=call('/v1/skills/operations/'+task['Id'])
  if current['State']>=3:return current
  time.sleep(.03)
 raise AssertionError('Skill operation did not reach a terminal state')
def ok(text):passed.append(text);print('PASS '+text,flush=True)
try:
 start();catalog=call('/v1/skills/catalog');assert catalog['Enabled'] is True
 assert len(catalog['Targets'])==10 and not events();assert call('/v1/skills/operations')==[]
 ok('explicit skills catalog is passive and exposes ten upstream installer targets')
 task=submit('source-list',Source='https://example.test/repo?a=1&b=2');done=result(task);assert done['State']==3 and 'review' in done['Result']
 assert events()[-1]['argv']==['-y','skills@1.5.22','add','https://example.test/repo?a=1&b=2','--list']
 ok('source enumeration passes one literal argument and does not install')

 mapping={'qwen':'qwen-code','opencode':'opencode','openclaw':'openclaw','qoder':'qoder','kimi':'kimi-code-cli','hermes':'hermes-agent','codebuddy':'codebuddy','codex':'codex','claude':'claude-code','pi':'pi'}
 assert {t['Backend']:t['Installer'] for t in catalog['Targets']}==mapping
 for backend,installer in mapping.items():
  done=result(submit('install',Source='owner/repo',Names=['review','pdf-tools'],Backends=[backend,backend]))
  assert done['State']==3,done
  assert events()[-1]['argv']==['-y','skills@1.5.22','add','owner/repo','--skill','review','--skill','pdf-tools','-g','--copy','-y','-a',installer]
 ok('all ten backend mappings use exact upstream copy/global/noninteractive arguments, with duplicate targets removed')
 done=result(submit('install',Source='./local source',Names=['review']))
 assert done['State']==3
 argv=events()[-1]['argv'];assert [argv[i+1] for i,x in enumerate(argv) if x=='-a']==catalog['DefaultTargets']
 ok('default target union follows installed/configured backends and keeps a literal local source path')
 for operation,kwargs,expected in [('list',{},['list','-g']),('remove',dict(Names=['pdf-tools']),['remove','pdf-tools','-g','-y']),('update',{},['update','-g'])]:
  done=result(submit(operation,**kwargs));assert done['State']==3,done;assert events()[-1]['argv'][2:]==expected
 assert json.loads((root/'installed.json').read_text())==['review']
 ok('list remove and update use global scope and observed installer state changes')
 count=len(events())
 for kwargs in [dict(Operation='execute'),dict(Operation='install',Source='owner/repo'),dict(Operation='install',Source='--all',Names=['review']),dict(Operation='install',Source='owner/repo',Names=['--all']),dict(Operation='remove',Names=[]),dict(Operation='source-list',Source='a\nb'),dict(Operation='list',Source='unexpected'),dict(Operation='update',Backends=['qwen'])]:
  call('/v1/skills/operations',dict(SubmissionKey=uuid.uuid4().hex,**kwargs),400)
 call('/v1/skills/operations',dict(SubmissionKey=uuid.uuid4().hex,Operation='install',Source='owner/repo',Names=['review'],Backends=['minimax']),422)
 assert len(events())==count
 ok('invalid operations, implicit whole-repository installs, option injection and unsupported targets are rejected before execution')
 assert all(not row.get('host_leaked') and not row.get('private_leaked') and row.get('forwarded')=='allowed' and row['cwd']==str(O) for row in events() if 'argv' in row)
 assert call('/v1/tasks')==[]
 ok('management runs in the configured host directory with scoped environment and separate user-task state')
 assert call('/v1/skills/catalog',credential=bob)['Enabled'] is False
 call('/v1/skills/operations',dict(SubmissionKey=uuid.uuid4().hex,Operation='list'),404,credential=bob)
 call('/v1/skills/operations/'+task['Id'],expected=404,credential=bob)
 call('/v1/skills/catalog',expected=401,credential='invalid')
 assert len(events())==count
 ok('host-wide management requires the configured owner; authentication and operation records remain isolated')
 key=uuid.uuid4().hex;request=dict(SubmissionKey=key,Operation='source-list',Source='hold')
 task=call('/v1/skills/operations',request,202)
 end=time.monotonic()+3
 while not any('hold' in row.get('argv',[]) for row in events()):assert time.monotonic()<end;time.sleep(.01)
 assert call('/v1/skills/operations/'+task['Id'])['State']==1
 assert call('/v1/skills/operations',request,202)['Id']==task['Id']
 call('/v1/skills/operations',dict(request,Source='different-source'),400)
 second=submit('list');assert call('/v1/skills/operations/'+second['Id'])['State']==0
 (root/'release').write_text('release')
 assert result(task)['State']==result(second)['State']==3
 assert sum('hold' in row.get('argv',[]) for row in events())==1
 stop();start();assert call('/v1/skills/operations',request,202)['Id']==task['Id']
 assert result(task)['State']==3 and sum('hold' in row.get('argv',[]) for row in events())==1
 ok('admission is durable and idempotent across disconnect/restart; one global writer serializes operations')
 task=submit('source-list',Source='hang')
 end=time.monotonic()+3
 while not any('child_pid' in row for row in events()):assert time.monotonic()<end;time.sleep(.01)
 cancelled=call('/v1/skills/operations/'+task['Id']+'/cancel',{},200);assert result(task)['State']==5
 pids=[row['pid'] for row in events() if 'hang' in row.get('argv',[])]+[row['child_pid'] for row in events() if 'child_pid' in row]
 for pid in pids:
  end=time.monotonic()+3
  while True:
   try:os.kill(pid,0)
   except ProcessLookupError:break
   assert time.monotonic()<end,('surviving cancelled installer',pid);time.sleep(.03)
 ok('explicit cancellation waits for installer and child process cleanup without claiming filesystem rollback')
 for source in ['failed','invalid-utf8','flood','hang']:
  task=submit('source-list',Source=source);done=result(task);assert done['State']==4,done
  assert 'repository unavailable' in done['Result'] if source=='failed' else 'unconfirmed' in done['Result'] or 'timed out' in done['Result']
  ok(source+': real installer failure remains failed with bounded output/time, never reported completed')
 # Completed management records survive a new Host and do not enter normal task-result speech.
 saved=call('/v1/skills/operations');stop();start();assert len(call('/v1/skills/operations'))==len(saved);assert call('/v1/tasks')==[]
 ok('management history survives Host restart and remains separate from foreground task delivery')

 # Revoke authorization while one operation is queued; hard-crashed work must never be replayed.
 (root/'release').unlink()
 revoke_start=len(events());first=submit('source-list',Source='hold')
 end=time.monotonic()+3
 while not any('hold' in row.get('argv',[]) for row in events()[revoke_start:]):assert time.monotonic()<end;time.sleep(.01)
 queued=submit('install',Source='owner/repo',Names=['revoked-skill'],Backends=['qwen'])
 assert call('/v1/skills/operations/'+queued['Id'])['State']==0
 stop(kill=True)
 for row in events()[revoke_start:]:
  if 'hold' in row.get('argv',[]):
   try:os.kill(row['pid'],9)
   except ProcessLookupError:pass
 changed=json.loads(config.read_text());changed['Skills']['AllowedOwners']=['bob'];config.write_text(json.dumps(changed));start()
 end=time.monotonic()+5
 while True:
  stored=json.loads((O/'data/skills-library/tasks.json').read_text())['Tasks']
  current=next(t for t in stored if t['Id']==queued['Id'])
  if current['State']>=3:break
  assert time.monotonic()<end;time.sleep(.03)
 assert current['State']==6 and not any('revoked-skill' in row.get('argv',[]) for row in events()),'REVOKED_SKILL_EXECUTED: queued work ran after administrator removed its owner'
 interrupted=next(t for t in stored if t['Id']==first['Id']);assert interrupted['State']==6
 ok('hard-crashed work is not replayed and queued work rechecks current owner authorization before execution')

 changed['Skills']['AllowedOwners']=['alice'];config.write_text(json.dumps(changed));stop();start()
 before_count=len(events());first=submit('source-list',Source='hold')
 end=time.monotonic()+3
 while not any('hold' in row.get('argv',[]) for row in events()[before_count:]):assert time.monotonic()<end;time.sleep(.01)
 request=dict(SubmissionKey=uuid.uuid4().hex,Operation='install',Source='owner/repo',Names=['changed-config-skill'],Backends=['qwen'])
 queued=call('/v1/skills/operations',request,202);assert call('/v1/skills/operations/'+queued['Id'])['State']==0
 stop(kill=True)
 for row in events()[before_count:]:
  if 'hold' in row.get('argv',[]):
   try:os.kill(row['pid'],9)
   except ProcessLookupError:pass
 changed['Skills']['Package']='skills@9.9.9';config.write_text(json.dumps(changed));start()
 assert result(queued)['State']==6 and not any('changed-config-skill' in row.get('argv',[]) for row in events()),'CHANGED_SKILL_CONFIG_EXECUTED: queued operation ran under a different installer configuration'
 assert call('/v1/skills/operations',request,202)['Id']==queued['Id'],'Same admitted request must still resolve after configuration changes'
 ok('queued installer intent is bound to its accepted configuration and duplicate admission still confirms the original operation')

 changed['Skills']['Package']='skills@1.5.22';config.write_text(json.dumps(changed));stop();start()
 accepted_targets=call('/v1/skills/catalog')['DefaultTargets'];missing=next((t for t in catalog['Targets'] if t['Installer'] not in accepted_targets),None)
 assert missing is not None,'Independent target drift test requires a backend not yet installed/configured'
 count_before=len(events());first=submit('source-list',Source='hold')
 end=time.monotonic()+3
 while not any('hold' in row.get('argv',[]) for row in events()[count_before:]):assert time.monotonic()<end;time.sleep(.01)
 queued=submit('install',Source='owner/repo',Names=['stable-default-target'])
 assert call('/v1/skills/operations/'+queued['Id'])['State']==0
 stop(kill=True)
 for row in events()[count_before:]:
  if 'hold' in row.get('argv',[]):
   try:os.kill(row['pid'],9)
   except ProcessLookupError:pass
 changed['Harnesses']=[dict(Id='newly-configured',Backend=missing['Backend'],Command=str(exe))];config.write_text(json.dumps(changed));start()
 assert result(queued)['State']==3
 actual=next(row['argv'] for row in events() if 'stable-default-target' in row.get('argv',[]));observed_targets=[actual[i+1] for i,x in enumerate(actual) if x=='-a']
 assert observed_targets==accepted_targets,'SKILL_TARGETS_DRIFTED: recovered install used a different target selection than admission'
 ok('accepted default targets remain fixed across new backend configuration and queued restart')
 config_data=json.loads(config.read_text());config_data.pop('Skills');config.write_text(json.dumps(config_data));stop();start()
 assert call('/v1/skills/catalog')['Enabled'] is False
 call('/v1/skills/operations',dict(SubmissionKey=uuid.uuid4().hex,Operation='list'),422)
 ok('default-disabled Host exposes no installer execution until explicitly configured')
finally:
 stop();(O/'http.json').write_text(json.dumps(records,indent=2)+'\n');(O/'results.json').write_text(json.dumps(dict(passed=passed,events=events()),indent=2)+'\n')
