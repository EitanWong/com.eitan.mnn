#!/usr/bin/env python3
"""Native ACP+HTTP/MCP checks for administrator-authorized foreground binding."""
import argparse, copy, json, os, socket, subprocess, sys, time, urllib.error, urllib.request, uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--fixed-red',action='store_true');a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();(RUN/'other').mkdir()
fixture=Path(__file__).parent/'Fixtures/coordinator_conversations_fixture.py'
secrets={k:uuid.uuid4().hex*2 for k in ['CONV_HOST','CONV_BOB','CONV_SCOPE']};env=dict(os.environ,**secrets,SIRIAI_COORD_CONVERSATIONS_ROOT=str(PEER))
with socket.socket() as sock: sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port)
config=dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='CONV_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='CONV_BOB')],Workspaces=dict(test=str(RUN),other=str(RUN/'other')),CoordinatorMcpScopes=[dict(Id='front',OwnerId='alice',ConversationId='fixed-phone',WorkspaceKey='test',HarnessId='worker',TokenEnvironmentVariable='CONV_SCOPE')],Harnesses=[dict(Id=mode,Protocol='acp',Command=sys.executable,Arguments=['-u',str(fixture),mode],ForwardEnvironment=['SIRIAI_COORD_CONVERSATIONS_ROOT'],TimeoutMilliseconds=0,**(dict(CoordinatorScopeId='front',AcpMcpServers=[dict(Name='coordinator',Type='http',Url=URL+'/mcp/coordinator',Headers=[dict(Name='Authorization',ValueEnvironmentVariable='CONV_SCOPE',Prefix='Bearer ')])]) if mode=='coordinator' else {})) for mode in ['coordinator','worker']])
CONFIG=RUN/'host.json';process=None;log=None;passed=[];wire=[];observations={};commands=[]
def ok(text): passed.append(text);print('PASS '+text,flush=True)
def rows(name):
    path=PEER/name;return [json.loads(v) for v in path.read_text().splitlines()] if path.exists() else []
def wait(fn,seconds=25):
    end=time.monotonic()+seconds
    while time.monotonic()<end:
        value=fn()
        if value:return value
        time.sleep(.025)
    raise AssertionError('Timed out waiting for coordinator conversation boundary')
def http(path,body=None,token=None,status=200):
    req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(token or secrets['CONV_HOST']),'Content-Type':'application/json','Accept':'application/json,text/event-stream'})
    try:
        with urllib.request.urlopen(req,timeout=25) as response: code,data=response.status,response.read()
    except urllib.error.HTTPError as error:code,data=error.code,error.read()
    assert code==status,(path,code,data);assert not any(v.encode() in data for v in secrets.values())
    return json.loads(data) if data else None
def mcp(name,args,error=False):
    frame=dict(jsonrpc='2.0',id=uuid.uuid4().hex,method='tools/call',params=dict(name=name,arguments=args));reply=http('/mcp/coordinator',frame,token=secrets['CONV_SCOPE']);wire.append(dict(request=frame,response=reply))
    assert reply['result'].get('isError',False)==error,reply
    return json.loads(reply['result']['content'][0]['text'])
def start(registration=None):
    global process,log
    CONFIG.write_text(json.dumps(config));log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');command=['dotnet',str(a.host),'--service','run','--directory',str(registration)] if registration else ['dotnet',str(a.host),'--config',str(CONFIG)]
    process=subprocess.Popen(command,env=os.environ if registration else env,stdout=log,stderr=log)
    def ready():
        assert process.poll() is None,'Host exited: '+Path(log.name).read_text()
        try:return http('/v1/harnesses')
        except urllib.error.URLError:return None
    return wait(ready)
def stop(kill=False):
    global process
    if process:
        if process.poll() is None:process.kill() if kill else process.terminate()
        try:process.wait(timeout=10)
        except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
        process=None;log.close()
def submit(conversation,objective='instant',**extra):
    return http('/v1/tasks',dict(ConversationId=conversation,SubmissionKey=uuid.uuid4().hex,HarnessId='coordinator',WorkspaceKey=extra.pop('workspace','test'),Objective=objective),**extra)
def task(value):return http('/v1/tasks/'+(value['Id'] if 'Id' in value else value['delegation_id']))
def terminal(value):
    t=task(value);return t if t['State'] in [3,4,5,6] else None
def child(parent):return next((e['child'] for e in rows('children.jsonl') if e['parent']==parent['Id']),None)
def turn(parent,phase='initial'):return next((e for e in rows('turns.jsonl') if e['turn']['run_id']==parent['Id'] and e['turn']['phase']==phase),None)
def release(parent): (PEER/('release-initial-'+parent['Id'])).touch()
def update(parent,text):
    http('/v1/tasks/'+parent['Id']+'/update',dict(RequestId=text,Text=text,AdmitOnly=True))
    return wait(lambda:next((i for i in task(parent).get('Instructions',[]) if i['Id']==text and i['State']==1),None))
def claim(conversation,expected):
    got=http('/v1/results/claim',dict(ConversationId=conversation,ClientId='checker',LeaseSeconds=60));assert got and got['Id']==expected['Id'],got
    return got
