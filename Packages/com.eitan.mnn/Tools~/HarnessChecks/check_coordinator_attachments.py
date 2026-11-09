#!/usr/bin/env python3
"""Native ACP+HTTP/MCP checks for administrator-authorized foreground binding."""
import argparse, base64, copy, hashlib, json, os, socket, subprocess, sys, time, urllib.error, urllib.request, uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();(RUN/'other').mkdir()
fixture=Path(__file__).parent/'Fixtures/coordinator_attachments_fixture.py'
secrets={k:uuid.uuid4().hex*2 for k in ['CONV_HOST','CONV_BOB','CONV_SCOPE']};env=dict(os.environ,**secrets,SIRIAI_COORD_ATTACHMENTS_ROOT=str(PEER))
with socket.socket() as sock: sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port)
config=dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='CONV_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='CONV_BOB')],Workspaces=dict(test=str(RUN),other=str(RUN/'other')),CoordinatorMcpScopes=[dict(Id='front',OwnerId='alice',ConversationBinding='ParentTask',WorkspaceKey='test',HarnessId='worker',TokenEnvironmentVariable='CONV_SCOPE')],Harnesses=[dict(Id=mode,Protocol='acp',Command=sys.executable,Arguments=['-u',str(fixture),mode],ForwardEnvironment=['SIRIAI_COORD_ATTACHMENTS_ROOT'],TimeoutMilliseconds=0,**(dict(CoordinatorScopeId='front',AcpMcpServers=[dict(Name='coordinator',Type='http',Url=URL+'/mcp/coordinator',Headers=[dict(Name='Authorization',ValueEnvironmentVariable='CONV_SCOPE',Prefix='Bearer ')])]) if mode=='coordinator' else {})) for mode in ['coordinator','worker']])
CAP=dict(image=True,audio=True,embeddedContext=True);(PEER/'capabilities.json').write_text(json.dumps(dict(coordinator=CAP,worker=CAP)))
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
    return http('/v1/tasks',dict(ConversationId=conversation,SubmissionKey=uuid.uuid4().hex,HarnessId='coordinator',WorkspaceKey=extra.pop('workspace','test'),Objective=objective,Attachments=extra.pop('attachments',[])),**extra)
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

