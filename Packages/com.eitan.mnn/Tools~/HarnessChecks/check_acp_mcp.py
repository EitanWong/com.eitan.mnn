#!/usr/bin/env python3
"""Observe configured native MCP sessions, credential isolation and restart identity."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse, copy, json, os, socket, stat, subprocess, sys, time, urllib.error, urllib.request, uuid
from pathlib import Path

SAMPLE=PACKAGE_ROOT
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--previous-host',type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args()
OUT=a.output.resolve();OUT.mkdir(parents=True,exist_ok=True);RUN=OUT/('run-'+uuid.uuid4().hex);RUN.mkdir();PEER=RUN/'peer';PEER.mkdir();FIXTURE=SAMPLE/'Tools~/HarnessChecks/Fixtures/acp_mcp_fixture.py'
secrets={n:uuid.uuid4().hex*2 for n in ['SIRIAI_MCP_TOKEN','SIRIAI_MCP_STDIO','SIRIAI_MCP_HTTP','SIRIAI_MCP_SSE']}
for name,file in [('SIRIAI_MCP_STDIO','stdio-secret.txt'),('SIRIAI_MCP_HTTP','http-secret.txt'),('SIRIAI_MCP_SSE','sse-secret.txt')]: (PEER/file).write_text(secrets[name])
(PEER/'tool-value.txt').write_text('native-file-value');(PEER/'capabilities.json').write_text(json.dumps(dict(http=True,sse=True)))
ENV=dict(os.environ,**secrets,SIRIAI_MCP_FIXTURE_ROOT=str(PEER),SIRIAI_MCP_ALIAS=secrets['SIRIAI_MCP_TOKEN']);TOKEN=secrets['SIRIAI_MCP_TOKEN'];private_values=set(secrets.values());records=[];passed=[];process=None;log=None
serverlog=(RUN/'mcp-http.log').open('w');server=subprocess.Popen([sys.executable,'-u',str(FIXTURE),'mcp-http'],env=ENV,stdout=serverlog,stderr=serverlog)
def wait(test,timeout=20):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        value=test()
        if value:return value
        time.sleep(.03)
    raise AssertionError('Timed out observing native MCP behavior')
port=wait(lambda:int((PEER/'port.txt').read_text()) if (PEER/'port.txt').exists() else None)
with socket.socket() as sock:sock.bind(('127.0.0.1',0));hostport=sock.getsockname()[1]
URL='http://127.0.0.1:'+str(hostport);CONFIG=RUN/'host.json';REG=RUN/'registration'
servers=[dict(Name='local-tool',Type='stdio',Command=str(Path(sys.executable).resolve()),Arguments=['-u',str(FIXTURE),'mcp-stdio','literal $ value','中文 argument'],Environment=[dict(Name='MCP_TOOL_SECRET',ValueEnvironmentVariable='SIRIAI_MCP_STDIO')]),
    dict(Name='http-tool',Type='http',Url='http://127.0.0.1:'+str(port)+'/http',Headers=[dict(Name='Authorization',ValueEnvironmentVariable='SIRIAI_MCP_HTTP',Prefix='Bearer '),dict(Name='X-Static',Value='literal $ header')]),
    dict(Name='sse-tool',Type='sse',Url='http://127.0.0.1:'+str(port)+'/sse',Headers=[dict(Name='Authorization',ValueEnvironmentVariable='SIRIAI_MCP_SSE',Prefix='Bearer '),dict(Name='X-Static',Value='literal $ header')])]
expected=[dict(name='local-tool',command=servers[0]['Command'],args=servers[0]['Arguments'],env=[dict(name='MCP_TOOL_SECRET',value=secrets['SIRIAI_MCP_STDIO'])]),
    *[dict(type=kind,name=kind+'-tool',url=servers[i]['Url'],headers=[dict(name='Authorization',value='Bearer '+secrets['SIRIAI_MCP_'+kind.upper()]),dict(name='X-Static',value='literal $ header')]) for i,kind in [(1,'http'),(2,'sse')]]]
(PEER/'expected.json').write_text(json.dumps(expected))
config=dict(Listen=URL,DataDirectory=str(RUN/'data'),Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SIRIAI_MCP_TOKEN')],Workspaces=dict(test=str(RUN)),Harnesses=[dict(Id='acp',Protocol='acp',Command=sys.executable,Arguments=['-u',str(FIXTURE),'agent'],ForwardEnvironment=['SIRIAI_MCP_FIXTURE_ROOT'],AcpMcpServers=servers)])
CONFIG.write_text(json.dumps(config))
def ok(text):passed.append(text);print('PASS '+text,flush=True)
def cli(directory=REG,path=CONFIG,env=ENV,expected_status=0):
    cmd=['dotnet',str(a.host),'--service','prepare','--id','acp-mcp','--config',str(path),'--runtime',str(Path('/opt/homebrew/bin/dotnet').resolve()),'--directory',str(directory)]
    r=subprocess.run(cmd,text=True,capture_output=True,env=env,timeout=40);records.append(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));assert all(v not in r.stdout+r.stderr for v in private_values);assert r.returncode==expected_status,records[-1]
def call(path,body=None,expected_status=200):
    req=urllib.request.Request(URL+path,None if body is None else json.dumps(body).encode(),headers={'Authorization':'Bearer '+TOKEN,'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=15) as response:code,raw=response.status,response.read()
    except urllib.error.HTTPError as error:code,raw=error.code,error.read()
    assert code==expected_status,(path,code,raw);assert all(v.encode() not in raw for v in private_values)
    return json.loads(raw) if raw else None
def start(expect_failure=False):
    global process,log
    log=(RUN/('host-'+uuid.uuid4().hex+'.log')).open('w');process=subprocess.Popen(['dotnet',str(a.host),'--service','run','--directory',str(REG)],env={k:v for k,v in os.environ.items() if k not in secrets},stdout=log,stderr=log)
    if expect_failure:
        code=process.wait(timeout=15);assert code!=0;log.flush();assert all(v not in Path(log.name).read_text() for v in secrets.values());stop();return
    def ready():
        if process.poll() is not None:raise AssertionError('Host exited '+str(process.returncode))
        try:return call('/v1/harnesses')
        except urllib.error.URLError:return None
    return wait(ready)
def stop():
    global process
    if process is not None:
        if process.poll() is None:process.terminate()
        try:process.wait(timeout=10)
        except subprocess.TimeoutExpired:process.kill();process.wait(timeout=5)
        process=None;log.close()
def wire():
    f=PEER/'wire.jsonl';return [json.loads(line) for line in f.read_text().splitlines()] if f.exists() else []
def methods(name):return [v['frame'] for v in wire() if v['direction']=='client' and v['frame'].get('method')==name]
def submit(conversation='phone',harness='acp'):
    return call('/v1/tasks',dict(ConversationId=conversation,SubmissionKey=uuid.uuid4().hex,HarnessId=harness,WorkspaceKey='test',Objective='Use configured MCP tools'),202)
def terminal(task):
    value=call('/v1/tasks/'+task['Id']);return value if value['State'] in [3,4,5,6] else None
def complete(task,result='MCP tools: native-file-value,native-file-value,native-file-value'):
    value=wait(lambda:terminal(task));assert value['State']==3 and value['Result']==result,value;return value
try:
    cli();start();first=submit();complete(first)
    assert methods('session/new')[-1]['params']['mcpServers']==expected
    events=[json.loads(line)['kind'] for line in (PEER/'events.jsonl').read_text().splitlines()]
    assert 'stdio-tools/call' in events and '/http-tools/call' in events and '/sse-tools/call' in events
    ok('configured stdio/HTTP/SSE MCP entries reach native session and execute independent real tool calls with exact arguments and scoped credentials')
    snapshot={p.name:p.read_bytes() for p in REG.iterdir() if p.name!='prepare.lock'};cli();assert snapshot=={p.name:p.read_bytes() for p in REG.iterdir() if p.name!='prepare.lock'}
    stored=json.loads((REG/'credentials.json').read_text());assert all(stored[n]==v for n,v in secrets.items())
    assert stat.S_IMODE((REG/'credentials.json').stat().st_mode)==0o600
    for file in [REG/'host.json',REG/'host.plist',REG/'registration.json',*(RUN/'data').glob('*.json')]:assert all(v.encode() not in file.read_bytes() for v in secrets.values()),file
    ok('MCP credential references join the private idempotent service snapshot without appearing in public task or durable task/session records')
    stop();start();complete(submit());assert len(methods('session/new'))==1 and methods('session/load')[-1]['params']['mcpServers']==expected
    stop();(PEER/'resume').touch();start();complete(submit());assert methods('session/resume')[-1]['params']['mcpServers']==expected
    ok('service restart without parent credentials reuses the same native session and passes exact MCP configuration to load and resume')
    stop();count=len(methods('session/prompt'))
    for label,cap in [('missing-http',dict(sse=True)),('missing-sse',dict(http=True)),('nonboolean',dict(http='true',sse=True))]:
        (PEER/'capabilities.json').write_text(json.dumps(cap));start();before=len(methods('session/new'))+len(methods('session/resume'))+len(methods('session/load'));bad=submit(label);value=wait(lambda:terminal(bad));assert value['State']==4 and 'MCP' in value['Result'],value
        assert len(methods('session/prompt'))==count and len(methods('session/new'))+len(methods('session/load'))+len(methods('session/resume'))==before;stop()
    (PEER/'capabilities.json').write_text(json.dumps(dict(http=True,sse=True)));start();complete(submit('missing-http'))
    ok('missing and nonboolean native MCP transport capability rejects before session creation or prompt and a corrected new process can use the untouched context')
    stop();(PEER/'capabilities.json').write_text(json.dumps(dict(sse=True)));start();before=len(methods('session/resume'))+len(methods('session/load'));count=len(methods('session/prompt'));bad=submit();assert wait(lambda:terminal(bad))['State']==4
    assert len(methods('session/resume'))+len(methods('session/load'))==before and len(methods('session/prompt'))==count;stop()
    (PEER/'capabilities.json').write_text(json.dumps(dict(http=True,sse=True)));start();complete(submit())
    ok('withdrawing MCP capability on an existing native session sends no resume/load or prompt and restoring it preserves that session')
    stop();count=len(methods('session/new'))
    for index,(name,file) in enumerate([('SIRIAI_MCP_STDIO','stdio-secret.txt'),('SIRIAI_MCP_HTTP','http-secret.txt'),('SIRIAI_MCP_SSE','sse-secret.txt')]):
        secrets[name]=uuid.uuid4().hex*2;ENV[name]=secrets[name];private_values.add(secrets[name]);(PEER/file).write_text(secrets[name])
        expected[index]['env' if index==0 else 'headers'][0]['value']=('' if index==0 else 'Bearer ')+secrets[name]
    (PEER/'expected.json').write_text(json.dumps(expected));cli();start();complete(submit());assert len(methods('session/new'))==count
    for file in [REG/'host.json',REG/'host.plist',REG/'registration.json',*(RUN/'data').glob('*.json')]:assert all(v.encode() not in file.read_bytes() for v in private_values),file
    ok('rotated referenced MCP credentials refresh exact native configuration on restart without changing native session identity or leaking old/new values')
    stop();(PEER/'reject-native').touch();start();before=len(methods('session/prompt'));bad=submit('native-reject');value=wait(lambda:terminal(bad));assert value['State']==6,value
    assert len(methods('session/prompt'))==before;stop();(PEER/'reject-native').unlink();start();bad=submit('native-reject');assert wait(lambda:terminal(bad))['State']==6 and len(methods('session/prompt'))==before
    ok('native MCP session rejection remains uncertain and is never replaced or retried on the same conversation')
    stop();changed=copy.deepcopy(config);changed['Harnesses'][0]['AcpMcpServers'][0]['Arguments'].append('changed');CONFIG.write_text(json.dumps(changed));cli();start(expect_failure=True);CONFIG.write_text(json.dumps(config));cli();start();complete(submit());stop()
    ok('changed MCP execution identity fences startup against persisted native session indices and restoring original configuration preserves continuity')
    variants=[]
    for label,mutate in [
        ('duplicate',lambda x:x.append(copy.deepcopy(x[0]))),
        ('unknown',lambda x:x[0].update(Type='udp')),
        ('inchannel-mixed-shape',lambda x:x[0].update(Type='acp',Url='https://example.invalid')),
        ('missing-command',lambda x:x[0].update(Command=str(RUN/'not-installed'))),
        ('invalid-url',lambda x:x[1].update(Url='file:///tmp/tool')),
        ('url-credentials',lambda x:x[1].update(Url='https://user:pass@example.invalid/tool')),
        ('mixed-shape',lambda x:x[0].update(Url='https://example.invalid')),
        ('duplicate-header',lambda x:x[1]['Headers'].append(dict(Name='authorization',Value='duplicate'))),
        ('header-injection',lambda x:x[1]['Headers'].append(dict(Name='X-Injected',Value='a\r\nb'))),
        ('ambiguous-value',lambda x:x[0]['Environment'][0].update(Value='also-literal')),
        ('missing-source',lambda x:x[0]['Environment'][0].update(ValueEnvironmentVariable='SIRIAI_MCP_MISSING')),
        ('host-auth-source',lambda x:x[0]['Environment'][0].update(ValueEnvironmentVariable='SIRIAI_MCP_TOKEN')),
        ('host-auth-literal',lambda x:x[1]['Headers'][0].update(Value='Bearer '+TOKEN,ValueEnvironmentVariable=None,Prefix=None)),
        ('host-auth-alias',lambda x:x[0]['Environment'][0].update(ValueEnvironmentVariable='SIRIAI_MCP_ALIAS')),
        ('host-auth-argument',lambda x:x[0]['Arguments'].append(TOKEN)),
        ('host-auth-url',lambda x:x[1].update(Url='https://example.invalid/?token='+TOKEN)),
        ('header-name-newline',lambda x:x[1]['Headers'][0].update(Name='Authorization\n')),
        ('unknown-field',lambda x:x[0].update(Unexpected=True)),
        ('wire-budget',lambda x:x[0].update(Arguments=['<'*4000]*128))]:
        bad=copy.deepcopy(config);mutate(bad['Harnesses'][0]['AcpMcpServers']);path=RUN/('invalid-'+label+'.json');path.write_text(json.dumps(bad));cli(directory=RUN/('invalid-reg-'+label),path=path,expected_status=2);variants.append(label)
    assert not (PEER/'errors.txt').exists()
    ok('invalid/ambiguous/unsupported MCP configuration, missing credentials, host-token forwarding and escaped budget overflow fail preparation before native execution')
    stop();local=copy.deepcopy(config);local['Harnesses'][0]['AcpMcpServers']=servers[:1];local['Harnesses'][0]['Id']='stdio-baseline';CONFIG.write_text(json.dumps(local));(PEER/'expected.json').write_text(json.dumps(expected[:1]));(PEER/'capabilities.json').write_text('{}');cli();start()
    # Reuse the public task API with the deliberately separate configured harness identity.
    task=call('/v1/tasks',dict(ConversationId='stdio-only',SubmissionKey=uuid.uuid4().hex,HarnessId='stdio-baseline',WorkspaceKey='test',Objective='Use configured MCP tools'),202)
    value=wait(lambda:terminal(task));assert value['State']==3 and value['Result']=='MCP tools: native-file-value',value
    ok('stdio MCP remains available as baseline ACP capability when the Agent advertises no optional MCP transports')
    if a.previous_host:
        stop();legacy=copy.deepcopy(config);legacy['Harnesses'][0].pop('AcpMcpServers');legacy['Harnesses'][0]['Id']='legacy-acp';CONFIG.write_text(json.dumps(legacy));(PEER/'expected.json').write_text('[]')
        current=a.host;a.host=a.previous_host;cli();start();complete(submit('legacy','legacy-acp'),'MCP tools: ');sid=methods('session/prompt')[-1]['params']['sessionId'];stop();count=len(methods('session/new'));a.host=current
        cli();start();complete(submit('legacy','legacy-acp'),'MCP tools: ');assert len(methods('session/new'))==count
        assert methods('session/prompt')[-1]['params']['sessionId']==sid
        ok('upgrading the actual prior Host with omitted MCP configuration preserves its native session fingerprint and resumes the same session')
finally:
    transcript=wire();stop();server.terminate();server.wait(timeout=10);serverlog.close()
    (OUT/'results.json').write_text(json.dumps(passed,indent=2)+'\n');(OUT/'wire.json').write_text(json.dumps(transcript,indent=2)+'\n');(OUT/'commands.json').write_text(json.dumps(records,indent=2)+'\n')