try:
    start();fixed=submit('fixed-phone',status=202);assert wait(lambda:terminal(fixed))['Result']=='collected:worker:instant'
    rejected=submit('new-device-guid',status=400);observations['fixed_second_conversation']=rejected
    ok('default Fixed mode retains native result and rejects a new device conversation with HTTP400')
    if a.fixed_red:
        submit('new-device-guid',status=202)
        raise AssertionError('Expected existing fixed scope to reproduce rejection')
    stop();scope=config['CoordinatorMcpScopes'][0];scope.pop('ConversationId');scope['ConversationBinding']='ParentTask';start()
    for bad in [dict(token=secrets['CONV_BOB']),dict(workspace='other')]:submit('new-device-guid',status=400,**bad)
    mcp('siriai_sessions_list',{},error=True)
    ok('ParentTask is explicit and still rejects wrong owner, wrong project and tools without a live parent turn')
    ca='mobile-'+uuid.uuid4().hex;cb='desktop-'+uuid.uuid4().hex
    first=submit(ca,'hold-a',status=202);ac=wait(lambda:child(first));at=wait(lambda:turn(first));wait(lambda:task(ac)['State']==1)
    second=submit(cb,'hold-b',status=202);bc=wait(lambda:child(second));bt=wait(lambda:turn(second))
    assert task(first)['State']==task(second)['State']==1
    assert ac['session_id']!=bc['session_id'] and task(ac)['ResultConversationId']!=task(bc)['ResultConversationId']
    sessions=rows('sessions.jsonl');sa=next(v for v in sessions if v['sid']==at['sid']);sb=next(v for v in sessions if v['sid']==bt['sid'])
    assert at['pid']!=bt['pid'] and at['sid']!=bt['sid'] and sa['cwd']!=sb['cwd'] and sa['cwd']!=str(RUN) and sb['cwd']!=str(RUN)
    observations['concurrent']=dict(first=first['Id'],second=second['Id'],first_conversation=ca,second_conversation=cb,first_native=sa,second_native=sb)
    ok('two new foreground GUIDs enter independent native sessions and control lanes while children retain the original project lane')
    for own,other,t in [(ac,bc,at),(bc,ac,bt)]:
        args=dict(turn_id=t['turn']['turn_id']);listing=mcp('siriai_sessions_list',args)['sessions'];assert [v['delegation_id'] for v in listing]==[own['delegation_id']],listing
        assert mcp('siriai_session_status',dict(**args,delegation_id=own['delegation_id']))['delegation_id']==own['delegation_id']
        mcp('siriai_session_status',dict(**args,delegation_id=other['delegation_id']),error=True)
        mcp('siriai_session_send',dict(**args,session_id=other['session_id'],request_id='cross-conversation',prompt='must not run'),error=True)
        mcp('siriai_session_cancel',dict(**args,delegation_id=other['delegation_id']),error=True)
    ok('live-turn list/status/send/cancel cannot cross conversations even when both share owner, backend, project and credential')
    release(first);release(second);wait(lambda:len([e for e in rows('prompts.jsonl') if e['mode']=='coordinator' and first['Id'] in e['objective']])>=1)
    update(first,'update-a');(PEER/'release-worker-hold-a').touch();assert wait(lambda:terminal(first))['Result']=='collected:worker:hold-a:update-a'
    wait(lambda:task(bc)['State']==1);update(second,'update-b');(PEER/'release-worker-hold-b').touch();assert wait(lambda:terminal(second))['Result']=='collected:worker:hold-b:update-b'
    claim(ca,first);claim(cb,second);assert len(rows('injections.jsonl'))==2
    for current in [first,second]:
        turns=[e for e in rows('turns.jsonl') if e['turn']['run_id']==current['Id']];assert len({(v['pid'],v['sid']) for v in turns})==1
    ok('each foreground updates its exact original child and claims only its own collected result in the same isolated native context')
    repeat=submit(ca,'instant-repeat',status=202);assert wait(lambda:terminal(repeat))['Result']=='collected:worker:instant-repeat';rt=turn(repeat)
    assert (rt['pid'],rt['sid'])==(at['pid'],at['sid'])
    ok('same foreground conversation reuses its native coordinator context without sharing another conversation history')
    config['MaximumConcurrent']=1;stop();start()
    blocker=submit(ca,'hold-crash',status=202);cc=wait(lambda:child(blocker));wait(lambda:task(cc)['State']==1)
    queued=submit(cb,'instant-queued',status=202);assert task(queued)['State']==0 and task(queued).get('BackendState')
    stop(kill=True);start();assert wait(lambda:terminal(blocker))['State']==6;assert wait(lambda:terminal(cc))['State']==6
    resumed=wait(lambda:terminal(queued));assert resumed['State']==3 and resumed['Result']=='collected:worker:instant-queued',resumed
    assert len([e for e in rows('turns.jsonl') if e['turn']['run_id']==blocker['Id']])==1
    ok('unchanged queued conversation binding survives a Host crash while unknown started work is interrupted without replay')
    blocker2=submit('rebind-blocker','hold-rebind',status=202);cc2=wait(lambda:child(blocker2));wait(lambda:task(cc2)['State']==1)
    rebound=submit('rebind-queued','instant-must-not-run',status=202);assert task(rebound)['State']==0
    stop(kill=True);scope['Id']='front-rebound';config['Harnesses'][0]['CoordinatorScopeId']='front-rebound';start()
    assert wait(lambda:terminal(rebound))['State']==6
    assert not any(e['turn']['run_id']==rebound['Id'] for e in rows('turns.jsonl'))
    fresh=submit('fresh-guid','instant-fresh',status=202);assert wait(lambda:terminal(fresh))['Result']=='collected:worker:instant-fresh'
    observations['queued_rebinding']=dict(interrupted=task(rebound),fresh=task(fresh))
    ok('queued admission seals scope identity so configuration rebinding interrupts it before any native turn; new explicit work remains usable')
    # An existing admission seal must not become optional when switching to Fixed.
    blocker3=submit('mode-blocker','hold-mode',status=202);cc3=wait(lambda:child(blocker3));wait(lambda:task(cc3)['State']==1)
    mode_changed=submit('switch-guid','instant-mode-must-not-run',status=202);assert task(mode_changed)['State']==0
    stop(kill=True);scope['ConversationBinding']='Fixed';scope['ConversationId']='switch-guid';start()
    assert wait(lambda:terminal(mode_changed))['State']==6
    assert not any(e['turn']['run_id']==mode_changed['Id'] for e in rows('turns.jsonl'))
    # A legacy fixed queued task has no seal. Authorizing a wider parent mode must
    # not silently authorize that old queued task under the new grant.
    fixed_blocker=submit('switch-guid','hold-legacy',status=202);fcc=wait(lambda:child(fixed_blocker));wait(lambda:task(fcc)['State']==1)
    legacy_queued=submit('switch-guid','instant-legacy-must-not-run',status=202);assert task(legacy_queued)['State']==0 and not task(legacy_queued).get('BackendState')
    stop(kill=True);scope['ConversationBinding']='ParentTask';scope.pop('ConversationId');start()
    assert wait(lambda:terminal(legacy_queued))['State']==6
    assert not any(e['turn']['run_id']==legacy_queued['Id'] for e in rows('turns.jsonl'))
    observations['mode_migration']=dict(parent_to_fixed=task(mode_changed),legacy_fixed_to_parent=task(legacy_queued))
    ok('ParentTask-to-Fixed rebinding and legacy unsealed Fixed-to-ParentTask queues are both interrupted without native execution')
    stop()
    invalid=[('parent-with-fixed',lambda c:c['CoordinatorMcpScopes'][0].update(ConversationId='accidental-fixed')),
             ('unknown-mode',lambda c:c['CoordinatorMcpScopes'][0].update(ConversationBinding='Wildcard')),
             ('undefined-number',lambda c:c['CoordinatorMcpScopes'][0].update(ConversationBinding=99)),
             ('unbound-parent',lambda c:c['Harnesses'][0].pop('CoordinatorScopeId')),
             ('fixed-without-id',lambda c:c['CoordinatorMcpScopes'][0].update(ConversationBinding='Fixed'))]
    for label,change in invalid:
        bad=copy.deepcopy(config);change(bad);path=RUN/('invalid-'+label+'.json');path.write_text(json.dumps(bad));command=['dotnet',str(a.host),'--service','prepare','--id','invalid','--config',str(path),'--runtime',str(Path('/opt/homebrew/bin/dotnet').resolve()),'--directory',str(RUN/('invalid-'+label))]
        r=subprocess.run(command,env=env,text=True,capture_output=True,timeout=30);commands.append(dict(command=command,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));assert r.returncode==2,commands[-1]
    ok('ambiguous, unknown, unbound and missing Fixed bindings fail configuration before execution')
    CONFIG.write_text(json.dumps(config));registration=RUN/'prepared-service'
    command=['dotnet',str(a.host),'--service','prepare','--id','conversation-binding','--config',str(CONFIG),'--runtime',str(Path('/opt/homebrew/bin/dotnet').resolve()),'--directory',str(registration)]
    r=subprocess.run(command,env=env,text=True,capture_output=True,timeout=30);commands.append(dict(command=command,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));assert r.returncode==0,commands[-1]
    start(registration);service_task=submit('prepared-new-guid','instant-service',status=202)
    assert wait(lambda:terminal(service_task))['Result']=='collected:worker:instant-service'
    ok('prepared C# Host reopens ParentTask configuration and scoped credentials without inheriting the parent environment')
    assert not rows('errors.jsonl'),rows('errors.jsonl')
finally:
    stop()
    for name,data in [('results',passed),('observations',observations),('commands',commands),('mcp-wire',wire+rows('mcp-wire.jsonl')),('wire',rows('wire.jsonl')),('events',dict(sessions=rows('sessions.jsonl'),turns=rows('turns.jsonl'),children=rows('children.jsonl'),injections=rows('injections.jsonl')))]: (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
