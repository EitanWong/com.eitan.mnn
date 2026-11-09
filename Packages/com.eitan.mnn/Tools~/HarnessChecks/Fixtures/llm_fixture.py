#!/usr/bin/env python3
"""Independent Chat Completions server. No SDK, external provider or model."""
import json,os,socket,sys,threading,time
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
TOKEN=os.environ['SIRIAI_LLM_FIXTURE_TOKEN'];LOCK=threading.Lock();CALLS=[];ERRORS=[]
class Handler(BaseHTTPRequestHandler):
    protocol_version='HTTP/1.1'
    def log_message(self,*args):pass
    def send(self,code,body,headers=None):
        if not isinstance(body,bytes):body=json.dumps(body,ensure_ascii=False).encode()
        self.send_response(code);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(body)))
        for key,value in (headers or {}).items():self.send_header(key,value)
        self.end_headers()
        try:self.wfile.write(body);self.wfile.flush()
        except (BrokenPipeError,ConnectionResetError):pass
    def do_GET(self):
        if self.path=='/__state':
            with LOCK:data=dict(calls=[dict(x) for x in CALLS],errors=list(ERRORS))
            self.send(200,data)
        else:self.send(404,{})
    def do_POST(self):
        body=json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        with LOCK:
            record=dict(path=self.path,body=body,disconnected=False,responded=False);CALLS.append(record)
        try:
            assert self.headers.get('Authorization')=='Bearer '+TOKEN,'Authorization'
            assert self.path=='/api/v1/chat/completions',self.path
            assert set(body)=={'model','messages','stream'},body
            assert body['model']=='fixture-model' and body['stream'] is False,body
            if body['messages'][-1]['content']=='no-system':
                assert body['messages']==[dict(role='user',content='no-system')]
            else:
                assert body['messages'][0]==dict(role='system',content='Answer clearly.')
                assert len(body['messages'])==2 and body['messages'][1]['role']=='user'
        except AssertionError as error:
            with LOCK:ERRORS.append(str(error))
            self.send(400,dict(error='bad projection'));return
        objective=body['messages'][-1]['content']
        if objective in ['slow','cancel','crash','timeout','no-system']:
            end=time.monotonic()+(1 if objective in ['slow','no-system'] else 20)
            while time.monotonic()<end:
                self.connection.settimeout(.05)
                try:
                    if self.connection.recv(1,socket.MSG_PEEK)==b'':
                        with LOCK:record['disconnected']=True
                        return
                except socket.timeout:pass
                except OSError:
                    with LOCK:record['disconnected']=True
                    return
            self.connection.settimeout(None)
        if objective=='redirect':self.send(307,{},dict(Location='http://127.0.0.1:'+sys.argv[1]+'/redirected'));return
        if objective=='error':self.send(429,dict(error=dict(message='fixture rate limit '+TOKEN)));return
        if objective=='invalid':self.send(200,b'{invalid');return
        if objective=='empty':payload=dict(choices=[dict(message=dict(content='  '))])
        elif objective=='tool-only':payload=dict(choices=[dict(message=dict(content=None,tool_calls=[dict(id='fake-call',type='function')]))])
        elif objective=='oversized-bytes':self.send(200,b' '*1100000);return
        elif objective=='oversized-text':payload=dict(choices=[dict(message=dict(content='x'*65537))])
        elif objective=='array':payload=dict(choices=[dict(message=dict(content=[' first ',dict(type='text',text=' 第二段 ')]))])
        elif objective=='fallback':payload=dict(output_text='fallback text')
        else:payload=dict(choices=[dict(message=dict(content='fixture answer: '+objective))])
        self.send(200,payload)
        with LOCK:record['responded']=True
if __name__=='__main__':ThreadingHTTPServer(('127.0.0.1',int(sys.argv[1])),Handler).serve_forever()
