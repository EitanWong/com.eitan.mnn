#!/usr/bin/env python3
"""Native ACP+HTTP/MCP checks for administrator-authorized foreground binding."""
import argparse, base64, hashlib, json, os, socket, subprocess, sys, time, urllib.error, urllib.request, uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();(RUN/'other').mkdir()
fixture=Path(__file__).parent/'Fixtures/instruction_attachments_fixture.py'
secrets={k:uuid.uuid4().hex*2 for k in ['CONV_HOST','CONV_BOB','CONV_SCOPE']};env=dict(os.environ,**secrets,SIRIAI_INSTRUCTION_CONTENT_ROOT=str(PEER))
with socket.socket() as sock: sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(port)
config=dict(Listen=URL,DataDirectory=str(RUN/'data'),MaximumConcurrent=2,Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='CONV_HOST'),dict(OwnerId='bob',TokenEnvironmentVariable='CONV_BOB')],Workspaces=dict(test=str(RUN),other=str(RUN/'other')),CoordinatorMcpScopes=[dict(Id='front',OwnerId='alice',ConversationBinding='ParentTask',WorkspaceKey='test',HarnessId='worker',TokenEnvironmentVariable='CONV_SCOPE')],Harnesses=[dict(Id=mode,Protocol='acp',Command=sys.executable,Arguments=['-u',str(fixture),mode],ForwardEnvironment=['SIRIAI_INSTRUCTION_CONTENT_ROOT'],TimeoutMilliseconds=0,**(dict(CoordinatorScopeId='front',AcpMcpServers=[dict(Name='coordinator',Type='http',Url=URL+'/mcp/coordinator',Headers=[dict(Name='Authorization',ValueEnvironmentVariable='CONV_SCOPE',Prefix='Bearer ')])]) if mode=='coordinator' else {})) for mode in ['coordinator','worker']])
CAP=dict(image=True,audio=True,embeddedContext=True);(PEER/'capabilities.json').write_text(json.dumps({mode:CAP for mode in ['coordinator','worker','extension','pull','restart']}))
for mode in ['extension','pull','restart']:
    item=dict(config['Harnesses'][1]);item.update(Id=mode,Arguments=['-u',str(fixture),mode]);config['Harnesses'].append(item)
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
def update_content(current,identity,text,parts,admit=False,status=200,token=None):return http('/v1/tasks/'+current['Id']+'/update',dict(RequestId=identity,Text=text,Attachments=parts,AdmitOnly=admit),status=status,token=token)
def instruction(current,identity,state=None):
    value=next((i for i in task(current).get('Instructions',[]) if i['Id']==identity),None)
    return value if value and (state is None or value['State']==state) else None
def updates(current):return [v for v in rows('updates.jsonl') if v['objective']==task(current)['Objective']]
def direct(mode,objective,attachments=None):
    current=http('/v1/tasks',dict(ConversationId=uuid.uuid4().hex,SubmissionKey=uuid.uuid4().hex,HarnessId=mode,WorkspaceKey='test',Objective=objective,Attachments=attachments or []),status=202)
    wait(lambda:next((v for v in rows('prompts.jsonl') if v['mode']==mode and v['objective']==objective),None));return current
def release(current): (PEER/('release-'+task(current)['Objective'])).touch()
def caps(**overrides):
    values={mode:CAP for mode in ['coordinator','worker','extension','pull','restart']};values.update(overrides);(PEER/'capabilities.json').write_text(json.dumps(values))
def expected(parts):
    # Expectations are explicit ACP wire shapes, independent of C# conversion code.
    return [dict(type='resource',resource=dict(uri='siriai://input/1/new.txt',mimeType='text/plain',text='新增上下文')),dict(type='image',data=b64(b'new-image'),mimeType='image/png',uri='siriai://input/2/new.png'),dict(type='audio',data=b64(b'new-audio'),mimeType='audio/wav'),dict(type='resource',resource=dict(uri='siriai://input/4/data.json',mimeType='application/json',text='{"new":true}')),dict(type='resource',resource=dict(uri='siriai://input/5/new.bin',mimeType='application/octet-stream',blob='AAEC')),dict(type='resource_link',uri='https://example.invalid/new',name='reference',mimeType='text/plain')]
