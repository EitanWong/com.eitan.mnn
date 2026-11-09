#!/usr/bin/env python3
"""Real native elicitation requests, HTTP controls and independent wire assertions."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse,json,os,socket,subprocess,sys,time,urllib.error,urllib.request,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();SAMPLE=PACKAGE_ROOT
token=uuid.uuid4().hex*2;env=dict(os.environ,SIRIAI_INPUT_TOKEN=token,SIRIAI_INPUT_ROOT=str(PEER));passed=[];process=None
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
url='http://127.0.0.1:'+str(port);config=dict(Listen=url,DataDirectory=str(RUN/'data'),Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_INPUT_TOKEN')],Workspaces=dict(test=str(RUN)),Harnesses=[dict(Id='input',Protocol='acp',Command=sys.executable,Arguments=['-u',str(SAMPLE/'Tools~/HarnessChecks/Fixtures/acp_input_fixture.py')],ForwardEnvironment=['SIRIAI_INPUT_ROOT'])]);config['Harnesses'].append(dict(config['Harnesses'][0],Id='short-input',TimeoutMilliseconds=1000));CONFIG=RUN/'host.json';CONFIG.write_text(json.dumps(config));log=(RUN/'host.log').open('w')
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def http(path,body=None,status=200):
 req=urllib.request.Request(url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=15) as response:code,raw=response.status,response.read()
 except urllib.error.HTTPError as e:code,raw=e.code,e.read()
 assert code==status,(path,code,raw);return json.loads(raw) if raw else None
def wait(fn):
 end=time.monotonic()+20
 while time.monotonic()<end:
  result=fn()
  if result:return result
  time.sleep(.03)
 raise AssertionError('Timed out waiting for native form behavior')
def submit(case={},conversation=None,harness='input'):return http('/v1/tasks',dict(ConversationId=conversation or uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId=harness,WorkspaceKey='test',Objective=json.dumps(case)),202)
def pending(task):
 value=http('/v1/tasks/'+task['Id']);return value if value['WaitingFor'] or value['State'] in [3,4,5,6] else None
def terminal(task):
 value=http('/v1/tasks/'+task['Id']);return value if value['State'] in [3,4,5,6] else None
def reply(task,text,action=0,status=200,request=None):return http('/v1/tasks/'+task['Id']+'/respond',dict(RequestId=request or task['RequestId'],Text=text,InputAction=action),status)
values=dict(name='用户',count=3,ratio=.25,enabled=True,color='red',tags=['中','a'])
try:
 process=subprocess.Popen(['dotnet',str(a.host),'--config',str(CONFIG)],env=env,stdout=log,stderr=log)
 def ready():
  assert process.poll() is None
  try:return http('/v1/harnesses')
  except urllib.error.URLError:return None
 wait(ready)
 first=submit();form=wait(lambda:pending(first));assert form['WaitingFor']==1 and form['State']==1,form
 assert form['InputForm']['Fields'][0]['Name']=='name' and len(form['InputForm']['Fields'])==7,form
 assert http('/v1/harnesses')['configured'][0]['capabilities']&2
 for caps in PEER.glob('capabilities-*.json'):
  c=json.loads(caps.read_text());assert c['elicitation']['form']=={} and not c.get('terminal',False) and not c.get('fs',{}).get('readTextFile',False)
 ok('native ACP advertises form capability and exposes typed bounded schema through durable task state')
 for bad in [dict(values,name='1'),dict(values,count=0),dict(values,count=2.5),dict(values,ratio=2),dict(values,enabled='true'),dict(values,color='green'),dict(values,tags=['bad']),dict(values,unknown='x'),{k:v for k,v in values.items() if k!='name'}]:
  reply(form,json.dumps(bad),status=400);assert http('/v1/tasks/'+form['Id'])['RequestId']==form['RequestId']
 reply(form,'{broken',status=400);reply(form,'[]',status=400)
 reply(form,json.dumps(values));done=wait(lambda:terminal(first));assert done['State']==3 and json.loads(done['Result'])==[dict(action='accept',content=values)],done
 reply(form,json.dumps(values),status=409)
 ok('typed user values validate before settling; invalid answers preserve original request and stale replies cannot answer another request')
 for action,word in [(3,'decline'),(2,'cancel')]:
  task=submit();request=wait(lambda:pending(task));reply(request,word,action);done=wait(lambda:terminal(task));assert json.loads(done['Result'])==[dict(action=word)]
 ok('decline and cancel resolve only the elicitation and remain distinct from task cancellation')
 task=submit(dict(count=2));request=wait(lambda:pending(task));first_id=request['RequestId'];reply(request,json.dumps(values));request=wait(lambda: (lambda x:x if x['WaitingFor']==1 and x['RequestId']!=first_id else None)(http('/v1/tasks/'+task['Id'])));reply(request,json.dumps(values),request=first_id,status=409);reply(request,json.dumps(values));done=wait(lambda:terminal(task));assert len(json.loads(done['Result']))==2
 ok('concurrent native forms serialize with independent public identities and cannot overwrite each other')
 for case in [dict(wrong_session=True),dict(mode='_future'),dict(schema=dict(type='object',properties=dict(x=dict(type='_future'))))]:
  task=submit(case);done=wait(lambda:terminal(task));assert done['State']==3 and 'error' in json.loads(done['Result'])[0],done
 ok('unadvertised modes, unrelated sessions and unknown field types are rejected without rendering or poisoning the native prompt')
 task=submit();request=wait(lambda:pending(task));http('/v1/tasks/'+task['Id']+'/cancel',{});done=wait(lambda:terminal(task));assert done['State']==5;reply(request,json.dumps(values),status=409)
 ok('native task cancellation expires pending form replies and waits for confirmed cancelled prompt')
 task=submit(harness='short-input');request=wait(lambda:pending(task));time.sleep(1.3);still=http('/v1/tasks/'+task['Id']);assert still['State']==1 and still['RequestId']==request['RequestId'],still;reply(request,json.dumps(values));assert wait(lambda:terminal(task))['State']==3
 ok('execution timeout pauses during an actual user form wait and resumes after the answer')
 task=submit(dict(after_answer_delay=2),harness='short-input');request=wait(lambda:pending(task));reply(request,json.dumps(values));done=wait(lambda:terminal(task));assert done['State']==4 and 'timed out' in done['Result'],done
 ok('the remaining execution timeout resumes after form settlement and still enforces a stuck native prompt')
 cases=[({}, {}, None),
  (dict(type='object',properties=dict(x=dict(type='integer',minimum=9007199254740992,maximum=9007199254740993)),required=['x']),dict(x=9007199254740993),dict(x=9007199254740994)),
  (dict(type='object',properties=dict(x=dict(type='string',minLength=1,maxLength=1)),required=['x']),dict(x='😀'),dict(x='😀😀')),
  (dict(type='object',properties=dict(x=dict(type='string',format='date')),required=['x']),dict(x='2026-11-09'),dict(x='2026-02-30'))]
 for schema,answer,bad in cases:
  task=submit(dict(schema=schema));request=wait(lambda:pending(task));assert request['WaitingFor']==1,request
  if bad is not None:reply(request,json.dumps(bad),status=400)
  reply(request,json.dumps(answer));done=wait(lambda:terminal(task));assert json.loads(done['Result'])==[dict(action='accept',content=answer)]
 ok('empty forms, exact int64 bounds, Unicode code-point lengths and string formats retain their native semantics')
 task=submit(dict(count=66));request=wait(lambda:pending(task));native=json.loads(request['BackendState'])['SessionId']
 wait(lambda: sum(v['sid']==native and 'error' in v['result'] for v in [json.loads(line) for line in (PEER/'decisions.jsonl').read_text().splitlines()])>=2)
 http('/v1/tasks/'+task['Id']+'/cancel',{});assert wait(lambda:terminal(task))['State']==5
 ok('native form floods are bounded and cancellation releases queued requests without completing work falsely')
 task=submit(conversation='restart-input');request=wait(lambda:pending(task));process.kill();process.wait(timeout=10);process=subprocess.Popen(['dotnet',str(a.host),'--config',str(CONFIG)],env=env,stdout=log,stderr=log);wait(ready)
 restored=http('/v1/tasks/'+task['Id']);assert restored['State']==6 and restored['RequestId'] is None and restored.get('InputForm') is None,restored
 again=submit(conversation='restart-input');assert wait(lambda:terminal(again))['State']==6
 fresh=submit();request=wait(lambda:pending(fresh));reply(request,json.dumps(values));assert wait(lambda:terminal(fresh))['State']==3
 ok('Host crash clears unanswerable forms, fences uncertain native history without replay and permits independent new work')
finally:
 if process is not None:
  process.terminate()
  try:process.wait(timeout=10)
  except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
 log.close();(OUT/'results.json').write_text(json.dumps(passed,indent=2)+'\n')
 wire=[json.loads(line) for line in (PEER/'wire.jsonl').read_text().splitlines()] if (PEER/'wire.jsonl').exists() else []
 (OUT/'wire.json').write_text(json.dumps(wire,ensure_ascii=False,indent=2)+'\n')
