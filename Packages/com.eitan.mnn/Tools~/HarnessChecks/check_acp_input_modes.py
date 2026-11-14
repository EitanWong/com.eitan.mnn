#!/usr/bin/env python3
"""External native input RPCs through the actual C# HTTP Host."""
import argparse,json,os,socket,subprocess,time,urllib.error,urllib.request,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();passed=[];process=None;log=None;observations={}
fixture=Path(__file__).with_name('Fixtures')/'acp_input_modes_fixture.py';secrets={k:uuid.uuid4().hex*2 for k in ['MODES_HOST','MODES_OTHER','MODES_SCOPE']};env=dict(os.environ,**secrets,SIRIAI_INPUT_MODES_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port);CONFIG=RUN/'host.json';config=dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='MODES_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='MODES_OTHER')],Workspaces=dict(test=str(RUN)),CoordinatorMcpScopes=[dict(Id='front',OwnerId='alice',ConversationBinding='ParentTask',WorkspaceKey='test',HarnessId='worker',TokenEnvironmentVariable='MODES_SCOPE')],Harnesses=[dict(Id=mode,Protocol='acp',Command='python3',Arguments=['-u',str(fixture),'coordinator' if mode=='coordinator' else 'direct'],ForwardEnvironment=['SIRIAI_INPUT_MODES_ROOT'],TimeoutMilliseconds=1000 if mode=='short' else 0,**(dict(CoordinatorScopeId='front',AcpMcpServers=[dict(Name='coordinator',Type='http',Url=URL+'/mcp/coordinator',Headers=[dict(Name='Authorization',ValueEnvironmentVariable='MODES_SCOPE',Prefix='Bearer ')])]) if mode=='coordinator' else {})) for mode in ['direct','short','worker','coordinator']]);CONFIG.write_text(json.dumps(config))
def rows(name):
 p=PEER/name;return [json.loads(v) for v in p.read_text().splitlines()] if p.exists() else []
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def http(path,body=None,status=200,token=None):
 req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(token or secrets['MODES_HOST']),'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=20) as response:code,data=response.status,response.read()
 except urllib.error.HTTPError as e:code,data=e.code,e.read()
 assert code==status,(path,code,data);return json.loads(data) if data else None
def wait(fn,seconds=20):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  value=fn()
  if value:return value
  time.sleep(.02)
 raise AssertionError('Timed out waiting for native input mode')
def start():
 global process,log
 log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--config',str(CONFIG)],env=env,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None,Path(log.name).read_text()
  try:return http('/v1/harnesses')
  except urllib.error.URLError:return None
 wait(ready)
def stop(kill=False):
 global process
 if process:
  process.kill() if kill else process.terminate()
  try:process.wait(timeout=10)
  except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
  process=None;log.close()
def submit(case,conversation=None,harness='direct'):return http('/v1/tasks',dict(ConversationId=conversation or uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId=harness,WorkspaceKey='test',Objective=json.dumps(case)),202)
def task(item):return http('/v1/tasks/'+item['Id'])
def pending(item):
 value=task(item);return value if value['WaitingFor'] or value['State'] in [3,4,5,6] else None
def terminal(item):
 value=task(item);return value if value['State'] in [3,4,5,6] else None
