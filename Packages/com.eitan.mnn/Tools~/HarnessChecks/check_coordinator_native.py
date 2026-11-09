"""Real C# Host + external ACP peer: scope-owned native project session routing."""
import argparse,concurrent.futures,json,os,socket,subprocess,sys,time,urllib.error,urllib.request,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();s=Path(__file__).resolve().parent;out=a.output.resolve();out.mkdir(parents=True,exist_ok=True);run=out/('run-'+uuid.uuid4().hex);run.mkdir();peer=run/'peer';peer.mkdir()
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
url='http://127.0.0.1:'+str(port);secrets={k:uuid.uuid4().hex*2 for k in ['HOST','BOB','FRONT','OTHER','BOBSCOPE','SPACE','HARNESS']};env=dict(os.environ,**{'NS_'+k:v for k,v in secrets.items()},NATIVE_PROJECT_ROOT=str(peer));process=None;log=None;passed=[];http_records=[]
scopes=[dict(Id=k,OwnerId='bob' if k=='BOBSCOPE' else 'alice',ConversationId='other-phone' if k=='OTHER' else 'phone',WorkspaceKey='other' if k=='SPACE' else 'test',HarnessId='otherworker' if k=='HARNESS' else 'worker',TokenEnvironmentVariable='NS_'+k) for k in ['FRONT','OTHER','BOBSCOPE','SPACE','HARNESS']]
config=dict(Listen=url,DataDirectory=str(run/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId=o,TokenEnvironmentVariable='NS_'+k) for o,k in [('alice','HOST'),('bob','BOB')]],Workspaces=dict(test=str(run),other=str(peer)),CoordinatorMcpScopes=scopes,Harnesses=[dict(Id=k,Protocol='acp',Command=sys.executable,Arguments=['-u',str(s/'Fixtures/coordinator_native_fixture.py')],ForwardEnvironment=['NATIVE_PROJECT_ROOT'],TimeoutMilliseconds=0) for k in ['worker','otherworker']]);cfg=run/'host.json';cfg.write_text(json.dumps(config))
def wait(fn):
 end=time.monotonic()+30
 while time.monotonic()<end:
  v=fn()
  if v:return v
  time.sleep(.03)
 raise AssertionError('Timed out waiting for native session operation')
def http(path,body=None,token='HOST',status=200):
 req=urllib.request.Request(url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+secrets[token],'Content-Type':'application/json','Accept':'application/json,text/event-stream'})
 try:
  with urllib.request.urlopen(req,timeout=15) as r:code,raw=r.status,r.read()
 except urllib.error.HTTPError as e:code,raw=e.code,e.read()
 assert code==status,(code,raw);v=json.loads(raw) if raw else None;http_records.append(dict(path=path,input=body,status=code,output=v));return v
def call(name,args=None,scope='FRONT',error=False):
 r=http('/mcp/coordinator',dict(jsonrpc='2.0',id=uuid.uuid4().hex,method='tools/call',params=dict(name='siriai_'+name,arguments=args or {})),scope)['result']
 assert bool(r.get('isError'))==error,('Native project session ID is not selectable in its authorized coordinator scope',r)
 return json.loads(r['content'][0]['text'])
def task(v):return http('/v1/tasks/'+v.get('delegation_id',v.get('Id','')))
def terminal(v):
 t=task(v);return t if t['State'] in [3,4,5,6] else None
