#!/usr/bin/env python3
"""Independent marketplace ZIP, managed plugin, model peer and real C# Host."""
import argparse, hashlib, json, os, socket, subprocess, sys, time, urllib.error, urllib.request, uuid, zipfile
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--dotnet',required=True,type=Path);p.add_argument('--plugin',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();WORK=RUN/'workspace';WORK.mkdir();AUTH=RUN/'authority';AUTH.mkdir();ZIP=RUN/'registry.zip'
TOKEN=uuid.uuid4().hex*2;OTHER=uuid.uuid4().hex*2;KEY=uuid.uuid4().hex;ENV=dict(os.environ,SIRIAI_DOTCRAFT_HOST_TOKEN=TOKEN,SIRIAI_DOTCRAFT_OTHER_TOKEN=OTHER,SIRIAI_DOTCRAFT_MODEL_KEY=KEY,DOTCRAFT_PLUGIN_REGISTRIES=str(RUN/'must-not-be-discovered'))
def port():
    with socket.socket() as sock:sock.bind(('127.0.0.1',0));return sock.getsockname()[1]
PEER='http://127.0.0.1:'+str(port());URL='http://127.0.0.1:'+str(port());CONFIG=RUN/'host.json';REGISTRY=RUN/'registries.json'
cfg=dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_DOTCRAFT_HOST_TOKEN'),dict(OwnerId='bob',TokenEnvironmentVariable='SIRIAI_DOTCRAFT_OTHER_TOKEN')],Workspaces=dict(test=str(WORK)),Harnesses=[dict(Id='dotcraft',Backend='dotcraft',Endpoint=PEER+'/v1',Model='fixture-model',TokenEnvironmentVariable='SIRIAI_DOTCRAFT_MODEL_KEY',DotCraftPluginRegistryConfig=str(REGISTRY),DotCraftPluginAuthorityConfig=str(AUTH/'config.json'),TimeoutMilliseconds=60000)])
CONFIG.write_text(json.dumps(cfg));REGISTRY.write_text(json.dumps(dict(Plugins=dict(DisableDefaultPluginRegistry=True,PluginRegistries=[dict(Name='independent-market',SourceType='archive',Url=str(ZIP))]))))
def archive(version='1.0.0',extra=None):
    manifest=dict(schemaVersion=1,id='siriai.fixture',version=version,displayName='Registry proof',capabilities=['dotnet'],dotnet=dict(minHostVersion='0.0.0',entryAssembly='./lib/DotCraftPlugin.dll',entryType='SiriAI.PluginFixture.Plugin'))
    market=dict(name='independent-market',plugins=[dict(name='siriai.fixture',source=dict(source='local',path='./plugins/siriai.fixture'),policy=dict(installation='AVAILABLE'))])
    with zipfile.ZipFile(ZIP,'w',zipfile.ZIP_DEFLATED) as ar:
        ar.writestr('registry/.craft/plugins/marketplace.json',json.dumps(market));ar.writestr('registry/plugins/siriai.fixture/.craft-plugin/plugin.json',json.dumps(manifest))
        for suffix in ['.dll','.deps.json']:ar.write(a.plugin.with_suffix(suffix),'registry/plugins/siriai.fixture/lib/DotCraftPlugin'+suffix)
        if extra:ar.writestr(*extra)
archive();records=[];passed=[];process=None;log=None;peer=None;peerlog=None
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def command(args,expected=0,environment=None):
    cmd=[str(v) for v in args];result=subprocess.run(cmd,env=ENV if environment is None else environment,capture_output=True,text=True,timeout=100);records.append(dict(command=cmd,stdout=result.stdout,stderr=result.stderr,exit_code=result.returncode));assert result.returncode==expected,records[-1]
    assert all(secret not in result.stdout+result.stderr for secret in [TOKEN,OTHER,KEY]);return json.loads(result.stdout) if expected==0 else result
def admin(action,*options,expected=0):return command([a.dotnet,a.host,'--plugins',action,'--config',CONFIG,'--harness','dotcraft',*options],expected)
def tree(root):return {str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in root.rglob('*') if p.is_file()}
def wait(probe):
    deadline=time.monotonic()+45
    while time.monotonic()<deadline:
        value=probe()
        if value:return value
        time.sleep(.05)
    raise AssertionError('Timed out waiting for native registry/plugin behavior')
def wire():
    try:
        with urllib.request.urlopen(PEER+'/__state',timeout=2) as response:return json.load(response)['calls']
    except urllib.error.URLError:return None
