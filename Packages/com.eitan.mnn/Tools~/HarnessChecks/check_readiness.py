"""C# Host backend readiness through actual child processes and independent HTTP peers."""
import argparse,concurrent.futures,json,os,socket,struct,subprocess,sys,threading,time,urllib.error,urllib.request,uuid
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();s=Path(__file__).resolve().parent;out=a.output.resolve();out.mkdir(parents=True,exist_ok=True);run=out/('run-'+uuid.uuid4().hex);run.mkdir();peer=run/'peer';peer.mkdir();passed=[];records=[];remote_records=[];process=None;log=None
host_token=uuid.uuid4().hex*2;bob_token=uuid.uuid4().hex*2;remote_token=uuid.uuid4().hex*2
class Handler(BaseHTTPRequestHandler):
 def log_message(self,*args):pass
 def do_GET(self):
  remote_records.append(dict(method='GET',path=self.path))
  if self.headers.get('Authorization')!='Bearer '+remote_token:self.send_response(401);self.end_headers();return
  if self.path=='/v1/health':value=dict(ProtocolVersion=1,OwnerId='remote-owner')
  elif self.path=='/v1/harnesses':value=dict(configured=[dict(id='remote',capabilities=23)],workspaces=['project'],installations=[])
  elif self.path=='/card':value=dict(name='independent-A2A',supportedInterfaces=[dict(protocolVersion='1.0',protocolBinding='JSONRPC',url=remote_url+'/rpc')])
  elif self.path=='/foreign-card':value=dict(name='foreign',supportedInterfaces=[dict(protocolVersion='1.0',protocolBinding='JSONRPC',url='https://ungranted.invalid/rpc')])
  else:self.send_response(404);self.end_headers();return
  b=json.dumps(value).encode();self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(b)));self.end_headers();self.wfile.write(b)
 def do_POST(self):remote_records.append(dict(method='POST',path=self.path));self.send_response(500);self.end_headers()