def events():return [json.loads(l) for l in (peer/'events.jsonl').read_text().splitlines()]
def ok(v):passed.append(v);print('PASS '+v,flush=True)
def start():
 global process,log
 log=(run/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--config',str(cfg)],env=env,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None,'Host exited'
  try:return http('/v1/harnesses')
  except urllib.error.URLError:return None
 wait(ready)
def stop():
 global process
 if process and process.poll() is None:process.kill();process.wait(timeout=10);log.close()
 process=None
try:
 start();first=call('session_start',dict(request_id='one',prompt='first',title='Owned project'));done=wait(lambda:terminal(first));assert done['State']==3,done;sid=done['SessionConfiguration']['SessionId'];alias=first['session_id'];assert len(sid)>128 and sid!=alias
 status=call('session_status',dict(session_id=sid));assert status['delegation_id']==first['delegation_id'] and status['native_session_id']==sid
 listing=call('sessions_list',dict(query='Owned project'));assert len(listing['sessions'])==1 and listing['sessions'][0]['native_session_id']==sid
 ok('native ID longer than128 is listed and selects the exact existing scope-owned project; managed aliases remain valid')
 stop();start();second=call('session_send',dict(request_id='two',session_id=sid,prompt='second'));assert second['session_id']==alias and second['native_session_id']==sid;done2=wait(lambda:terminal(second));assert done2['Result']=='worker:second|prior=1',done2
 loads=[e for e in events() if e['method'] in ['session/load','session/resume']];assert loads and loads[-1]['params']['sessionId']==sid and loads[-1]['params']['cwd']==str(run)
 assert len([e for e in events() if e['method']=='session/new'])==1
 ok('Host restart resumes the exact native ID and original cwd with prior history; no replacement native session is created')
 def retry(i):return call('session_send',dict(request_id='two',session_id=sid if i%2 else alias,prompt='second'))
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:again=list(pool.map(retry,range(4)))
 assert all(v['delegation_id']==second['delegation_id'] for v in again)
 call('session_send',dict(request_id='two',session_id=sid,prompt='changed'),error=True)
 assert len([e for e in events() if e['method']=='prompt' and e['text']=='second'])==1
 ok('native and managed aliases share one durable intent identity; concurrent exact retries execute once and conflicting text is rejected')
 (peer/'short-next').touch();other=call('session_start',dict(request_id='other',prompt='other'));otherdone=wait(lambda:terminal(other));othersid=otherdone['SessionConfiguration']['SessionId'];assert len(othersid)<128
 assert call('session_status',dict(session_id=othersid))['session_id']==other['session_id']
 call('session_status',dict(session_id=sid,delegation_id=other['delegation_id']),error=True)
 call('session_status',dict(session_id=sid,delegation_id=second['delegation_id']))
 for scope in ['OTHER','BOBSCOPE','SPACE','HARNESS']:
  assert not call('sessions_list',scope=scope)['sessions']
  for op,args in [('session_status',dict(session_id=sid)),('session_send',dict(request_id='foreign',session_id=sid,prompt='must-not-run')),('session_cancel',dict(session_id=sid))]:call(op,args,scope,error=True)
 for foreign in [othersid+'unknown','backend-global-session',str(run)]:call('session_send',dict(request_id='unknown-'+uuid.uuid4().hex,session_id=foreign,prompt='must-not-run'),error=True)
 assert not any(e['method']=='prompt' and e['text']=='must-not-run' for e in events())
 ok('owner, foreground conversation, workspace and harness boundaries reject foreign native IDs; both supplied identities must agree')
 slow=call('session_send',dict(request_id='slow',session_id=sid,prompt='wait'));wait(lambda:any(e['method']=='prompt' and e['text']=='wait' for e in events()))
 call('session_send',dict(request_id='busy',session_id=alias,prompt='must-not-run'),error=True)
 assert call('session_status',dict(session_id=sid))['delegation_id']==slow['delegation_id']
 assert call('sessions_list',dict(query='Owned project'))['sessions'][0]['native_session_id']==sid
 assert call('session_cancel',dict(session_id=sid))['status']=='cancelled'
 ok('native ID selects the latest busy task; ordinary send rejects and cancellation observes native cancelled completion')
 crash=call('session_send',dict(request_id='crash',session_id=sid,prompt='crash'));wait(lambda:any(e['method']=='prompt' and e['text']=='crash' for e in events()));stop();start();wait(lambda:terminal(crash))
 before=len([e for e in events() if e['method']=='prompt']);fenced=call('session_send',dict(request_id='after-crash',session_id=sid,prompt='must-not-run'));fenced_done=wait(lambda:terminal(fenced));assert fenced_done['State']==6 and 'unconfirmed' in fenced_done['Result'],fenced_done
 assert len([e for e in events() if e['method']=='prompt'])==before
 assert call('session_status',dict(session_id=alias))['delegation_id']==fenced['delegation_id']
 assert call('sessions_list',dict(query='Owned project'))['sessions'][0]['native_session_id']==sid
 ok('lost native prompt outcome remains fenced after restart; native-ID continuation never retries input or clears uncertainty')
 call('session_send',dict(request_id='too-long',session_id='x'*4097,prompt='must-not-run'),error=True)
 # Assert all known IDs still select their own exact durable conversation after the failed operations.
 assert call('session_status',dict(session_id=othersid))['session_id']==other['session_id']
 assert all(secret not in json.dumps(http_records) for secret in secrets.values())
 ok('native identity budgets are bounded and rejected operations neither switch target nor disclose credentials')
 (peer/'reuse-next').write_text(othersid);collision=call('session_start',dict(request_id='collision',prompt='collision'));collision_done=wait(lambda:terminal(collision));assert collision_done['SessionConfiguration']['SessionId']==othersid
 for op,args in [('session_status',dict(session_id=othersid)),('session_send',dict(request_id='ambiguous',session_id=othersid,prompt='must-not-run')),('session_cancel',dict(session_id=othersid))]:call(op,args,error=True)
 assert call('session_status',dict(session_id=other['session_id']))['delegation_id']==other['delegation_id']
 assert call('session_status',dict(session_id=collision['session_id']))['delegation_id']==collision['delegation_id']
 ok('a peer returning one native ID for two owned contexts is rejected as ambiguous; exact managed aliases stay distinct')
finally:
 stop();(out/'results.json').write_text(json.dumps(passed,indent=2)+'\n');(out/'http.json').write_text(json.dumps(http_records,indent=2)+'\n')
 if (peer/'events.jsonl').exists():(out/'events.json').write_text(json.dumps(events(),indent=2)+'\n')