parts=[part(0,'新增上下文','text/plain','new.txt'),part(3,b64(b'new-image'),'image/png','new.png'),part(3,b64(b'new-audio'),'audio/wav','new.wav'),part(2,'{"new":true}',None,'data.json'),part(3,'AAEC','application/octet-stream','new.bin'),part(1,'https://example.invalid/new','text/plain','reference')];blocks=expected(parts);old=[part(0,'old initial input','text/plain','old.txt')]
try:
    start()
    for mode in ['extension','pull','restart']:
        current=direct(mode,'hold-'+mode,old)
        if mode=='pull':wait(lambda:any(v['mode']=='pull' and v['direction']=='agent' and v['frame'].get('method')=='craft/drainMidTurnQueue' for v in rows('wire.jsonl')))
        update_content(current,'content','新的要求',parts);assert wait(lambda:instruction(current,'content',1))
        observed=wait(lambda:updates(current));assert observed[-1]['blocks']==[dict(type='text',text='新的要求')]+blocks,observed
        assert instruction(current,'content')['Attachments']==parts
        release(current);done=wait(lambda:terminal(current));assert done['State']==3,done
        result=json.loads(done['Result']);assert result['effects']==1 and result['updates']==[[dict(type='text',text='新的要求')]+blocks],result
        ok('native '+mode+' receives exact new typed update in original session; initial input and effects are not replayed')
    current=direct('extension','hold-required')
    update_content(current,'required','required',parts);assert wait(lambda:instruction(current,'required',1))
    done=wait(lambda:terminal(current));assert done['State']==3 and json.loads(done['Result'])['effects']==1,done
    assert updates(current)[-1]['blocks']==[dict(type='text',text='required')]+blocks
    ok('promptRequired boundary delivers typed content in confirmed same-session followup without replaying initial effects')
    current=direct('extension','hold-retry')
    from concurrent.futures import ThreadPoolExecutor
    with ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(lambda _:update_content(current,'same','same text',parts),range(4)))
    assert len(updates(current))==1 and len(task(current)['Instructions'])==1
    update_content(current,'same','same text',[dict(parts[0],Payload='changed')],status=409)
    update_content(current,'same','different text',parts,status=409)
    release(current);wait(lambda:terminal(current));update_content(current,'same','same text',parts)
    ok('concurrent exact retries share one durable content instruction; changed bytes or text reject before another native injection')
    current=direct('extension','hold-invalid');count=len(task(current).get('Instructions',[]));wire_count=len(updates(current))
    for value,status in [([part(3,'!!!','image/png')],400),([part(0,'<'*160000)],422),([parts[0]]*33,400),([part(0,'x'*140000),part(0,'y'*140000)],400)]:update_content(current,uuid.uuid4().hex,'invalid',value,status=status)
    assert len(task(current).get('Instructions',[]))==count and len(updates(current))==wire_count
    update_content(current,'foreign','foreign',parts,token=secrets['CONV_BOB'],status=404)
    http('/v1/tasks/'+current['Id']+'/cancel',{});wait(lambda:terminal(current))
    ok('invalid typed content/count/bytes/wire overflow and foreign owner cannot admit or send an update')
    budget=direct('extension','hold-budget');large=[part(0,'x'*140000,'text/plain','large.txt')]
    update_content(budget,'first-large','large',large);assert wait(lambda:instruction(budget,'first-large',1))
    update_content(budget,'second-large','large-again',large,status=400)
    assert len(task(budget)['Instructions'])==1 and len(updates(budget))==1
    http('/v1/tasks/'+budget['Id']+'/cancel',{});wait(lambda:terminal(budget))
    ok('cumulative durable instruction content stays within the task byte budget; rejected extra input leaves earlier instruction intact')
    parent=submit('phone-update','hold-coordinator',attachments=old,status=202);owned=wait(lambda:child(parent));wait(lambda:next((v for v in rows('prompts.jsonl') if v['mode']=='worker' and v['objective']=='hold-coordinator'),None))
    update_content(parent,'parent-content','route new input',parts)
    assert wait(lambda:instruction(parent,'parent-content',1));assert task(owned)['Instructions'][0]['Attachments']==parts
    turn_update=wait(lambda:next((v for v in rows('turns.jsonl') if v['turn']['run_id']==parent['Id'] and v['turn']['phase']=='update'),None))
    assert turn_update['blocks'][1:]==blocks and updates(owned)[-1]['blocks']==[dict(type='text',text='route new input')]+blocks
    assert task(parent)['Attachments']==old and task(owned)['Attachments']==old
    release(owned);done=wait(lambda:terminal(parent));assert done['State']==3 and done['Result'].startswith('collected:'),done
    assert len([v for v in rows('prompts.jsonl') if v['mode']=='worker' and v['objective']=='hold-coordinator'])==1
    ok('foreground content update goes through a new original coordinator turn to the exact durable child with only its new inputs')
    stop()
    for mode,capability in [('extension','image'),('extension','audio'),('extension','embeddedContext'),('worker','image'),('coordinator','image')]:
        missing=dict(CAP);missing.pop(capability);caps(**{mode:missing});start();objective='hold-no-'+mode+'-'+capability
        if mode in ['worker','coordinator']:
            current=submit(uuid.uuid4().hex,objective,status=202);owned=wait(lambda:child(current));wait(lambda:next((v for v in rows('prompts.jsonl') if v['mode']=='worker' and v['objective']==objective),None))
        else:current=direct(mode,objective);owned=current
        before=len(updates(owned));update_content(current,'unsupported','cannot drop content',parts);assert wait(lambda:instruction(current,'unsupported',2)),task(current)
        assert len(updates(owned))==before
        http('/v1/tasks/'+current['Id']+'/cancel',{});wait(lambda:terminal(current));stop()
    ok('missing native content capability at coordinator or child rejects new inputs without falsely Delivered or text fallback')
    caps();start()
    for text,state in [('reject',2),('unknown',3)]:
        current=direct('extension','hold-'+text);update_content(current,text,text,parts);assert wait(lambda:instruction(current,text,state));update_content(current,text,text,parts);assert len(updates(current))==1
        if text=='reject':http('/v1/tasks/'+current['Id']+'/cancel',{})
        wait(lambda:terminal(current))
    ok('native rejection and uncertain content delivery remain distinct; same-identity retries never replay them')
    current=direct('extension','hold-lost');update_content(current,'lost','lost',parts,admit=True);wait(lambda:updates(current));assert instruction(current,'lost',0)
    stop(kill=True);start();done=wait(lambda:terminal(current));assert done['State']==6 and instruction(current,'lost',3)['Attachments']==parts
    update_content(current,'lost','lost',parts);assert len(updates(current))==1
    ok('Host restart retains typed pending instruction as Uncertain with exact bytes and does not replay an unconfirmed native update')
    assert not rows('errors.jsonl'),rows('errors.jsonl')
finally:
    stop()
    for name,data in [('results',passed),('observations',observations),('commands',commands),('mcp-wire',wire+rows('mcp-wire.jsonl')),('wire',rows('wire.jsonl')),('events',dict(turns=rows('turns.jsonl'),children=rows('children.jsonl'),prompts=rows('prompts.jsonl'),updates=rows('updates.jsonl')))]: (OUT/(name+'.json')).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
