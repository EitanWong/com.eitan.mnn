#!/usr/bin/env python3
"""Independent ACP Agent + MCP tool fixture; no production implementation is imported."""
import json, os, queue, subprocess, sys, threading, time, urllib.parse, urllib.request, uuid
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MODE=sys.argv[1];ROOT=Path(os.environ['SIRIAI_MCP_FIXTURE_ROOT']);ROOT.mkdir(exist_ok=True)
def rpc(frame):
    method=frame['method']
    if method=='initialize':value=dict(protocolVersion=frame['params']['protocolVersion'],capabilities=dict(tools={}),serverInfo=dict(name='independent-mcp',version='1'))
    elif method=='tools/list':value=dict(tools=[dict(name='read_fixture',description='Read fixture workspace evidence',inputSchema=dict(type='object',properties={}))])
    elif method=='tools/call':
        assert frame['params']==dict(name='read_fixture',arguments={})
        value=dict(content=[dict(type='text',text=(ROOT/'tool-value.txt').read_text())],isError=False)
    else:raise AssertionError(method)
    return dict(jsonrpc='2.0',id=frame['id'],result=value)
def calls():
    return [dict(jsonrpc='2.0',id=i,method=method,params=params) for i,method,params in [
        (1,'initialize',dict(protocolVersion='2024-11-05',capabilities={},clientInfo=dict(name='independent-acp-agent',version='1'))),
        (2,'tools/list',{}),(3,'tools/call',dict(name='read_fixture',arguments={}))]]
def event(kind):
    with (ROOT/'events.jsonl').open('a') as f:f.write(json.dumps(dict(kind=kind,pid=os.getpid()))+'\n')
if MODE=='mcp-stdio':
    assert os.environ.get('MCP_TOOL_SECRET')==(ROOT/'stdio-secret.txt').read_text()
    assert sys.argv[2:]==['literal $ value','中文 argument']
    event('stdio-start')
    for line in sys.stdin:
        value=json.loads(line)
        if 'id' not in value:continue
        result=rpc(value);print(json.dumps(result),flush=True);event('stdio-'+value['method'])
    sys.exit(0)
if MODE=='mcp-http':
    streams={}
    class Handler(BaseHTTPRequestHandler):
        def log_message(self,*_):pass
        def authorized(self):
            expected='Bearer '+(ROOT/('http-secret.txt' if self.path=='/http' else 'sse-secret.txt')).read_text()
            if self.headers.get('Authorization')!=expected:self.send_error(401);return False
            assert self.headers.get('X-Static')=='literal $ header';return True
        def do_GET(self):
            if self.path!='/sse' or not self.authorized():return
            sid=uuid.uuid4().hex;messages=queue.Queue();streams[sid]=messages
            self.send_response(200);self.send_header('Content-Type','text/event-stream');self.send_header('Cache-Control','no-cache');self.end_headers()
            self.wfile.write(('event: endpoint\ndata: /messages?id='+sid+'\n\n').encode());self.wfile.flush()
            try:
                for _ in range(3):
                    value=messages.get(timeout=10);self.wfile.write(('event: message\ndata: '+json.dumps(value)+'\n\n').encode());self.wfile.flush()
            finally:streams.pop(sid,None)
        def do_POST(self):
            if not self.authorized():return
            frame=json.loads(self.rfile.read(int(self.headers['Content-Length'])));event(('/http' if self.path=='/http' else '/sse')+'-'+frame['method'])
            result=json.dumps(rpc(frame))
            if self.path.startswith('/messages?'):
                sid=urllib.parse.parse_qs(urllib.parse.urlparse(self.path).query)['id'][0];streams[sid].put(json.loads(result));self.send_response(202);self.send_header('Content-Length','0');self.end_headers();return
            data=result.encode();self.send_response(200);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(data)));self.end_headers();self.wfile.write(data)
    server=ThreadingHTTPServer(('127.0.0.1',0),Handler);(ROOT/'port.txt').write_text(str(server.server_port));server.serve_forever()
    sys.exit(0)

