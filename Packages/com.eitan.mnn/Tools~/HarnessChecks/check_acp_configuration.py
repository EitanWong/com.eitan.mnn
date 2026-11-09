#!/usr/bin/env python3
import argparse,json,os,socket,subprocess,time,urllib.request,urllib.error,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();passed=[];observations={};process=None;log=None
fixture=Path(__file__).with_name('Fixtures')/'acp_configuration_fixture.py';tokens={k:uuid.uuid4().hex*2 for k in ['CONFIG_HOST','CONFIG_OTHER']};env=dict(os.environ,**tokens,SIRIAI_CONFIG_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port)
def entry(name,variant='normal',**kw):return dict(Id=name,Protocol='acp',Command='python3',Arguments=['-u',str(fixture),variant],ForwardEnvironment=['SIRIAI_CONFIG_ROOT'],TimeoutMilliseconds=0,**kw)
config=dict(Listen=URL,DataDirectory=str(RUN/'data'),Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='CONFIG_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='CONFIG_OTHER')],Workspaces=dict(test=str(RUN)),Harnesses=[entry('forced',Model=' Reasoner Plus '),entry('auto',Model=' AUTO '),entry('default'),entry('exact',Model='REASONER-V2'),entry('fallback','fallback',Model='Reasoner Plus'),entry('ambiguous','ambiguous',Model='Reasoner Plus'),entry('missing','missing',Model='Reasoner Plus'),entry('unknown','unknown-type',Model='Reasoner Plus'),entry('absent-choice',Model='not-offered'),entry('reject','reject',Model='Reasoner Plus'),entry('unconfirmed','unconfirmed',Model='Reasoner Plus'),entry('no-confirmation','missing-response',Model='Reasoner Plus'),entry('lost','disconnect',Model='Reasoner Plus'),entry('settings',Model='Reasoner Plus',SessionConfigOptions={'mode':'Automatic','thinking':True},SessionMode='auto'),entry('mode-missing',SessionMode='not-offered'),entry('mode-silent','silent-mode',SessionMode='auto')]);CONFIG=RUN/'host.json';CONFIG.write_text(json.dumps(config))
def rows(name):
 p=PEER/name;return [json.loads(x) for x in p.read_text().splitlines()] if p.exists() else []
def http(path,body=None,status=200,token=None):
 req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(token or tokens['CONFIG_HOST']),'Content-Type':'application/json'})
 try:
  with urllib.request.urlopen(req,timeout=20) as response:code,data=response.status,response.read()
 except urllib.error.HTTPError as e:code,data=e.code,e.read()
 assert code==status,(path,code,data);return json.loads(data) if data else None
def wait(fn,seconds=20):
 end=time.monotonic()+seconds
 while time.monotonic()<end:
  v=fn()
  if v:return v
  time.sleep(.02)
 raise AssertionError('Timed out observing session configuration')
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
def submit(harness='forced',case={},conversation=None,token=None):return http('/v1/tasks',dict(ConversationId=conversation or uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId=harness,WorkspaceKey='test',Objective=json.dumps(case)),202,token)
def terminal(item,token=None):
 v=http('/v1/tasks/'+item['Id'],token=token);return v if v['State'] in [3,4,5,6] else None
def complete(harness='forced',case={},conversation=None,token=None):
 item=submit(harness,case,conversation,token);return wait(lambda:terminal(item,token))
