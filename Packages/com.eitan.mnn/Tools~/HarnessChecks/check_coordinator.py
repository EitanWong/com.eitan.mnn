#!/usr/bin/env python3
"""Native Agent -> C# MCP coordinator -> durable child Harness integration."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse,concurrent.futures,json,os,socket,subprocess,sys,time,urllib.error,urllib.request,uuid
from pathlib import Path
SAMPLE=PACKAGE_ROOT;p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir()
secrets={name:uuid.uuid4().hex*2 for name in ['SIRIAI_COORD_TOKEN','SIRIAI_COORD_OTHER','SIRIAI_COORD_SCOPE','SIRIAI_COORD_SCOPE_OTHER']};env=dict(os.environ,**secrets,SIRIAI_COORD_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port);MCP=URL+'/mcp/coordinator';fixture=SAMPLE/'Tools~/HarnessChecks/Fixtures/coordinator_fixture.py';config=dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_COORD_TOKEN'),dict(OwnerId='bob',TokenEnvironmentVariable='SIRIAI_COORD_OTHER')],Workspaces=dict(test=str(RUN)),CoordinatorMcpScopes=[dict(Id='front',OwnerId='alice',ConversationId='phone',WorkspaceKey='test',HarnessId='worker',TokenEnvironmentVariable='SIRIAI_COORD_SCOPE'),dict(Id='other',OwnerId='bob',ConversationId='other-phone',WorkspaceKey='test',HarnessId='worker',TokenEnvironmentVariable='SIRIAI_COORD_SCOPE_OTHER')],Harnesses=[dict(Id=mode,Protocol='acp',Command=sys.executable,Arguments=['-u',str(fixture),mode],ForwardEnvironment=['SIRIAI_COORD_ROOT'],AcpMcpServers=[] if mode=='worker' else [dict(Name='siriai-coordinator',Type='http',Url=MCP,Headers=[dict(Name='Authorization',ValueEnvironmentVariable='SIRIAI_COORD_SCOPE',Prefix='Bearer ')])]) for mode in ['coordinator','worker']]);CONFIG=RUN/'host.json';CONFIG.write_text(json.dumps(config));REG=RUN/'registration';records=[];passed=[];process=None;log=None;wire=[]
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def wait(fn,timeout=30):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  value=fn()
  if value:return value
  time.sleep(.03)
 raise AssertionError('Timed out waiting for coordinator behavior')
def http(path,body=None,token=secrets['SIRIAI_COORD_TOKEN'],status=200,extra={}):
 headers={'Authorization':'Bearer '+token,'Content-Type':'application/json','Accept':'application/json,text/event-stream',**extra};req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers=headers)
 try:
  with urllib.request.urlopen(req,timeout=15) as response:code,raw=response.status,response.read()
 except urllib.error.HTTPError as e:code,raw=e.code,e.read()
 assert code==status,(path,code,raw);assert all(secret.encode() not in raw for secret in secrets.values());return json.loads(raw) if raw else None
def mcp(name,args={},token=secrets['SIRIAI_COORD_SCOPE'],error=False):
 frame=dict(jsonrpc='2.0',id=uuid.uuid4().hex,method='tools/call',params=dict(name=name,arguments=args));reply=http('/mcp/coordinator',frame,token);wire.extend([dict(direction='client',frame=frame),dict(direction='server',frame=reply)]);result=reply['result'];assert result.get('isError',False)==error,result;return json.loads(result['content'][0]['text'])
def prepare():
 cmd=['dotnet',str(a.host),'--service','prepare','--id','coordinator','--config',str(CONFIG),'--runtime',str(Path('/opt/homebrew/bin/dotnet').resolve()),'--directory',str(REG)];r=subprocess.run(cmd,text=True,capture_output=True,env=env,timeout=30);records.append(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));assert r.returncode==0,records[-1]
def start():
 global process,log
 log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--service','run','--directory',str(REG)],env=os.environ,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None,'Host exited'
  try:return http('/v1/harnesses')
  except urllib.error.URLError:return None
 return wait(ready)
def stop(kill=False):
 global process
 if process:
  if process.poll() is None:process.kill() if kill else process.terminate()
  try:process.wait(timeout=10)
  except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
  process=None;log.close()
def native(operation):
 task=http('/v1/tasks',dict(ConversationId='coordinator-chat',SubmissionKey=uuid.uuid4().hex,HarnessId='coordinator',WorkspaceKey='test',Objective=json.dumps(operation)),status=202)
 value=wait(lambda:terminal(task));assert value['State']==3,value;result=json.loads(value['Result']);assert not result.get('isError',False),result;return json.loads(result['content'][0]['text'])
def terminal(task):
 value=http('/v1/tasks/'+task['Id']);return value if value['State'] in [3,4,5,6] else None
try:
 prepare();start();init=http('/mcp/coordinator',dict(jsonrpc='2.0',id='init',method='initialize',params=dict(protocolVersion='2025-06-18',capabilities={},clientInfo=dict(name='independent-checker',version='1'))),secrets['SIRIAI_COORD_SCOPE']);assert init['result']['serverInfo']['name']=='siriai-coordinator'
 first=native(dict(op='start',request_id='start-one',prompt='first',title='First child'));assert first['status']=='queued',first;child=wait(lambda:terminal(dict(Id=first['delegation_id'])));assert child['Result']=='worker:first|prior=0',child
 assert child['ConversationId']==first['session_id'] and child['ResultConversationId']=='phone' and child['Title']=='First child'
 ok('native ACP coordinator starts durable child asynchronously in the original workspace with independent execution session and foreground result destination')
 retry=mcp('siriai_session_start',dict(request_id='start-one',prompt='first',title='First child'));assert retry['delegation_id']==first['delegation_id']
 mcp('siriai_session_start',dict(request_id='start-one',prompt='different'),error=True)
 ok('explicit intent IDs deduplicate exact retries and reject conflicting reuse without duplicate tool execution')
 stop();start();status=mcp('siriai_session_status',dict(session_id=first['session_id']));assert status['delegation_id']==first['delegation_id'] and status['status']=='completed',status
 second=native(dict(op='send',request_id='send-one',session_id=first['session_id'],prompt='second'));child2=wait(lambda:terminal(dict(Id=second['delegation_id'])));assert child2['Result']=='worker:second|prior=1',child2
 ok('Host restart retains delegated identity; send queues a new task into the same original native context and keeps prior history')
 other=native(dict(op='start',request_id='start-two',prompt='independent'));third=wait(lambda:terminal(dict(Id=other['delegation_id'])));assert third['Result']=='worker:independent|prior=0' and other['session_id']!=first['session_id']
 listing=mcp('siriai_sessions_list',dict(limit=100));assert len(listing['sessions'])==2 and any(s['title']=='First child' for s in listing['sessions'])
 ok('independent delegated sessions keep separate native contexts while session listing returns durable titles and latest tasks')
 with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:parallel=list(pool.map(lambda _:mcp('siriai_session_start',dict(request_id='parallel',prompt='parallel')),range(4)))
 assert len({v['delegation_id'] for v in parallel})==1;wait(lambda:terminal(dict(Id=parallel[0]['delegation_id'])))
 body=json.dumps(dict(jsonrpc='2.0',id='lost-reply',method='tools/call',params=dict(name='siriai_session_start',arguments=dict(request_id='lost-reply',prompt='lost-reply',title='Lost reply')))).encode()
 with socket.create_connection(('127.0.0.1',port)) as sock:
  head=('POST /mcp/coordinator HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer '+secrets['SIRIAI_COORD_SCOPE']+'\r\nContent-Type: application/json\r\nContent-Length: '+str(len(body))+'\r\nConnection: close\r\n\r\n').encode();sock.sendall(head+body)
  # Observe the first response byte, then discard its body: admission has happened, acknowledgment is lost to the caller.
  assert sock.recv(1)
 lost=wait(lambda:mcp('siriai_sessions_list',dict(query='Lost reply'))['sessions']);again=mcp('siriai_session_start',dict(request_id='lost-reply',prompt='lost-reply',title='Lost reply'));assert again['delegation_id']==lost[0]['delegation_id'];wait(lambda:terminal(dict(Id=again['delegation_id'])))
 assert len([s for s in (PEER/'events.jsonl').read_text().splitlines() if json.loads(s).get('kind')=='worker-start' and json.loads(s).get('objective') in ['parallel','lost-reply']])==2
 ok('concurrent identical submissions and a discarded admitted HTTP response each execute exactly one child intent')
 slow=mcp('siriai_session_start',dict(request_id='slow-one',prompt='wait'));wait(lambda:any(json.loads(s).get('objective')=='wait' for s in (PEER/'events.jsonl').read_text().splitlines()))
 queued=mcp('siriai_session_start',dict(request_id='queued-cancel',prompt='must-not-run'));assert queued['status']=='queued';assert mcp('siriai_session_cancel',dict(delegation_id=queued['delegation_id']))['status']=='cancelled'
 cancelled=mcp('siriai_session_cancel',dict(delegation_id=slow['delegation_id']));assert cancelled['status']=='cancelled',cancelled
 ok('scoped cancellation waits for the actual native cancelled prompt result')
 assert not any(json.loads(s).get('objective')=='must-not-run' for s in (PEER/'events.jsonl').read_text().splitlines())
 for args in [dict(session_id=first['session_id']),dict(delegation_id=first['delegation_id'])]:mcp('siriai_session_status',args,token=secrets['SIRIAI_COORD_SCOPE_OTHER'],error=True)
 mcp('siriai_session_start',dict(request_id='override',prompt='bad',owner_id='bob',workspace='elsewhere'),error=True)
 http('/v1/tasks',token=secrets['SIRIAI_COORD_SCOPE'],status=401);http('/mcp/coordinator',dict(jsonrpc='2.0',id=1,method='ping'),secrets['SIRIAI_COORD_TOKEN'],status=404)
 http('/mcp/coordinator',dict(jsonrpc='2.0',id=1,method='ping'),secrets['SIRIAI_COORD_SCOPE'],status=403,extra={'Origin':'https://evil.invalid'})
 ok('dedicated scope credentials cannot access public task APIs, another owner/session, override execution scope or use an untrusted Origin')
 for label,change in [('duplicate',lambda c:c['CoordinatorMcpScopes'].append(c['CoordinatorMcpScopes'][0])),('unknown-owner',lambda c:c['CoordinatorMcpScopes'][0].update(OwnerId='unknown')),('unknown-workspace',lambda c:c['CoordinatorMcpScopes'][0].update(WorkspaceKey='unknown')),('unknown-harness',lambda c:c['CoordinatorMcpScopes'][0].update(HarnessId='unknown')),('host-credential',lambda c:c['CoordinatorMcpScopes'][0].update(TokenEnvironmentVariable='SIRIAI_COORD_TOKEN')),('duplicate-credential',lambda c:c['CoordinatorMcpScopes'][0].update(TokenEnvironmentVariable='SIRIAI_COORD_SCOPE_OTHER')),('missing-credential',lambda c:c['CoordinatorMcpScopes'][0].update(TokenEnvironmentVariable='SIRIAI_COORD_MISSING')),('unknown-field',lambda c:c['CoordinatorMcpScopes'][0].update(Unrecognized=True))]:
  bad=json.loads(json.dumps(config));change(bad);path=RUN/('invalid-'+label+'.json');path.write_text(json.dumps(bad));cmd=['dotnet',str(a.host),'--service','prepare','--id','invalid-scope','--config',str(path),'--runtime',str(Path('/opt/homebrew/bin/dotnet').resolve()),'--directory',str(RUN/('invalid-'+label))];r=subprocess.run(cmd,env=env,text=True,capture_output=True,timeout=30);records.append(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));assert r.returncode==2 and all(v not in r.stdout+r.stderr for v in secrets.values()),records[-1]
 ok('eight invalid grant configurations fail preparation before any delegated work can execute')
 parent=http('/v1/tasks',dict(ConversationId='crash-coordinator',SubmissionKey='crash-parent',HarnessId='coordinator',WorkspaceKey='test',Objective=json.dumps(dict(op='start',request_id='queued-before-crash',prompt='queued-after-restart',hold_after_admission=True))),status=202)
 wait(lambda:(PEER/'held-admission.json').exists());admitted=json.loads((PEER/'held-admission.json').read_text());queued=json.loads(admitted['content'][0]['text']);assert http('/v1/tasks/'+queued['delegation_id'])['State']==0
 stop(kill=True);start();restored=wait(lambda:terminal(dict(Id=queued['delegation_id'])));assert restored['State']==3 and restored['Result']=='worker:queued-after-restart|prior=0',restored
 retry=mcp('siriai_session_start',dict(request_id='queued-before-crash',prompt='queued-after-restart'));assert retry['delegation_id']==queued['delegation_id'];assert wait(lambda:terminal(parent))['State']==6
 ok('admitted but unstarted child survives Host crash and executes once; the uncertain parent prompt is interrupted without replay')
 stored=json.loads((REG/'credentials.json').read_text());assert all(stored[k]==v for k,v in secrets.items())
 for file in [REG/'host.json',*(RUN/'data').glob('*.json')]:assert all(v.encode() not in file.read_bytes() for v in secrets.values()),file
 assert not list(PEER.glob('errors-*.txt'))
 (OUT/'client-input.json').write_text(json.dumps(dict(url=URL,token=secrets['SIRIAI_COORD_TOKEN'],conversation='phone',tasks=[first['delegation_id'],second['delegation_id'],other['delegation_id']],data=str(RUN/'data'))))
 ok('private service credentials reopen without parent environment and public/durable records contain no Host or coordinator tokens')
finally:
 stop();agentwire=[json.loads(s) for s in (PEER/'wire.jsonl').read_text().splitlines()] if (PEER/'wire.jsonl').exists() else [];nativewire=[json.loads(s) for s in (PEER/'mcp-wire.jsonl').read_text().splitlines()] if (PEER/'mcp-wire.jsonl').exists() else []
 (OUT/'results.json').write_text(json.dumps(passed,indent=2)+'\n');(OUT/'commands.json').write_text(json.dumps(records,indent=2)+'\n');(OUT/'wire.json').write_text(json.dumps(agentwire,indent=2)+'\n');(OUT/'mcp-wire.json').write_text(json.dumps(wire+nativewire,indent=2)+'\n')
