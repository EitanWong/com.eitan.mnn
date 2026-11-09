#!/usr/bin/env python3
"""Verify managed backend profiles over the real host, with independently checked ACP processes."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse,json,os,shutil,socket,subprocess,sys,time,urllib.request,uuid
from pathlib import Path
SAMPLE=PACKAGE_ROOT;ROOT=PROJECT_ROOT
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);p.add_argument('--host',type=Path,default=ROOT/'TestArtifacts~/SiriAIHarness/Host/bin/Debug/net8.0/SiriAI.HarnessHost.dll');a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);run=OUT/('run-'+uuid.uuid4().hex);run.mkdir();bins=run/'bin space';bins.mkdir()
TOKEN=uuid.uuid4().hex*2
ENV=dict(os.environ,SIRIAI_PROFILE_HOST_TOKEN=TOKEN,PATH=str(bins)+os.pathsep+os.environ.get('PATH',''))
# Native CLI and adapter names are distinct: wrong selection will fail argv and environment assertions.
names=['qwen','opencode','openclaw','qodercli','mcode','kimi','hermes','codebuddy','codex','codex-acp','claude','claude-agent-acp','dsh','pi','pi-acp']
for name in names:
 wrapper=bins/name;wrapper.write_text('#!'+sys.executable+'\nimport runpy\nrunpy.run_path('+repr(str(SAMPLE/'Tools~/HarnessChecks/Fixtures/backend_profile_fixture.py'))+',run_name="__main__")\n');wrapper.chmod(0o700)
expected={
 'qwen':('qwen',['--acp'],['OPENAI_API_KEY','QWEN_CODE_TEST']),
 'qoder':('qodercli',['--acp'],['QODER_TEST']),
 'opencode':('opencode',['acp'],['OPENCODE_TEST']),
 'minimax':('mcode',['acp'],['MINIMAX_TEST']),
 'kimi':('kimi',['acp'],['KIMI_TEST']),
 'hermes':('hermes',['acp','--accept-hooks'],['HERMES_TEST']),
 'codebuddy':('codebuddy',['--acp'],['CODEBUDDY_TEST']),
 'deepseek':('dsh',['--profile','acp'],['DEEPSEEK_API_KEY','DSH_TEST']),
 'codex':('codex-acp',[],['OPENAI_API_KEY','CODEX_TEST']),
 'claude':('claude-agent-acp',[],['ANTHROPIC_API_KEY','CLAUDE_TEST']),
 'pi':('pi-acp',[],['ANTHROPIC_API_KEY','OPENAI_API_KEY','PI_TEST']),
 'openclaw':('openclaw',['acp','--url','ws://127.0.0.1:18789','--verbose'],['OPENCLAW_TEST']),
 'acp':('qwen',[],[])}
secrets=sorted(set(x for _,_,names in expected.values() for x in names))
ENV.update({name:'fixture-'+name for name in secrets})
ENV['UNRELATED_PROVIDER_SECRET']='do-not-forward'
ENV['TMPDIR']=str(run/'tmp space');(run/'tmp space').mkdir()
records=[];passed=[]
def cli(args,expected=0):
 cmd=['dotnet',str(a.host),*args];r=subprocess.run(cmd,env=ENV,text=True,capture_output=True,timeout=35)
 records.append(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode))
 (OUT/'commands.json').write_text(json.dumps(records,indent=2))
 assert r.returncode==expected,records[-1]
 assert TOKEN not in r.stdout+r.stderr
 return json.loads(r.stdout) if r.returncode==0 else r.stderr

def ok(text):passed.append(text);print('PASS '+text,flush=True)
def http(url,path,body=None):
 request=urllib.request.Request(url+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+TOKEN,'Content-Type':'application/json'})
 with urllib.request.urlopen(request,timeout=5) as r:return json.loads(r.read())
def one(backend,mode='native',missing=False,unconfirmed=False,always_only=False):
 folder=run/(backend+'-'+mode+('-missing' if missing else '-unconfirmed' if unconfirmed else '-always' if always_only else ''));folder.mkdir()
 with socket.socket() as s:s.bind(('127.0.0.1',0));port=s.getsockname()[1]
 url='http://127.0.0.1:'+str(port);command,args,names=expected[backend];args=args.copy()
 if mode=='full' and backend in ['qoder','codebuddy']:args+=['--dangerously-skip-permissions']
 values={'TMPDIR':ENV['TMPDIR']};setting=None
 if backend=='codex':values['CODEX_PATH']=str(bins/'codex')
 if backend=='claude':values['CLAUDE_CODE_EXECUTABLE']=str(bins/'claude')
 if backend=='pi':values.update(PI_BIN=str(bins/'pi'),PI_ACP_PI_COMMAND=str(bins/'pi'))
 if backend=='codex' and mode=='full':values['INITIAL_AGENT_MODE']='agent-full-access'
 if backend=='minimax' and mode=='full':setting=['permissionMode','bypassPermissions']
 if backend=='kimi' and mode=='full':setting=['mode','auto']
 expectation=folder/'expect.json';capture=folder/'wire.json'
 expectation.write_text(json.dumps(dict(arguments=args,present=names,absent=[n for n in secrets if n not in names]+['SIRIAI_PROFILE_HOST_TOKEN','UNRELATED_PROVIDER_SECRET'],values=values,setting=setting,missing_option=missing,unconfirmed=unconfirmed,always_only=always_only,request_permission=backend=='qwen' and mode=='full')))
 ENV['SIRIAI_PROFILE_EXPECTATION']=str(expectation);ENV['SIRIAI_PROFILE_CAPTURE']=str(capture)
 harness=dict(Id='chosen',Backend=backend,PermissionMode=mode,ForwardEnvironment=['SIRIAI_PROFILE_EXPECTATION','SIRIAI_PROFILE_CAPTURE'])
 if backend=='acp':harness['Command']=str(bins/'qwen')
 if backend=='openclaw':
  harness['Endpoint']='http://127.0.0.1:18789'
  harness['TokenEnvironmentVariable']='CUSTOM_GATEWAY_CREDENTIAL'
  ENV['CUSTOM_GATEWAY_CREDENTIAL']='fixture-gateway'
  value=json.loads(expectation.read_text());value['values']['OPENCLAW_GATEWAY_TOKEN']='fixture-gateway';expectation.write_text(json.dumps(value))
 config=folder/'input.json';config.write_text(json.dumps(dict(Listen=url,DataDirectory=str(folder/'data'),Identities=[dict(OwnerId='profile-owner',TokenEnvironmentVariable='SIRIAI_PROFILE_HOST_TOKEN')],Workspaces=dict(test=str(folder)),Harnesses=[harness])))
 registration=folder/'service'
 prepare_args=['--service','prepare','--id','profile','--config',str(config),'--runtime',str(Path(shutil.which('dotnet')).resolve()),'--directory',str(registration)]
 cli(prepare_args)
 snapshot={p.name:p.read_bytes() for p in registration.iterdir() if p.name!='prepare.lock'}
 cli(prepare_args)
 assert snapshot=={p.name:p.read_bytes() for p in registration.iterdir() if p.name!='prepare.lock'}
 prepared=json.loads((registration/'host.json').read_text());resolved=prepared['Harnesses'][0]
 assert resolved['Command']==str(bins/command) and resolved['Arguments']==args,resolved
 assert resolved['PermissionMode']==('full' if backend=='pi' else mode)
 log=(folder/'host.log').open('w');proc=subprocess.Popen(['dotnet',str(a.host),'--service','run','--directory',str(registration)],env={k:v for k,v in os.environ.items() if k not in secrets},stdout=log,stderr=log)
 try:
  end=time.monotonic()+12
  while True:
   try:catalog=http(url,'/v1/harnesses');break
   except Exception:
    if proc.poll() is not None or time.monotonic()>end:raise
    time.sleep(.05)
  configured=catalog['configured'][0]
  assert configured['backend']==backend
  assert configured['capabilities']==(59 if backend=='pi' else 63)
  assert configured['permissionMode']==('full' if backend=='pi' else mode)
  task=http(url,'/v1/tasks',dict(ConversationId='profiles',SubmissionKey=uuid.uuid4().hex,HarnessId='chosen',WorkspaceKey='test',Objective='Profile task'))
  while True:
   value=http(url,'/v1/tasks/'+task['Id'])
   if value['State'] in [3,4,5,6]:break
   assert time.monotonic()<end,value
   time.sleep(.04)
  wire=json.loads(capture.read_text()) if capture.exists() else {}
  if missing or unconfirmed:assert value['State']==4 and not wire.get('prompted'),(value,wire)
  else:
   assert value['State']==3 and value['Result']=='profile completed',(value,wire)
   if setting:assert wire['methods'].index('session/set_config_option')<wire['methods'].index('session/prompt')
   if backend=='qwen' and mode=='full':assert wire['permission_confirmed']
  ok(backend+' '+mode+(' rejects an unadvertised session setting before prompt' if missing else ' rejects an unconfirmed session setting before prompt' if unconfirmed else ' resolves correct transport, scoped environment and completes via ACP'))
 finally:
  proc.terminate()
  try:proc.wait(timeout=8)
  except subprocess.TimeoutExpired:proc.kill();proc.wait(timeout=5)
  log.close()
try:
 for backend in expected:one(backend)
 for backend in ['qoder','codebuddy','minimax','kimi','codex','qwen']:one(backend,'full')
 one('minimax','full',True)
 one('kimi','full',unconfirmed=True)
 one('qwen','full',always_only=True)
 catalog=cli(['--backend-profiles'])
 discovery=cli(['--discover'])
 assert {item['id'] for item in discovery}>={'codex','claude','pi','openclaw','muse','dotcraft'}
 assert all(not item['interoperabilityVerified'] for item in discovery)
 assert next(item for item in discovery if item['id']=='codex')['adapterExecutable']==str(bins/'codex-acp')
 assert not next(item for item in discovery if item['id']=='muse')['protocolCandidate']
 ok('discovery emits typed records with separate adapter candidates and no interoperability claim')
 assert {x['Id'] for x in catalog}>={'muse','llm','dotcraft','http','a2a',*expected.keys()}
 ok('single profile catalog includes all upstream backends and explicit pending integrations')
 source=run/'qwen-native/input.json';base=json.loads(source.read_text())
 for name,patch in [
  ('unknown',dict(Backend='unknown')),
  ('muse',dict(Backend='muse')),
  ('llm',dict(Backend='llm')),
  ('dotcraft',dict(Backend='dotcraft')),
  ('openclaw-full',dict(Backend='openclaw',PermissionMode='full')),
  ('generic-full',dict(Backend='acp',PermissionMode='full',Command=str(bins/'qwen'))),
  ('remote-plaintext',dict(Backend='openclaw',Endpoint='http://example.org:18789')),
  ('url-token',dict(Backend='openclaw',Endpoint='https://example.org/?token=hidden')),
  ('host-credential',dict(Backend='qwen',ForwardEnvironment=['SIRIAI_PROFILE_HOST_TOKEN'])),
  ('token-collision',dict(Backend='openclaw',TokenEnvironmentVariable='SIRIAI_PROFILE_HOST_TOKEN')),
  ('wrong-cli',dict(Backend='codex',Command=str(bins/'codex'))),
  ('wrong-argv',dict(Backend='qwen',Arguments=[]))]:
  value=dict(base);value['Harnesses']=[dict(Id='invalid',**patch)]
  file=run/(name+'.json');file.write_text(json.dumps(value))
  cli(['--service','prepare','--id','negative','--config',str(file),'--runtime',str(Path(shutil.which('dotnet')).resolve()),'--directory',str(run/('rejected-'+name))],expected=2)
 ok('unsupported integrations, invalid permissions/arguments/addresses and host-token forwarding fail before execution')
finally:
 (OUT/'results.json').write_text(json.dumps(dict(passed=passed,run=str(run),vendor_inference=False),indent=2))
