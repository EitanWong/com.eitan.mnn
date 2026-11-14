#!/usr/bin/env python3
"""External native ACP/HTTP behavior checks, independent from C# projection code."""
import argparse, base64, hashlib, json, os, socket, subprocess, time, urllib.error, urllib.request, uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir()
fixture=Path(__file__).parent/'Fixtures/coordinator_artifacts_fixture.py';secrets={k:uuid.uuid4().hex*2 for k in ['ART_HOST','ART_SCOPE','ART_OTHER']};env=dict(os.environ,**secrets,SIRIAI_COORD_ARTIFACT_ROOT=str(PEER))
with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port);CONFIG=RUN/'host.json';process=None;log=None;passed=[];plans={};observations={}
config=dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='ART_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='ART_OTHER')],Workspaces=dict(test=str(RUN)),CoordinatorMcpScopes=[dict(Id='front',OwnerId='alice',ConversationBinding='ParentTask',WorkspaceKey='test',HarnessId='worker',TokenEnvironmentVariable='ART_SCOPE')],Harnesses=[dict(Id=mode,Protocol='acp',Command='python3',Arguments=['-u',str(fixture),mode],ForwardEnvironment=['SIRIAI_COORD_ARTIFACT_ROOT'],TimeoutMilliseconds=0,**(dict(CoordinatorScopeId='front',AcpMcpServers=[dict(Name='coordinator',Type='http',Url=URL+'/mcp/coordinator',Headers=[dict(Name='Authorization',ValueEnvironmentVariable='ART_SCOPE',Prefix='Bearer ')])]) if mode=='coordinator' else {})) for mode in ['coordinator','worker']])
CONFIG.write_text(json.dumps(config))
def rows(name):
    p=PEER/name;return [json.loads(v) for v in p.read_text().splitlines()] if p.exists() else []
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def wait(fn,seconds=25):
    end=time.monotonic()+seconds
    while time.monotonic()<end:
        value=fn()
        if value:return value
        time.sleep(.025)
    raise AssertionError('Timed out waiting for artifact boundary')
def http(path,body=None,token=None,status=200):
    request=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+(token or secrets['ART_HOST']),'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(request,timeout=20) as r:code,data=r.status,r.read()
    except urllib.error.HTTPError as e:code,data=e.code,e.read()
    assert code in (status if isinstance(status,tuple) else (status,)),(path,code,data);assert not any(v.encode() in data for v in secrets.values())
    return json.loads(data) if data else None
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
def save(): (PEER/'plans.json').write_text(json.dumps(plans,ensure_ascii=False))
def task(value):return http('/v1/tasks/'+(value['Id'] if 'Id' in value else value['delegation_id']))
def terminal(value):
    value=task(value);return value if value['State'] in [3,4,5,6] else None
def children(parent):return [v['child'] for v in rows('children.jsonl') if v['parent']==parent['Id']]
def submit(name,conversation=None):
    save();return http('/v1/tasks',dict(ConversationId=conversation or uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId='coordinator',WorkspaceKey='test',Objective=name),status=202)
def claim(conversation):return http('/v1/results/claim',dict(ConversationId=conversation,ClientId='phone',LeaseSeconds=60),status=(200,204))
def source_parts(value):return [part for artifact in value.get('Artifacts',[]) for part in artifact['Parts']]
def assert_collection(parent,kids):
    assert len(parent.get('CollectedResults',[]))==len(kids),parent
    assert {v['TaskId'] for v in parent['CollectedResults']}=={task(k)['Id'] for k in kids}
    for kid in kids:
        value=task(kid);assert value['ResultPending']==False and value['ResultCollectedBy']==parent['Id'],value
        assert not value['ContextInjected'] and not value['PlaybackStarted'] and not value['PlaybackCompleted']
        assert claim(value['DeliveryConversationId'] if 'DeliveryConversationId' in value else value['ResultConversationId']) is None
