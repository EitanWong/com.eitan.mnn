"""Real C# Host plus independent ACP/RFC6455 peers; no real vendor inference."""
import argparse,importlib.util,json,os,socket,subprocess,sys,threading,time,urllib.error,urllib.request,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True);run=out/('run-'+uuid.uuid4().hex);run.mkdir();root=run/'peer';root.mkdir();token=uuid.uuid4().hex*2;bob=uuid.uuid4().hex*2;gateway_token=uuid.uuid4().hex*2;records=[];passed=[];process=None;log=None
fixture=Path(__file__).parent/'Fixtures/openclaw_delegation_fixture.py';spec=importlib.util.spec_from_file_location('peer',fixture);peer=importlib.util.module_from_spec(spec);spec.loader.exec_module(peer);gateway=peer.Gateway(root,gateway_token);threading.Thread(target=gateway.serve_forever,daemon=True).start()
wrapper=run/'openclaw';wrapper.write_text('#!'+sys.executable+'\nexec(compile(open('+repr(str(fixture))+', "rb").read(), '+repr(str(fixture))+', "exec"))\n');wrapper.chmod(0o755)
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
url='http://127.0.0.1:'+str(port);env=dict(os.environ,DELEGATION_HOST_TOKEN=token,DELEGATION_BOB_TOKEN=bob,OPENCLAW_DELEGATION_ROOT=str(root),NATIVE_GATEWAY_TOKEN=gateway_token);config=run/'host.json';config.write_text(json.dumps(dict(Listen=url,DataDirectory=str(run/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='DELEGATION_HOST_TOKEN'),dict(OwnerId='bob',TokenEnvironmentVariable='DELEGATION_BOB_TOKEN')],Workspaces=dict(project=str(run),other=str(root)),Harnesses=[dict(Id='openclaw',Backend='openclaw',Command=str(wrapper),Endpoint='http://127.0.0.1:'+str(gateway.server_port),TokenEnvironmentVariable='NATIVE_GATEWAY_TOKEN',TimeoutMilliseconds=10000)])))
def call(path,body=None,expected=200,auth=None):
 req=urllib.request.Request(url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(auth or token),'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=20) as response:status,raw=response.status,response.read()
 except urllib.error.HTTPError as e:status,raw=e.code,e.read()
 records.append(dict(path=path,input=body,status=status,output=raw.decode()));assert status==expected,(path,status,raw);assert all(secret.encode() not in raw for secret in [token,bob,gateway_token]);return json.loads(raw) if raw else None
def wait(fn,timeout=15):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  value=fn()
  if value:return value
  time.sleep(.025)
 raise AssertionError('OpenClaw native transition not observed')
def start():
 global process,log
 log=(run/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--config',str(config)],env=env,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None,Path(log.name).read_text()
  try:return call('/v1/harnesses')
  except urllib.error.URLError:return None
 return wait(ready)
def stop():
 global process
 if process and process.poll() is None:process.kill();process.wait(timeout=10)
 if log:log.close()
 process=None
def events():return [json.loads(line) for line in (root/'wire.jsonl').read_text().splitlines()] if (root/'wire.jsonl').exists() else []
def native(method):return [e for e in events() if e['kind']=='acp' and e['method']==method]
def rpc(method):return [e for e in events() if e['kind']=='gateway' and e['method']==method]
def submit(text,conversation=None,auth=None,workspace='project'):return call('/v1/tasks',dict(ConversationId=conversation or uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId='openclaw',WorkspaceKey=workspace,Objective=text),202,auth)
def task(item,auth=None):return call('/v1/tasks/'+item['Id'],auth=auth)
def terminal(item,auth=None):
 t=task(item,auth);return t if t['State'] in [3,4,5,6] else None
def collecting(item,auth=None):
 t=task(item,auth);assert t['State'] not in [3,4,5,6],('Native delegation ended before child completion',t);state=json.loads(t.get('BackendState') or '{}');return t if state.get('Phase')=='collecting' else None
def children(item,auth=None):return json.loads(task(item,auth)['BackendState'])['Children']
def release(item,auth=None):
 for child in children(item,auth):(root/('release-'+child['RunId'])).touch()
def ok(text):passed.append(text);print('PASS '+text,flush=True)
try:
 start();assert not rpc('connect');item=submit('single:first',conversation='phone');wait(lambda:collecting(item));assert len(children(item))==1 and json.loads(task(item)['BackendState'])['TaskId']==item['Id'];assert call('/v1/results/claim',dict(ConversationId='phone',ClientId='phone',LeaseSeconds=60),204) is None
 before=len(native('session/prompt'));wait(lambda:rpc('agent.wait'));assert not rpc('chat.history');assert task(item)['State']==1;release(item);done=wait(lambda:terminal(item));assert done['State']==3 and 'child result 0: single:first' in done['Result'] and 'STALE HISTORY' not in done['Result'] and len(native('session/prompt'))==before+1,done
 assert any('child result 0' in part['Payload'] for artifact in done['Artifacts'] for part in artifact['Parts']);assert len(rpc('chat.history'))==1
 claimed=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-phone',LeaseSeconds=60));assert claimed['Id']==item['Id'] and claimed['Result']==done['Result'] and claimed['Artifacts']==done['Artifacts'];call('/v1/tasks/'+item['Id']+'/receipt',dict(LeaseId=claimed['LeaseId'],Receipt=4))
 ok('ACP end_turn keeps parent Running until exact native run finishes; final child text/artifact reaches durable frontend results without replaying the objective')
 for scenario in ['wrapped','json-string','send','multi','pending']:
  item=submit(scenario+':shape');wait(lambda:collecting(item));assert len(children(item))==(2 if scenario=='multi' else 1);release(item);done=wait(lambda:terminal(item));assert done['State']==3 and scenario+':shape' in done['Result'],done
 ok('split tool fields, nested/string native outputs, sessions_send, duplicate updates and multiple exact native children are recognized once')
 item=submit('single:resume',conversation='resume');wait(lambda:collecting(item));ids=children(item);n=len(native('session/prompt'));stop();start();wait(lambda:collecting(item));assert children(item)==ids and len(native('session/prompt'))==n;release(item);done=wait(lambda:terminal(item));assert done['State']==3 and 'single:resume' in done['Result'] and len(native('session/prompt'))==n+1
 ok('Host crash resumes read-only native wait/history then exact original coordinator result turn; no child task or objective is replayed')
 item=submit('single:cancel');wait(lambda:collecting(item));ids=children(item);done=call('/v1/tasks/'+item['Id']+'/cancel',{});assert done['State']==5,done;abort=rpc('chat.abort')[-1]['params'];assert abort==dict(runId=ids[0]['RunId'],sessionKey=ids[0]['SessionKey']);assert not any(e['params'].get('sessionKey')==ids[0]['SessionKey'] for e in rpc('chat.history'))
 ok('cancellation sends exact native run/session identity, observes explicit aborted termination and never fetches a cancelled result as success')
 item=submit('slow-wait:cancel');wait(lambda:collecting(item));rid=children(item)[0]['RunId'];wait(lambda:any(e['params'].get('runId')==rid for e in rpc('agent.wait')));begin=time.monotonic();done=call('/v1/tasks/'+item['Id']+'/cancel',{});assert done['State']==5 and time.monotonic()-begin<2,(done,time.monotonic()-begin)
 ok('native cancellation interrupts an outstanding long poll and aborts without waiting for its thirty-second response slice')
 for scenario in ['abort-lost','abort-weak']:
  item=submit(scenario+':cancel');wait(lambda:collecting(item));done=call('/v1/tasks/'+item['Id']+'/cancel',{});assert done['State']==6,done
 ok('lost or weak native abort acknowledgement remains Interrupted instead of falsely reporting confirmed cancellation')
 (root/'hold-abort-response').touch();item=submit('abort-crash:journal');wait(lambda:collecting(item));rid=children(item)[0]['RunId'];before_prompt=len(native('session/prompt'))
 def cancelling():
  try:call('/v1/tasks/'+item['Id']+'/cancel',{})
  except (OSError,ValueError):pass
 thread=threading.Thread(target=cancelling,daemon=True);thread.start();wait(lambda:any(e['params'].get('runId')==rid for e in rpc('chat.abort')));wait(lambda:children(item)[0]['AbortRequested']);stop();thread.join(timeout=3);(root/'hold-abort-response').unlink();start();done=wait(lambda:terminal(item));assert done['State']==6 and len([e for e in rpc('chat.abort') if e['params']['runId']==rid])==1 and len(native('session/prompt'))==before_prompt
 ok('crash after journalled abort resumes only read-only termination checks and never replays an unacknowledged abort or native prompt')
 item=submit('abort-race:completed');wait(lambda:collecting(item));done=call('/v1/tasks/'+item['Id']+'/cancel',{});assert done['State']==3 and 'abort-race:completed' in done['Result'],done
 ok('a confirmed already-ended native run wins cancellation and returns its completed result instead of a false Cancelled state')
 item=submit('hold-prompt:uncertain',conversation='uncertain');wait(lambda:len([e for e in native('session/prompt') if e['params']['prompt'][0]['text']=='hold-prompt:uncertain'])==1);n=len(native('session/prompt'));stop();start();done=wait(lambda:terminal(item));assert done['State']==6 and len(native('session/prompt'))==n;follow=submit('direct:no-replay',conversation='uncertain');assert wait(lambda:terminal(follow))['State']==6
 ok('a crash before coordinator prompt completion never treats partial child handles as a safe recovery boundary or replays the native prompt')
 alice=submit('single:alice',auth=token,workspace='project');b=submit('single:bob',auth=bob,workspace='other');wait(lambda:collecting(alice));wait(lambda:collecting(b,bob));assert children(alice)[0]['RunId']!=children(b,bob)[0]['RunId'];call('/v1/tasks/'+alice['Id'],auth=bob,expected=404);call('/v1/tasks/'+alice['Id']+'/cancel',{},auth=bob,expected=404);release(alice);assert wait(lambda:terminal(alice))['State']==3;assert task(b,bob)['State']==1;release(b,bob);assert wait(lambda:terminal(b,bob))['State']==3
 ok('owner/workspace/task scopes keep native handles, cancellations, histories and result delivery isolated')
 for scenario in ['failed','empty','foreign-history','oversized','disconnect','malformed','overflow','completion-run-mismatch']:
  item=submit(scenario+':failure')
  if scenario not in ['malformed','overflow','failed','disconnect']:
   wait(lambda:collecting(item));release(item)
  done=wait(lambda:terminal(item));assert done['State'] in [4,6] and done['State']!=3,(scenario,done)
 ok('native failure, missing/mismatched/oversized history, disconnect and malformed/overflowed native grants never become successful task results')
 (root/'wrong-rpc-id').touch();item=submit('single:correlation');wait(lambda:collecting(item));release(item);assert wait(lambda:terminal(item))['State']==3;(root/'wrong-rpc-id').unlink()
 ok('Gateway handshake authenticates protocol3/4, keeps credentials out of task responses and correlates only matching RPC IDs')
 n=len(rpc('connect'));item=submit('direct:unchanged');done=wait(lambda:terminal(item));assert done['State']==3 and done['Result']=='direct answer' and len(rpc('connect'))==n
 ok('ordinary ACP results keep existing behavior and never open a native Gateway RPC connection')
 page=call('/v1/tasks/'+item['Id']+'/native-sessions');history=call('/v1/tasks/'+item['Id']+'/native-history/load',{});assert page['Supported'] is False and history['Supported'] is True
 ok('wrapped OpenClaw retains existing owned native session-list and history routes without granting foreign sessions')
 stop();cfg=json.loads(config.read_text());cfg['Harnesses'][0].pop('TokenEnvironmentVariable');cfg['DataDirectory']=str(run/'environment-token-data');config.write_text(json.dumps(cfg));env['OPENCLAW_GATEWAY_TOKEN']=gateway_token;start();item=submit('single:env-token');wait(lambda:collecting(item));release(item);assert wait(lambda:terminal(item))['State']==3
 ok('Gateway RPC shares the explicitly allowed official OPENCLAW_GATEWAY_TOKEN environment with the ACP bridge')
 stop();cfg['Harnesses'][0]['TimeoutMilliseconds']=0;config.write_text(json.dumps(cfg));start();item=submit('ignore-cancel:unknown',conversation='unconfirmed-native-cancel');wait(lambda:any(e['params']['prompt'][0]['text']=='ignore-cancel:unknown' for e in native('session/prompt')));begin=time.monotonic();done=call('/v1/tasks/'+item['Id']+'/cancel',{});assert done['State']==6 and time.monotonic()-begin<13,done
 n=len(native('session/prompt'));follow=submit('direct:must-not-replace',conversation='unconfirmed-native-cancel');assert wait(lambda:terminal(follow))['State']==6 and len(native('session/prompt'))==n
 ok('an unacknowledged coordinator prompt cancellation returns Interrupted within its confirmation budget and fences later work instead of hanging indefinitely')
 assert not [e for e in events() if e['kind']=='error']
finally:
 stop();gateway.shutdown();gateway.server_close();(out/'results.json').write_text(json.dumps(passed,indent=2));(out/'http.json').write_text(json.dumps(records,indent=2));(out/'events.json').write_text(json.dumps(events(),indent=2));print('EVIDENCE '+str(run),flush=True)