def part(kind,payload,media=None,name=None):return dict(Kind=kind,Payload=payload,MediaType=media,FileName=name)
def b64(data):return base64.b64encode(data).decode()
def digest(blocks):return hashlib.sha256(json.dumps(blocks,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()).hexdigest()
def prompts(mode):return [v for v in rows('prompts.jsonl') if v['mode']==mode]
def prompt_for(parent,phase='initial'):
    return next((v for v in prompts('coordinator') if (lambda t:t['run_id']==parent['Id'] and t['phase']==phase)(json.loads(v['objective']))),None)
def worker_for(child):
    native=task(child)
    sessions=[e['sid'] for e in rows('sessions.jsonl') if e['mode']=='worker' and e['cwd']==str(RUN)]
    # Children start once in their own native context; their objective is unique per scenario.
    return next((v for v in prompts('worker') if v['sid'] in sessions and v['objective']==native['Objective']),None)
def configure_caps(coordinator=None,worker=None):
    (PEER/'capabilities.json').write_text(json.dumps(dict(coordinator=CAP if coordinator is None else coordinator,worker=CAP if worker is None else worker)))
parts=[part(0,'你好\ncontext','text/plain','说明.txt'),part(2,'{"answer":42}',None,'data.json'),part(3,b64(b'image-bytes'),'image/png','one.png'),part(3,b64(b'audio-bytes'),'audio/wav','one.wav'),part(3,b64('编码文本'.encode()),'text/plain','utf8.txt'),part(3,b64(bytes([0,255,16])),'application/octet-stream','bytes.bin'),part(1,'https://example.invalid/remote.bin','application/octet-stream','remote.bin')]
expected=[dict(type='resource',resource=dict(uri='siriai://input/1/%E8%AF%B4%E6%98%8E.txt',mimeType='text/plain',text='你好\ncontext')),dict(type='resource',resource=dict(uri='siriai://input/2/data.json',mimeType='application/json',text='{"answer":42}')),dict(type='image',data=b64(b'image-bytes'),mimeType='image/png',uri='siriai://input/3/one.png'),dict(type='audio',data=b64(b'audio-bytes'),mimeType='audio/wav'),dict(type='resource',resource=dict(uri='siriai://input/5/utf8.txt',mimeType='text/plain',text='编码文本')),dict(type='resource',resource=dict(uri='siriai://input/6/bytes.bin',mimeType='application/octet-stream',blob=b64(bytes([0,255,16])))),dict(type='resource_link',uri='https://example.invalid/remote.bin',name='remote.bin',mimeType='application/octet-stream')]
try:
    catalog=start()
    direct=http('/v1/tasks',dict(ConversationId='direct',SubmissionKey=uuid.uuid4().hex,HarnessId='worker',WorkspaceKey='test',Objective='direct-seven',Attachments=parts),status=202)
    assert wait(lambda:terminal(direct))['Result']=='native-content:'+digest(expected)
    assert prompts('worker')[-1]['blocks'][1:]==expected
    ok('direct ACP receives seven exact typed inputs; existing content projection works independently of coordinator routing')
    current=submit('mobile-'+uuid.uuid4().hex,'first-seven',attachments=parts,status=202)
    current_child=wait(lambda:child(current));done=wait(lambda:terminal(current));assert done['State']==3 and done['Result']=='collected:native-content:'+digest(expected),done
    assert prompt_for(current)['blocks'][1:]==expected and wait(lambda:worker_for(current_child))['blocks'][1:]==expected
    assert task(current)['Attachments']==task(current_child)['Attachments']==parts
    assert len([v for v in prompts('worker') if v['objective']=='first-seven'])==1
    assert next(v for v in catalog['configured'] if v['id']=='coordinator')['capabilities']&16
    claim(task(current)['ConversationId'],current)
    ok('coordinator and its exact durable child receive Unicode/JSON/image/audio/text/blob/URL content unchanged; only collected parent result reaches foreground')
    # Durable request identity includes attachments even after the response/result.
    original=task(current);payload=dict(ConversationId=original['ConversationId'],SubmissionKey=original['SubmissionKey'],HarnessId='coordinator',WorkspaceKey='test',Objective='first-seven',Attachments=parts)
    assert http('/v1/tasks',payload,status=202)['Id']==current['Id']
    http('/v1/tasks',dict(payload,Attachments=[dict(parts[0],Payload='changed')]),status=400)
    assert len([v for v in prompts('worker') if v['objective']=='first-seven'])==1
    ok('parent and child exact retries deduplicate persistent input identity; changing attachment bytes cannot reuse the original intent')
    before=len(http('/v1/tasks'));before_prompts=len(prompts('coordinator'))
    for attachment,status in [([part(3,'!!!','image/png','bad.png')],400),([part(3,b64(bytes([255])),'text/plain','bad.txt')],400),([part(0,'<'*155000,'text/plain','escaped.txt')],422),([part(0,'a'*140000),part(0,'b'*140000)],400),([parts[0]]*33,400)]:
        submit('invalid-'+uuid.uuid4().hex,'invalid',attachments=attachment,status=status)
    assert len(http('/v1/tasks'))==before and len(prompts('coordinator'))==before_prompts
    ok('invalid base64/UTF8, attachment count/byte limits and escaped coordinator wire overflow reject before durable admission or native prompt')
    boundary=[part(0,'<'*149850,'text/plain','escaped-boundary.txt')]
    direct_boundary=http('/v1/tasks',dict(ConversationId='direct-boundary',SubmissionKey=uuid.uuid4().hex,HarnessId='worker',WorkspaceKey='test',Objective='direct-boundary',Attachments=boundary),status=202)
    assert wait(lambda:terminal(direct_boundary))['State']==3
    count=len(http('/v1/tasks'));coordinator_count=len(prompts('coordinator'))
    submit('coordinator-boundary','initial-envelope',attachments=boundary,status=422)
    assert len(http('/v1/tasks'))==count and len(prompts('coordinator'))==coordinator_count
    ok('initial envelope preflight accounts for coordinator JSON overhead: same near-limit input fits direct ACP but rejects coordinator before admission')
    first=submit('isolated-a','hold-admission-a',attachments=[parts[0]],status=202);at=wait(lambda:turn(first))
    second=submit('isolated-b','hold-admission-b',attachments=[parts[1]],status=202);bt=wait(lambda:turn(second))
    assert prompt_for(first)['blocks'][1:]==[expected[0]]
    assert prompt_for(second)['blocks'][1:]==[dict(type='resource',resource=dict(uri='siriai://input/1/data.json',mimeType='application/json',text='{"answer":42}'))]
    mcp('siriai_session_start',dict(turn_id=at['turn']['turn_id'],request_id='foreign-input',prompt='must not run',attachments=parts),error=True)
    mcp('siriai_session_start',dict(turn_id=at['turn']['turn_id'],request_id='foreign-parent',prompt='must not run',parent_task_id=second['Id']),error=True)
    mcp('siriai_session_start',dict(turn_id=at['turn']['turn_id'],request_id='foreign-path',prompt='must not run',path='/private/foreign'),error=True)
    (PEER/('release-admission-'+first['Id'])).touch();ac=wait(lambda:child(first));wait(lambda:task(ac)['State']==1)
    (PEER/('release-admission-'+second['Id'])).touch();bc=wait(lambda:child(second))
    assert task(ac)['Attachments']==[parts[0]] and task(bc)['Attachments']==[parts[1]]
    update(first,'updated-first');(PEER/'release-worker-hold-admission-a').touch();assert wait(lambda:terminal(first))['Result']=='collected:native-content:'+digest(prompt_for(first)['blocks'][1:])+':updated-first'
    wait(lambda:task(bc)['State']==1);update(second,'updated-second');(PEER/'release-worker-hold-admission-b').touch();assert wait(lambda:terminal(second))['Result']=='collected:native-content:'+digest(prompt_for(second)['blocks'][1:])+':updated-second'
    for parent in [first,second]:assert len(prompt_for(parent,'update')['blocks'])==len(prompt_for(parent,'result')['blocks'])==1
    assert all(len(v['blocks'])==2 and v['blocks'][0]['type']=='text' for v in rows('injections.jsonl'))
    ok('concurrent foreground runs retain only their own inputs; MCP rejects injected content/parent/path overrides; update and result rounds do not replay original attachments')
    plain=submit('isolated-a','plain-after-content',status=202);plain_child=wait(lambda:child(plain));assert wait(lambda:terminal(plain))['Result']=='collected:native-content:'+digest([])
    assert task(plain_child).get('Attachments') in [None,[]] and len(prompt_for(plain)['blocks'])==1
    ok('later text-only work in the same native coordinator conversation does not inherit the previous parent input payload')
    stop()
    for mode,capability,index in [('coordinator','image',2),('coordinator','audio',3),('coordinator','embeddedContext',0),('worker','image',2),('worker','audio',3),('worker','embeddedContext',0)]:
        caps=dict(CAP);caps.pop(capability);configure_caps(**{mode:caps});start();conv='no-'+mode+'-'+capability;objective=conv;previous_native=len(prompts(mode));denied=submit(conv,objective,attachments=[parts[index]],status=202);value=wait(lambda:terminal(denied));assert value['State']==4,value
        assert len(prompts(mode))==previous_native and 'capabilit' in value['Result'].lower(),value
        if mode=='coordinator':assert not child(denied)
        else:
            denied_child=wait(lambda:child(denied));assert task(denied_child)['State']==4 and not worker_for(denied_child)
        follow=submit(conv,objective+'-text',status=202);assert wait(lambda:terminal(follow))['State']==3
        stop()
    ok('both coordinator and worker negotiate image/audio/embeddedContext independently; missing capability sends no unsupported prompt and text follow-up remains usable')
    configure_caps(coordinator={},worker={});start();url_only=submit('url-only','url-reference',attachments=[parts[-1]],status=202);url_child=wait(lambda:child(url_only));assert wait(lambda:terminal(url_only))['State']==3
    assert prompt_for(url_only)['blocks'][1:]==wait(lambda:worker_for(url_child))['blocks'][1:]==[expected[-1]]
    ok('URL reference passes through baseline ACP capabilities without Host download or extra native image/audio claims')
    stop();configure_caps();config['MaximumConcurrent']=1;start()
    blocker=submit('crash-owner','hold-crash',attachments=parts,status=202);blocked_child=wait(lambda:child(blocker));wait(lambda:task(blocked_child)['State']==1);wait(lambda:worker_for(blocked_child))
    queued=submit('queued-owner','queued-content',attachments=[parts[0]],status=202);assert task(queued)['State']==0
    stop(kill=True);start();assert wait(lambda:terminal(blocker))['State']==6 and wait(lambda:terminal(blocked_child))['State']==6
    recovered=wait(lambda:terminal(queued));assert recovered['State']==3,recovered;queued_child=wait(lambda:child(queued))
    assert task(queued_child)['Attachments']==[parts[0]] and wait(lambda:worker_for(queued_child))['blocks'][1:]==[expected[0]]
    assert len([v for v in prompts('worker') if v['objective']=='hold-crash'])==1
    assert len([v for v in prompts('worker') if v['objective']=='queued-content'])==1
    assert task(current)['Attachments']==parts and task(current_child)['Attachments']==parts
    ok('durable queued inputs survive Host crash and execute once; unknown started attachment work is interrupted without replay and earlier inputs remain intact')
    stop();scope=config['CoordinatorMcpScopes'][0];scope.pop('ConversationBinding');scope['ConversationId']='fixed-content';start()
    fixed=submit('fixed-content','fixed-content',attachments=[parts[0]],status=202);fixed_child=wait(lambda:child(fixed));assert wait(lambda:terminal(fixed))['State']==3 and task(fixed_child)['Attachments']==[parts[0]]
    ok('default Fixed conversation binding also supports typed coordinator inputs without changing static scope identity')
    assert not rows('errors.jsonl'),rows('errors.jsonl')
finally:
    stop()
    observations.update(expected_blocks=expected,parent=current if 'current' in locals() else None)
    for name,data in [('results',passed),('observations',observations),('commands',commands),('mcp-wire',wire+rows('mcp-wire.jsonl')),('wire',rows('wire.jsonl')),('events',dict(sessions=rows('sessions.jsonl'),turns=rows('turns.jsonl'),children=rows('children.jsonl'),prompts=rows('prompts.jsonl'),injections=rows('injections.jsonl')))]: (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
