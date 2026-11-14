#!/usr/bin/env python3
"""Independent stdio/Streamable HTTP MCP peer for actual official DotCraft SDK checks."""
import argparse,json,os,sys,threading,time,uuid
from pathlib import Path
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
p=argparse.ArgumentParser();p.add_argument('--http',type=int);p.add_argument('--records');a=p.parse_args()
records=Path(a.records or os.environ['SIRIAI_MCP_RECORDS']);gate=threading.Lock();pending={};sessions=set();pid=os.getpid()
def record(value):
    with gate:
        with records.open('a') as f:f.write(json.dumps(dict(pid=pid,transport='http' if a.http else 'stdio',**value))+'\n')
def dispatch(item,session):
    method=item.get('method');rid=item.get('id');params=item.get('params') or {}
    record(dict(event='request',method=method,id=rid,session=session,params=params))
    if method=='notifications/cancelled':
        pending.setdefault((session,str(params['requestId'])),threading.Event()).set();return None
    if rid is None:return None
    if method=='initialize':result=dict(protocolVersion='2025-06-18',capabilities=dict(tools={}),serverInfo=dict(name='independent-mcp-peer',version='1.0'))
    elif method=='ping':result={}
    elif method=='tools/list':result=dict(tools=[dict(name=n,description='Independent '+n,inputSchema=dict(type='object',properties=dict(text=dict(type='string')),required=['text'],additionalProperties=False),annotations=dict(readOnlyHint=n!='mutate',destructiveHint=n=='mutate',idempotentHint=False,openWorldHint=False)) for n in ['echo','mutate','hold']])
    elif method in ['resources/list','resources/templates/list']:result={'resources' if method=='resources/list' else 'resourceTemplates':[]}
    elif method=='tools/call':
        name=params['name'];args=params.get('arguments') or {};assert list(args)==['text'] and isinstance(args['text'],str)
        record(dict(event='call',name=name,arguments=args,session=session,cwd=os.getcwd(),allowed=os.environ.get('SIRIAI_MCP_ALLOWED'),forbiddenPresent=[n for n in ['SIRIAI_DOTCRAFT_HOST_TOKEN','SIRIAI_DOTCRAFT_OTHER_TOKEN','SIRIAI_DOTCRAFT_MODEL_KEY','SIRIAI_MCP_UNLISTED'] if n in os.environ]))
        if name=='hold':pending.setdefault((session,str(rid)),threading.Event()).wait(30 if 'crash' in args['text'] else 5)
        record(dict(event='tool_finished',name=name,arguments=args,session=session))
        result=dict(content=[dict(type='text',text='SIRIAI_MCP_NATIVE_OK:'+args['text'])],isError=False)
    else:return dict(jsonrpc='2.0',id=rid,error=dict(code=-32601,message='unsupported fixture method'))
    return dict(jsonrpc='2.0',id=rid,result=result)
if a.http:
    class Peer(BaseHTTPRequestHandler):
        def log_message(self,*args):pass
        def do_GET(self):self.send_response(405);self.end_headers()
        def do_DELETE(self):
            record(dict(event='delete',session=self.headers.get('Mcp-Session-Id')));self.send_response(204);self.end_headers()
        def do_POST(self):
            assert self.path=='/mcp'
            if self.headers.get('X-Mcp-Key')!=os.environ['SIRIAI_MCP_HTTP_KEY']:
                self.send_response(403);self.end_headers();return
            if self.headers.get('Transfer-Encoding','').lower()=='chunked':
                chunks=[]
                while True:
                    size=int(self.rfile.readline().split(b';',1)[0],16)
                    if not size:self.rfile.readline();break
                    chunks.append(self.rfile.read(size));assert self.rfile.read(2)==b'\r\n'
                raw=b''.join(chunks)
            else:raw=self.rfile.read(int(self.headers['Content-Length']))
            item=json.loads(raw);session=self.headers.get('Mcp-Session-Id')
            if item.get('method')=='initialize':session=uuid.uuid4().hex;sessions.add(session)
            elif session not in sessions:self.send_response(404);self.end_headers();return
            response=dispatch(item,session)
            self.send_response(200 if response else 202);self.send_header('Content-Type','application/json');self.send_header('Mcp-Session-Id',session);self.end_headers()
            if response:
                try:self.wfile.write(json.dumps(response).encode())
                except (BrokenPipeError,ConnectionResetError):pass
    ThreadingHTTPServer(('127.0.0.1',a.http),Peer).serve_forever()
else:
    record(dict(event='start',cwd=os.getcwd()))
    def handle(item):
        result=dispatch(item,'stdio-'+str(pid))
        if result:
            with gate:print(json.dumps(result),flush=True)
    for line in sys.stdin:
        threading.Thread(target=handle,args=(json.loads(line),),daemon=True).start()