remote=ThreadingHTTPServer(('127.0.0.1',0),Handler);remote_url='http://127.0.0.1:'+str(remote.server_address[1]);threading.Thread(target=remote.serve_forever,daemon=True).start()
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
url='http://127.0.0.1:'+str(port);env=dict(os.environ,READY_HOST=host_token,READY_BOB=bob_token,READY_REMOTE=remote_token,READINESS_PEER=str(peer));fixtures=[dict(Id=m,Protocol='msp' if m=='msp' else 'acp',Command=sys.executable,Arguments=['-u',str(s/'Fixtures/readiness_fixture.py'),m],ForwardEnvironment=['READINESS_PEER'],TimeoutMilliseconds=0,**(dict(AcpMcpServers=[dict(Name='external',Type='http',Url=remote_url+'/mcp',Headers=[dict(Name='Authorization',ValueEnvironmentVariable='READY_REMOTE',Prefix='Bearer ')])]) if m=='no-http' else {})) for m in ['normal','invalid','exit','hang','hold','no-http','msp']]
fixtures += [dict(Id='http',Protocol='http',Endpoint=remote_url,RemoteHarnessId='remote',RemoteWorkspace='project',TokenEnvironmentVariable='READY_REMOTE',RemoteCapabilities=23),dict(Id='http-missing',Protocol='http',Endpoint=remote_url,RemoteHarnessId='missing',RemoteWorkspace='project',TokenEnvironmentVariable='READY_REMOTE',RemoteCapabilities=23),dict(Id='a2a',Protocol='a2a',Endpoint=remote_url,AgentCardPath='/card',TokenEnvironmentVariable='READY_REMOTE'),dict(Id='a2a-foreign',Protocol='a2a',Endpoint=remote_url,AgentCardPath='/foreign-card',TokenEnvironmentVariable='READY_REMOTE'),dict(Id='llm',Protocol='llm',Endpoint=remote_url,Model='fixture',TokenEnvironmentVariable='READY_REMOTE')]
cfg=run/'host.json';cfg.write_text(json.dumps(dict(Listen=url,DataDirectory=str(run/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='READY_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='READY_BOB')],Workspaces=dict(project=str(run),other=str(peer)),Harnesses=fixtures)))
def http(path,body=None,status=200,token=None):
 req=urllib.request.Request(url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(token or host_token),'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=20) as r:code,raw=r.status,r.read()
 except urllib.error.HTTPError as e:code,raw=e.code,e.read()
 records.append(dict(path=path,input=body,status=code,output=raw.decode()));assert code==status,('Explicit backend protocol readiness is unavailable',path,code,raw)
 assert all(t.encode() not in raw for t in [host_token,bob_token,remote_token]);return json.loads(raw) if raw else None
path=lambda id:'/v1/harnesses/'+id+'/readiness'
def read(id='normal',workspace='project',**kw):return http(path(id)+'?workspace='+workspace,**kw)
def probe(id='normal',workspace='project',**kw):return http(path(id),dict(WorkspaceKey=workspace),**kw)
def events():return [json.loads(l) for l in (peer/'events.jsonl').read_text().splitlines()] if (peer/'events.jsonl').exists() else []
def wait(fn):
 end=time.monotonic()+25
 while time.monotonic()<end:
  v=fn()
  if v:return v
  time.sleep(.03)
 raise AssertionError('Readiness transition was not observed')
def ok(v):passed.append(v);print('PASS '+v,flush=True)
def start():
 global process,log
 log=(run/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--config',str(cfg)],env=env,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None,Path(log.name).read_text()
  try:return http('/v1/harnesses')
  except urllib.error.URLError:return None
 wait(ready)
def stop():
 global process
 if process and process.poll() is None:process.kill();process.wait(timeout=10);log.close()
 process=None
try:
 start();assert read()['State']=='unknown';assert not events();assert not http('/v1/tasks')
 ok('catalog/Host health and cached readiness reads never start a backend or task; unchecked is explicit')
 checked=probe();assert checked['State']=='protocol_verified' and checked['Evidence']=='acp_initialize' and checked['CheckedUtcTicks']>0,checked
 c=checked['AcpCapabilities'];assert c['LoadSession'] and c['ResumeSession'] and c['ListSessions'] and c['Images'] and not c['Audio'] and c['EmbeddedContext'] and c['McpHttp'] and not c['McpSse'] and c['Steering'],c
 assert read()['State']=='protocol_verified';assert read(token=bob_token)['State']=='unknown';assert read(workspace='other')['State']=='unknown'
 assert all(e.get('method')=='initialize' for e in events())
 ok('explicit ACP initialize records observed capabilities while native sessions, prompts and other owner/workspace snapshots remain untouched')
 for id in ['invalid','exit','no-http']:
  r=probe(id);assert r['State']=='unavailable' and r['AcpCapabilities'] is None,r
 assert all(e.get('method')=='initialize' for e in events())
 ok('invalid protocol, backend process exit and missing required external MCP transport fail honestly before any native session')
 r=probe('msp');assert r['State']=='protocol_verified' and r['Evidence']=='msp_initialize',r
 assert [e['method'] for e in events() if e['mode']=='msp']==['initialize','initialized']
 ok('official MSP initialization/initialized confirms the native schema contract without creating a session or turn')
 assert probe('http')['State']=='protocol_verified';assert probe('http-missing')['State']=='unavailable'
 assert probe('a2a')['Evidence']=='a2a_agent_card';assert probe('a2a-foreign')['State']=='unavailable'
 assert all(e['method']=='GET' for e in remote_records)
 ok('HTTP checks authenticated Host version and exact remote aliases; A2A checks a supported granted card binding without sending work')
 n=len(remote_records);r=probe('llm');assert r['State']=='unsupported' and len(remote_records)==n
 ok('LLM has no universal non-inference probe and is explicitly unsupported; no fabricated model/inference success')
 with concurrent.futures.ThreadPoolExecutor() as pool:
  future=pool.submit(probe,'hold');wait(lambda:read('hold')['State']=='checking');probe('hold',status=409);(peer/'release-hold').touch();assert future.result()['State']=='protocol_verified'
 assert len([e for e in events() if e['mode']=='hold'])==1
 ok('one writer per owner/harness/workspace exposes checking and rejects concurrent probe admission instead of spawning duplicates')
 (peer/'release-hold').unlink()
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
  held=[pool.submit(probe,'hold',w,token=t) for w,t in [('project',host_token),('project',bob_token),('other',host_token),('other',bob_token)]]
  wait(lambda:all(read('hold',w,token=t)['State']=='checking' for w,t in [('project',host_token),('project',bob_token),('other',host_token),('other',bob_token)]))
  n=len([e for e in events() if e['mode']=='normal']);probe('normal',status=409);assert len([e for e in events() if e['mode']=='normal'])==n
  (peer/'release-hold').touch();assert all(f.result()['State']=='protocol_verified' for f in held)
 ok('four independent owner/workspace checks fill the global bound; a fifth check is rejected before starting another backend')
 (peer/'release-hold').unlink()
 body=json.dumps(dict(WorkspaceKey='project')).encode()
 sock=socket.create_connection(('127.0.0.1',port));head=('POST '+path('hold')+' HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer '+host_token+'\r\nContent-Type: application/json\r\nContent-Length: '+str(len(body))+'\r\n\r\n').encode();sock.sendall(head+body)
 wait(lambda:read('hold')['State']=='checking');sock.setsockopt(socket.SOL_SOCKET,socket.SO_LINGER,struct.pack('ii',1,0));sock.close();begin=time.monotonic();wait(lambda:read('hold')['State']=='unavailable');assert time.monotonic()-begin<5
 (peer/'release-hold').touch();assert probe('hold')['State']=='protocol_verified'
 ok('disconnect cancels only its pending check, records unavailable and releases admission for an explicit retry')
 begin=time.monotonic();r=probe('hang');assert r['State']=='unavailable' and time.monotonic()-begin<15
 assert read('hang')['State']=='unavailable'
 ok('an unresponsive native initialization is bounded and leaves an observed unavailable state')
 before=len(events());read('missing',status=404);probe('missing',status=404);probe(workspace='missing',status=400);read(token='wrong',status=401)
 assert len(events())==before
 ok('authentication and exact configured harness/workspace validation precede all process or network effects')
 # Real task execution is independent of protocol probes; preserve its native index bytes and task revision.
 t=http('/v1/tasks',dict(ConversationId='foreground',SubmissionKey='live-task',HarnessId='normal',WorkspaceKey='project',Objective='wait'),202)
 wait(lambda:any(e.get('method')=='session/prompt' for e in events()));old=http('/v1/tasks/'+t['Id']);files={p.name:p.read_bytes() for p in (run/'data').glob('acp-sessions-*.json')}
 assert probe()['State']=='protocol_verified';after=http('/v1/tasks/'+t['Id']);assert after['Revision']==old['Revision'] and after['State']==1
 assert {p.name:p.read_bytes() for p in (run/'data').glob('acp-sessions-*.json')}==files
 assert http('/v1/tasks/'+t['Id']+'/cancel',{})['State']==5
 ok('a separate readiness connection neither changes an active task nor mutates native ownership/uncertainty indexes; explicit task cancellation still works')
 stop();start();assert read()['State']=='unknown'
 ok('Host restart clears transient protocol evidence; previous checks never imply current service or model readiness')
finally:
 stop();remote.shutdown();remote.server_close();(out/'results.json').write_text(json.dumps(passed,indent=2)+'\n');(out/'http.json').write_text(json.dumps(records,indent=2)+'\n');(out/'events.json').write_text(json.dumps(events(),indent=2)+'\n');(out/'remote-http.json').write_text(json.dumps(remote_records,indent=2)+'\n')
