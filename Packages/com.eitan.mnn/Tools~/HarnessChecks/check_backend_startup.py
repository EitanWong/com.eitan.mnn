"""Delayed externally supervised Gateway through the real durable C# Host, not a model inference test."""
import argparse,json,os,socket,subprocess,sys,threading,time,urllib.error,urllib.request,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True);run=out/('run-'+uuid.uuid4().hex);run.mkdir();root=run/'peer';root.mkdir();records=[];passed=[];process=None;log=None;listeners=[]
fixture=Path(__file__).parent/'Fixtures/openclaw_startup_fixture.py';wrapper=run/'openclaw';wrapper.write_text('#!'+sys.executable+'\nexec(compile(open('+repr(str(fixture))+', "rb").read(), '+repr(str(fixture))+', "exec"))\n');wrapper.chmod(0o755)
def port():
 with socket.socket() as sock:sock.bind(('127.0.0.1',0));return sock.getsockname()[1]
hostport=port();gatewayport=port();url='http://127.0.0.1:'+str(hostport);token=uuid.uuid4().hex*2;env=dict(os.environ,STARTUP_HOST_TOKEN=token,OPENCLAW_STARTUP_TEST_ROOT=str(root));config=run/'host.json';base=dict(Id='wait',Backend='openclaw',Protocol='acp',Command=str(wrapper),Endpoint='http://127.0.0.1:'+str(gatewayport),OpenClawStartupWaitMilliseconds=5000,TimeoutMilliseconds=0)
settings=dict(Listen=url,DataDirectory=str(run/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='STARTUP_HOST_TOKEN')],Workspaces=dict(project=str(run),other=str(root)),Harnesses=[base,dict(base,Id='direct',OpenClawStartupWaitMilliseconds=0),dict(base,Id='short',OpenClawStartupWaitMilliseconds=1200)]);config.write_text(json.dumps(settings))
def call(path,body=None,expected=200):
 req=urllib.request.Request(url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=15) as response:status,raw=response.status,response.read()
 except urllib.error.HTTPError as e:status,raw=e.code,e.read()
 records.append(dict(path=path,input=body,status=status,output=raw.decode()));assert status==expected,(path,status,raw);assert token.encode() not in raw;return json.loads(raw) if raw else None
def wait(fn,timeout=12):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  value=fn()
  if value:return value
  time.sleep(.025)
 raise AssertionError('Backend startup transition not observed')