def call(path,body=None,token=TOKEN,expected=200):
    request=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(request,timeout=15) as response:status,raw=response.status,response.read()
    except urllib.error.HTTPError as error:status,raw=error.code,error.read()
    records.append(dict(path=path,input=body,status=status,output=raw.decode()));assert status==expected,(status,raw);assert all(secret.encode() not in raw for secret in [TOKEN,OTHER,KEY]);return json.loads(raw) if raw else None
def start():
    global process,log
    log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen([str(a.dotnet),str(a.host),'--config',str(CONFIG)],env=ENV,stdout=log,stderr=log)
    def ready():
        assert process.poll() is None,process.returncode
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
def submit(text,token=TOKEN):return call('/v1/tasks',dict(ConversationId='phone',SubmissionKey=uuid.uuid4().hex,HarnessId='dotcraft',WorkspaceKey='test',Objective=text),token,202)
def current(item,token=TOKEN):return call('/v1/tasks/'+item['Id'],token=token)
def terminal(item,token=TOKEN):
    value=current(item,token);return value if value['State'] in [3,4,5,6] else None
def finished(text,token=TOKEN):
    item=submit(text,token);value=wait(lambda:terminal(item,token));assert value['State']==3,value;return item,value
def events():return [json.loads(line) for f in WORK.rglob('events.jsonl') for line in f.read_text().splitlines()]
try:
    catalog=admin('catalog');assert len(catalog['Plugins'])==1,catalog;assert 'must-not-be-discovered' not in json.dumps(catalog);entry=catalog['Plugins'][0];assert entry['Id']=='siriai.fixture' and entry['Marketplace']=='independent-market' and entry['Version']=='1.0.0';HOME=Path(catalog['Home']);assert not (HOME/'plugins/siriai.fixture').exists()
    ok('official catalog resolves the configured independent ZIP marketplace without installing or executing code')
    registry_bytes=REGISTRY.read_bytes();saved=tree(HOME)
    try:
        valid=json.loads(registry_bytes)['Plugins']['PluginRegistries'][0]
        for value in [[valid,None],[valid,dict(Name='invalid')],dict(Name='not-an-array')]:
            REGISTRY.write_text(json.dumps(dict(Plugins=dict(PluginRegistries=value))));admin('catalog',expected=2);assert tree(HOME)==saved
    finally:REGISTRY.write_bytes(registry_bytes)
    ok('malformed registry entries fail explicitly without changing the accepted management data')
    admin('install','--plugin','siriai.fixture','--expected-hash','0'*64,expected=2);assert not (HOME/'plugins/siriai.fixture').exists()
    installed=admin('install','--plugin','siriai.fixture','--expected-hash',entry['ContentHash']);ROOT=Path(installed['Root']);assert (ROOT/'.builtin').exists() and (ROOT/'lib/DotCraftPlugin.dll').read_bytes()==a.plugin.read_bytes();before=tree(ROOT)
    again=admin('install','--plugin','siriai.fixture','--expected-hash',entry['ContentHash']);assert again['Root']==str(ROOT) and tree(ROOT)==before
    assert not list((RUN/'data').rglob('dotnet-plugin-trust.json'));ok('explicit official deployment copies the exact package, rejects stale hashes, is idempotent across CLI processes and grants no execution trust')
    saved=tree(HOME);archive(extra=('../../registry-escaped.txt','MUST_NOT_WRITE'));admin('catalog',expected=2);assert tree(HOME)==saved and not (RUN/'registry-escaped.txt').exists()
    archive();blob=bytearray(ZIP.read_bytes());index=blob.index(b'PK\x01\x02');blob[index+24:index+28]=(512*1024*1024+1).to_bytes(4,'little');ZIP.write_bytes(blob);admin('catalog',expected=2);assert tree(HOME)==saved
    archive();admin('install','--plugin','../escape','--expected-hash',entry['ContentHash'],expected=2);ok('traversal and oversized archive metadata fail before extraction; installed package and accepted cache remain intact')
    cfg['Harnesses'][0]['DotCraftPluginRoots']=[str(ROOT)];CONFIG.write_text(json.dumps(cfg));peerlog=(RUN/'peer.log').open('w');peer=subprocess.Popen([sys.executable,'-u',str(Path(__file__).parent/'Fixtures/dotcraft_model_fixture.py'),PEER.rsplit(':',1)[1]],env=ENV,stdout=peerlog,stderr=peerlog);wait(lambda:wire() is not None);start();count=len(wire())
    admin('install','--plugin','siriai.fixture','--expected-hash',entry['ContentHash'],expected=2);assert tree(ROOT)==before;item=submit('plugin-echo-untrusted');value=wait(lambda:terminal(item));assert value['State']==4 and len(wire())==count,value;untrusted=item;untrusted_result=value['Result']
    stop();command([a.dotnet,a.plugin,ROOT,AUTH/'config.json',RUN/'grant-workspace']);start();first,value=finished('plugin-echo-registry');original=value['Result'];data=json.loads(original);assert data['value']=='SIRIAI_PLUGIN_OK:plugin-echo-registry' and data['count']==1 and data['workspacePath']==str(WORK)
    ok('running Host excludes package writers; installed managed plugin stays blocked until independent official trust and then executes through the actual Agent')
    _,value=finished('plugin-echo-registry-bob',OTHER);bob=json.loads(value['Result']);assert bob['count']==1 and bob['dataRoot']!=data['dataRoot'];call('/v1/tasks/'+first['Id'],token=OTHER,expected=404)
    item=submit('plugin-mutate-registry')
    def permission():
        value=current(item);return value if value['WaitingFor']==2 else None
    prompt=wait(permission);assert not any(e['action']=='mutated:plugin-mutate-registry' for e in events());call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Allow once',Allow=True));assert wait(lambda:terminal(item))['State']==3
    assert len([e for e in events() if e['action']=='mutated:plugin-mutate-registry'])==1;ok('registry installation preserves official invocation metadata, owner state boundaries and RequiresApproval policy')
    item=submit('plugin-hold-registry-crash');wait(lambda:any(e['action']=='hold:plugin-hold-registry-crash' for e in events()));count=len(wire());stop(kill=True);start();assert wait(lambda:terminal(item))['State']==6 and len(wire())==count;assert current(first)['Result']==original
    claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-phone',LeaseSeconds=60));assert claim and claim['Id']==untrusted['Id'] and claim['Result']==untrusted_result
    call('/v1/tasks/'+claim['Id']+'/receipt',dict(LeaseId=claim['LeaseId'],Receipt=4))
    claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-phone',LeaseSeconds=60));assert claim and claim['Id']==first['Id'] and claim['Result']==original;stop();ok('Host crash never replays installed plugin work; completed results survive and reach reconnected foreground')
    archive('1.0.1');synced=admin('sync');assert synced['Action']=='sync';stamp=json.loads((HOME/'config.json').read_text())['Plugins']['PluginRegistries'][0]['LastUpdated'];assert stamp
    new=admin('catalog')['Plugins'][0];assert json.loads((HOME/'config.json').read_text())['Plugins']['PluginRegistries'][0].get('LastUpdated')==stamp;assert new['Version']=='1.0.1' and new['ContentHash']!=entry['ContentHash'];assert tree(ROOT)==before
    admin('update','--plugin','siriai.fixture','--expected-hash',entry['ContentHash'],expected=2);updated=admin('update','--plugin','siriai.fixture','--expected-hash',new['ContentHash']);assert updated['ContentHash']==new['ContentHash'];start();count=len(wire());item=submit('plugin-echo-updated-untrusted');assert wait(lambda:terminal(item))['State']==4 and len(wire())==count;stop()
    command([a.dotnet,a.plugin,ROOT,AUTH/'config.json',RUN/'updated-grant']);start();_,value=finished('plugin-echo-updated');assert 'SIRIAI_PLUGIN_OK:plugin-echo-updated' in value['Result'];assert current(first)['Result']==original;stop();ok('explicit refresh and pinned update install new bytes without inheriting old code trust or losing prior results')
    service=RUN/'service'
    command([a.dotnet,a.host,'--service','prepare','--id','registry-test','--config',CONFIG,'--runtime',a.dotnet.resolve(),'--directory',service])
    cold=command([a.dotnet,a.host,'--plugins','catalog','--directory',service,'--harness','dotcraft'],environment=dict(os.environ));assert cold['Plugins'][0]['ContentHash']==new['ContentHash']
    assert json.loads((HOME/'config.json').read_text())['Plugins']['PluginRegistries'][0].get('LastUpdated')==stamp
    command([a.dotnet,a.host,'--plugins','catalog','--config',CONFIG,'--directory',service,'--harness','dotcraft'],expected=2)
    command([a.dotnet,a.host,'--plugins','install','--config',CONFIG,'--harness','dotcraft','--plugin','../escape','--expected-hash',new['ContentHash']],expected=2)
    ok('prepared service credentials support explicit cold administration; catalog reads retain native refresh metadata and invalid options are rejected')
finally:
    wire_result=wire() or [];stop()
    if peer:peer.terminate();peer.wait(timeout=10)
    if peerlog:peerlog.close()
    (OUT/'http.json').write_text(json.dumps(records,indent=2));(OUT/'wire.json').write_text(json.dumps(wire_result,indent=2));(OUT/'results.json').write_text(json.dumps(dict(run=str(RUN),passed=passed,events=events()),indent=2))
