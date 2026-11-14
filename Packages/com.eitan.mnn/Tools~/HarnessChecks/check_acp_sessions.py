#!/usr/bin/env python3
"""Observe native ACP continuity through the actual durable C# HTTP host."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse,json,os,socket,subprocess,sys,time,urllib.error,urllib.request,uuid
from pathlib import Path
SAMPLE=PACKAGE_ROOT
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--output',required=True,type=Path);p.add_argument('--mode',choices=['load','resume','none','load-fails','capability-drop','timeout','ignore-cancel','admission-lost','capacity','capacity-none'],default='load');a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();(RUN/'other').mkdir()
TOKEN=uuid.uuid4().hex*2;OTHER=uuid.uuid4().hex*2
ENV=dict(os.environ,SIRIAI_ACP_SESSION_TOKEN=TOKEN,SIRIAI_ACP_SESSION_OTHER=OTHER,SIRIAI_ACP_SESSION_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));PORT=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(PORT);CONFIG=RUN/'host.json';REG=RUN/'registration'
config=dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_ACP_SESSION_TOKEN'),dict(OwnerId='bob',TokenEnvironmentVariable='SIRIAI_ACP_SESSION_OTHER')],Workspaces=dict(test=str(RUN),other=str(RUN/'other')),Harnesses=[dict(Id='acp',Protocol='acp',Command=sys.executable,Arguments=['-u',str(SAMPLE/'Tools~/HarnessChecks/Fixtures/acp_session_fixture.py'),a.mode],ForwardEnvironment=['SIRIAI_ACP_SESSION_ROOT'],TimeoutMilliseconds=250 if a.mode in ['timeout','ignore-cancel','admission-lost'] else 60000)])
CONFIG.write_text(json.dumps(config));records=[];passed=[];process=None;log=None
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def wait(test,timeout=20):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        value=test()
        if value:return value
        time.sleep(.03)
    raise AssertionError('Timed out waiting for ACP session behavior')
def cli():
    cmd=['dotnet',str(a.host),'--service','prepare','--id','acp-sessions','--config',str(CONFIG),'--runtime',str(Path('/opt/homebrew/bin/dotnet').resolve()),'--directory',str(REG)]
    r=subprocess.run(cmd,env=ENV,text=True,capture_output=True,timeout=40);records.append(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));(OUT/'commands.json').write_text(json.dumps(records,indent=2));assert r.returncode==0,records[-1]
def call(path,body=None,token=TOKEN,expected=200):
    request=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(request,timeout=18) as response:status,raw=response.status,response.read()
    except urllib.error.HTTPError as e:status,raw=e.code,e.read()
    assert status==expected,(path,status,raw)
    assert TOKEN.encode() not in raw and OTHER.encode() not in raw
    return json.loads(raw) if raw else None
def start():
    global process,log
    log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--service','run','--directory',str(REG)],env=os.environ,stdout=log,stderr=log)
    def ready():
        if process.poll() is not None:raise AssertionError('host exited '+str(process.returncode))
        try:return call('/v1/harnesses')
        except urllib.error.URLError:return None
    return wait(ready)
def stop(kill=False):
    global process
    if process is not None:
        process.kill() if kill else process.terminate()
        try:process.wait(timeout=12)
        except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
        process=None;log.close()
def wire():return [json.loads(line) for line in (PEER/'wire.jsonl').read_text().splitlines()] if (PEER/'wire.jsonl').exists() else []
def methods(method):return [x for x in wire() if x['direction']=='client' and x['frame'].get('method')==method]
def submit(text,key=None,token=TOKEN,conversation='phone',workspace='test'):
    return call('/v1/tasks',dict(ConversationId=conversation,SubmissionKey=key or uuid.uuid4().hex,HarnessId='acp',WorkspaceKey=workspace,Objective=text),token=token,expected=202)
def terminal(item,token=TOKEN):
    value=call('/v1/tasks/'+item['Id'],token=token);return value if value['State'] in [3,4,5,6] else None
def state(item,token=TOKEN):return json.loads(call('/v1/tasks/'+item['Id'],token=token).get('BackendState') or '{}')
try:
    cli();catalog=start();assert not wire()
    if a.mode=='admission-lost':
        item=submit('unknown-admission');assert wait(lambda:terminal(item))['State']==6
        assert len(methods('session/new'))==1 and not methods('session/prompt')
        stop();start();follow=submit('unsafe');assert wait(lambda:terminal(follow))['State']==6
        assert len(methods('session/new'))==1 and not methods('session/prompt')
        ok('lost session creation acknowledgement remains durably fenced even without a task checkpoint')
        sys.exit(0)
    if a.mode in ['capacity','capacity-none']:
        first=submit('first',conversation='context-0');assert wait(lambda:terminal(first))['State']==3
        for i in range(1,16 if a.mode=='capacity-none' else 18):
            item=submit('other',conversation='context-'+str(i));assert wait(lambda:terminal(item))['State']==3
        if a.mode=='capacity-none':
            item=submit('too-many',conversation='context-16');assert wait(lambda:terminal(item))['State']==4
            assert len(methods('session/new'))==16
        follow=submit('restored',conversation='context-0');value=wait(lambda:terminal(follow));assert value['State']==3 and value['Result']=='native result: restored; prior=1',value
        assert len(methods('session/new'))==(16 if a.mode=='capacity-none' else 18) and len(methods('session/load'))==(0 if a.mode=='capacity-none' else 1)
        ok('bounded live process cache preserves non-resumable context or reloads the same evicted resumable native session')
        sys.exit(0)
    if a.mode in ['timeout','ignore-cancel']:
        item=submit('hold');value=wait(lambda:terminal(item));assert value['State']==(4 if a.mode=='timeout' else 6),value
        assert len(methods('session/prompt'))==1 and len(methods('session/cancel'))==1
        if a.mode=='ignore-cancel':
            follow=submit('unsafe');assert wait(lambda:terminal(follow))['State']==6 and len(methods('session/prompt'))==1
        ok('timeout preserves confirmed cancellation versus unconfirmed native termination and never retries an ambiguous prompt')
        sys.exit(0)
    first=submit('slow','intent');assert first['State'] in [0,1];assert submit('slow','intent')['Id']==first['Id'];call('/v1/tasks/'+first['Id'],token=OTHER,expected=404)
    value=wait(lambda:terminal(first));assert value['State']==3 and value['Result']=='native result: slow; prior=0',value
    second=submit('second');value=wait(lambda:terminal(second));assert value['State']==3 and value['Result']=='native result: second; prior=1',value
    assert len(methods('session/new'))==1 and state(first)['SessionId']==state(second)['SessionId']
    ok('nonblocking idempotent tasks reuse the same live native ACP context, including agents without load support')
    if a.mode in ['none','load-fails','capability-drop']:
        stop()
        if a.mode=='capability-drop':(PEER/'drop-capability').touch()
        start();before=len(methods('session/prompt'));item=submit('after-restart');value=wait(lambda:terminal(item));assert value['State']==6,value
        assert len(methods('session/new'))==1 and len(methods('session/prompt'))==before
        ok('unavailable or rejected native resume fences the existing context without a replacement session or prompt')
        sys.exit(0)
    for token,conversation,workspace in [(OTHER,'phone','test'),(TOKEN,'other-chat','test'),(TOKEN,'phone','other')]:
        item=submit('isolated',token=token,conversation=conversation,workspace=workspace);assert wait(lambda:terminal(item,token))['State']==3
    assert len(methods('session/new'))==4
    ok('native session identity separates owner, conversation and canonical workspace')
    item=submit('permission')
    def pending():
        value=call('/v1/tasks/'+item['Id']);return value if value['WaitingFor']==2 else None
    question=wait(pending);time.sleep(.12);assert pending()['RequestId']==question['RequestId']
    execution=call('/v1/tasks/'+item['Id'])['Execution']
    tool_steps=[step for step in execution if step['Kind']==3]
    assert tool_steps and json.loads(tool_steps[-1]['Text'])['toolCallId']=='tool-0' and tool_steps[-1]['State']=='Running',execution
    assert [step['Sequence'] for step in execution]==list(range(1,len(execution)+1)),execution
    ok('native ACP tool updates retain raw public details and contiguous durable execution identities')
    call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=question['RequestId'],Text='Allow once',Allow=True));assert wait(lambda:terminal(item))['State']==3
    ok('pending native permission remains routable while tool activity arrives on the same context')
    item=submit('parallel-permissions');question=wait(pending)
    call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=question['RequestId'],Text='Allow once',Allow=True))
    first_request=question['RequestId'];question=wait(lambda:(lambda q:q if q and q['RequestId']!=first_request else None)(pending()))
    call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=first_request,Text='Allow once',Allow=True),expected=409)
    call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=question['RequestId'],Text='Allow once',Allow=True));assert wait(lambda:terminal(item))['State']==3
    ok('concurrent native permission requests are presented sequentially with separate foreground identities')
    item=submit('permission-cancel');wait(pending);assert call('/v1/tasks/'+item['Id']+'/cancel',{})['State']==5
    follow=submit('after-permission-cancel');assert wait(lambda:terminal(follow))['State']==3
    ok('cancelling an approval wait expires that request without destroying the persistent session')
    item=submit('idle-exit');assert wait(lambda:terminal(item))['State']==3;time.sleep(.15)
    follow=submit('after-idle-exit');value=wait(lambda:terminal(follow));assert value['State']==3 and state(follow)['SessionId']==state(first)['SessionId'],value
    ok('an idle native process can restart and resume its confirmed context without creating another session')
    prior=len(json.loads((PEER/'state.json').read_text())[state(first)['SessionId']]['prompts'])
    stop();start();count=len(methods('session/new'));item=submit('after-restart');value=wait(lambda:terminal(item));assert value['State']==3 and value['Result']=='native result: after-restart; prior='+str(prior),value
    assert len(methods('session/new'))==count and state(item)['SessionId']==state(first)['SessionId']
    chosen='session/resume' if a.mode=='resume' else 'session/load';assert methods(chosen)
    assert 'HISTORY ONLY' not in value['Result'] and call('/v1/tasks/'+first['Id'])['Result']=='native result: slow; prior=0'
    ok('capability-negotiated native resume survives host restart and excludes loaded historical text from the new result')
    for token,conversation,workspace in [(OTHER,'phone','test'),(TOKEN,'other-chat','test'),(TOKEN,'phone','other')]:
        item=submit('isolated-after-restart',token=token,conversation=conversation,workspace=workspace);value=wait(lambda:terminal(item,token))
        assert value['State']==3 and value['Result']=='native result: isolated-after-restart; prior=1',value
    ok('all isolated owners, conversations and workspaces reload their own persisted native histories')
    item=submit('hold');wait(lambda:any(x['frame']['params']['prompt'][0]['text']=='hold' for x in methods('session/prompt')))
    value=call('/v1/tasks/'+item['Id']+'/cancel',{});assert value['State']==5,value
    follow=submit('after-cancel');assert wait(lambda:terminal(follow))['State']==3
    ok('confirmed native cancellation terminates only the current task and preserves subsequent conversation use')
    item=submit('cancel-race');wait(lambda:any(x['frame']['params']['prompt'][0]['text']=='cancel-race' for x in methods('session/prompt')))
    value=call('/v1/tasks/'+item['Id']+'/cancel',{});assert value['State']==3 and 'cancel-race' in value['Result'],value
    ok('completed native prompt wins a cancellation race without becoming a false Cancelled result')
    item=submit('crash');wait(lambda:any(x['frame']['params']['prompt'][0]['text']=='crash' for x in methods('session/prompt')));count=len(methods('session/prompt'));stop(kill=True);start()
    value=wait(lambda:terminal(item));assert value['State']==6 and len(methods('session/prompt'))==count,value
    follow=submit('unsafe-followup');assert wait(lambda:terminal(follow))['State']==6
    assert len(methods('session/prompt'))==count
    fresh=submit('fresh-context',conversation='new-explicit-chat');assert wait(lambda:terminal(fresh))['State']==3
    ok('crashed ambiguous prompt is not replayed; its context remains fenced while a separate new conversation can execute')
    item=submit('disconnect',conversation='disconnect-chat');value=wait(lambda:terminal(item));assert value['State']==6,value
    count=len(methods('session/prompt'));follow=submit('unsafe',conversation='disconnect-chat');assert wait(lambda:terminal(follow))['State']==6 and len(methods('session/prompt'))==count
    ok('native transport loss produces Interrupted and never silently replaces the uncertain session')
    claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-mobile',LeaseSeconds=60));assert claim is not None
    ok('durable native result remains claimable by a reconnected foreground without re-executing the task')
    assert not (PEER/'errors.txt').exists(),(PEER/'errors.txt').read_text()
finally:
    transcript=wire();stop();(OUT/'results.json').write_text(json.dumps(passed,indent=2)+'\n');(OUT/'wire.json').write_text(json.dumps(transcript,indent=2)+'\n')
