import argparse,json,os,socket,subprocess,time,urllib.request,urllib.error,uuid,concurrent.futures
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();passed=[];records=[];process=None;log=None
fixture=Path(__file__).with_name('Fixtures')/'acp_history_fixture.py';tokens={k:uuid.uuid4().hex*2 for k in ['HISTORY_HOST','HISTORY_OTHER']};env=dict(os.environ,**tokens,SIRIAI_HISTORY_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port);CONFIG=RUN/'host.json';CONFIG.write_text(json.dumps(dict(Listen=URL,DataDirectory=str(RUN/'data'),Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='HISTORY_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='HISTORY_OTHER')],Workspaces=dict(test=str(RUN),other=str(PEER)),MaximumConcurrent=4,Harnesses=[dict(Id=v,Protocol='acp',Command='python3',Arguments=['-u',str(fixture),v],ForwardEnvironment=['SIRIAI_HISTORY_ROOT'],TimeoutMilliseconds=0) for v in ['normal','resume-only','bulk','oversized','empty','slow','disconnect','callbacks','bad-update']])))
def rows(name):
 p=PEER/name;return [json.loads(x) for x in p.read_text().splitlines()] if p.exists() else []
def http(path,body=None,status=200,token=None,method=None):
 req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),method=method,headers={'Authorization':'Bearer '+(token or tokens['HISTORY_HOST']),'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=25) as response:code,data=response.status,response.read()
 except urllib.error.HTTPError as e:code,data=e.code,e.read()
 records.append(dict(path=path,method=req.get_method(),status=code,expected=status,body=data.decode()))
 assert code==status,('Owned native history unavailable or unexpected status',path,code,data);return json.loads(data) if data else None
def wait(fn,seconds=20):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  v=fn()
  if v:return v
  time.sleep(.02)
 raise AssertionError('Timed out observing native history')
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
def submit(harness='normal',conversation=None,text='original',token=None,workspace='test'):
 return http('/v1/tasks',dict(ConversationId=conversation or uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId=harness,WorkspaceKey=workspace,Objective=text),202,token)
def done(item,token=None):
 v=http('/v1/tasks/'+item['Id'],token=token);return v if v['State'] in [3,4,5,6] else None
def complete(**kw):return wait(lambda:done(item,kw.get('token'))) if (item:=submit(**kw)) else None
def sid(item):return item['SessionConfiguration']['SessionId']
def path(item):return '/v1/tasks/'+item['Id']+'/native-history/load'
def history(item,**kw):return http(path(item),method='POST',**kw)
def ok(msg):passed.append(msg);print('PASS '+msg,flush=True)
try:
 start();first=complete(conversation='owned');before=http('/v1/tasks/'+first['Id']);counts={n:len(rows(n)) for n in ['new.jsonl','resumes.jsonl','prompts.jsonl']}
 value=history(first);assert value['Supported'] and value['SessionId']==sid(first) and not value['Uncertain'] and not value['Truncated'],value
 entries=value['Entries'];assert [e['Kind'] for e in entries]==['user_message_chunk','agent_message_chunk','plan','tool_call','tool_call_update','agent_message_chunk'],entries
 assert entries[0]['Text']=='original' and entries[1]['Text']=='fresh:original' and json.loads(entries[-1]['Payload'])['content']['type']=='image'
 assert 'FOREIGN SECRET' not in json.dumps(value) and 'AFTER LOAD RESPONSE' not in json.dumps(value)
 assert all(len(rows(n))==c for n,c in counts.items());assert http('/v1/tasks/'+first['Id'])==before
 assert rows('loads.jsonl')[-1]['params']==dict(sessionId=sid(first),cwd=str(RUN),mcpServers=[])
 ok('explicit official session/load returns owned user/assistant/tool/plan/multimodal replay, closes at response, and sends no new/resume/prompt/config or outbox mutations')
 foreign=complete(token=tokens['HISTORY_OTHER']);other=complete(conversation='other');workspace=complete(workspace='other')
 for item,token in [(foreign,tokens['HISTORY_OTHER']),(other,None),(workspace,None)]:
  v=history(item,token=token);assert v['SessionId']==sid(item) and all(e['Text']!='FOREIGN SECRET' for e in v['Entries'])
 history(first,status=404,token=tokens['HISTORY_OTHER']);http(path(first),status=405)
 ok('task-derived owner/conversation/workspace/session identity gates explicit POST; no caller-supplied foreign session imports')
 unsupported=complete(harness='resume-only');count=len(rows('loads.jsonl'));v=history(unsupported);assert not v['Supported'] and v['Entries']==[] and len(rows('loads.jsonl'))==count
 ok('resume-only backend reports history unsupported and never substitutes resume, load or prompt')
 for h in ['bulk','oversized']:
  item=complete(harness=h);v=history(item);assert v['Truncated'] and len(v['Entries'])<=256 and len(json.dumps(v).encode())<900000
  assert [e['Sequence'] for e in v['Entries']]==sorted(e['Sequence'] for e in v['Entries'])
  if h=='bulk':assert 'entry399 ' in v['Entries'][-1]['Text'] and v['Entries'][0]['Sequence']>1
  else:assert v['Entries'][-1]['Kind']=='agent_message_chunk'
 empty=complete(harness='empty');assert history(empty)['Entries']==[]
 ok('bounded recent replay preserves order and exact payloads, reports eviction/oversize truthfully and accepts empty history')
 # An in-flight task must not have its native session reloaded.
 active=submit(conversation='owned',text='hold');wait(lambda:any(r['text']=='hold' and r['sid']==sid(first) for r in rows('prompts.jsonl')));count=len(rows('loads.jsonl'));history(first,status=409);assert len(rows('loads.jsonl'))==count;wait(lambda:done(active))
 ok('active original context rejects historical reload before a native load is sent')
 slow=complete(harness='slow',conversation='slow-owned');count=len(rows('loads.jsonl'))
 with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
  read=pool.submit(history,slow);wait(lambda:len(rows('loads.jsonl'))>count);queued=submit(harness='slow',conversation='slow-owned',text='new work');v=read.result();new=wait(lambda:done(queued));assert new['State']==3 and new['Result']=='fresh:new work' and sid(new)==sid(slow)
  assert 'fresh:new work' not in json.dumps(v)
 ok('new work waits for the same-context history lease and executes once after load; replay never contaminates the new result')
 callback=complete(harness='callbacks');v=history(callback);assert v['Supported'];wait(lambda:any(r['message'].get('id')=='forbidden-permission' and 'error' in r['message'] for r in rows('wire.jsonl')))
 ok('history probe rejects native execution callbacks instead of granting permissions or starting provider tools')
 broken=complete(harness='disconnect',conversation='broken');history(broken,status=500);count=len(rows('prompts.jsonl'));blocked=complete(harness='disconnect',conversation='broken');assert blocked['State']==6 and len(rows('prompts.jsonl'))==count
 malformed=complete(harness='bad-update');history(malformed,status=500)
 ok('disconnected or malformed native load returns no partial success and fences ambiguous context without replaying work')
 # HTTP abort terminates its own process; preserve uncertain admission for interrupted load.
 canceled=complete(harness='slow',conversation='cancel-owned');count=len(rows('loads.jsonl'));from http.client import HTTPConnection
 client=HTTPConnection('127.0.0.1',port);client.request('POST',path(canceled),headers={'Authorization':'Bearer '+tokens['HISTORY_HOST']});wait(lambda:len(rows('loads.jsonl'))>count);pid=rows('loads.jsonl')[-1]['pid'];client.close()
 def gone():
  try:os.kill(pid,0);return False
  except ProcessLookupError:return True
 wait(gone,10);v=history(canceled);assert v['Supported'] and v['Uncertain'];count=len(rows('prompts.jsonl'));blocked=complete(harness='slow',conversation='cancel-owned');assert blocked['State']==6 and len(rows('prompts.jsonl'))==count
 ok('aborted load kills only its probe; explicit later history remains available but never clears the previous uncertainty fence')
 reads=[complete(harness='slow') for _ in range(3)];count=len(rows('loads.jsonl'));clients=[HTTPConnection('127.0.0.1',port) for _ in range(2)]
 for client,item in zip(clients,reads):client.request('POST',path(item),headers={'Authorization':'Bearer '+tokens['HISTORY_HOST']})
 wait(lambda:len(rows('loads.jsonl'))>=count+2);pids=[r['pid'] for r in rows('loads.jsonl')[-2:]];history(reads[-1],status=409)
 for client in clients:client.close()
 def all_gone():
  for probe in pids:
   try:os.kill(probe,0);return False
   except ProcessLookupError:pass
  return True
 wait(all_gone,10)
 ok('history probe admission is bounded across independent contexts and aborted leases release cleanly')
 stop();start();v=history(first);assert v['SessionId']==sid(first) and v['Entries'][0]['Text']=='original';count=len(rows('prompts.jsonl'));new=complete(conversation='owned',text='after restart');assert new['State']==3 and new['Result']=='fresh:after restart' and sid(new)==sid(first) and len(rows('prompts.jsonl'))==count+1
 claimed=http('/v1/results/claim',dict(ConversationId='owned',ClientId='history-check',LeaseSeconds=60));assert claimed['Id']==first['Id']
 ok('exact persisted native history survives Host restart, fresh original-context work remains isolated and original outbox result stays claimable')
finally:
 stop()
 for name,data in [('results',passed),('http',records),('wire',rows('wire.jsonl')),('events',{n:rows(n) for n in ['new.jsonl','resumes.jsonl','prompts.jsonl','loads.jsonl']})]: (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