def start():
 global process,log
 log=(run/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--config',str(config)],env=env,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None,Path(log.name).read_text()
  try:return call('/v1/harnesses')
  except urllib.error.URLError:return None
 wait(ready)
def stop():
 global process
 if process and process.poll() is None:process.kill();process.wait(timeout=10)
 if log:log.close()
 process=None
def events():return [json.loads(line) for line in (root/'wire.jsonl').read_text().splitlines()] if (root/'wire.jsonl').exists() else []
def count(method=None):return len([e for e in events() if e['kind']=='spawn']) if method is None else len([e for e in events() if e.get('frame',{}).get('method')==method])
def submit(text,harness='wait',conversation=None,key=None,workspace='project'):return call('/v1/tasks',dict(ConversationId=conversation or uuid.uuid4().hex,SubmissionKey=key or uuid.uuid4().hex,HarnessId=harness,WorkspaceKey=workspace,Objective=text),202)
def task(item):return call('/v1/tasks/'+item['Id'])
def terminal(item):
 t=task(item);return t if t['State'] in [3,4,5,6] else None
def native_state(value):
 state=json.loads(value['BackendState']);return json.loads(state['NativeCheckpoint']) if 'NativeCheckpoint' in state else state
def pending(item):
 t=task(item);assert t['State'] not in [3,4,5,6],('Automatic startup waiting is unavailable',t);return t if 'Waiting for OpenClaw Gateway' in (t.get('Activity') or '') else None
def listen():
 sock=socket.socket();sock.setsockopt(socket.SOL_SOCKET,socket.SO_REUSEADDR,1);sock.bind(('127.0.0.1',gatewayport));sock.listen();sock.settimeout(.05);alive=threading.Event();alive.set()
 def serve():
  while alive.is_set():
   try:client,_=sock.accept();client.close()
   except socket.timeout:pass
   except OSError:break
 thread=threading.Thread(target=serve,daemon=True);thread.start();listeners.append((sock,alive,thread))
def close_listeners():
 for sock,alive,thread in listeners:alive.clear();sock.close();thread.join(timeout=1)
 listeners.clear()
def ok(message):passed.append(message);print('PASS '+message,flush=True)
try:
 start();assert not events();item=submit('delayed',conversation='delayed',key='intent');waiting=wait(lambda:pending(item));assert submit('delayed',conversation='delayed',key='intent')['Id']==item['Id'];assert not events();saved=native_state(waiting);assert saved['WaitingForBackend'] and saved['StartupDeadlineUtcTicks']>0
 assert call('/v1/results/claim',dict(ConversationId='delayed',ClientId='phone',LeaseSeconds=60),expected=204) is None
 ok('durable nonblocking startup admission exposes foreground activity, deduplicates intent and sends no native command or premature result')
 listen();done=wait(lambda:terminal(item));assert done['State']==3 and done['Result']=='startup result: delayed',done;assert count('session/new')==1 and count('session/prompt')==1;assert not native_state(done)['WaitingForBackend'];close_listeners()
 ok('delayed TCP endpoint starts exactly one official-shaped ACP bridge/session/prompt; TCP reachability alone is never final success')
 n=count();item=submit('live-followup',conversation='delayed');done=wait(lambda:terminal(item));assert done['State']==3 and done['Result']=='startup result: live-followup' and count()==n,done
 ok('an already open native conversation does not re-enter startup waiting or require a second TCP connection')
 n=count();item=submit('cancel');wait(lambda:pending(item));begin=time.monotonic();cancelled=call('/v1/tasks/'+item['Id']+'/cancel',{});assert cancelled['State']==5 and time.monotonic()-begin<2,cancelled;assert count()==n
 ok('cancellation during startup confirms no ACP execution and promptly releases the scheduling lane')
 item=submit('restart');waiting=wait(lambda:pending(item));deadline=native_state(waiting)['StartupDeadlineUtcTicks'];n=count();stop();start();waiting=wait(lambda:pending(item));assert native_state(waiting)['StartupDeadlineUtcTicks']==deadline and count()==n;listen();done=wait(lambda:terminal(item));assert done['State']==3 and done['Result']=='startup result: restart';close_listeners()
 ok('Host crash resumes only persisted pre-native waiting with the same task identity and absolute deadline; no work is replayed')
 item=submit('never-ready','short',conversation='timeout-context');wait(lambda:pending(item));done=wait(lambda:terminal(item));assert done['State']==4 and 'no ACP process' in done['Result'] and 'startup wait' in done['Result'],done
 listen();follow=submit('after-timeout','short',conversation='timeout-context');done=wait(lambda:terminal(follow));assert done['State']==3;close_listeners()
 ok('startup timeout is Failed with no native effects; the same conversation remains usable without an uncertainty fence')
 item=submit('absolute-budget','short');waiting=wait(lambda:pending(item));deadline=native_state(waiting)['StartupDeadlineUtcTicks'];stop();time.sleep(1.3);n=count();start();done=wait(lambda:terminal(item));assert done['State']==4 and count()==n and native_state(done)['StartupDeadlineUtcTicks']==deadline
 ok('elapsed startup budget survives downtime and restart; an expired wait cannot reset or start a bridge')
 n=count();before_prompts=count('session/prompt');item=submit('direct-cold','direct');done=wait(lambda:terminal(item));assert done['State']==6 and count()==n+1 and count('session/prompt')==before_prompts,done
 ok('default external Gateway remains a direct official bridge connection and never silently enters a TCP wait or retry')
 listen();(root/'reject').touch();n=count();item=submit('rejected',conversation='rejected-context');done=wait(lambda:terminal(item));assert done['State']==6 and count()==n+1;(root/'reject').unlink();follow=submit('inspect-first',conversation='rejected-context');assert wait(lambda:terminal(follow))['State']==6 and count()==n+1
 ok('a reachable port with rejected native initialization remains Interrupted; initialization/authentication is never retried as startup')
 (root/'hold-initialize').touch();before_initialize=count('initialize');item=submit('unknown-native-setup',conversation='setup-fence');wait(lambda:count('initialize')==before_initialize+1);wait(lambda:not native_state(task(item))['WaitingForBackend']);n=count()
 def disconnecting_cancel():
  try:call('/v1/tasks/'+item['Id']+'/cancel',{})
  except (OSError,ValueError):pass
 cancel_thread=threading.Thread(target=disconnecting_cancel,daemon=True);cancel_thread.start();wait(lambda:task(item)['State']==2);stop();cancel_thread.join(timeout=3);(root/'hold-initialize').unlink();start();done=wait(lambda:terminal(item));assert done['State']==6 and count()==n;follow=submit('must-not-replay',conversation='setup-fence');assert wait(lambda:terminal(follow))['State']==6 and count()==n
 ok('the durable recovery right is revoked before initialize; a crash with unconfirmed native cancellation stays Interrupted and fences replacement work')
 before_prompts=count('session/prompt');item=submit('hold',conversation='prompt-fence');wait(lambda:count('session/prompt')==before_prompts+1);n=count();stop();start();assert wait(lambda:terminal(item))['State']==6;follow=submit('must-not-replay',conversation='prompt-fence');assert wait(lambda:terminal(follow))['State']==6 and count()==n
 ok('a crash after native prompt admission remains Interrupted and is never mistaken for recoverable startup waiting')
 before_prompts=count('session/prompt');blocker=submit('hold',conversation='queue-block');wait(lambda:count('session/prompt')==before_prompts+1);queued=submit('queued-cancel');q=task(queued);assert q['State']==0 and native_state(q)['StartupDeadlineUtcTicks']==0;assert call('/v1/tasks/'+queued['Id']+'/cancel',{})['State']==5
 queued=submit('queued-restart');q=task(queued);assert q['State']==0 and native_state(q)['StartupDeadlineUtcTicks']==0;n=count();stop();start();assert wait(lambda:terminal(blocker))['State']==6;done=wait(lambda:terminal(queued));assert done['State']==3 and done['Result']=='startup result: queued-restart' and count()==n+1
 ok('queued admission consumes no startup budget, can be cancelled without native work and starts once after Host restart')
 n=count();call('/v1/harnesses');call('/v1/harnesses/wait/readiness?workspace=project');assert count()==n
 ok('passive health/catalog/readiness queries never launch a Gateway or ACP process')
 stop()
 for change in [dict(OpenClawStartupWaitMilliseconds=-1),dict(OpenClawStartupWaitMilliseconds=60001),dict(Endpoint='https://remote.invalid'),dict(Backend='acp',Arguments=[])]:
  invalid=run/('invalid-'+uuid.uuid4().hex+'.json');invalid.write_text(json.dumps(dict(settings,Harnesses=[dict(base,**change)])))
  cmd=['dotnet',str(a.host),'--config',str(invalid)];p=subprocess.run(cmd,env=env,capture_output=True,text=True,timeout=15);records.append(dict(command=cmd,stdout=p.stdout,stderr=p.stderr,exit_code=p.returncode));assert p.returncode!=0 and ('Startup waiting requires' in p.stderr or 'must be between 0 and 60000' in p.stderr),p.stderr
 ok('configuration rejects negative/over-budget, remote and non-OpenClaw waits before any backend execution')
 cmd=['dotnet',str(a.host),'--service','prepare','--id','startup-wait','--config',str(config),'--runtime',str(Path('/opt/homebrew/bin/dotnet').resolve()),'--directory',str(run/'registration')]
 p=subprocess.run(cmd,env=env,capture_output=True,text=True,timeout=30);records.append(dict(command=cmd,stdout=p.stdout,stderr=p.stderr,exit_code=p.returncode));assert p.returncode==0,p.stderr
 snapshots=list((run/'registration').glob('*.json'));assert any(any(h.get('OpenClawStartupWaitMilliseconds')==5000 for h in json.loads(f.read_text()).get('Harnesses',[])) for f in snapshots)
 ok('private service configuration retains the opt-in startup contract without installing or starting a system service')
finally:
 stop();close_listeners();(out/'results.json').write_text(json.dumps(passed,indent=2));(out/'http.json').write_text(json.dumps(records,indent=2));(out/'events.json').write_text(json.dumps(events(),indent=2));print('EVIDENCE '+str(run),flush=True)