def resource(text,name='result.txt'):return dict(type='resource',resource=dict(uri='file:///fixture/'+name,mimeType='text/plain',text=text))
try:
    start();blocks=[resource('完整内容🙂'),dict(type='image',data='AAEC',mimeType='image/png'),dict(type='audio',data='AwQF',mimeType='audio/wav'),dict(type='resource_link',uri='https://example.invalid/report',name='report')]
    plans.update(first=dict(children=['first-worker']),**{'first-worker':dict(blocks=blocks)})
    parent=submit('first');kid=wait(lambda:children(parent))[0];done=wait(lambda:terminal(parent));child=task(kid)
    observations['baseline_candidate']=dict(parent=done,child=child)
    assert done['State']==3 and len(child['Artifacts'])==4
    assert source_parts(done)==source_parts(child),'Completed child artifacts missing from foreground parent'
    assert_collection(done,[kid]);assert claim(done['ConversationId'])['Id']==done['Id']
    ok('exact native child typed artifacts and provenance reach the original foreground parent; internal result is collected without playback receipts')
    # Duplicate native IDs across sources and coordinator phases stay distinct.
    plans.update(multi=dict(children=['multi-a','multi-b'],initial_blocks=[resource('initial coordinator')],result_blocks=[resource('final coordinator')]),**{'multi-a':dict(blocks=[resource('A','同名.txt')]),'multi-b':dict(blocks=[resource('B','同名.txt')])})
    multi=submit('multi');value=wait(lambda:terminal(multi));kids=children(multi);assert value['State']==3 and len(value['Artifacts'])==4,value
    assert len({v['Id'] for v in value['Artifacts']})==4
    child_outputs=[v for v in value['Artifacts'] if v.get('SourceTaskId')]
    assert {v['SourceTaskId'] for v in child_outputs}=={task(k)['Id'] for k in kids}
    assert all(v['SourceArtifactId']=='acp-content-1' for v in child_outputs)
    assert {p['Payload'] for p in source_parts(value)}=={'A','B','initial coordinator','final coordinator'}
    assert_collection(value,kids);saved=value
    stop();start();assert task(multi)['Artifacts']==saved['Artifacts'] and task(multi)['CollectedResults']==saved['CollectedResults'];assert_collection(task(multi),kids)
    ok('multiple child and coordinator phase outputs keep exact bytes, unique stable identities and source provenance after restart')
    # Terminal children remain pending during the native result turn, even if leased.
    plans.update(held=dict(children=['held-worker'],hold_result=True),**{'held-worker':dict(blocks=[resource('held')])})
    held=submit('held');held_kid=wait(lambda:children(held))[0]
    wait(lambda:next((v for v in rows('turns.jsonl') if v['turn']['run_id']==held['Id'] and v['turn']['phase']=='result'),None))
    assert task(held)['State']==1 and task(held_kid)['ResultPending'] and not task(held_kid).get('ResultCollectedBy')
    lease=claim(task(held_kid)['ResultConversationId']);assert lease['Id']==task(held_kid)['Id']
    (PEER/('release-result-'+held['Id'])).touch();held_done=wait(lambda:terminal(held));assert_collection(held_done,[held_kid])
    http('/v1/tasks/'+lease['Id']+'/receipt',dict(LeaseId=lease['LeaseId'],Receipt=4),status=404)
    ok('no early child acknowledgement; atomic parent collection revokes an old internal lease without faking text or speech presentation')
    # Restart before parent commit must preserve completed child data and outbox.
    plans.update(crash=dict(children=['crash-worker'],hold_result=True),**{'crash-worker':dict(blocks=[resource('recoverable child')])})
    crash=submit('crash');crash_kid=wait(lambda:children(crash))[0]
    wait(lambda:next((v for v in rows('turns.jsonl') if v['turn']['run_id']==crash['Id'] and v['turn']['phase']=='result'),None));before=task(crash_kid)
    prompts_before=len(rows('prompts.jsonl'));stop(kill=True);start();lost=wait(lambda:terminal(crash))
    assert lost['State']==6 and not lost.get('CollectedResults') and task(crash_kid)['ResultPending'] and not task(crash_kid).get('ResultCollectedBy')
    assert task(crash_kid)['Artifacts']==before['Artifacts'] and len(rows('prompts.jsonl'))==prompts_before
    ok('Host crash before parent commit preserves exact child artifacts and pending result; unknown coordination is interrupted without replay')
    # Child failure cannot be hidden by a coordinator claiming success.
    plans.update(mixed=dict(children=['good-worker','bad-worker']),**{'good-worker':dict(blocks=[resource('good')]),'bad-worker':dict(blocks=[resource('partial bad')],reason='max_tokens')})
    mixed=submit('mixed');bad=wait(lambda:terminal(mixed));assert bad['State']==4 and len(bad['Artifacts'])==2,bad
    assert_collection(bad,children(mixed));assert claim(bad['ConversationId'])['Id']==bad['Id']
    ok('failed child forces Failed parent while preserving all known partial artifacts and a durable deliverable result')
    # Explicit count, payload and escaped response overflow: no subset called success.
    for name,child_plans in [('count',[dict(blocks=[resource(str(i)) for i in range(17)])]*2),('bytes',[dict(blocks=[resource('x'*140000)])]*2),('wire',[dict(blocks=[resource('\x01'*60000)])]*3)]:
        names=[name+'-'+str(i) for i in range(len(child_plans))];plans[name]=dict(children=names)
        plans.update(dict(zip(names,child_plans)));current=submit(name);over=wait(lambda:terminal(current));assert over['State']==4 and 'budget' in over['Result'].lower(),over
        assert not over.get('CollectedResults') and not over.get('Artifacts')
        for k in children(current):
            full=task(k);assert full['State']==3 and full['Artifacts'] and full['ResultPending'] and not full.get('ResultCollectedBy'),full
            assert full['Id'] in over['Result']
    ok('count, payload byte and serialized wire overflow fail explicitly; full child outputs and their internal outbox remain intact')
    # A completed target continued in its native context still belongs to the original run.
    plans.update(continued=dict(children=['early-worker','late-worker']),**{'early-worker':dict(blocks=[resource('early')]),'late-worker':dict(blocks=[resource('late')],hold=True),'next-worker':dict(blocks=[resource('next')])})
    continued=submit('continued');wait(lambda:len(children(continued))==2);wait(lambda:terminal(children(continued)[0]));wait(lambda:task(children(continued)[1])['State']==1)
    http('/v1/tasks/'+continued['Id']+'/update',dict(RequestId='continue-exact',Text='next-worker'))
    wait(lambda:next((v for v in rows('children.jsonl') if v['parent']==continued['Id'] and v.get('continuation')),None))
    (PEER/'release-late-worker').touch();continued_done=wait(lambda:terminal(continued));all_kids=children(continued)
    assert len(all_kids)==3 and len(continued_done['Artifacts'])==3 and {p['Payload'] for p in source_parts(continued_done)}=={'early','late','next'},continued_done
    assert_collection(continued_done,all_kids)
    ok('collection includes earlier terminal tasks and the exact same-session continuation, rather than only the latest target')
    # Independent foreground run and a different owner cannot be swept into this collection.
    plans.update(other=dict(children=['other-worker']),**{'other-worker':dict(blocks=[resource('other run')])});other=submit('other',conversation=done['ConversationId']);other_done=wait(lambda:terminal(other));assert_collection(other_done,children(other))
    assert {v['SourceTaskId'] for v in other_done['Artifacts']}=={task(k)['Id'] for k in children(other)}
    http('/v1/tasks/'+other['Id'],token=secrets['ART_OTHER'],status=404)
    assert task(parent)['Artifacts']==done['Artifacts']
    ok('same foreground conversation keeps separate original run provenance and foreign owners cannot read the collected result')
    plans.update(fair=dict(children=['fair-a','fair-b','fair-c']),**{name:dict(blocks=[],text=(name+' ')*7000) for name in ['fair-a','fair-b','fair-c']})
    fair=submit('fair');fair_done=wait(lambda:terminal(fair));views=next(v['turn']['targets'] for v in rows('turns.jsonl') if v['turn']['run_id']==fair['Id'] and v['turn']['phase']=='result')
    assert fair_done['State']==3 and len(views)==3 and all(v['result'] and v['result_is_preview'] and v['artifact_count']==0 for v in views),views
    assert sum(len(v['result']) for v in views)<=32768 and max(len(v['result']) for v in views)<=10922
    assert_collection(fair_done,children(fair))
    ok('result context allocates each target a bounded preview and artifact count; a long first result cannot erase later targets')
    assert not rows('errors.jsonl'),rows('errors.jsonl')
finally:
    stop()
    for name,data in [('results',passed),('observations',observations),('wire',rows('wire.jsonl')),('mcp-wire',rows('mcp-wire.jsonl')),('events',dict(turns=rows('turns.jsonl'),children=rows('children.jsonl'),prompts=rows('prompts.jsonl')))]: (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
