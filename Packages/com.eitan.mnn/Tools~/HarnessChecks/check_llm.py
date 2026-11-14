#!/usr/bin/env python3
"""Verify persistent real C# host against an independent non-streaming LLM HTTP peer."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse,json,os,shutil,socket,subprocess,sys,time,urllib.error,urllib.request,uuid
from pathlib import Path
SAMPLE=PACKAGE_ROOT;ROOT=PROJECT_ROOT
p=argparse.ArgumentParser();p.add_argument('--output',required=True,type=Path);p.add_argument('--host',type=Path,default=ROOT/'TestArtifacts~/SiriAIHarness/Host/bin/Debug/net8.0/SiriAI.HarnessHost.dll');a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir()
TOKEN=uuid.uuid4().hex*2;OTHER=uuid.uuid4().hex*2;KEY=uuid.uuid4().hex
ENV=dict(os.environ,SIRIAI_LLM_CHECK_HOST=TOKEN,SIRIAI_LLM_CHECK_OTHER=OTHER,SIRIAI_LLM_FIXTURE_TOKEN=KEY)
def port():
 with socket.socket() as s:s.bind(('127.0.0.1',0));return s.getsockname()[1]
PEER='http://127.0.0.1:'+str(port());URL='http://127.0.0.1:'+str(port());CONFIG=RUN/'host.json';REG=RUN/'service'
(RUN/'parallel').mkdir()
CONFIG.write_text(json.dumps(dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,
 Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_LLM_CHECK_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='SIRIAI_LLM_CHECK_OTHER')],
 Workspaces=dict(test=str(RUN),parallel=str(RUN/'parallel')),Harnesses=[dict(Id='llm',Backend='llm',Endpoint=PEER+'/api/v1/',Model='fixture-model',SystemPrompt='Answer clearly.',TokenEnvironmentVariable='SIRIAI_LLM_FIXTURE_TOKEN'),
 dict(Id='plain',Backend='llm',Endpoint=PEER+'/api/v1/',Model='fixture-model',TokenEnvironmentVariable='SIRIAI_LLM_FIXTURE_TOKEN',TimeoutMilliseconds=0),
 dict(Id='timeout',Backend='llm',Endpoint=PEER+'/api/v1/',Model='fixture-model',SystemPrompt='Answer clearly.',TokenEnvironmentVariable='SIRIAI_LLM_FIXTURE_TOKEN',TimeoutMilliseconds=100)])))
records=[];passed=[];process=None;hostlog=None
peerlog=(RUN/'peer.log').open('w');peer=subprocess.Popen([sys.executable,'-u',str(SAMPLE/'Tools~/HarnessChecks/Fixtures/llm_fixture.py'),PEER.rsplit(':',1)[1]],env=ENV,stdout=peerlog,stderr=peerlog)
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def wait(test,timeout=12):
 end=time.monotonic()+timeout
 while time.monotonic()<end:
  value=test()
  if value:return value
  time.sleep(.03)
 raise AssertionError('Timed out waiting for observed LLM behavior')
def peerstate():
 try:
  with urllib.request.urlopen(PEER+'/__state',timeout=2) as r:return json.load(r)
 except urllib.error.URLError:return None
def cli(args,expected=0,env=ENV):
 cmd=['dotnet',str(a.host),*args];r=subprocess.run(cmd,env=env,text=True,capture_output=True,timeout=35)
 records.append(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));(OUT/'commands.json').write_text(json.dumps(records,indent=2))
 assert r.returncode==expected,records[-1]
 assert all(secret not in r.stdout+r.stderr for secret in [TOKEN,OTHER,KEY]),'credential output'
 return r

def call(path,body=None,token=TOKEN,expected=200):
 request=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(request,timeout=12) as r:code,raw=r.status,r.read()
 except urllib.error.HTTPError as e:code,raw=e.code,e.read()
 assert code==expected,(path,code,raw)
 assert all(secret.encode() not in raw for secret in [TOKEN,OTHER,KEY]),'credential response'
 return json.loads(raw) if raw else None

def start():
 global process,hostlog
 hostlog=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w')
 env={k:v for k,v in os.environ.items() if k not in ['SIRIAI_LLM_CHECK_HOST','SIRIAI_LLM_CHECK_OTHER','SIRIAI_LLM_FIXTURE_TOKEN']}
 process=subprocess.Popen(['dotnet',str(a.host),'--service','run','--directory',str(REG)],env=env,stdout=hostlog,stderr=hostlog)
 def ready():
  if process.poll() is not None:raise AssertionError('host exited '+str(process.returncode))
  try:return call('/v1/harnesses')
  except urllib.error.URLError:return None
 return wait(ready)
def stop(kill=False):
 global process
 if process is not None:
  process.kill() if kill else process.terminate()
  try:process.wait(timeout=10)
  except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
  process=None;hostlog.close()
def submit(text,key=None,harness='llm',attachments=None,workspace='test'):
 return call('/v1/tasks',dict(ConversationId='phone',SubmissionKey=key or uuid.uuid4().hex,HarnessId=harness,WorkspaceKey=workspace,Objective=text,Attachments=attachments),expected=202)
def terminal(identity):
 value=call('/v1/tasks/'+identity)
 return value if value['State'] in [3,4,5,6] else None
try:
 wait(peerstate)
 cli(['--service','prepare','--id','llm-check','--config',str(CONFIG),'--runtime',str(Path(shutil.which('dotnet')).resolve()),'--directory',str(REG)])
 snapshot={p.name:p.read_bytes() for p in REG.iterdir() if p.name!='prepare.lock'}
 cli(['--service','prepare','--id','llm-check','--config',str(CONFIG),'--runtime',str(Path(shutil.which('dotnet')).resolve()),'--directory',str(REG)])
 assert snapshot=={p.name:p.read_bytes() for p in REG.iterdir() if p.name!='prepare.lock'}
 assert KEY not in (REG/'host.json').read_text()
 catalog=start();assert catalog['configured'][0]['backend']=='llm' and catalog['configured'][0]['capabilities']==1
 assert catalog['configured'][0]['cancellationScope']=='http-request'
 assert len(peerstate()['calls'])==0
 ok('named LLM config prepares idempotently, starts with saved credentials, advertises request-scoped cancellation and makes no startup inference')
 first=submit('slow','intent');assert first['State'] in [0,1]
 assert submit('slow','intent')['Id']==first['Id'];call('/v1/tasks/'+first['Id'],token=OTHER,expected=404)
 result=wait(lambda:terminal(first['Id']));assert result['State']==3 and result['Result']=='fixture answer: slow',result
 wire=peerstate()['calls'];assert len(wire)==1 and wire[0]['body']['messages'][1]['content']=='slow'
 raw=json.dumps(wire);assert first['Id'] not in raw and 'phone' not in raw and 'alice' not in raw and str(RUN) not in raw
 ok('nonblocking durable admission sends one stateless request without task/owner/workspace/history/tools and finishes after client disconnect')
 stop();start();assert call('/v1/tasks/'+first['Id'])['Result']==result['Result'];assert len(peerstate()['calls'])==1
 claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='new-phone',LeaseSeconds=60));assert claim['Id']==first['Id']
 call('/v1/tasks/'+first['Id']+'/receipt',dict(LeaseId=claim['LeaseId'],Receipt=4))
 ok('completed result survives process restart and is claimable by a reconnected foreground client')
 for objective,text in [('array','first\n第二段'),('fallback','fallback text')]:
  item=submit(objective);value=wait(lambda:terminal(item['Id']));assert value['State']==3 and value['Result']==text,value
 ok('string/content-array/output_text response forms map to durable text results')
 for objective in ['invalid','empty','tool-only','error','redirect','oversized-bytes','oversized-text']:
  count=len(peerstate()['calls']);item=submit(objective);value=wait(lambda:terminal(item['Id']));assert value['State']==4,value
  assert value['Result'] and len(value['Result'])<=4096 and len(peerstate()['calls'])==count+1
  if objective=='error':assert '429' in value['Result']
 ok('HTTP errors, redirects, malformed/empty/tool-only/oversized results fail once without retries, token leakage or false completion')
 count=len(peerstate()['calls']);call('/v1/tasks',dict(ConversationId='phone',SubmissionKey='attachments',HarnessId='llm',WorkspaceKey='test',Objective='input',Attachments=[dict(Kind=0,Payload='data')]),expected=422)
 assert len(peerstate()['calls'])==count
 ok('unsupported attachments are rejected before LLM admission or network work')
 parallel=submit('slow',workspace='parallel')
 item=submit('cancel');wait(lambda:any(x['body']['messages'][-1]['content']=='cancel' for x in peerstate()['calls']))
 call('/v1/tasks/'+item['Id']+'/cancel',{},token=OTHER,expected=404)
 cancelled=call('/v1/tasks/'+item['Id']+'/cancel',{});assert cancelled['State']==5,cancelled
 assert 'provider' in cancelled['Result'].lower() and 'unknown' in cancelled['Result'].lower(),cancelled
 wait(lambda:any(x['disconnected'] for x in peerstate()['calls'] if x['body']['messages'][-1]['content']=='cancel'))
 ok('owner-authorized cancellation waits for local HTTP abort and reports remote provider cancellation as unknown')
 assert wait(lambda:terminal(parallel['Id']))['State']==3
 ok('cancelling one request leaves another workspace request running to completion')
 item=submit('no-system',harness='plain');value=wait(lambda:terminal(item['Id']));assert value['State']==3 and value['Result']=='fixture answer: no-system',value
 ok('optional system prompt and zero timeout send only one user message and allow a delayed response')
 item=submit('timeout',harness='timeout');value=wait(lambda:terminal(item['Id']));assert value['State']==4 and 'timed out' in value['Result'].lower(),value
 ok('configured timeout is a failed request rather than a user-cancel success')
 item=submit('crash');wait(lambda:any(x['body']['messages'][-1]['content']=='crash' for x in peerstate()['calls']));count=len(peerstate()['calls']);stop(kill=True);start()
 value=call('/v1/tasks/'+item['Id']);assert value['State']==6 and len(peerstate()['calls'])==count,value
 assert submit('crash',item['SubmissionKey'])['Id']==item['Id'];assert len(peerstate()['calls'])==count
 ok('host crash interrupts unknown in-flight LLM outcome without reissuing the inference request')
 for name,patch in [('missing-model',dict(Model='')),('bad-url',dict(Endpoint='https://example.org/v1?token=secret')),('plaintext',dict(Endpoint='http://example.org/v1')),('missing-key',dict(TokenEnvironmentVariable='SIRIAI_LLM_ABSENT')),('bad-timeout',dict(TimeoutMilliseconds=-1))]:
  value=json.loads(CONFIG.read_text());value['Harnesses']=[dict(value['Harnesses'][0],**patch)];bad=RUN/(name+'.json');bad.write_text(json.dumps(value))
  cli(['--service','prepare','--id','negative','--config',str(bad),'--runtime',str(Path(shutil.which('dotnet')).resolve()),'--directory',str(RUN/('negative-'+name))],expected=2)
 ok('invalid URL/model/credential/timeout configuration fails before provider requests')
 assert not peerstate()['errors'],peerstate()['errors']
finally:
 stop();(OUT/'wire.json').write_text(json.dumps(peerstate(),indent=2,ensure_ascii=False));peer.terminate();peer.wait(timeout=5);peerlog.close()
 (OUT/'results.json').write_text(json.dumps(dict(passed=passed,run=str(RUN),vendor_inference=False),indent=2,ensure_ascii=False))
