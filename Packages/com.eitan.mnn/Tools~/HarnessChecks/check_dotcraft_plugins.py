#!/usr/bin/env python3
"""Real official DotCraft plugin assembly + independent model peer + task Host."""
import argparse, json, os, shutil, socket, subprocess, sys, time, urllib.error, urllib.request, uuid
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--host',required=True,type=Path);p.add_argument('--dotnet',required=True,type=Path)
p.add_argument('--plugin',required=True,type=Path);p.add_argument('--output',required=True,type=Path)
a=p.parse_args();OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True)
RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();WORK=RUN/'workspace';WORK.mkdir();SECOND=RUN/'second';SECOND.mkdir()
ROOT=RUN/'plugin';(ROOT/'lib').mkdir(parents=True);(ROOT/'.craft-plugin').mkdir()
for suffix in ['.dll','.deps.json']:shutil.copyfile(a.plugin.with_suffix(suffix),ROOT/'lib'/('DotCraftPlugin'+suffix))
(ROOT/'.craft-plugin/plugin.json').write_text(json.dumps(dict(schemaVersion=1,id='siriai.fixture',version='1.0.0',displayName='Independent SiriAI proof',capabilities=['dotnet'],dotnet=dict(minHostVersion='0.0.0',entryAssembly='./lib/DotCraftPlugin.dll',entryType='SiriAI.PluginFixture.Plugin'))))
AUTH=RUN/'authority';AUTH.mkdir();(RUN/'untrusted').mkdir();TOKEN=uuid.uuid4().hex*2;OTHER=uuid.uuid4().hex*2;KEY=uuid.uuid4().hex
ENV=dict(os.environ,SIRIAI_DOTCRAFT_HOST_TOKEN=TOKEN,SIRIAI_DOTCRAFT_OTHER_TOKEN=OTHER,SIRIAI_DOTCRAFT_MODEL_KEY=KEY)
records=[];passed=[];process=None;log=None;peer=None;peerlog=None
def port():
    with socket.socket() as sock:sock.bind(('127.0.0.1',0));return sock.getsockname()[1]
