#!/usr/bin/env python3
"""Independent streaming A2A peer: known-ID reconnect, fragmented UTF-8, artifacts and controls."""
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from urllib.parse import urlparse,parse_qs
import json,sys,time,threading,uuid
port,version,binding=int(sys.argv[1]),sys.argv[2],sys.argv[3]
legacy=version=='0.3';schema=legacy and binding=='JSONRPC'
tasks={};calls=[];errors=[];lock=threading.RLock()
def part(text):return dict(kind='text',text=text) if schema else dict(text=text,mediaType='text/plain')
def message(text):
    result=dict(messageId='agent-message',role='agent' if schema else 'ROLE_AGENT')
    result['content' if legacy and not schema else 'parts']=[part(text)]
    if schema:result['kind']='message'
    return result
def snapshot(identity):
    t=tasks[identity];state=t['state']
    status=dict(state=state if schema else 'TASK_STATE_'+state.upper().replace('-','_'),message=message('需要目录' if state=='input-required' else '已完成' if state=='completed' else '执行中'))
    value=dict(id=identity,contextId=t['context'],status=status,artifacts=t['artifacts'])
    if schema:value['kind']='task'
    return value
def event(kind,value):
    if schema:return dict(value,kind={'statusUpdate':'status-update','artifactUpdate':'artifact-update'}.get(kind,kind))
    return {kind:value}
