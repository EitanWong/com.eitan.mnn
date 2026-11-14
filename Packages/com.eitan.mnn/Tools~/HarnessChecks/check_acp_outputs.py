#!/usr/bin/env python3
"""Observe native output artifacts, terminal outcomes and restart durability via the C# HTTP Host."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse, json, os, socket, subprocess, sys, time, urllib.error, urllib.request, uuid
from pathlib import Path

SAMPLE=PACKAGE_ROOT
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir()
TOKEN=uuid.uuid4().hex*2;OTHER=uuid.uuid4().hex*2
ENV=dict(os.environ,SIRIAI_ACP_OUTPUT_TOKEN=TOKEN,SIRIAI_ACP_OUTPUT_OTHER=OTHER,SIRIAI_ACP_SESSION_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));PORT=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(PORT);CONFIG=RUN/'host.json';REG=RUN/'registration';process=None;log=None;passed=[];records=[]
CONFIG.write_text(json.dumps(dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,
    Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_ACP_OUTPUT_TOKEN'),dict(OwnerId='bob',TokenEnvironmentVariable='SIRIAI_ACP_OUTPUT_OTHER')],
    Workspaces=dict(test=str(RUN)),Harnesses=[dict(Id='acp',Protocol='acp',Command=sys.executable,
    Arguments=['-u',str(SAMPLE/'Tools~/HarnessChecks/Fixtures/acp_session_fixture.py'),'output'],ForwardEnvironment=['SIRIAI_ACP_SESSION_ROOT'],TimeoutMilliseconds=60000)])))
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def wait(test,timeout=20):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        result=test()
        if result:return result
        time.sleep(.025)
    raise AssertionError('Timed out waiting for ACP output behavior')
def call(path,body=None,token=TOKEN,expected=200):
    req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=15) as response:status,raw=response.status,response.read()
    except urllib.error.HTTPError as error:status,raw=error.code,error.read()
    assert status==expected,(path,status,raw)
    assert TOKEN.encode() not in raw and OTHER.encode() not in raw
    return json.loads(raw) if raw else None
def start():
    global process,log
    log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--service','run','--directory',str(REG)],stdout=log,stderr=log,env=os.environ)
    def ready():
        if process.poll() is not None:raise AssertionError('Host exited '+str(process.returncode))
        try:return call('/v1/harnesses')
        except urllib.error.URLError:return None
    return wait(ready)
def stop(kill=False):
    global process
    if process is not None:
        process.kill() if kill else process.terminate()
        try:process.wait(timeout=10)
        except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
        process=None;log.close()
def wire():
    q=PEER/'wire.jsonl';return [json.loads(line) for line in q.read_text().splitlines()] if q.exists() else []
def prompts():return [v['frame'] for v in wire() if v['direction']=='client' and v['frame'].get('method')=='session/prompt']
def submit(text,conversation='output',token=TOKEN,key=None):
    return call('/v1/tasks',dict(HarnessId='acp',WorkspaceKey='test',ConversationId=conversation,SubmissionKey=key or uuid.uuid4().hex,Objective=text),token,202)
def terminal(item):
    value=call('/v1/tasks/'+item['Id']);return value if value['State'] in [3,4,5,6] else None
def partial(item,count):
    value=call('/v1/tasks/'+item['Id'])
    if value['State'] in [3,4,5,6]:raise AssertionError(('terminal before expected partial artifacts',value))
    return value if len(value.get('Artifacts',[]))>=count else None
def parts(value):return [artifact['Parts'][0] for artifact in value['Artifacts']]

try:
    cmd=['dotnet',str(a.host),'--service','prepare','--id','acp-output','--config',str(CONFIG),'--runtime',str(Path('/opt/homebrew/bin/dotnet').resolve()),'--directory',str(REG)]
    result=subprocess.run(cmd,env=ENV,capture_output=True,text=True,timeout=40);records.append(dict(command=cmd,stdout=result.stdout,stderr=result.stderr,exit_code=result.returncode));assert result.returncode==0,records[-1]
    start();item=submit('output-mixed',key='mixed');assert submit('output-mixed',key='mixed')['Id']==item['Id']
    value=wait(lambda:partial(item,2),timeout=3);assert value['WaitingFor']==2 and value['RequestId'];request=value['RequestId'];ids=[x['Id'] for x in value['Artifacts']]
    time.sleep(.1);value=call('/v1/tasks/'+item['Id']);assert value['RequestId']==request and len(value['Artifacts'])==2
    assert parts(value)[0]['Payload']=='AAEC' and parts(value)[1]['Payload']=='原生文件\n内容'
    call('/v1/tasks/'+item['Id'],token=OTHER,expected=404)
    ok('native artifacts become durable partial results while preserving the exact pending approval identity and owner isolation')
    call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=request,Text='Allow once',Allow=True));value=wait(lambda:terminal(item))
    assert value['State']==3 and value['Result']=='Files ready. Done.' and len(value['Artifacts'])==10,value
    assert [x['Id'] for x in value['Artifacts'][:2]]==ids and len({x['Id'] for x in value['Artifacts']})==10
    actual=parts(value)
    assert [x['Kind'] for x in actual]==[3,0,3,3,1,2,3,0,3,0]
    assert [x['Payload'] for x in actual[:5]]==['AAEC','原生文件\n内容','AwQF','BgcI','https://example.invalid/report.pdf']
    assert actual[0]['FileName']=='图.png' and actual[2]['FileName']=='agent-output-3'
    native_link=json.loads(actual[5]['Payload']);assert native_link['uri']=='file:///private/report.txt' and native_link['type']=='resource_link'
    assert actual[6]['Payload']=='CQoL' and actual[6]['MediaType']=='application/octet-stream'
    assert [x['Payload'] for x in actual[7:]]==['','',' \n ']
    original=value;count=len(prompts());stop();start()
    assert call('/v1/tasks/'+item['Id'])['Artifacts']==original['Artifacts'] and len(prompts())==count
    ok('mixed text/image/audio/resource/link/data-URI and empty files survive completion and host restart with exact bytes and stable artifact identities')
    follow=submit('text-after-output');value=wait(lambda:terminal(follow));assert value['State']==3 and value['Artifacts']==[] and 'HISTORY' not in value['Result'],value
    only=submit('output-only');value=wait(lambda:terminal(only));assert value['State']==3 and len(value['Artifacts'])==1 and value['Result'],value
    assert 'HISTORY' not in json.dumps(value['Artifacts'])
    duplicate=submit('output-duplicate');value=wait(lambda:terminal(duplicate));assert value['State']==3 and len(value['Artifacts'])==2 and value['Artifacts'][0]['Id']!=value['Artifacts'][1]['Id']
    ok('artifact-only completion succeeds, each native content block is retained, and subsequent prompts or loaded history do not leak previous artifacts')
    links=submit('output-links');value=wait(lambda:terminal(links));assert value['State']==3 and len(value['Artifacts'])==6,value
    actual=parts(value);assert [x['Payload'] for x in actual[1:3]]==['/wArKw==','AQI=']
    assert json.loads(actual[3]['Payload'])['uri']=='urn:mnn:report' and actual[4]['FileName']=='🙂'*256 and value['Artifacts'][4]['Name']=='🙂'*120
    assert actual[5]['Payload']=='5L2g5aW9' and actual[5]['MediaType']=='text/plain;charset=utf-8',actual[5]
    count=len(prompts());before=len(call('/v1/tasks'))
    for kind,payload in [(0,''),(0,'   '),(3,'')]:
        call('/v1/tasks',dict(HarnessId='acp',WorkspaceKey='test',ConversationId='empty-input',SubmissionKey=uuid.uuid4().hex,Objective='invalid-input',Attachments=[dict(Kind=kind,Payload=payload)]),expected=400)
    assert len(call('/v1/tasks'))==before and len(prompts())==count
    ok('data URI percent bytes and opaque references remain exact, Unicode filenames keep their full metadata, and empty output support does not permit empty task inputs')
    for objective,limit in [('output-invalid-base64',1),('output-missing-mime',1),('output-unknown',1),('output-invalid-resource',1),('output-long-name',1),('output-url-credentials',1),('output-count-overflow',32),('output-bytes-overflow',2),('output-escaped-overflow',2)]:
        bad=submit(objective,conversation=objective);value=wait(lambda:terminal(bad));assert value['State']==4 and 'output' in value['Result'].lower() and len(value['Artifacts'])==limit,(objective,value)
        good=submit('safe-text',conversation=objective);assert wait(lambda:terminal(good))['State']==3
    ok('invalid or unsupported native content and artifact count/byte overflow fail explicitly after confirmed terminal while retaining bounded valid partial output')
    cancel=submit('output-cancel',conversation='cancel');wait(lambda:partial(cancel,1));value=call('/v1/tasks/'+cancel['Id']+'/cancel',{});assert value['State']==5 and len(value['Artifacts'])==1,value
    disconnected=submit('output-disconnect',conversation='disconnect');value=wait(lambda:terminal(disconnected));assert value['State']==6 and len(value['Artifacts'])==1,value
    invalid_disconnect=submit('output-invalid-disconnect',conversation='invalid-disconnect');value=wait(lambda:terminal(invalid_disconnect));assert value['State']==6 and len(value['Artifacts'])==1 and 'output' in value['Result'].lower(),value
    ok('confirmed cancellation and transport loss retain known partial artifacts while preserving Cancelled versus Interrupted outcomes')
    crash=submit('output-crash',conversation='crash');wait(lambda:partial(crash,1));count=len(prompts());stop(kill=True);start();value=wait(lambda:terminal(crash));assert value['State']==6 and len(value['Artifacts'])==1 and len(prompts())==count,value
    fenced=submit('unsafe-followup',conversation='crash');assert wait(lambda:terminal(fenced))['State']==6 and len(prompts())==count
    claim=call('/v1/results/claim',dict(ConversationId='output',ClientId='reconnected-phone',LeaseSeconds=60));assert claim['Id']==item['Id'] and claim['Artifacts']==original['Artifacts']
    ok('cold recovery keeps persisted partial artifacts without replay and reconnected foreground claims the completed task with all native artifacts')
    assert not (PEER/'errors.txt').exists()
finally:
    transcript=wire();stop();(OUT/'results.json').write_text(json.dumps(passed,indent=2)+'\n');(OUT/'wire.json').write_text(json.dumps(transcript,indent=2)+'\n');(OUT/'commands.json').write_text(json.dumps(records,indent=2)+'\n')
