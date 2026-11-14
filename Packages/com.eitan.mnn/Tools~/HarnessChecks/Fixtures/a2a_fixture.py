#!/usr/bin/env python3
"""Independent wire fixture pinned to A2A SDK 1.3.0 schemas; no production/vendor inference."""
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from urllib.parse import urlparse
import json,sys,time,threading,uuid
port,version,binding=int(sys.argv[1]),sys.argv[2],sys.argv[3]
legacy=version=='0.3';schema=legacy and binding=='JSONRPC'
lock=threading.Lock();tasks={};requests=[];errors=[]

def msg(text,identity='agent-message'):
    part=dict(kind='text',text=text) if schema else dict(text=text)
    return dict(messageId=identity,role='agent' if schema else 'ROLE_AGENT',**({'parts':[part]} if not legacy or schema else {'content':[part]}))

def task(identity):
    value=tasks[identity];state=value['state']
    if state=='working' and time.monotonic()>value['until']:state='completed';value['state']=state
    if state=='cancelling' and time.monotonic()>value['until']:state='canceled';value['state']=state
    wire=state if schema else 'TASK_STATE_'+state.upper().replace('-','_')
    text='fixture finished' if state=='completed' else 'Supply '+state
    result=dict(id=identity,contextId=value['context'],status=dict(state=wire,timestamp=str(time.time()),message=msg(text,'status-'+str(value['revision']))))
    if schema:result['kind']='task'
    if state=='completed':
        if schema:parts=[dict(kind='text',text='report'),dict(kind='file',file=dict(uri='https://example.org/report',mimeType='text/plain')),dict(kind='data',data=dict(ok=True)),dict(kind='file',file=dict(bytes='aGk=',mimeType='text/plain',name='a.txt'))]
        elif legacy:parts=[dict(text='report'),dict(file=dict(fileWithUri='https://example.org/report',mimeType='text/plain')),dict(data=dict(data=dict(ok=True))),dict(file=dict(fileWithBytes='aGk=',mimeType='text/plain'))]
        else:parts=[dict(text='report'),dict(url='https://example.org/report',mediaType='text/plain'),dict(data=dict(ok=True)),dict(raw='aGk=',mediaType='text/plain',filename='a.txt')]
        result['artifacts']=[dict(artifactId='report-1',name='Report',parts=parts)]
    return result

class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):pass
    def send(self,value,code=200):
        body=json.dumps(value).encode();self.send_response(code);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
    def do_GET(self):self.handle_call()
    def do_POST(self):self.handle_call()
    def handle_call(self):
        try:
            with lock:
                path=urlparse(self.path).path
                if path=='/__state':self.send(dict(tasks=tasks,requests=requests,errors=errors));return
                assert self.headers.get('Authorization')=='Bearer '+sys.argv[4], 'Missing configured bearer'
                if path=='/.well-known/agent-card.json':
                    url='http://127.0.0.1:'+str(port+1 if 'evil=1' in self.path else port)
                    if binding=='JSONRPC':url+='/rpc/'
                    if schema or legacy:card=dict(protocolVersion='0.3.0',url=url,preferredTransport=binding,capabilities=dict(streaming=False))
                    else:card=dict(supportedInterfaces=[dict(url=url,protocolBinding=binding,protocolVersion='1.0')],capabilities=dict(streaming=False))
                    card.update(name='Independent protocol fixture',description='Deterministic test peer',version='1',defaultInputModes=['text/plain'],defaultOutputModes=['text/plain','application/json'],skills=[])
                    self.send(card);return
                body=b''
                if self.headers.get('Transfer-Encoding')=='chunked':
                    while True:
                        size=int(self.rfile.readline().split(b';')[0],16)
                        if size==0:self.rfile.readline();break
                        body+=self.rfile.read(size);self.rfile.read(2)
                else:body=self.rfile.read(int(self.headers.get('Content-Length',0)))
                payload=json.loads(body or b'{}')
                assert self.headers.get('A2A-Version')==version, 'Missing negotiated A2A-Version header'
                requests.append(dict(path=self.path,payload=payload))
                if binding=='JSONRPC':
                    assert path=='/rpc/', 'Card RPC endpoint path/trailing slash changed'
                    assert payload['jsonrpc']=='2.0' and payload['id']
                    methods={'message/send':'send','tasks/get':'get','tasks/cancel':'cancel'} if legacy else {'SendMessage':'send','GetTask':'get','CancelTask':'cancel'}
                    op=methods[payload['method']];params=payload['params']
                else:
                    prefix='/v1' if legacy else ''
                    assert path.startswith(prefix+'/')
                    params=payload
                    op='send' if path==prefix+'/message:send' else 'cancel' if path.endswith(':cancel') else 'get'
                if op=='send':
                    message=params['message']
                    assert message['role']==('user' if schema else 'ROLE_USER')
                    parts=message['content' if legacy and not schema else 'parts'];text=parts[0]['text']
                    assert set(message)<=set(['kind','messageId','role','parts','content','taskId','contextId'])
                    if schema:assert message['kind']=='message'
                    cfg=params['configuration'];assert cfg.get('blocking',False)==False if legacy else cfg['returnImmediately']==True
                    if text=='message':result=msg('immediate reply');result.update(kind='message') if schema else None
                    elif message.get('taskId'):
                        identity=message['taskId'];old=tasks[identity]
                        assert message['contextId']==old['context'] and text in ['user answer','confirmed']
                        old['state']='input-required' if old['state']=='auth-required' else 'working';old['until']=time.monotonic()+.1;old['revision']+=1
                        result=task(identity)
                    else:
                        assert 'taskId' not in message and 'contextId' not in message
                        identity='remote-'+uuid.uuid4().hex
                        tasks[identity]=dict(context='context-'+uuid.uuid4().hex,state='auth-required' if text=='input' else 'working',until=time.monotonic()+2,revision=1,outages=3 if text=='outage' else 0)
                        result=task(identity)
                    if text=='ambiguous':self.close_connection=True;self.connection.shutdown(2);return
                    if not schema:result={'task':result} if 'status' in result else {'message':result}
                else:
                    identity=params['id'] if binding=='JSONRPC' else path.split('/tasks/')[1].split(':')[0]
                    assert identity in tasks
                    if op=='get' and tasks[identity]['outages']>0:
                        tasks[identity]['outages']-=1;self.send(dict(error='temporary outage'),503);return
                    if op=='cancel':tasks[identity]['state']='cancelling';tasks[identity]['until']=time.monotonic()+.15
                    result=task(identity)
                if binding=='JSONRPC':result=dict(jsonrpc='2.0',id=payload['id'],result=result)
                self.send(result)
        except Exception as error:
            errors.append(repr(error));self.send(dict(error=repr(error)),400)

ThreadingHTTPServer(('127.0.0.1',port),Handler).serve_forever()