assert MODE=='agent'
WRITE=threading.Lock();sessions={};pending={}
def record(direction,frame):
    with (ROOT/'wire.jsonl').open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),direction=direction,frame=frame))+'\n')
def emit(frame):
    frame=dict(jsonrpc='2.0',**frame)
    with WRITE:record('agent',frame);print(json.dumps(frame),flush=True)
def run_mcp(server):
    if 'type' not in server:
        env=dict(os.environ)
        for entry in server['env']:env[entry['name']]=entry['value']
        process=subprocess.Popen([server['command'],*server['args']],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,env=env)
        try:
            response=None
            for frame in calls():
                process.stdin.write(json.dumps(frame)+'\n');process.stdin.flush();response=json.loads(process.stdout.readline());assert response['id']==frame['id']
            return response['result']['content'][0]['text']
        finally:
            process.stdin.close();process.wait(timeout=5);assert process.returncode==0
    response=None;stream=None
    headers=dict({'Content-Type':'application/json'},**{h['name']:h['value'] for h in server['headers']})
    def read_event():
        fields={}
        while True:
            line=stream.readline().decode().strip()
            if not line and fields:return fields
            if ': ' in line:
                key,value=line.split(': ',1);fields[key]=value
    endpoint=server['url']
    if server['type']=='sse':
        stream=urllib.request.urlopen(urllib.request.Request(endpoint,headers=headers),timeout=10)
        handshake=read_event();assert handshake['event']=='endpoint';endpoint=urllib.parse.urljoin(endpoint,handshake['data'])
    for frame in calls():
        request=urllib.request.Request(endpoint,json.dumps(frame).encode(),headers=headers)
        with urllib.request.urlopen(request,timeout=5) as result:raw=result.read().decode()
        response=json.loads(raw if stream is None else read_event()['data'])
    if stream is not None:stream.close()
    return response['result']['content'][0]['text']
try:
    # MCP-only credentials must not become general ACP process environment variables.
    for name in ['SIRIAI_MCP_STDIO','SIRIAI_MCP_HTTP','SIRIAI_MCP_SSE','SIRIAI_MCP_TOKEN']:
        assert name not in os.environ,name
    for line in sys.stdin:
        frame=json.loads(line);record('client',frame);method=frame.get('method');params=frame.get('params',{})
        if method=='initialize':
            capabilities=json.loads((ROOT/'capabilities.json').read_text())
            emit(dict(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,sessionCapabilities=dict(resume={}) if (ROOT/'resume').exists() else {},mcpCapabilities=capabilities),agentInfo=dict(name='independent-mcp-agent',version='1'))))
        elif method in ['session/new','session/load','session/resume']:
            assert params['cwd']==os.getcwd()
            expected=json.loads((ROOT/'expected.json').read_text())
            if params['mcpServers']!=expected:
                emit(dict(id=frame['id'],error=dict(code=-32000,message='MCP configuration mismatch')));continue
            if (ROOT/'reject-native').exists():
                emit(dict(id=frame['id'],error=dict(code=-32000,message='Native MCP setup rejected')));continue
            sid=params.get('sessionId') or 'mcp-'+uuid.uuid4().hex;sessions[sid]=params['mcpServers']
            if method=='session/new':
                (ROOT/('session-'+sid)).touch();emit(dict(id=frame['id'],result=dict(sessionId=sid)))
            else:
                assert (ROOT/('session-'+sid)).exists()
                emit(dict(id=frame['id'],result={}))
        elif method=='session/prompt':
            results=[run_mcp(server) for server in sessions[params['sessionId']]]
            text='MCP tools: '+','.join(results)
            emit(dict(method='session/update',params=dict(sessionId=params['sessionId'],update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text)))))
            emit(dict(id=frame['id'],result=dict(stopReason='end_turn')))
        else:raise AssertionError(method)
except Exception:
    import traceback
    (ROOT/'errors.txt').write_text(traceback.format_exc());raise
