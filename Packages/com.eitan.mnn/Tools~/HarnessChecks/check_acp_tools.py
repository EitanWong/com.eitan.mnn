import argparse,json,os,socket,subprocess,time,urllib.request,urllib.error,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();passed=[];records=[];process=None;log=None
fixture=Path(__file__).with_name('Fixtures')/'acp_tools_fixture.py';tokens={k:uuid.uuid4().hex*2 for k in ['TOOL_HOST','TOOL_OTHER']};env=dict(os.environ,**tokens,SIRIAI_TOOL_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port);CONFIG=RUN/'host.json';CONFIG.write_text(json.dumps(dict(Listen=URL,DataDirectory=str(RUN/'data'),Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='TOOL_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='TOOL_OTHER')],Workspaces=dict(test=str(RUN)),Harnesses=[dict(Id='tools',Protocol='acp',Command='python3',Arguments=['-u',str(fixture)],ForwardEnvironment=['SIRIAI_TOOL_ROOT'],TimeoutMilliseconds=0)])))
def rows(name):
 p=PEER/name;return [json.loads(x) for x in p.read_text().splitlines()] if p.exists() else []
def http(path,body=None,status=200,token=None):
 req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(token or tokens['TOOL_HOST']),'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=25) as response:code,data=response.status,response.read()
 except urllib.error.HTTPError as e:code,data=e.code,e.read()
 records.append(dict(path=path,status=code,expected=status,body=data.decode()));assert code==status,(path,code,data);return json.loads(data) if data else None
def wait(fn,seconds=20):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  v=fn()
  if v:return v
  time.sleep(.02)
 raise AssertionError('Timed out observing native tool state')
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
def submit(mode='normal',conversation=None,token=None):return http('/v1/tasks',dict(ConversationId=conversation or uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId='tools',WorkspaceKey='test',Objective=json.dumps(dict(mode=mode))),202,token)
def done(item,token=None):
 v=http('/v1/tasks/'+item['Id'],token=token);return v if v['State'] in [3,4,5,6] else None
def complete(mode='normal',conversation=None,token=None):
 item=submit(mode,conversation,token);return wait(lambda:done(item,token))
def call(item,key):return next(v for v in item['NativeTools']['Calls'] if v['ToolCallId']==key)
def ok(msg):passed.append(msg);print('PASS '+msg,flush=True)
try:
 start();first=complete(conversation='owned');assert first.get('NativeTools'),'Native ACP tool deltas are not merged into a durable user-visible state'
 primary=call(first,'native-1');payload=json.loads(primary['Payload']);assert primary['Title']=='<b>Inspect work</b>' and primary['Status']=='completed' and primary['Kind']=='read';assert payload['rawInput']=={'path':'original.txt'} and payload['rawOutput']=={'answer':42};assert payload['locations']==[] and len(payload['content'])==1 and payload['content'][0]['type']=='diff';assert not primary['OmittedFields'] and not first['NativeTools']['Truncated']
 assert call(first,'native-2')['Title']=='Independent tool' and call(first,'native-2')['Status']=='failed';assert call(first,'orphan')['Title']=='Late title';assert len(first['NativeTools']['Calls'])==3 and first['Result']=='fresh result' and 'FOREIGN SECRET' not in json.dumps(first)
 ok('official partial tool deltas merge exact ID, preserve omitted/null typed fields, replace arrays/raw output and retain interleaved identities')
 item=submit();observed=[]
 def running():
  v=http('/v1/tasks/'+item['Id']);observed.append(v);return v.get('NativeTools') and any(c['Status']=='in_progress' for c in v['NativeTools']['Calls'])
 wait(running);later=wait(lambda:done(item));assert later['State']==3 and any(v['State']==1 for v in observed)
 ok('merged native tool state reaches the foreground while task is running and remains at terminal completion')
 before=json.loads(json.dumps(first));again=complete('empty',conversation='owned');assert again['SessionConfiguration']['SessionId']==first['SessionConfiguration']['SessionId'] and again['NativeTools']['Calls']==[] and http('/v1/tasks/'+first['Id'])==before
 foreign=complete(token=tokens['TOOL_OTHER']);assert foreign['SessionConfiguration']['SessionId']!=first['SessionConfiguration']['SessionId'];http('/v1/tasks/'+first['Id'],status=404,token=tokens['TOOL_OTHER'])
 ok('new task resets native tool cache even when native IDs are reused, historical task and other owner contexts remain isolated')
 long=complete('long');assert json.loads(call(long,'native-1')['Payload'])['rawOutput']['text'].endswith('EXACT END');assert len(json.loads(call(long,'native-1')['Payload'])['rawOutput']['text'])>4096
 ok('durable typed current state retains valid native content beyond legacy execution-event text clipping')
 big=complete('oversize');b=call(big,'big');assert b['Status']=='completed' and 'rawInput' in b['OmittedFields'] and big['NativeTools']['Truncated'] and json.loads(b['Payload'])['rawOutput']=={'answer':42}
 bulk=complete('bulk');assert bulk['NativeTools']['Truncated'] and len(bulk['NativeTools']['Calls'])<=32 and bulk['NativeTools']['Calls'][-1]['ToolCallId']=='bulk-39' and len(json.dumps(bulk).encode())<1048576
 for c in bulk['NativeTools']['Calls']:json.loads(c['Payload'])
 ok('oversized fields and many tools are bounded with explicit omission flags, valid JSON, latest updates and response budget')
 escaped=complete('escaped');assert escaped['State']==3 and len(escaped['NativeTools']['Calls'])==1 and len(escaped['NativeTools']['Calls'][0]['ToolCallId'])==4096
 bad=complete('invalid');assert bad['State']==6
 ok('malformed native tool identity interrupts without silently attaching its result to another tool')
 stop();start();persisted=http('/v1/tasks/'+first['Id']);assert persisted['NativeTools']==first['NativeTools'];assert http('/v1/tasks/'+long['Id'])['NativeTools']==long['NativeTools']
 claimed=http('/v1/results/claim',dict(ConversationId='owned',ClientId='tool-check',LeaseSeconds=60));assert claimed['Id']==first['Id'] and claimed['NativeTools']==first['NativeTools'] and claimed['Result']=='fresh result'
 ok('merged tool state survives actual Host restart and outbox remains independent and claimable')
finally:
 stop()
 for name,data in [('results',passed),('http',records),('wire',rows('wire.jsonl')),('events',dict(prompts=rows('prompts.jsonl'),new=rows('new.jsonl')))]: (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
