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
 start();item=call('/v1/tasks',dict(ConversationId='foreground',SubmissionKey=uuid.uuid4().hex,HarnessId='openclaw',WorkspaceKey='project',Objective='single:coordinator',Attachments=[dict(Kind=0,Payload='original input',MediaType='text/plain',FileName='input.txt')]),202);wait(lambda:collecting(item));assert call('/v1/results/claim',dict(ConversationId='foreground',ClientId='phone',LeaseSeconds=60),204) is None
 initial=native('session/prompt')[-1];assert len(initial['params']['prompt'])==2;release(item);done=wait(lambda:terminal(item));assert done['State']==3 and done['Result'].startswith('Summary: single:coordinator'),('Missing original coordinator result turn',done)
 prompts=native('session/prompt');assert len(prompts)==2 and prompts[1]['params']['sessionId']==initial['params']['sessionId'];message=json.loads(prompts[1]['params']['prompt'][0]['text']);assert message['task_id']==item['Id'] and message['targets'][0]['run_id']==children(item)[0]['RunId'] and message['targets'][0]['session_id']==children(item)[0]['SessionKey'];assert len(prompts[1]['params']['prompt'])==1
 assert any('child result 0: single:coordinator' in p['Payload'] for ar in done['Artifacts'] for p in ar['Parts']);claimed=call('/v1/results/claim',dict(ConversationId='foreground',ClientId='phone',LeaseSeconds=60));assert claimed['Result']==done['Result'] and claimed['Artifacts']==done['Artifacts'];call('/v1/tasks/'+item['Id']+'/receipt',dict(LeaseId=claimed['LeaseId'],Receipt=4))
 ok('original native coordinator receives exact child result and produces final frontend text; raw child artifacts and result receipt retain parent provenance')
 item=submit('multi:coordinator');wait(lambda:collecting(item));release(item);done=wait(lambda:terminal(item));assert done['State']==3 and 'child result 0' in done['Result'] and 'child result 1' in done['Result'];assert len(json.loads(native('session/prompt')[-1]['params']['prompt'][0]['text'])['targets'])==2
 ok('multiple child results are passed together to one original coordinator result turn')
 item=submit('single:update-denied');wait(lambda:collecting(item));n=len(native('session/prompt'));response=call('/v1/tasks/'+item['Id']+'/update',dict(Text='change target'),422);assert len(native('session/prompt'))==n;release(item);assert wait(lambda:terminal(item))['State']==3
 ok('unsupported native collecting update is rejected without inventing a directional child update or sending a prompt')
 item=submit('hold-prompt:update-before-end',conversation='update-before-end');wait(lambda:json.loads(task(item).get('BackendState') or '{}').get('Children'));n=len(native('session/prompt'));call('/v1/tasks/'+item['Id']+'/update',dict(Text='retask native child'),422);assert len(native('session/prompt'))==n;assert call('/v1/tasks/'+item['Id']+'/cancel',{})['State']==5
 ok('observed native child grants also reject unsupported directed updates before initial ACP end_turn')
 item=submit('single:restart',conversation='restart');wait(lambda:collecting(item));sid=native('session/prompt')[-1]['params']['sessionId'];n=len(native('session/prompt'));stop();start();wait(lambda:collecting(item));assert len(native('session/prompt'))==n;release(item);done=wait(lambda:terminal(item));assert done['State']==3 and done['Result'].startswith('Summary: single:restart');assert len(native('session/prompt'))==n+1 and native('session/prompt')[-1]['params']['sessionId']==sid;assert native('session/load')[-1]['params']['sessionId']==sid
 ok('cold collecting recovery only reads child work then loads exact original coordinator and summarizes once without replaying the objective')
 (root/'hold-summary').touch();item=submit('single:summary-crash',conversation='crash');wait(lambda:collecting(item));release(item);wait(lambda:json.loads(task(item)['BackendState'])['Phase']=='summarizing');wait(lambda:any('single:summary-crash' in e['params']['prompt'][0]['text'] and e['params']['prompt'][0]['text'].startswith('{') for e in native('session/prompt')));n=len(native('session/prompt'));stop();(root/'hold-summary').unlink();start();done=wait(lambda:terminal(item));assert done['State']==6 and len(native('session/prompt'))==n;follow=submit('direct:fenced',conversation='crash');assert wait(lambda:terminal(follow))['State']==6 and len(native('session/prompt'))==n
 ok('crash after persisted summarizing boundary never replays result prompt and fences original conversation')
 item=submit('single:index-missing',conversation='missing');wait(lambda:collecting(item));stop();index=next((run/'data').glob('acp-sessions-*.json'));saved=json.loads(index.read_text());saved['Sessions']={};index.write_text(json.dumps(saved));n=len(native('session/new'));np=len(native('session/prompt'));start();wait(lambda:collecting(item));release(item);done=wait(lambda:terminal(item));assert done['State']==6 and len(native('session/new'))==n and len(native('session/prompt'))==np
 ok('missing coordinator session index never creates a replacement native context to summarize children')
 (root/'no-resume').touch();item=submit('single:nonresumable',conversation='no-resume');wait(lambda:collecting(item));stop();np=len(native('session/prompt'));start();wait(lambda:collecting(item));release(item);assert wait(lambda:terminal(item))['State']==6 and len(native('session/prompt'))==np;(root/'no-resume').unlink()
 ok('nonresumable original coordinator fails honestly after restart instead of silently using a new session')
 (root/'hold-summary').touch();item=submit('single:cancel-summary');wait(lambda:collecting(item));release(item);wait(lambda:any('single:cancel-summary' in e['params']['prompt'][0]['text'] and e['params']['prompt'][0]['text'].startswith('{') for e in native('session/prompt')));done=call('/v1/tasks/'+item['Id']+'/cancel',{});assert done['State']==5,done;(root/'hold-summary').unlink()
 ok('cancellation during original coordinator result turn observes exact native prompt termination')
 for flag in ['disconnect-summary','rogue-summary','fail-summary']:
  (root/flag).touch();item=submit('single:'+flag);wait(lambda:collecting(item));release(item);done=wait(lambda:terminal(item));assert done['State'] in [4,6],(flag,done);(root/flag).unlink()
 ok('lost summary, unsolicited tool execution and incomplete result stop reason never become successful final feedback')
 item=submit('mixed:failure');wait(lambda:collecting(item));release(item);done=wait(lambda:terminal(item));assert done['State']==4 and done['Result'].startswith('Summary: mixed:failure') and 'failed:' in done['Result'];message=json.loads(native('session/prompt')[-1]['params']['prompt'][0]['text']);assert [t['status'] for t in message['targets']]==['completed','failed']
 ok('coordinator sees every mixed child terminal status; a successful summary cannot upgrade native failure to Completed')
 item=submit('longmulti:bounded');wait(lambda:collecting(item));release(item);done=wait(lambda:terminal(item));assert done['State']==3;message=json.loads(native('session/prompt')[-1]['params']['prompt'][0]['text']);assert len(message['targets'])==2 and all(t['result_is_preview'] and len(t['result'])<=16384 for t in message['targets']);assert len([p for ar in done['Artifacts'] for p in ar['Parts'] if ' tail ' in p['Payload']])==2
 ok('fair per-child result preview budget preserves each contribution while full native texts remain in exact raw artifacts')
 (root/'summary-artifact').touch();item=submit('single:artifact');wait(lambda:collecting(item));release(item);done=wait(lambda:terminal(item));art=next(ar for ar in done['Artifacts'] if any(p['Payload']=='summary attachment' for p in ar['Parts']));assert done['State']==3 and art['SourceTaskId']==item['Id'] and art['Id'].startswith('openclaw-result-');(root/'summary-artifact').unlink()
 ok('native result-turn attachments are delivered with distinct stable parent-scoped artifact identities')
 item=submit('single:foreign-load',conversation='foreign-load');wait(lambda:collecting(item));stop();(root/'foreign-load').touch();np=len(native('session/prompt'));start();wait(lambda:collecting(item));release(item);assert wait(lambda:terminal(item))['State']==6 and len(native('session/prompt'))==np;(root/'foreign-load').unlink()
 ok('a native load acknowledgement naming another session is rejected before any result prompt is sent')
 n=len(native('session/prompt'));ng=len(rpc('connect'));item=submit('direct:unchanged');done=wait(lambda:terminal(item));assert done['State']==3 and done['Result']=='direct answer' and len(native('session/prompt'))==n+1 and len(rpc('connect'))==ng
 ok('nondelegated OpenClaw stays one ordinary ACP turn with no Gateway access')
 stop();cfg=json.loads(config.read_text());cfg['Harnesses'][0]['TimeoutMilliseconds']=400;cfg['DataDirectory']=str(run/'deadline-data');config.write_text(json.dumps(cfg));start();(root/'hold-summary').touch();item=submit('single:summary-deadline',conversation='deadline');wait(lambda:collecting(item));release(item);done=wait(lambda:terminal(item));assert done['State']==6,done;n=len(native('session/prompt'));follow=submit('direct:after-deadline',conversation='deadline');assert wait(lambda:terminal(follow))['State']==6 and len(native('session/prompt'))==n;(root/'hold-summary').unlink()
 ok('absolute collection deadline also ends an unconfirmed summary and fences later work without replay')
 stop();invalid=json.loads(config.read_text());env['COORD_SCOPE_TOKEN']=uuid.uuid4().hex*2;invalid['DataDirectory']=str(run/'invalid-data');invalid['CoordinatorMcpScopes']=[dict(Id='native',OwnerId='alice',ConversationId='phone',WorkspaceKey='project',HarnessId='worker',TokenEnvironmentVariable='COORD_SCOPE_TOKEN')];invalid['Harnesses'].append(dict(Id='worker',Protocol='acp',Command=sys.executable,Arguments=['-c','pass']));invalid['Harnesses'][0].update(CoordinatorScopeId='native',AcpMcpServers=[dict(Name='coordinator',Type='http',Url=url+'/mcp/coordinator',Headers=[dict(Name='Authorization',ValueEnvironmentVariable='COORD_SCOPE_TOKEN',Prefix='Bearer ')])]);bad=run/'invalid.json';bad.write_text(json.dumps(invalid))
 result=subprocess.run(['dotnet',str(a.host),'--config',str(bad)],env=env,capture_output=True,text=True,timeout=8);assert result.returncode!=0 and 'OpenClaw uses native delegation' in result.stderr;(out/'invalid-config.json').write_text(json.dumps(dict(command=['dotnet',str(a.host),'--config',str(bad)],stdout=result.stdout,stderr=result.stderr,exit_code=result.returncode),indent=2))
 ok('explicit HTTP-MCP coordinator configuration is rejected for native OpenClaw with actionable configuration guidance')
 assert not [e for e in events() if e['kind']=='error']
finally:
 stop();gateway.shutdown();gateway.server_close();(out/'results.json').write_text(json.dumps(passed,indent=2));(out/'http.json').write_text(json.dumps(records,indent=2));(out/'events.json').write_text(json.dumps(events(),indent=2));print('EVIDENCE '+str(run),flush=True)