def reply(value,text='',action=0,status=200,request=None,token=None):return http('/v1/tasks/'+value['Id']+'/respond',dict(RequestId=request or value['RequestId'],Text=text,InputAction=action),status,token)
try:
 start();first=submit(dict(requests=[dict(mode='url',elicitationId='first-url',url='https://example.invalid/auth?state=原样')]))
 value=wait(lambda:pending(first));observations['first']=value
 assert value['State']==1 and value['WaitingFor']==1 and value.get('InputPrompt',{}).get('Mode')==1,'Native URL input was rejected instead of reaching the original pending task'
 assert value['InputPrompt']['Url']=='https://example.invalid/auth?state=原样' and value['InputPrompt']['NativeId']=='first-url'
 reply(value,'Continue');done=wait(lambda:terminal(first));assert json.loads(done['Result'])==[dict(result=dict(action='accept'))],done
 ok('native URL elicitation reaches original durable task and explicit user continuation returns accept without claiming authentication')

 for capability in rows('capabilities.jsonl'):
  assert capability['capabilities']['elicitation']==dict(form={},url={}) and 'text' not in capability['capabilities']['elicitation']
 ok('official form/url capabilities advertised; pinned text compatibility is not mislabeled as an official capability')
 # All replies settle exactly the current native request; foreign/stale/invalid replies preserve it.
 case=dict(requests=[dict(mode='url',elicitationId='complete-exact')]);item=submit(case);value=wait(lambda:pending(item));request=value['RequestId']
 reply(value,request='foreign',status=409);reply(value,token=secrets['MODES_OTHER'],status=404);reply(value,action=1,status=422)
 reply(value,'x'*8193,status=400);reply(value,None,status=400)
 (PEER/'complete-complete-exact').write_text('unknown-native');time.sleep(.12)
 assert not task(item)['InputPrompt']['BackendCompleted']
 (PEER/'complete-complete-exact').write_text('');value=wait(lambda:(lambda x:x if x.get('InputPrompt',{}).get('BackendCompleted') else None)(task(item)))
 time.sleep(.1);assert task(item)['RequestId']==request and not task(item)['ResultPending']
 assert not any(v['sid']==json.loads(value['BackendState'])['SessionId'] for v in rows('decisions.jsonl'))
 reply(value,'');done=wait(lambda:terminal(item));assert done['State']==3 and not done.get('InputPrompt');reply(value,status=409)
 ok('exact completion notification updates the original pending display without auto-answer; foreign/stale/invalid controls cannot settle it')
 for spec,text,content in [(dict(mode='text'),'  用户原样  ',dict(response='用户原样')),(dict(omit_mode=True),'  ',{}),(dict(mode='text',schema=dict(properties={'答案':{}})),' 是 ',{'答案':'是'}),(dict(mode='text',schema=dict(properties={'a':{},'b':{}})),'no',dict(response='no'))]:
  item=submit(dict(requests=[spec]));value=wait(lambda:pending(item));assert value['InputPrompt']['Mode']==0 and not value.get('InputForm'),value
  reply(value,text);done=wait(lambda:terminal(item));assert json.loads(done['Result'])==[dict(result=dict(action='accept',content=content))],done
 ok('pinned Qwen explicit/omitted text mode trims answers, preserves Unicode, maps one schema key and emits empty content for empty input')
 for mode in ['url','text']:
  for action,word in [(3,'decline'),(2,'cancel')]:
   item=submit(dict(requests=[dict(mode=mode)]));value=wait(lambda:pending(item));reply(value,'',action);done=wait(lambda:terminal(item));assert done['State']==3 and json.loads(done['Result'])==[dict(result=dict(action=word))],done
 ok('URL and text decline/cancel are distinct native actions; neither falsely cancels the durable task')
 for spec in [dict(mode='_future'),dict(mode=''),dict(mode='url',omit_id=True),dict(mode='url',wrong_session=True),dict(mode='url',request_scope=True),*[dict(mode='url',url=url) for url in ['file:///tmp/a','javascript:alert(1)','https://user:secret@example.invalid','https://example.invalid/ bad','https://example.invalid/\n','relative']]]:
  item=submit(dict(requests=[spec]));done=wait(lambda:terminal(item));assert done['State']==3 and 'error' in json.loads(done['Result'])[0] and not done.get('InputPrompt'),done
 ok('unknown modes, invalid URL handoffs and unsupported request/foreign session scopes fail explicitly without displaying a usable prompt')
 conversation='same-native-flow';item=submit(dict(requests=[dict(mode='url',elicitationId='never-reuse')]),conversation);value=wait(lambda:pending(item));reply(value);assert wait(lambda:terminal(item))['State']==3
 item=submit(dict(requests=[dict(mode='url',elicitationId='never-reuse')]),conversation);done=wait(lambda:terminal(item));assert 'error' in json.loads(done['Result'])[0],done
 ok('reused native URL identity is rejected on the same process so late completion cannot mark a replacement flow')
 # Concurrent heterogeneous requests all use the existing permission gate.
 item=submit(dict(requests=[dict(mode='url'),dict(mode='text'),dict(mode='form',schema=dict(type='object',properties={})),dict(mode='permission')]))
 identities=[]
 for i in range(4):
  value=wait(lambda:(lambda x:x if x['WaitingFor'] and x['RequestId'] not in identities else None)(task(item)))
  assert value['State']==1 and not (value.get('InputForm') and value.get('InputPrompt'));identities.append(value['RequestId'])
  if len(identities)>1:reply(value,request=identities[-2],status=409)
  reply(value,'{}' if value.get('InputForm') else 'Answer')
 done=wait(lambda:terminal(item));results=json.loads(done['Result']);assert len(results)==4 and all('result' in v for v in results),done
 assert results[3]==dict(result=dict(outcome=dict(outcome='selected',optionId='deny'))),results
 ok('permissions, typed forms, URL and text prompts serialize independently under exact public request identities')
 for mode in ['url','text']:
  item=submit(dict(requests=[dict(mode=mode)]),harness='short');value=wait(lambda:pending(item));time.sleep(1.25)
  assert task(item)['State']==1 and task(item)['RequestId']==value['RequestId'];reply(value);assert wait(lambda:terminal(item))['State']==3
  item=submit(dict(requests=[dict(mode=mode)],after_answer_delay=2),harness='short');value=wait(lambda:pending(item));reply(value);done=wait(lambda:terminal(item));assert done['State']==4 and 'timed out' in done['Result'],done
 ok('execution budget pauses through both URL and text user waits and resumes enforcing a stuck prompt after settlement')
 item=submit(dict(requests=[dict(mode='url'),dict(mode='text')]));value=wait(lambda:pending(item));http('/v1/tasks/'+item['Id']+'/cancel',{});done=wait(lambda:terminal(item));assert done['State']==5 and not done.get('InputPrompt');reply(value,status=409)
 ok('native task cancellation expires active and queued heterogeneous requests and clears persisted prompts')
 item=submit(dict(requests=[dict(mode='url') for _ in range(66)]));value=wait(lambda:pending(item));native=json.loads(value['BackendState'])['SessionId']
 wait(lambda:sum(v['sid']==native and 'error' in v['reply'] for v in rows('decisions.jsonl'))>=2)
 http('/v1/tasks/'+item['Id']+'/cancel',{});assert wait(lambda:terminal(item))['State']==5
 ok('URL request floods are bounded and cancelling releases queued waits')
 # Coordinator's own initial and result prompts remain on its original durable parent.
 parent=submit(dict(requests=[dict(mode='url',elicitationId='parent-initial')],result_requests=[dict(mode='text')],child={}),harness='coordinator');value=wait(lambda:pending(parent));assert value['Id']==parent['Id'] and value['InputPrompt']['NativeId']=='parent-initial';reply(value)
 second=wait(lambda:(lambda x:x if x['WaitingFor']==1 and x['RequestId']!=value['RequestId'] else None)(task(parent)));assert second['InputPrompt']['Mode']==0
 reply(second,'coordinator final');done=wait(lambda:terminal(parent));assert done['State']==3 and 'coordinator final' in done['Result'],done
 children=rows('children.jsonl');assert children and len(done['CollectedResults'])==1,done
 ok('coordinator initial URL and result-phase text route to the original parent while the durable child result remains collected')
 item=submit(dict(requests=[dict(mode='url')]),conversation='restart-url');value=wait(lambda:pending(item));before=len(rows('prompts.jsonl'));stop(True);start()
 restored=task(item);assert restored['State']==6 and restored['RequestId'] is None and not restored.get('InputPrompt'),restored
 reply(value,status=409);again=submit({},conversation='restart-url');assert wait(lambda:terminal(again))['State']==6
 assert len(rows('prompts.jsonl'))==before
 fresh=submit(dict(requests=[dict(mode='text')]));value=wait(lambda:pending(fresh));reply(value,'fresh');assert wait(lambda:terminal(fresh))['State']==3
 ok('Host crash clears unanswerable URL prompt, fences uncertain history without replay and leaves fresh independent work available')
 assert not rows('errors.jsonl'),rows('errors.jsonl')
finally:
 stop()
 for name,data in [('results',passed),('observations',observations),('wire',rows('wire.jsonl')),('mcp-wire',rows('mcp-wire.jsonl')),('events',dict(requests=rows('requests.jsonl'),decisions=rows('decisions.jsonl'),capabilities=rows('capabilities.jsonl'),prompts=rows('prompts.jsonl')))]: (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