def ok(message):passed.append(message);print('PASS '+message,flush=True)
try:
 start();item=submit();done=wait(lambda:terminal(item));observations['first']=done
 assert done['State']==3 and json.loads(done['Result'])['configOptions'][0]['currentValue']=='reasoner-v2','Configured ACP model was ignored before the original native prompt'
 assert done['SessionConfiguration']['Options'][0]['CurrentValue']=='reasoner-v2',done
 assert rows('sets.jsonl')[0]['params']['value']=='reasoner-v2'
 ok('configured model display name resolves to exact grouped native value, is confirmed before prompt, persists and reaches the original task')

 for harness in ['exact','fallback']:
  value=complete(harness);assert value['State']==3 and value['SessionConfiguration']['Options'][0]['CurrentValue']=='reasoner-v2',value
 ok('category selection, model/models fallback, exact IDs and case-insensitive labels resolve to the actual native identity')
 for harness in ['auto','default']:
  before=len(rows('sets.jsonl'));value=complete(harness);assert value['State']==3 and value['SessionConfiguration']['Options'][0]['CurrentValue']=='small' and len(rows('sets.jsonl'))==before,value
 ok('empty and auto model preserve backend selection and send no synthetic configuration command')
 for harness in ['ambiguous','missing','unknown','absent-choice','mode-missing','reject']:
  before=len(rows('prompts.jsonl'));value=complete(harness);assert value['State']==4 and len(rows('prompts.jsonl'))==before,value
 ok('missing capability, unsupported type/value, ambiguous labels, unavailable mode and explicit native rejection stop before any prompt')
 for harness in ['unconfirmed','no-confirmation','lost','mode-silent']:
  before=len(rows('prompts.jsonl'));value=complete(harness);assert value['State']==(4 if harness=='unconfirmed' else 6) and len(rows('prompts.jsonl'))==before,value
 ok('reported wrong selection fails; absent state, lost transport and missing mode confirmation remain Interrupted without executing the original prompt')
 value=complete('settings');assert value['State']==3,value
 native=json.loads(value['Result']);assert [o['currentValue'] for o in native['configOptions']]==['reasoner-v2','auto',True] and value['SessionConfiguration']['CurrentModeId']=='auto',value
 sets=[v['params'] for v in rows('sets.jsonl') if v['sid']==value['SessionConfiguration']['SessionId']];assert [(v.get('configId'),v.get('value'),v.get('type')) for v in sets[:3]]==[('mode','auto',None),('thinking',True,'boolean'),('engine','reasoner-v2',None)],sets
 ok('explicit profile selectors, typed booleans and model override apply in order, then session/set_mode waits for actual current_mode_update')
 conversation='continued-config';first=complete(conversation=conversation);sid=first['SessionConfiguration']['SessionId'];count=len(rows('sets.jsonl'))
 again=complete(conversation=conversation);assert again['SessionConfiguration']['SessionId']==sid and len(rows('sets.jsonl'))==count
 changed=complete(case={'updates':True},conversation=conversation);assert changed['SessionConfiguration']['Options'][0]['CurrentValue']=='small' and changed['SessionConfiguration']['CurrentModeId']=='ask' and len(changed['SessionConfiguration']['Options'])==3,changed
 assert http('/v1/tasks/'+first['Id'])['SessionConfiguration']['Options'][0]['CurrentValue']=='reasoner-v2'
 foreign=complete(token=tokens['CONFIG_OTHER']);assert foreign['SessionConfiguration']['SessionId']!=sid;http('/v1/tasks/'+first['Id'],status=404,token=tokens['CONFIG_OTHER'])
 ok('same native conversation retains its context, scoped config/mode updates affect only the current task, historical snapshots and other owners stay isolated')
 stop();start();restored=http('/v1/tasks/'+changed['Id']);assert restored['SessionConfiguration']==changed['SessionConfiguration']
 before=len(rows('sets.jsonl'));loaded=complete(conversation=conversation);assert loaded['State']==3 and loaded['SessionConfiguration']['SessionId']==sid and loaded['SessionConfiguration']['Options'][0]['CurrentValue']=='reasoner-v2' and len(rows('sets.jsonl'))==before+1,loaded
 assert sum(v['sid']==sid for v in rows('prompts.jsonl'))==4
 ok('completed snapshots persist through Host restart; native load revalidates configured selection before only the new prompt, without replaying old prompts')
 # An unconfirmed configuration attempt fences the old conversation even after a new explicit submission.
 broken='uncertain-native-setting';value=complete('lost',conversation=broken);before=len(rows('sets.jsonl'));again=complete('lost',conversation=broken);assert value['State']==6 and again['State']==6 and len(rows('sets.jsonl'))==before
 ok('uncertain setting outcome fences the original native context and a later task cannot silently retry or replace it')
finally:
 stop()
 for name,data in [('results',passed),('observations',observations),('wire',rows('wire.jsonl')),('events',dict(sets=rows('sets.jsonl'),prompts=rows('prompts.jsonl')))]: (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