PEER='http://127.0.0.1:'+str(port());URL='http://127.0.0.1:'+str(port());CONFIG=RUN/'host.json';REG=RUN/'service'
base=dict(Backend='dotcraft',Endpoint=PEER+'/v1',Model='fixture-model',TokenEnvironmentVariable='SIRIAI_DOTCRAFT_MODEL_KEY',DotCraftEnabledTools=[],DotCraftPluginRoots=[str(ROOT)],DotCraftPluginAuthorityConfig=str(AUTH/'config.json'),TimeoutMilliseconds=60000)
harnesses=[dict(base,Id='dotcraft'),dict(base,Id='disabled',DotCraftDisabledPlugins=['siriai.fixture']),dict(base,Id='untrusted',DotCraftPluginAuthorityConfig=str(RUN/'untrusted/config.json'))]
CONFIG.write_text(json.dumps(dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=3,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_DOTCRAFT_HOST_TOKEN'),dict(OwnerId='bob',TokenEnvironmentVariable='SIRIAI_DOTCRAFT_OTHER_TOKEN')],Workspaces=dict(test=str(WORK),second=str(SECOND)),Harnesses=harnesses)))
def command(cmd,env=ENV):
    done=subprocess.run([str(v) for v in cmd],env=env,capture_output=True,text=True,timeout=120)
    records.append(dict(command=[str(v) for v in cmd],stdout=done.stdout,stderr=done.stderr,exit_code=done.returncode));return done
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def wait(probe,seconds=40):
    end=time.monotonic()+seconds
    while time.monotonic()<end:
        value=probe()
        if value:return value
        time.sleep(.05)
    raise AssertionError('Timed out waiting for actual plugin behavior')
def wire():
    try:
        with urllib.request.urlopen(PEER+'/__state',timeout=2) as response:return json.load(response)['calls']
    except urllib.error.URLError:return None
def call(path,body=None,token=TOKEN,expected=200):
    req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=15) as response:status,raw=response.status,response.read()
    except urllib.error.HTTPError as error:status,raw=error.code,error.read()
    records.append(dict(path=path,input=body,status=status,output=raw.decode()));assert status==expected,(status,raw)
    assert all(secret.encode() not in raw for secret in [TOKEN,OTHER,KEY]);return json.loads(raw) if raw else None
def start():
    global process,log
    log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w')
    process=subprocess.Popen([str(a.dotnet),str(a.host),'--service','run','--directory',str(REG)],env=os.environ,stdout=log,stderr=log)
    def ready():
        assert process.poll() is None,('Host exited',process.returncode)
        try:return call('/v1/harnesses')
        except urllib.error.URLError:return None
    return wait(ready)
def stop(kill=False):
    global process
    if process:
        process.kill() if kill else process.terminate()
        try:process.wait(timeout=20)
        except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
        process=None;log.close()
def submit(text,token=TOKEN,workspace='test',harness='dotcraft'):
    return call('/v1/tasks',dict(ConversationId='phone',SubmissionKey=uuid.uuid4().hex,HarnessId=harness,WorkspaceKey=workspace,Objective=text),token=token,expected=202)
def current(item,token=TOKEN):return call('/v1/tasks/'+item['Id'],token=token)
def terminal(item,token=TOKEN):
    value=current(item,token);return value if value['State'] in [3,4,5,6] else None
def finished(text,**kwargs):
    item=submit(text,**kwargs);value=wait(lambda:terminal(item,kwargs.get('token',TOKEN)));assert value['State']==3,value;return item,value
def payload(value):
    raw=json.loads(value['Result'])
    if isinstance(raw,list):raw=json.loads(raw[0]['text'])
    return raw
def events():
    return [dict(json.loads(line),file=str(file)) for root in [WORK,SECOND] for file in root.rglob('events.jsonl') for line in file.read_text().splitlines()]
def permission(item):
    value=current(item);return value if value['WaitingFor']==2 else None
def prepare(config=CONFIG,directory=REG):
    return command([a.dotnet,a.host,'--service','prepare','--id','plugin-test','--config',config,'--runtime',a.dotnet.resolve(),'--directory',directory])
try:
    grant=command([a.dotnet,a.plugin,ROOT,AUTH/'config.json',RUN/'grant-workspace']);assert grant.returncode==0,records[-1]
    assert (AUTH/'dotnet-plugin-trust.json').exists();ok('independent administrator grants the SDK accepted bundle through official TrustAsync')
    peerlog=(RUN/'peer.log').open('w');peer=subprocess.Popen([sys.executable,'-u',str(Path(__file__).parent/'Fixtures/dotcraft_model_fixture.py'),PEER.rsplit(':',1)[1]],env=ENV,stdout=peerlog,stderr=peerlog)
    wait(lambda:wire() is not None);result=prepare();assert result.returncode==0,records[-1];start();assert wire()==[]
    first,value=finished('plugin-echo');first_result=value['Result'];assert 'PLUGIN_TOOL_MISSING' not in value['Result'],value
    data=payload(value);assert data['value']=='SIRIAI_PLUGIN_OK:plugin-echo' and data['count']==1,data
    assert data['workspacePath']==str(WORK) and data['threadId'] and data['turnId'] and data['callId']
    assert str(WORK) in data['dataRoot'];ok('configured official managed plugin really executes and returns exact input and native invocation identity')
    _,value=finished('plugin-echo-again');second=payload(value);assert second['count']==2 and second['generationId']==data['generationId']
    _,value=finished('plugin-echo-bob',token=OTHER);bob=payload(value);assert bob['count']==1 and bob['dataRoot']!=data['dataRoot']
    _,value=finished('plugin-echo-second',workspace='second');other=payload(value);assert other['count']==1 and other['dataRoot'] not in [data['dataRoot'],bob['dataRoot']]
    call('/v1/tasks/'+first['Id'],token=OTHER,expected=404);ok('separate owner and workspace runtimes isolate plugin generation, mutable counter, data path and task access')
    before=len(wire());item=submit('plugin-echo-untrusted',harness='untrusted');value=wait(lambda:terminal(item));assert value['State']==4 and 'plugin' in value['Result'].lower() and len(wire())==before,value
    assert len([e for e in events() if e['action']=='activated'])==3;ok('untrusted configured .NET bundle fails readiness before any model request or plugin activation')
    _,value=finished('plugin-echo-disabled',harness='disabled');assert 'PLUGIN_TOOL_MISSING' in value['Result'];assert len([e for e in events() if e['action']=='activated'])==3
    ok('explicitly disabled plugin contributes no tool and never activates despite an existing trust grant')
    for label,allow in [('deny',False),('allow',True)]:
        item=submit('plugin-mutate-'+label);prompt=wait(lambda:permission(item));assert not any(e['action']=='mutated:plugin-mutate-'+label for e in events())
        count=len(wire());call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='yes',Allow=allow),token=OTHER,expected=404);assert len(wire())==count
        call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='yes' if allow else 'no',Allow=allow))
        value=wait(lambda:terminal(item));assert value['State']==3,value
        assert any(e['action']=='mutated:plugin-mutate-'+label for e in events())==allow
    ok('native plugin mutation waits for exact owner permission; denial has no mutation and approval invokes once')
    item=submit('plugin-hold-cancel');wait(lambda:any(e['action']=='hold:plugin-hold-cancel' for e in events()));value=call('/v1/tasks/'+item['Id']+'/cancel',{});assert value['State']==5,value
    wait(lambda:any(e['action']=='cancelled:plugin-hold-cancel' for e in events()));ok('cancellation reaches the executing C# plugin cancellation token and confirms native turn termination')
    item=submit('plugin-hold-crash');wait(lambda:any(e['action']=='hold:plugin-hold-crash' for e in events()));count=len(wire());stop(kill=True);start();value=wait(lambda:terminal(item));assert value['State']==6 and len(wire())==count,value
    assert len([e for e in events() if e['action']=='hold:plugin-hold-crash'])==1;assert current(first)['Result']==first_result
    claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-phone',LeaseSeconds=60));assert claim and claim['Result']==first_result
    ok('reconnected foreground claims the exact persisted plugin result through the existing feedback delivery path')
    _,value=finished('plugin-echo-after-restart');after=payload(value);assert after['count']==1 and after['dataRoot']==data['dataRoot'];ok('crash recovery never replays ambiguous plugin work; new explicit work reactivates with persisted data and fresh volatile state')
    stop();assert any(e['action']=='disposed' for e in events());ok('official Host shutdown disposes generation owned plugin resources')
    # Changed bytes invalidate the native trust grant. Host realm identities also change.
    (ROOT/'changed.txt').write_text('UNAPPROVED_BUNDLE_CHANGE');count=len(wire());start();item=submit('plugin-echo-modified');value=wait(lambda:terminal(item));assert value['State']==4 and len(wire())==count,value
    ok('modified bundle bytes cannot inherit an old fingerprint trust grant or execute a model turn')
    stop()
    # The administrator config parser must reject invalid plugin declarations at entry.
    for label,update in [('null',dict(DotCraftPluginRoots=None)),('missing',dict(DotCraftPluginRoots=[str(RUN/'absent')])),('authority-in-workspace',dict(DotCraftPluginAuthorityConfig=str(WORK/'config.json')))]:
        cfg=json.loads(CONFIG.read_text());cfg['Harnesses'][0].update(update);bad=RUN/(label+'.json');bad.write_text(json.dumps(cfg));failure=prepare(bad,RUN/('bad-'+label));assert failure.returncode!=0,label
    ok('invalid roots and workspace owned trust authorities are rejected before service activation')
finally:
    wire_result=wire() or []
    stop()
    if peer:peer.terminate();peer.wait(timeout=10)
    if peerlog:peerlog.close()
    (OUT/'http.json').write_text(json.dumps(records,indent=2));(OUT/'wire.json').write_text(json.dumps(wire_result,indent=2))
    (OUT/'results.json').write_text(json.dumps(dict(run=str(RUN),passed=passed,events=events()),indent=2))