class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args):pass
    def send(self,value,code=200):
        body=json.dumps(value).encode();self.send_response(code);self.send_header('Content-Type','application/json');self.send_header('Content-Length',str(len(body)));self.end_headers();self.wfile.write(body)
    def body(self):
        body=b''
        if self.headers.get('Transfer-Encoding')=='chunked':
            while True:
                size=int(self.rfile.readline().split(b';')[0],16)
                if not size:self.rfile.readline();break
                body+=self.rfile.read(size);self.rfile.read(2)
        else:body=self.rfile.read(int(self.headers.get('Content-Length',0)))
        return json.loads(body or b'{}')
    def do_GET(self):self.call()
    def do_POST(self):self.call()
    def call(self):
        try:
            path=urlparse(self.path).path
            with lock:
                if path=='/__state':self.send(dict(tasks={k:{x:y for x,y in v.items() if x!='release'} for k,v in tasks.items()},calls=calls,errors=errors));return
                if path=='/__release':tasks[parse_qs(urlparse(self.path).query)['id'][0]]['release'].set();self.send(dict(ok=True));return
                assert self.headers.get('Authorization')=='Bearer '+sys.argv[4]
                if path=='/.well-known/agent-card.json':
                    endpoint='http://127.0.0.1:'+str(port)+('/rpc/' if binding=='JSONRPC' else '')
                    card=dict(name='Streaming fixture',description='Independent wire test',version='1',capabilities=dict(streaming=True),defaultInputModes=['text/plain','image/png','application/json'],defaultOutputModes=['text/plain'],skills=[])
                    if legacy:card.update(protocolVersion='0.3.0',url=endpoint,preferredTransport=binding)
                    else:card['supportedInterfaces']=[dict(url=endpoint,protocolBinding=binding,protocolVersion='1.0')]
                    self.send(card);return
                payload=self.body();assert self.headers.get('A2A-Version')==version
                if binding=='JSONRPC':
                    method=payload['method'];params=payload['params'];rpcid=payload['id']
                    names={'SendStreamingMessage':'stream','SendMessage':'send','GetTask':'get','CancelTask':'cancel','SubscribeToTask':'subscribe'} if not legacy else {'message/stream':'stream','message/send':'send','tasks/get':'get','tasks/cancel':'cancel','tasks/resubscribe':'subscribe'}
                    op=names[method]
                else:
                    params=payload;rpcid=None
                    op='stream' if path.endswith('/message:stream') else 'send' if path.endswith('/message:send') else 'subscribe' if path.endswith(':subscribe') else 'cancel' if path.endswith(':cancel') else 'get'
                    if op=='subscribe':assert self.command==('GET' if legacy else 'POST')
                calls.append(dict(op=op,payload=payload,path=self.path))
                if op in ['stream','send']:
                    m=params['message'];parts=m['content' if legacy and not schema else 'parts'];text=parts[0]['text']
                    if text=='stream':
                        assert len(parts)==5 and parts[1]['text']=='附加文本'
                        if not legacy:assert parts[2]['url']=='https://example.org/input.png' and parts[3]['data']==dict(key=42) and parts[4]['raw']=='aGk=' and parts[4]['filename']=='input.txt'
                        elif schema:assert parts[2]['file']['uri']=='https://example.org/input.png' and parts[3]['data']==dict(key=42) and parts[4]['file']['bytes']=='aGk=' and parts[4]['file']['name']=='input.txt'
                        else:assert parts[2]['file']['fileWithUri']=='https://example.org/input.png' and parts[3]['data']['data']==dict(key=42) and parts[4]['file']['fileWithBytes']=='aGk='
                    if m.get('taskId'):
                        identity=m['taskId'];t=tasks[identity];assert m['contextId']==t['context'] and text=='Reports'
                        t['state']='working';result=snapshot(identity)
                    else:
                        assert op=='stream','Agent advertises streaming; native stream admission is required'
                        if text=='message':self.stream(None,rpcid,[event('message',message('直接回复'))]);return
                        identity='stream-'+uuid.uuid4().hex
                        tasks[identity]=dict(context='ctx-'+uuid.uuid4().hex,state='input-required' if text=='input' else 'working',objective=text,artifacts=[],release=threading.Event(),inputs=parts)
                        result=snapshot(identity)
                        if text=='restart':result.pop('contextId',None)
                else:
                    identity=params['id'] if binding=='JSONRPC' else path.split('/tasks/')[1].split(':')[0]
                    t=tasks[identity]
                    if op=='cancel':t['state']='canceled';t['release'].set()
                    if t['release'].is_set() and t['state']=='working':t['state']='completed'
                    result=snapshot(identity)
                if op in ['stream','subscribe']:
                    initial=event('task',result) if not schema else result
                else:
                    if op=='send' and not schema:result={'task':result}
                    if binding=='JSONRPC':result=dict(jsonrpc='2.0',id=rpcid,result=result)
                    self.send(result);return
            self.stream(identity,rpcid,[] if op=='subscribe' and t['objective']=='silent' else [initial])
        except (BrokenPipeError,ConnectionResetError):pass
        except Exception as error:
            with lock:errors.append(repr(error))
            try:self.send(dict(error=repr(error)),400)
            except OSError:pass
    def emit(self,value,rpcid):
        if binding=='JSONRPC':value=dict(jsonrpc='2.0',id=rpcid,result=value)
        raw=json.dumps(value,ensure_ascii=False)
        # A valid multiline JSON SSE event, split inside multibyte UTF-8 by arbitrary writes.
        split=raw.index(',')+1 if ',' in raw else len(raw)
        data=('data: '+raw[:split]+'\r\ndata: '+raw[split:]+'\r\n\r\n').encode()
        for start in range(0,len(data),7):self.wfile.write(data[start:start+7]);self.wfile.flush()
    def stream(self,identity,rpcid,initial):
        self.send_response(200);self.send_header('Content-Type','text/event-stream');self.send_header('Connection','close');self.end_headers()
        self.wfile.write(b'\xef\xbb\xbf: heartbeat\r\n\r\n');self.wfile.flush()
        for value in initial:self.emit(value,rpcid)
        if identity is None:return
        with lock:t=tasks[identity];objective=t['objective']
        if objective=='silent' and not initial:
            while not t['release'].wait(.1):self.wfile.write(b': no new updates\n\n');self.wfile.flush()
            return
        if t['state']=='input-required':
            self.emit(event('statusUpdate',dict(taskId=identity,contextId=t['context'],status=snapshot(identity)['status'],final=True)),rpcid);return
        if objective=='malformed':self.wfile.write(b'data: {bad json}\n\n');self.wfile.flush();return
        if objective=='oversized':self.wfile.write(b'data: '+b'x'*(1024*1024+1)+b'\n\n');self.wfile.flush();return
        for index,text in enumerate(['first','第二段']):
            artifact=dict(artifactId='report',name='Report',parts=[part(text)])
            with lock:
                if index==0:t['artifacts']=[artifact]
                else:t['artifacts'][0]['parts']+=artifact['parts']
            self.emit(event('artifactUpdate',dict(taskId='unrelated' if objective=='mismatch' else identity,contextId=t['context'],artifact=artifact,append=index>0,lastChunk=index==1)),rpcid)
            time.sleep(.08)
        if objective=='gap':return
        if objective=='ui':
            artifact=dict(artifactId='download',name='Download',parts=[dict(raw='aGk=',mediaType='text/plain',filename='../../report.txt'),dict(url='https://example.org/result',mediaType='text/plain')])
            with lock:t['artifacts'].append(artifact)
            self.emit(event('artifactUpdate',dict(taskId=identity,contextId=t['context'],artifact=artifact,append=False,lastChunk=True)),rpcid)
            t['release'].set()
        while not t['release'].wait(.1):
            self.wfile.write(b': still running\n\n');self.wfile.flush()
        with lock:
            if t['state']=='working':t['state']='completed'
            status=snapshot(identity)['status']
        self.emit(event('statusUpdate',dict(taskId=identity,contextId=t['context'],status=status,final=True)),rpcid)
ThreadingHTTPServer(('127.0.0.1',port),Handler).serve_forever()
