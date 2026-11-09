import argparse,json,os,socket,subprocess,time,urllib.request,urllib.error,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();passed=[];http_records=[];process=None;log=None
fixture=Path(__file__).with_name('Fixtures')/'acp_list_fixture.py';tokens={k:uuid.uuid4().hex*2 for k in ['LIST_HOST','LIST_OTHER']};env=dict(os.environ,**tokens,SIRIAI_LIST_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port)
config=dict(Listen=URL,DataDirectory=str(RUN/'data'),Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='LIST_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='LIST_OTHER')],Workspaces=dict(test=str(RUN),other=str(PEER)),Harnesses=[dict(Id=v,Protocol='acp',Command='python3',Arguments=['-u',str(fixture),v],ForwardEnvironment=['SIRIAI_LIST_ROOT'],TimeoutMilliseconds=0) for v in ['normal','unsupported','wrong-cwd','malformed','loop','slow','oversize','wide']]);CONFIG=RUN/'host.json';CONFIG.write_text(json.dumps(config))
def rows(name):
 p=PEER/name;return [json.loads(x) for x in p.read_text().splitlines()] if p.exists() else []
def http(path,body=None,status=200,token=None):
 req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(token or tokens['LIST_HOST']),'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=25) as response:code,data=response.status,response.read()
 except urllib.error.HTTPError as e:code,data=e.code,e.read()
 http_records.append(dict(path=path,status=code,expected=status,body=data.decode(),owner='other' if token else 'primary'))
 assert code==status,('Owned native listing unavailable or unexpected status',path,code,data);return json.loads(data) if data else None
def wait(fn,seconds=20):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  v=fn()
  if v:return v
  time.sleep(.02)
 raise AssertionError('Timed out observing native listing')
def start():
 global process,log
 log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--config',str(CONFIG)],env=env,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None,Path(log.name).read_text()
  try:return http('/v1/harnesses')
  except urllib.error.URLError:return None
 wait(ready)
def stop():
 global process
 if process:
  process.terminate()
  try:process.wait(timeout=10)
  except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
  process=None;log.close()
def submit(harness='normal',conversation=None,token=None,workspace='test'):
 item=http('/v1/tasks',dict(ConversationId=conversation or uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId=harness,WorkspaceKey=workspace,Objective='do one task'),202,token)
 def done():
  v=http('/v1/tasks/'+item['Id'],token=token);return v if v['State'] in [3,4,5,6] else None
 return wait(done)
def path(item,cursor=None):return '/v1/tasks/'+item['Id']+'/native-sessions'+(('?cursor='+urllib.parse.quote(cursor,safe='')) if cursor else '')
def page(item,cursor=None,**kw):return http(path(item,cursor),**kw)
def ok(msg):passed.append(msg);print('PASS '+msg,flush=True)
def sid(item):return item['SessionConfiguration']['SessionId']
try:
 start();first=submit(conversation='owned');before=http('/v1/tasks/'+first['Id']);counts={n:len(rows(n)) for n in ['new.jsonl','loads.jsonl','prompts.jsonl']}
 one=page(first);assert one['Supported'] and one['Sessions']==[] and one['NextCursor'] and not one['Uncertain'],one
 two=page(first,one['NextCursor']);assert len(two['Sessions'])==1 and two['Sessions'][0]['SessionId']==sid(first) and two['Sessions'][0]['Cwd']==str(RUN) and not two.get('NextCursor'),two
 assert 'OTHER OWNER SECRET' not in json.dumps([one,two])
 assert all(len(rows(n))==c for n,c in counts.items())
 assert http('/v1/tasks/'+first['Id'])==before
 ok('real session/list paginates exact owned native context; no new/load/resume/prompt, task/outbox mutations or foreign metadata')
 foreign=submit(token=tokens['LIST_OTHER']);different=submit(conversation='other-conversation');workspace=submit(workspace='other')
 for item,token in [(foreign,tokens['LIST_OTHER']),(different,None),(workspace,None)]:
  v=page(item,token=token);v=page(item,v['NextCursor'],token=token);assert [s['SessionId'] for s in v['Sessions']]==[sid(item)]
 page(first,status=404,token=tokens['LIST_OTHER'])
 for item,token in [(foreign,tokens['LIST_OTHER']),(different,None),(workspace,None)]:page(item,one['NextCursor'],status=400,token=token)
 page(first,one['NextCursor'][:-1]+('A' if one['NextCursor'][-1]!='A' else 'B'),status=400)
 page(first,'x'*20000,status=400)
 ok('owner/conversation/workspace authorization and authenticated task-scoped cursor reject reuse, tampering and oversized input')
 unsupported=submit('unsupported');count=len(rows('lists.jsonl'));v=page(unsupported);assert not v['Supported'] and v['Sessions']==[] and len(rows('lists.jsonl'))==count
 ok('missing native capability is explicit and sends no invented session/list or replacement session')
 wrong=submit('wrong-cwd');v=page(wrong);v=page(wrong,v['NextCursor']);assert v['Sessions']==[]
 ok('matching session ID from a different native cwd is not exposed as the owned workspace')
 for h in ['malformed','oversize']:
  item=submit(h);v=page(item,status=500) if h=='malformed' else page(item)
  if h=='oversize':page(item,v['NextCursor'],status=500)
 loop=submit('loop');v=page(loop);page(loop,v['NextCursor'],status=500)
 ok('invalid arrays, excessive native pages and repeated cursors fail boundedly without task execution')
 wide=submit('wide');v=page(wide);assert len(v['NextCursor'])>4000;v=page(wide,v['NextCursor']);assert v['Sessions'][0]['SessionId']==sid(wide)
 ok('large signed native cursor survives GET transport and remains scoped to the original task')
 # Cancel two actual HTTP reads while isolated native list processes are waiting.
 slow=submit('slow');count=len(rows('lists.jsonl'))
 from http.client import HTTPConnection
 clients=[HTTPConnection('127.0.0.1',port) for _ in range(2)]
 for client in clients:client.request('GET',path(slow),headers={'Authorization':'Bearer '+tokens['LIST_HOST']})
 wait(lambda:len(rows('lists.jsonl'))>=count+2);pids=[r['pid'] for r in rows('lists.jsonl')[-2:]]
 page(slow,status=409)
 for client in clients:client.close()
 def gone():
  for pid in pids:
   try:os.kill(pid,0);return False
   except ProcessLookupError:pass
  return True
 wait(gone,10);assert http('/v1/tasks/'+slow['Id'])['State']==3
 ok('two-probe limit rejects excess reads; abort terminates only probe processes and preserves durable task state')
 count=len(rows('prompts.jsonl'));again=submit(conversation='owned');assert sid(again)==sid(first) and len(rows('prompts.jsonl'))==count+1 and again['Result']=='new-task-result'
 ok('subsequent original-context task executes once and receives only its fresh output')
 # Persist uncertainty explicitly, as an interrupted durable native operation would.
 stop()
 # Locate exact index by the owned native identity, independent of its implementation filename.
 index=next(p for p in (RUN/'data').glob('acp-sessions-*.json') if sid(first) in p.read_text());data=json.loads(index.read_text());key=next(k for k,v in data['Sessions'].items() if v['SessionId']==sid(first));data['Sessions'][key]['Uncertain']=True;index.write_text(json.dumps(data));start()
 page(first,one['NextCursor'],status=400)
 v=page(first);v=page(first,v['NextCursor']);assert v['Uncertain'] and v['Sessions'][0]['SessionId']==sid(first)
 count=len(rows('prompts.jsonl'));blocked=submit(conversation='owned');assert blocked['State']==6 and len(rows('prompts.jsonl'))==count
 ok('restart expires process-local cursors, lists the exact persisted uncertain session and never clears its execution fence')
 # Reading never consumes the pending result.
 claimed=http('/v1/results/claim',dict(ConversationId='owned',ClientId='listing-check',LeaseSeconds=60));assert claimed['Id']==first['Id']
 ok('original result remains claimable after listing; no false presentation receipt')
finally:
 stop()
 for name,data in [('results',passed),('http',http_records),('wire',rows('wire.jsonl')),('events',{n:rows(n) for n in ['new.jsonl','loads.jsonl','prompts.jsonl','lists.jsonl']})]: (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
