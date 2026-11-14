#!/usr/bin/env python3
"""Exercise SiriAI HTTP tasks backed by the actual official DotCraft Harness."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse, json, os, socket, subprocess, sys, time, urllib.error, urllib.request, uuid
from pathlib import Path

SAMPLE = PACKAGE_ROOT
p = argparse.ArgumentParser()
p.add_argument('--host', required=True, type=Path)
p.add_argument('--dotnet', required=True, type=Path)
p.add_argument('--output', required=True, type=Path)
p.add_argument('--mode', default='regular', choices=['regular','mcp','instructions','input','input-timeout','timeout','provider-error','provider-overflow','code-mode','code-mode-only','code-mode-timeout'])
a = p.parse_args()
OUT = a.output.resolve(); OUT.mkdir(parents=True, exist_ok=True)
RUN = OUT / ('run-' + uuid.uuid4().hex); RUN.mkdir()
WORK = RUN / 'workspace'; WORK.mkdir()
(WORK / 'proof.txt').write_text('SIRIAI_NATIVE_DOTCRAFT_READ_OK\n')
TOKEN = uuid.uuid4().hex * 2; OTHER = uuid.uuid4().hex * 2; KEY = uuid.uuid4().hex
ENV = dict(os.environ, SIRIAI_MCP_HTTP_KEY=uuid.uuid4().hex, SIRIAI_MCP_ALLOWED='ONLY_MCP_ALLOWED_VALUE', SIRIAI_MCP_UNLISTED='NEVER_FORWARD_THIS', SIRIAI_DOTCRAFT_HOST_TOKEN=TOKEN, SIRIAI_DOTCRAFT_OTHER_TOKEN=OTHER, SIRIAI_DOTCRAFT_MODEL_KEY=KEY, SIRIAI_DOTCRAFT_WRITE_ROOT=str(RUN))
def port():
    with socket.socket() as s:
        s.bind(('127.0.0.1', 0)); return s.getsockname()[1]
PEER = 'http://127.0.0.1:' + str(port()); URL = 'http://127.0.0.1:' + str(port())
CONFIG = RUN / 'host.json'; REG = RUN / 'service'
CONFIG.write_text(json.dumps(dict(Listen=URL, DataDirectory=str(RUN/'data'), MaximumConcurrent=2,
    Identities=[dict(OwnerId='alice', TokenEnvironmentVariable='SIRIAI_DOTCRAFT_HOST_TOKEN'), dict(OwnerId='bob', TokenEnvironmentVariable='SIRIAI_DOTCRAFT_OTHER_TOKEN')],
    Workspaces=dict(test=str(WORK)), Harnesses=[dict(Id='dotcraft', Backend='dotcraft', Endpoint=PEER+'/v1', Model='fixture-model', TokenEnvironmentVariable='SIRIAI_DOTCRAFT_MODEL_KEY', DotCraftEnabledTools=['ReadFile','WriteFile','RequestUserInput'], TimeoutMilliseconds=3000 if a.mode in ['timeout','input-timeout'] else 60000)])))
if a.mode.startswith('code-mode'):
    cfg=json.loads(CONFIG.read_text());cfg['Harnesses'][0]['DotCraftCodeMode']='only' if a.mode=='code-mode-only' else 'on'
    cfg['Workspaces']['second']=str(RUN/'second-workspace');Path(cfg['Workspaces']['second']).mkdir(exist_ok=True)
    (Path(cfg['Workspaces']['second'])/'proof.txt').write_text('SIRIAI_SECOND_WORKSPACE_READ_OK\n')
    cfg['Harnesses'][0]['TimeoutMilliseconds']=3000 if a.mode=='code-mode-timeout' else 60000
    CONFIG.write_text(json.dumps(cfg))
mcp_peer=None; mcp_log=None; MCP_RECORDS=RUN/'mcp.jsonl'
if a.mode=='mcp' or a.mode.startswith('code-mode'):
    mcp_port=port();mcp_log=(RUN/'mcp-http.log').open('w')
    mcp_fixture=SAMPLE/'Tools~/HarnessChecks/Fixtures/dotcraft_mcp_fixture.py'
    mcp_peer=subprocess.Popen([sys.executable,'-u',str(mcp_fixture),'--http',str(mcp_port),'--records',str(MCP_RECORDS)],env=ENV,stdout=mcp_log,stderr=mcp_log)
    cfg=json.loads(CONFIG.read_text());cfg['MaximumConcurrent']=3;cfg['Workspaces']['second']=str(RUN/'second-workspace');Path(cfg['Workspaces']['second']).mkdir(exist_ok=True)
    if a.mode=='mcp':cfg['Harnesses'][0]['DotCraftEnabledTools']=[]
    cfg['Harnesses'][0]['DotCraftMcpServers']=[dict(Name='proofstdio',Type='stdio',Command=str(Path(sys.executable).resolve()),Arguments=['-u',str(mcp_fixture)],Environment=[dict(Name='SIRIAI_MCP_RECORDS',Value=str(MCP_RECORDS)),dict(Name='SIRIAI_MCP_ALLOWED',ValueEnvironmentVariable='SIRIAI_MCP_ALLOWED')]),dict(Name='proofhttp',Type='http',Url='http://127.0.0.1:'+str(mcp_port)+'/mcp',Headers=[dict(Name='X-Mcp-Key',ValueEnvironmentVariable='SIRIAI_MCP_HTTP_KEY')])]
    CONFIG.write_text(json.dumps(cfg))
records = []; passed = []; process = None; log = None
peerlog = (RUN/'peer.log').open('w')
peer = subprocess.Popen([sys.executable, '-u', str(SAMPLE/'Tools~/HarnessChecks/Fixtures/dotcraft_model_fixture.py'), PEER.rsplit(':',1)[1]], env=ENV, stdout=peerlog, stderr=peerlog)
def ok(message):
    passed.append(message); print('PASS '+message, flush=True)
def wait(test, timeout=35):
    end = time.monotonic()+timeout
    while time.monotonic()<end:
        value=test()
        if value: return value
        time.sleep(.05)
    raise AssertionError('Timed out waiting for actual DotCraft behavior')
def wire():
    try:
        with urllib.request.urlopen(PEER+'/__state', timeout=2) as r: return json.load(r)['calls']
    except urllib.error.URLError: return None
def call(path, body=None, token=TOKEN, expected=200):
    request=urllib.request.Request(URL+path, None if body is None else json.dumps(body).encode(), headers={'Authorization':'Bearer '+token, 'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(request, timeout=15) as r: status, raw=r.status, r.read()
    except urllib.error.HTTPError as e: status, raw=e.code, e.read()
    records.append(dict(path=path,input=body,status=status,output=raw.decode()))
    assert status==expected, (path,status,raw)
    assert all(secret.encode() not in raw for secret in [TOKEN,OTHER,KEY])
    return json.loads(raw) if raw else None
def start():
    global process,log
    log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w')
    process=subprocess.Popen([str(a.dotnet),str(a.host),'--service','run','--directory',str(REG)], env=dict(os.environ,**({'SIRIAI_MCP_UNLISTED':ENV['SIRIAI_MCP_UNLISTED']} if a.mode=='mcp' else {})), stdout=log, stderr=log)
    def ready():
        if process.poll() is not None: raise AssertionError('host exited '+str(process.returncode))
        try: return call('/v1/harnesses')
        except urllib.error.URLError: return None
    return wait(ready)
def stop(kill=False):
    global process
    if process is not None:
        process.kill() if kill else process.terminate()
        try: process.wait(timeout=15)
        except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=5)
        process=None; log.close()
def submit(text,key=None,token=TOKEN,attachments=None,workspace='test',conversation='phone'):
    return call('/v1/tasks',dict(ConversationId=conversation,SubmissionKey=key or uuid.uuid4().hex,HarnessId='dotcraft',WorkspaceKey=workspace,Objective=text,Attachments=attachments),token=token,expected=202)
def terminal(item,token=TOKEN):
    value=call('/v1/tasks/'+item['Id'],token=token)
    return value if value['State'] in [3,4,5,6] else None
try:
    wait(lambda:wire() is not None)
    cmd=[str(a.dotnet),str(a.host),'--service','prepare','--id','dotcraft-test','--config',str(CONFIG),'--runtime',str(a.dotnet.resolve()),'--directory',str(REG)]
    r=subprocess.run(cmd,env=ENV,text=True,capture_output=True,timeout=40)
    records.append(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode)); (OUT/'commands.json').write_text(json.dumps(records,indent=2))
    assert r.returncode==0, records[-1]
    assert KEY not in (REG/'host.json').read_text()
    catalog=start(); native=next(h for h in catalog['configured'] if h['id']=='dotcraft')
    assert native['backend']=='dotcraft' and native['cancellationScope']=='dotcraft-turn'
    assert not next(h for h in catalog['installations'] if h['id']=='dotcraft')['protocolCandidate']
    assert wire()==[]
    ok('official DotCraft host prepares with private model credentials and makes no startup model request')
    if a.mode.startswith('code-mode'):
        def current(item,token=TOKEN):return call('/v1/tasks/'+item['Id'],token=token)
        def finished(text,token=TOKEN,workspace='test'):
            item=submit(text,token=token,workspace=workspace);done=wait(lambda:terminal(item,token));assert done['State']==3 and 'CODEMODE_MISSING' not in done['Result'],done;return item,done
        first,done=finished('code-read')
        assert done['Result'].count('SIRIAI_NATIVE_DOTCRAFT_READ_OK')>=2,done
        model=wire()[0]['body'];names={t.get('function',{}).get('name') for t in model.get('tools',[])}
        assert 'CodeMode' in names and 'CodeModeWait' in names,names
        assert ('ReadFile' in names)==(a.mode!='code-mode-only'),names
        ok('official CodeMode worker executes concurrent nested ReadFile tools and configured on/only model surface')
        def workers(parent=None):
            probe=subprocess.run(['ps','-eo','pid=,ppid=,args='],capture_output=True,text=True,check=True)
            found=[]
            for line in probe.stdout.splitlines():
                parts=line.strip().split(None,2)
                if len(parts)==3 and ' script-worker code-mode' in parts[2] and 'SiriAI.DotCraftHost' in parts[2] and (parent is None or int(parts[1])==parent):found.append(dict(pid=int(parts[0]),parent=int(parts[1]),command=parts[2]))
            return found
        native_workers=wait(lambda:workers(process.pid));assert len(native_workers)==1,native_workers
        child=native_workers[0];assert str(a.dotnet.resolve()) in child['command'],child
        environment=subprocess.run(['ps','eww','-p',str(child['pid']),'-o','command='],capture_output=True,text=True,check=True).stdout
        assert all(secret not in environment for secret in [KEY,TOKEN,OTHER,ENV['SIRIAI_MCP_ALLOWED'],ENV['SIRIAI_MCP_HTTP_KEY']])
        records.append(dict(worker=child,credential_environment_absent=True))
        ok('worker uses the actual Host dotnet runtime despite PATH and inherits no service/model credential values')
        def mcp_events():return [json.loads(line) for line in MCP_RECORDS.read_text().splitlines()] if MCP_RECORDS.exists() else []
        def loop_started():
            value=current(item)
            return value if value['State']==1 and any(e.get('event')=='call' and e['arguments'].get('text')=='code-loop' for e in mcp_events()[loop_event_count:]) else None
        if a.mode=='code-mode-timeout':
            loop_event_count=len(mcp_events());item=submit('code-loop');wait(loop_started);value=wait(lambda:terminal(item))
            assert value['State']==4 and 'timed out' in value['Result'],value
            count=len(wire());assert current(item)['WaitingFor']==0 and not current(item).get('InputForm')
            assert current(item)['State']==4 and len(wire())==count
            ok('whole-turn timeout terminates code execution without replay or false completion')
            sys.exit(0)
        _,done=finished('code-mcp');assert 'SIRIAI_MCP_NATIVE_OK:code-mcp' in done['Result'],done
        events=[json.loads(line) for line in MCP_RECORDS.read_text().splitlines()];assert any(e.get('event')=='call' and e['arguments']==dict(text='code-mcp') for e in events)
        ok('CodeMode discovers and invokes configured official MCP tools through the same native dispatcher')
        _,done=finished('code-store');assert 'SIRIAI_CODEMODE_STORE_OK' in done['Result'],done
        _,done=finished('code-load');assert 'SIRIAI_CODEMODE_STORE_OK' in done['Result'],done
        _,done=finished('code-load',OTHER);assert 'SIRIAI_CODEMODE_STORE_EMPTY' in done['Result'],done
        _,done=finished('code-load',workspace='second');assert 'SIRIAI_CODEMODE_STORE_EMPTY' in done['Result'],done
        separate=submit('code-load',conversation='another-phone');done=wait(lambda:terminal(separate));assert 'SIRIAI_CODEMODE_STORE_EMPTY' in done['Result'],done
        live_workers=workers(process.pid);assert len(live_workers)==3 and len({e['pid'] for e in live_workers})==3,live_workers
        call('/v1/tasks/'+first['Id'],token=OTHER,expected=404)
        ok('same conversation retains official store while another conversation, owner and workspace remain isolated')
        _,done=finished('code-sandbox');assert all(word in done['Result'] for word in ['process','require','fetch','console','undefined']),done
        _,done=finished('code-invalid');assert 'ThisToolDoesNotExist' in done['Result'],done
        _,done=finished('code-deadline');assert 'deadline' in done['Result'].lower() or 'timed' in done['Result'].lower(),done
        _,done=finished('code-after-cancel');assert 'SIRIAI_NATIVE_DOTCRAFT_READ_OK' in done['Result'],done
        ok('official JavaScript sandbox, unknown tool failure and cell deadline preserve future explicit work')
        item=submit('code-permission-deny')
        def permission():
            value=current(item);return value if value['WaitingFor']==2 else None
        prompt=wait(permission);state=json.loads(prompt['BackendState'])
        call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId='stale',Text='Allow',Allow=True),expected=409)
        call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Deny',Allow=False));done=wait(lambda:terminal(item))
        assert done['State']==3 and not (RUN/'code-permission-deny.txt').exists(),done
        item=submit('code-permission-allow');prompt=wait(permission)
        call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Allow once',Allow=True));done=wait(lambda:terminal(item))
        assert done['State']==3 and (RUN/'code-permission-allow.txt').read_text()=='SIRIAI_CODEMODE_APPROVED_WRITE_OK',done
        assert json.loads(done['BackendState'])['ThreadId']==state['ThreadId']
        ok('nested CodeMode writes pass exact existing permission identity; Deny prevents side effects and AcceptOnce executes')
        loop_event_count=len(mcp_events());item=submit('code-loop')
        wait(loop_started);cancel=call('/v1/tasks/'+item['Id']+'/cancel',{});assert cancel['State']==5,cancel
        count=len(wire())
        wait(lambda:any(e.get('event')=='tool_finished' and e['arguments'].get('text')=='code-loop' for e in mcp_events()[loop_event_count:]))
        assert current(item)['State']==5 and len(wire())==count
        _,done=finished('code-after-cancel');assert 'NO_ABORTED_STORE' in done['Result'] and 'SIRIAI_NATIVE_DOTCRAFT_READ_OK' in done['Result'],done
        assert current(item)['State']==5
        ok('native Turn cancellation terminates the active cell; late remote tool completion cannot resurrect it and explicit new work succeeds')
        loop_event_count=len(mcp_events());item=submit('code-loop');wait(loop_started);uncertain_id=item['Id'];wire_count=len(wire());old_workers={e['pid'] for e in workers(process.pid)}
        stop(kill=True);wait(lambda:not old_workers.intersection(e['pid'] for e in workers()),timeout=15)
        start();done=wait(lambda:terminal(item));assert done['State']==6 and len(wire())==wire_count,done
        assert current(first)['Result'].count('SIRIAI_NATIVE_DOTCRAFT_READ_OK')>=2
        ok('Host crash closes script workers and cold recovery marks uncertain work Interrupted without model/tool replay')
        _,done=finished('code-load');assert 'SIRIAI_CODEMODE_STORE_EMPTY' in done['Result'],done
        _,done=finished('code-after-cancel');assert 'SIRIAI_NATIVE_DOTCRAFT_READ_OK' in done['Result'],done
        claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-phone',LeaseSeconds=60));assert claim and claim['Result']
        ok('explicit restart reconnects native history; volatile CodeMode store is empty and durable results reach foreground claim')
        stopping_workers={e['pid'] for e in workers(process.pid)};stop();wait(lambda:not stopping_workers.intersection(e['pid'] for e in workers()))
        # Reject invalid mode and cross-protocol activation during preparation, before runtime/model input.
        base=json.loads(CONFIG.read_text());before=len(wire())
        for mode,backend in [('invalid','dotcraft'),(None,'dotcraft'),('on','llm')]:
            cfg=json.loads(json.dumps(base));cfg['Harnesses'][0]['DotCraftCodeMode']=mode;cfg['Harnesses'][0]['Backend']=backend
            if backend!='dotcraft':cfg['Harnesses'][0]['DotCraftMcpServers']=[]
            path=RUN/('invalid-mode-'+str(mode)+'-'+backend+'.json');path.write_text(json.dumps(cfg))
            badcmd=[str(a.dotnet),str(a.host),'--service','prepare','--id','bad-code','--config',str(path),'--runtime',str(a.dotnet.resolve()),'--directory',str(RUN/('invalid-'+str(mode)+'-'+backend))]
            bad=subprocess.run(badcmd,env=ENV,text=True,capture_output=True,timeout=40);records.append(dict(command=badcmd,stdout=bad.stdout,stderr=bad.stderr,exit_code=bad.returncode));assert bad.returncode!=0
        assert len(wire())==before
        ok('invalid and foreign-backend CodeMode configuration is rejected before script startup or model input')
        sys.exit(0)
    if a.mode=='mcp':
        def events():return [json.loads(line) for line in MCP_RECORDS.read_text().splitlines()] if MCP_RECORDS.exists() else []
        def calls():return [e for e in events() if e['event']=='call']
        def current(item,token=TOKEN):return call('/v1/tasks/'+item['Id'],token=token)
        def finished(text,token=TOKEN,workspace='test'):
            item=submit(text,token=token,workspace=workspace);done=wait(lambda:terminal(item,token));assert done['State']==3 and 'SIRIAI_MCP_NATIVE_OK:'+text in done['Result'],done;return item,done
        first,result=finished('mcp-stdio-echo');stdio=next(e for e in calls() if e['arguments']['text']=='mcp-stdio-echo')
        assert stdio['transport']=='stdio' and stdio['arguments']==dict(text='mcp-stdio-echo') and stdio['cwd']==str(WORK) and stdio['allowed']=='ONLY_MCP_ALLOWED_VALUE' and stdio['forbiddenPresent']==[],stdio
        ok('official DotCraft SDK discovers configured stdio MCP tool and invokes exact arguments in workspace with only explicit environment')
        event_count=len(events());model_count=len(wire())
        base=json.loads(CONFIG.read_text());stdio_config=base['Harnesses'][0]['DotCraftMcpServers'][0];http_config=base['Harnesses'][0]['DotCraftMcpServers'][1]
        invalid=[('case-alias',dict(stdio_config,Environment=[dict(Name='KEY',Value='one'),dict(Name='key',Value='two')])),('relative-command',dict(stdio_config,Command='python3')),('missing-command',dict(stdio_config,Command=str(RUN/'missing'))),('unknown-transport',dict(stdio_config,Type='acp')),('mixed-http',dict(http_config,Command='/bin/sh')),('url-credentials',dict(http_config,Url='https://user:pass@example.invalid/mcp')),('remote-http',dict(http_config,Url='http://example.invalid/mcp')),('host-reference',dict(http_config,Headers=[dict(Name='Authorization',ValueEnvironmentVariable='SIRIAI_DOTCRAFT_HOST_TOKEN')])),('host-literal',dict(http_config,Headers=[dict(Name='Authorization',Value=TOKEN)])),('missing-reference',dict(http_config,Headers=[dict(Name='Authorization',ValueEnvironmentVariable='SIRIAI_MCP_MISSING')])),('duplicate-header',dict(http_config,Headers=[dict(Name='X',Value='one'),dict(Name='x',Value='two')])),('wire-overflow',dict(stdio_config,Environment=[dict(Name='KEY_'+str(i),Value='x'*8192) for i in range(32)]))]
        for label,badserver in invalid:
            cfg=json.loads(json.dumps(base));cfg['Harnesses'][0]['DotCraftMcpServers']=[badserver];path=RUN/('invalid-'+label+'.json');path.write_text(json.dumps(cfg));badcmd=[str(a.dotnet),str(a.host),'--service','prepare','--id','bad-mcp','--config',str(path),'--runtime',str(a.dotnet.resolve()),'--directory',str(RUN/('invalid-'+label))]
            bad=subprocess.run(badcmd,env=ENV,capture_output=True,text=True,timeout=30);records.append(dict(command=badcmd,stdout=bad.stdout,stderr=bad.stderr,exit_code=bad.returncode));assert bad.returncode!=0, 'Invalid MCP configuration accepted: '+label
        assert len(events())==event_count and len(wire())==model_count
        ok('MCP configuration rejects native case aliases, invalid transports/process/URL/headers/credentials and escaped-budget overflow without invalid server startup or model input')
        item,done=finished('mcp-http-echo');http=next(e for e in calls() if e['arguments']['text']=='mcp-http-echo');assert http['transport']=='http'
        assert len({e['pid'] for e in calls() if e['transport']=='stdio'})==1
        ok('configured Streamable HTTP MCP performs authenticated native initialization/list/call and returns its real result')
        allowed_names=set(json.loads((REG/'credentials.json').read_text()));assert {'SIRIAI_MCP_HTTP_KEY','SIRIAI_MCP_ALLOWED'}<=allowed_names
        assert all(secret not in (REG/'host.json').read_text() for secret in [ENV['SIRIAI_MCP_HTTP_KEY'],ENV['SIRIAI_MCP_ALLOWED']])
        ok('service preparation persists MCP environment references separately and the independently started Host resolves them without caller environment')
        item=submit('mcp-stdio-mutate-deny')
        def permission():
            v=current(item);return v if v['WaitingFor']==2 else None
        prompt=wait(permission);count=len(calls());call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId='stale',Text='Allow',Allow=True),expected=409)
        call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Deny',Allow=False));done=wait(lambda:terminal(item));assert done['State']==3 and len(calls())==count
        item=submit('mcp-stdio-mutate-allow');prompt=wait(permission);call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Allow once',Allow=True));done=wait(lambda:terminal(item));assert done['State']==3 and 'SIRIAI_MCP_NATIVE_OK' in done['Result']
        ok('official MCP read-only hints and approval broker preserve foreground permission ownership; rejected write is not called and AcceptOnce executes it')
        bob,_=finished('mcp-stdio-bob',OTHER);other,_=finished('mcp-stdio-other-workspace',workspace='second')
        stdios=[next(e for e in calls() if e['arguments']['text']==text) for text in ['mcp-stdio-echo','mcp-stdio-bob','mcp-stdio-other-workspace']]
        assert len({e['pid'] for e in stdios})==3 and len({e['session'] for e in stdios})==3 and stdios[-1]['cwd']==str(RUN/'second-workspace')
        init=[e for e in events() if e['transport']=='http' and e.get('method')=='initialize'];assert len({e['session'] for e in init})==3
        call('/v1/tasks/'+first['Id'],token=OTHER,expected=404)
        ok('owners and workspaces receive separate native runtimes, stdio processes, HTTP sessions and conversation state')
        item=submit('mcp-stdio-hold-cancel');wait(lambda:any(e['arguments']['text']=='mcp-stdio-hold-cancel' for e in calls()));count=len(wire());cancel=call('/v1/tasks/'+item['Id']+'/cancel',{});assert cancel['State']==5 and len(wire())==count,cancel
        wait(lambda:any(e.get('event')=='tool_finished' and e['arguments']['text']=='mcp-stdio-hold-cancel' for e in events()))
        assert current(item)['State']==5 and len(wire())==count
        ok('native turn cancellation remains terminal after independent MCP server finishes late; remote tool stop is not falsely confirmed')
        item=submit('mcp-http-hold-cancel');wait(lambda:any(e['arguments']['text']=='mcp-http-hold-cancel' for e in calls()));count=len(wire());cancel=call('/v1/tasks/'+item['Id']+'/cancel',{});assert cancel['State']==5 and len(wire())==count,cancel
        wait(lambda:any(e.get('event')=='tool_finished' and e['arguments']['text']=='mcp-http-hold-cancel' for e in events()));assert current(item)['State']==5 and len(wire())==count
        ok('HTTP MCP late completion also cannot resurrect a cancelled native Turn or trigger another model request')
        item=submit('mcp-http-hold-crash');wait(lambda:any(e['arguments']['text']=='mcp-http-hold-crash' for e in calls()));count=len(calls());model_count=len(wire());stop(kill=True);start();done=wait(lambda:terminal(item));assert done['State']==6 and len(calls())==count and len(wire())==model_count,done
        assert current(first)['Result']==result['Result']
        ok('Host crash retains native MCP history and completed result while cold recovery never replays uncertain tool work')
        item,done=finished('mcp-stdio-after-restart');assert next(e for e in calls() if e['arguments']['text']=='mcp-stdio-after-restart')['pid']!=stdio['pid']
        claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-phone',LeaseSeconds=60));assert claim and claim['Result']
        ok('new explicit task reconnects MCP after restart, continues native history and feeds durable foreground result delivery')
        # Failed configured MCP must prevent task input, rather than silently running with missing tools.
        stop();cfg=json.loads(CONFIG.read_text());cfg['Harnesses'][0]['DotCraftMcpServers'][1]['Headers'][0]['Prefix']='WRONG-';CONFIG.write_text(json.dumps(cfg));model_count=len(wire())
        prepared=subprocess.run(cmd,env=ENV,text=True,capture_output=True,timeout=40);records.append(dict(command=cmd,stdout=prepared.stdout,stderr=prepared.stderr,exit_code=prepared.returncode));assert prepared.returncode==0
        start();item=submit('mcp-startup-failure');failed=wait(lambda:terminal(item));assert failed['State']==4 and 'MCP startup failed' in failed['Result'] and len(wire())==model_count,failed
        ok('failed MCP readiness stops before any model task input and reports explicit failure with clean runtime shutdown')
        sys.exit(0)
    if a.mode in ['input','input-timeout']:
        def current(item,token=TOKEN): return call('/v1/tasks/'+item['Id'],token=token)
        def question(item):
            value=current(item); return value if value['WaitingFor']==1 else None
        def reply(item,prompt,text='unused',action=0,expected=200,token=TOKEN):
            return call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text=text,Allow=False,InputAction=action),expected=expected,token=token)
        item=submit('ask-input-three');prompt=wait(lambda:question(item))
        assert prompt.get('InputForm'), 'DotCraft native questions must reach the typed SiriAI InputForm'
        fields=prompt['InputForm']['Fields'];assert [f['Name'] for f in fields]==['destination','format','private-note'],fields
        assert [f['Label'] for f in fields]==['Output','Format','Private'] and all(f['Type']=='string' and f['Required'] and f.get('AllowOther') for f in fields)
        assert fields[0]['Description']=='Where should the result go?' and fields[0]['Choices'][0]==dict(Value='Reports',Label='Reports',Description='Report folder')
        assert fields[2]['Secret'] and not fields[0].get('Secret',False)
        assert 'Questions' not in prompt['Activity'] and 'RequestId' not in prompt['Activity']
        before=json.loads(prompt['BackendState']);count=len(wire())
        ok('official native three-question request projects IDs, question text, described suggestions, freeform and secret metadata into the original task form')
        if a.mode=='input-timeout':
            value=wait(lambda:terminal(item));assert value['State']==4 and 'timed out' in value['Result'],value
            assert not value.get('InputForm') and len(wire())==count
            ok('configured native turn timeout includes input waiting and confirms cancellation without fabricated answers or replay')
            sys.exit(0)
        good=dict(destination='Reports',format='自定义格式',**{'private-note':'PRIVATE_REPLY_SENTINEL'})
        invalid=[{},dict(good,unknown='x'),dict(destination='Reports',format='x'),dict(good,format=3),dict(good,format=['x']),dict(good,format='   '),dict(Answers=[]),dict(Answers=dict(destination=dict(Answers=['one','two']))),dict(Answers=dict(destination=dict(Answers=['one'],extra=True)))]
        for body in invalid: reply(item,prompt,json.dumps(body),expected=400)
        reply(item,prompt,'{"destination":"Reports","destination":"Archive","format":"x","private-note":"y"}',expected=400)
        reply(item,prompt,'{bad',expected=400)
        reply(item,prompt,json.dumps(dict(good,format='x'*8200)),expected=400)
        assert current(item)['RequestId']==prompt['RequestId'] and current(item)['InputForm']==prompt['InputForm'] and len(wire())==count
        ok('missing, unknown, duplicate, malformed, non-string, blank and oversized answers leave the same native request pending without a model call')
        reply(item,dict(RequestId='stale'),json.dumps(good),expected=409)
        reply(item,prompt,json.dumps(good),expected=404,token=OTHER)
        call('/v1/tasks/unknown-task/respond',dict(RequestId=prompt['RequestId'],Text=json.dumps(good)),expected=404)
        assert current(item)['RequestId']==prompt['RequestId']
        ok('owner, task and request identity fence all structured replies before native resolution')
        reply(item,prompt,json.dumps(good));done=wait(lambda:terminal(item));assert done['State']==3,done
        assert json.loads(done['Result'].split(': ',1)[1])==dict(answers={k:dict(answers=[v]) for k,v in good.items()}) and not done.get('InputForm') and done['WaitingFor']==0
        after=json.loads(done['BackendState']);assert (before['ThreadId'],before['TurnId'])==(after['ThreadId'],after['TurnId'])
        response=json.loads(next(m['content'] for m in reversed(wire()[-1]['body']['messages']) if m['role']=='tool'))
        assert response==dict(answers={k:dict(answers=[v]) for k,v in good.items()}),response
        native_history=[json.loads(line) for p in WORK.rglob('*.jsonl') for line in p.read_text().splitlines() if 'PRIVATE_REPLY_SENTINEL' in line]
        assert native_history, 'official native response is persisted'
        native_items=[e['itemAppended']['item'] for p in WORK.rglob('*.jsonl') for e in map(json.loads,p.read_text().splitlines()) if e.get('itemAppended') and e['itemAppended']['turnId']==before['TurnId']]
        native_request=next(i['payload'] for i in native_items if i['type']=='UserInputRequest')
        native_reply=next(i['payload'] for i in native_items if i['type']=='UserInputResponse')
        assert native_reply['requestId']==native_request['requestId'] and native_reply['response']==response
        reply(item,prompt,json.dumps(good),expected=409)
        ok('flat form answers reach the official tool in its native response shape on exact original thread/turn; completion clears form and stale retry is rejected')
        legacy=submit('ask-input-three');legacyprompt=wait(lambda:question(legacy))
        reply(legacy,legacyprompt,json.dumps(dict(Answers={k:dict(Answers=[v]) for k,v in good.items()})))
        legacydone=wait(lambda:terminal(legacy));assert legacydone['State']==3 and json.loads(legacydone['Result'].split(': ',1)[1])==response
        ok('existing native multi-question Answers envelope remains compatible and passes the same field validation')
        second=submit('ask-input-three');nextprompt=wait(lambda:question(second));assert nextprompt['RequestId']!=prompt['RequestId']
        reply(second,prompt,json.dumps(good),expected=409)
        reply(second,nextprompt,'Declined',action=3);declined=wait(lambda:terminal(second));assert declined['State']==3 and '"answers":{}' in declined['Result'].replace(' ',''),declined
        ok('new question identity rejects an earlier callback; Decline resolves official empty Answers and lets the native turn continue')
        item=submit('ask-input-three');prompt=wait(lambda:question(item));count=len(wire());reply(item,prompt,'Cancel',action=2)
        cancelled=wait(lambda:terminal(item));assert cancelled['State']==5 and not cancelled.get('InputForm') and len(wire())==count,cancelled
        ok('Cancel form terminates the same native turn without submitting an empty or invented user answer')
        item=submit('ask-input');prompt=wait(lambda:question(item));reply(item,prompt,'Archive');single=wait(lambda:terminal(item));assert single['State']==3 and 'Archive' in single['Result']
        ok('existing single-question plain-text reply remains compatible with the structured form path')
        item=submit('ask-input-three');prompt=wait(lambda:question(item));count=len(wire());stop(kill=True);start();interrupted=wait(lambda:terminal(item));assert interrupted['State']==6 and not interrupted.get('InputForm') and len(wire())==count,interrupted
        reply(item,prompt,json.dumps(good),expected=409)
        ok('crash while waiting preserves native history, clears obsolete form and never replays uncertain tool or user answers')
        assert current(second)['Result']==declined['Result']
        claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-phone',LeaseSeconds=60));assert claim and claim['Result']
        ok('completed responses and declined results survive restart and remain available to foreground feedback')
        sys.exit(0)
    if a.mode=='instructions':
        image='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6J8sAAAAASUVORK5CYII='
        newparts=[dict(Kind=0,Payload='NEW_TEXT_ATTACHMENT',MediaType='text/plain',FileName='new.txt'),dict(Kind=3,Payload=image,MediaType='image/png',FileName='new.png')]
        def current(item,token=TOKEN):return call('/v1/tasks/'+item['Id'],token=token)
        def native(item):return json.loads(current(item).get('BackendState') or '{}')
        def active(text):
            item=submit(text);wait(lambda:native(item).get('TurnId'));wait(lambda:any(text in json.dumps(c['body']['messages']) for c in wire()));return item
        def update(item,text,identity,parts=None,expected=200,token=TOKEN):return call('/v1/tasks/'+item['Id']+'/update',dict(RequestId=identity,Text=text,Attachments=parts),token=token,expected=expected)
        first=active('slow-steer');before=native(first);updated=update(first,'UPDATED_IMAGE_INSTRUCTION','image-steer',newparts)
        value=wait(lambda:terminal(first));assert value['State']==3 and 'UPDATED_IMAGE_INSTRUCTION' in value['Result'] and 'NEW_TEXT_ATTACHMENT' in value['Result'],value
        saved=native(first);assert (saved['ThreadId'],saved['TurnId'])==(before['ThreadId'],before['TurnId']);ins=current(first)['Instructions'];assert len(ins)==1 and ins[0]['State']==1 and ins[0]['Attachments']==newparts
        latest=wire()[-1]['body']['messages'];m=next(m for m in reversed(latest) if m['role']=='user');assert 'data:image/png;base64,'+image in json.dumps(m) and 'NEW_TEXT_ATTACHMENT' in json.dumps(m)
        configured=next(h for h in catalog['configured'] if h['id']=='dotcraft');assert int(configured['capabilities'])&32
        ok('real official DotCraft SteerTurnAsync accepts new text+image attachments on exact original thread/turn and changes the visible result')
        count=len(wire());assert update(first,'UPDATED_IMAGE_INSTRUCTION','image-steer',newparts)['Id']==first['Id'];changed=[dict(Kind=0,Payload='different',MediaType='text/plain',FileName='new.txt')];update(first,'UPDATED_IMAGE_INSTRUCTION','image-steer',changed,409);assert len(wire())==count
        ok('durable instruction identity makes identical retries idempotent and rejects changed attachment payload after completion')
        item=active('hold-boundary');before=current(item)
        for parts in [[dict(Kind=1,Payload='https://example.invalid/image.png',MediaType='image/png')],[dict(Kind=3,Payload='AQI=',MediaType='application/octet-stream')],[dict(Kind=2,Payload='{}',MediaType='application/json')]]:
            update(item,'unsupported',uuid.uuid4().hex,parts,422)
        assert not current(item).get('Instructions');update(item,'foreign','foreign',newparts,404,OTHER);call('/v1/tasks/foreign-task/update',dict(RequestId='foreign',Text='foreign',Attachments=newparts),expected=404);assert not current(item).get('Instructions')
        assert call('/v1/tasks/'+item['Id']+'/cancel',{})['State']==5
        ok('unsupported URL/binary/JSON updates fail before admission; another owner or task cannot steer this native turn')
        item=active('slow-multiple');ids=native(item);update(item,'FIRST_NEW_CONTENT','first',[dict(Kind=0,Payload='FIRST_ATTACHMENT',MediaType='text/plain')]);update(item,'SECOND_NEW_CONTENT','second',[dict(Kind=3,Payload=image,MediaType='image/png')]);done=wait(lambda:terminal(item));assert done['State']==3,done;assert native(item)['TurnId']==ids['TurnId'];entries=current(item)['Instructions'];assert [i['Id'] for i in entries]==['first','second'] and all(i['State']==1 for i in entries);messages=wire()[-1]['body']['messages'];assert all(text in json.dumps(messages) for text in ['FIRST_NEW_CONTENT','FIRST_ATTACHMENT','SECOND_NEW_CONTENT','data:image/png;base64,'])
        ok('sequential content instructions keep distinct durable identities and reach the same official native turn')
        item=active('hold-cancel-after-update');update(item,'hold-new-content','cancel-update',newparts);done=call('/v1/tasks/'+item['Id']+'/cancel',{});assert done['State']==5,done;assert current(item)['Instructions'][0]['State']==1
        ok('cancelling after content delivery confirms native turn termination while preserving the exact delivered instruction record')
        item=active('hold-crash-with-update');update(item,'hold-delivered-content','crash-update',newparts);snapshot=current(item);count=len(wire());stop(kill=True);start();done=wait(lambda:terminal(item));assert done['State']==6 and len(wire())==count;assert current(item)['Instructions']==snapshot['Instructions']
        ok('Host crash retains delivered attachment provenance and never resends uncertain native model or tool work')
        # Original text-only update remains the same native steering behavior.
        item=active('slow-text-only');update(item,'TEXT_ONLY_STILL_WORKS','text-only');done=wait(lambda:terminal(item));assert done['State']==3 and 'TEXT_ONLY_STILL_WORKS' in done['Result']
        ok('existing text-only steering remains supported through the same content projection')
        claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-phone',LeaseSeconds=60));assert claim and claim['Result'];stop();start();assert current(first)['Instructions'][0]['Attachments']==newparts and current(first)['Result']==value['Result']
        ok('completed content instructions and final feedback survive Host restart and foreground reconnection')
        sys.exit(0)
    if a.mode!='regular':
        item=submit('hold-timeout' if a.mode=='timeout' else a.mode)
        value=wait(lambda:terminal(item));assert value['State']==4,value
        assert wire() and len(wire())==1
        if a.mode=='timeout':assert 'timed out' in value['Result']
        ok(a.mode+' exposes the observed failure without false success, retry or silent result truncation')
        sys.exit(0)
    first=submit('slow','intent'); assert first['State'] in [0,1]
    assert submit('slow','intent')['Id']==first['Id'];call('/v1/tasks/'+first['Id'],token=OTHER,expected=404)
    result=wait(lambda:terminal(first));assert result['State']==3 and result['Result']=='DotCraft fixture answer: slow',result
    ok('foreground admission is nonblocking and idempotent while actual DotCraft executes independently')
    item=submit('read-proof');result=wait(lambda:terminal(item));assert result['State']==3 and 'SIRIAI_NATIVE_DOTCRAFT_READ_OK' in result['Result'],result
    assert any(m['role']=='tool' for c in wire() for m in c['body']['messages'])
    ok('actual DotCraft agent loop invokes its official ReadFile tool and returns the observed file content')
    for objective,allow in [('permission-deny',False),('permission-allow',True)]:
        item=submit(objective)
        def permission():
            value=call('/v1/tasks/'+item['Id']);return value if value['WaitingFor']==2 else None
        prompt=wait(permission)
        assert objective+'.txt' in prompt['Activity'] and not (RUN/(objective+'.txt')).exists()
        call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId='stale',Text='Allow once',Allow=True),expected=409)
        call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Allow once',Allow=allow),token=OTHER,expected=404)
        call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Allow once',Allow=allow))
        assert wait(lambda:terminal(item))['State']==3
        assert (RUN/(objective+'.txt')).exists()==allow
    ok('official out-of-workspace write waits for exact foreground approval; rejection prevents the side effect and AcceptOnce permits it')
    item=submit('ask-input')
    def question():
        value=call('/v1/tasks/'+item['Id']);return value if value['WaitingFor']==1 else None
    prompt=wait(question)
    call('/v1/tasks/'+item['Id']+'/respond',dict(RequestId=prompt['RequestId'],Text='Reports',Allow=False))
    value=wait(lambda:terminal(item));assert value['State']==3 and 'Reports' in value['Result'],value
    ok('native RequestUserInput waits for the foreground and treats Unity Allow=false as an answer')
    bob=submit('bob',token=OTHER);assert wait(lambda:terminal(bob,OTHER))['State']==3
    checkpoints=[json.loads(call('/v1/tasks/'+t['Id'],token=token)['BackendState']) for t,token in [(first,TOKEN),(item,TOKEN),(bob,OTHER)]]
    assert checkpoints[0]['ThreadId']==checkpoints[1]['ThreadId']!=checkpoints[2]['ThreadId']
    ok('native conversation continuity reuses the owner/workspace thread and separates another owner')
    stop();start();assert call('/v1/tasks/'+first['Id'])['Result']=='DotCraft fixture answer: slow'
    follow=submit('after-restart');assert wait(lambda:terminal(follow))['State']==3
    after=json.loads(call('/v1/tasks/'+follow['Id'])['BackendState']);assert after['ThreadId']==checkpoints[0]['ThreadId']
    ok('host restart preserves completed results and resumes persisted native DotCraft conversation history')
    item=submit('slow-steer')
    wait(lambda:any('slow-steer' in json.dumps(c['body']['messages']) for c in wire()))
    call('/v1/tasks/'+item['Id']+'/update',dict(Text='Focus on the verified result'))
    value=wait(lambda:terminal(item));assert value['State']==3 and 'Focus on the verified result' in value['Result'],value
    ok('mid-turn steering reaches the same native DotCraft turn and changes its visible response')
    image='iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6J8sAAAAASUVORK5CYII='
    item=submit('image-input',attachments=[dict(Kind=3,Payload=image,MediaType='image/png')]);assert wait(lambda:terminal(item))['State']==3
    assert any('data:image/png;base64,' in json.dumps(c['body']['messages']) for c in wire())
    before=len(wire())
    call('/v1/tasks',dict(ConversationId='phone',SubmissionKey='bad-attachment',HarnessId='dotcraft',WorkspaceKey='test',Objective='bad',Attachments=[dict(Kind=1,Payload='https://example.invalid/image.png')]),expected=422)
    assert len(wire())==before
    ok('inline images reach the native provider and unsupported remote attachments fail before task admission')
    item=submit('hold-cancel');wait(lambda:json.loads(call('/v1/tasks/'+item['Id']).get('BackendState') or '{}').get('TurnId'))
    wait(lambda:any('hold-cancel' in json.dumps(c['body']['messages']) for c in wire()))
    result=call('/v1/tasks/'+item['Id']+'/cancel',{});assert result['State']==5,result
    ok('cancel waits for official native turn termination rather than treating request admission as completion')
    item=submit('hold-crash');wait(lambda:json.loads(call('/v1/tasks/'+item['Id']).get('BackendState') or '{}').get('TurnId'))
    wait(lambda:any('hold-crash' in json.dumps(c['body']['messages']) for c in wire()))
    before=len(wire());stop(kill=True);start();result=wait(lambda:terminal(item));assert result['State']==6,result
    assert len(wire())==before
    ok('cold recovery reports interrupted native work without replaying uncertain tool or model side effects')
    claim=call('/v1/results/claim',dict(ConversationId='phone',ClientId='reconnected-phone',LeaseSeconds=60));assert claim is not None
    ok('reconnected foreground can claim durable completion for its existing feedback and speech pipeline')
    for path in WORK.rglob('*'):
        if path.is_file():assert KEY.encode() not in path.read_bytes(),str(path)
    ok('model credential is absent from native workspace persistence and public task responses')
finally:
    transcript=wire()
    stop();peer.terminate();peer.wait(timeout=5);peerlog.close()
    if mcp_peer is not None:mcp_peer.terminate();mcp_peer.wait(timeout=5);mcp_log.close()
    (OUT/'http.json').write_text(json.dumps(records,indent=2)+'\n')
    (OUT/'results.json').write_text(json.dumps(passed,indent=2)+'\n')
    if transcript is not None: (OUT/'wire.json').write_text(json.dumps(transcript,indent=2)+'\n')
