"""Independent RFC6455 Gateway and ACP peer, deliberately sharing no C# implementation."""
import base64,hashlib,json,os,socket,struct,sys,threading,time,uuid
from http.server import BaseHTTPRequestHandler,ThreadingHTTPServer
from pathlib import Path
lock=threading.Lock()
def write(root,kind,**fields):
 with lock:
  with (root/'wire.jsonl').open('a') as out:out.write(json.dumps(dict(kind=kind,**fields))+'\n')
def save(path,value):
 temp=path.with_suffix('.'+uuid.uuid4().hex+'.tmp');temp.write_text(json.dumps(value));temp.replace(path)
def acp():
 root=Path(os.environ['OPENCLAW_DELEGATION_ROOT']);sid='native-'+uuid.uuid4().hex;held=None
 def emit(value):print(json.dumps(dict(jsonrpc='2.0',**value)),flush=True)
 for line in sys.stdin:
  frame=json.loads(line);method=frame.get('method');write(root,'acp',method=method,params=frame.get('params'),id=frame.get('id'))
  if method=='initialize':emit(dict(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=not (root/'no-resume').exists(),promptCapabilities=dict(embeddedContext=True)))))
  elif method in ['session/new','session/load']:
   sid=frame.get('params',{}).get('sessionId',sid);emit(dict(id=frame['id'],result=dict(sessionId='foreign-session' if method=='session/load' and (root/'foreign-load').exists() else sid)))
  elif method=='session/prompt':
   objective=frame['params']['prompt'][0]['text'];scenario=objective.split(':')[0]
   if objective.startswith('{'):
    message=json.loads(objective)
    if message.get('kind')=='siriai-openclaw-result':
     assert message['task_id'] and message['targets'] and len(frame['params']['prompt'])==1
     assert all(t['run_id'] and t['session_id'] and t['status'] for t in message['targets'])
     (root/'summary-observed').touch()
     if (root/'hold-summary').exists():held=frame['id'];continue
     if (root/'disconnect-summary').exists():return
     if (root/'rogue-summary').exists():
      emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='tool_call',toolCallId='rogue',name='sessions_spawn',status='in_progress'))));continue
     if (root/'summary-artifact').exists():
      emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='resource',resource=dict(uri='siriai://result/summary.txt',mimeType='text/plain',text='summary attachment'))))))
     text='Summary: '+message['objective']+' | '+' | '.join(t['status']+': '+t['result'] for t in message['targets'])
     emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text)))))
     emit(dict(id=frame['id'],result=dict(stopReason='max_tokens' if (root/'fail-summary').exists() else 'end_turn')));continue
   if scenario=='direct':
    emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='direct answer')))));emit(dict(id=frame['id'],result=dict(stopReason='end_turn')));continue
   for i in range(2 if scenario in ['multi','mixed','longmulti'] else 17 if scenario=='overflow' else 1):
    runid='run-'+uuid.uuid4().hex;session='agent:child:'+uuid.uuid4().hex
    item=dict(runId=runid,sessionKey=session,scenario='failed' if scenario=='mixed' and i==1 else scenario,result='child result '+str(i)+': '+objective,done=False,aborted=False);item['result']=('x'*32000+' tail '+str(i)) if scenario=='longmulti' else item['result'];save(root/(runid+'.json'),item)
    output=dict(runId=runid,childSessionKey=session,status='accepted')
    if scenario=='malformed':output.pop('runId')
    if scenario=='wrapped':output=dict(content=[dict(type='text',text=json.dumps(dict(details=output)))])
    if scenario=='json-string':output=json.dumps(output)
    update=dict(sessionUpdate='tool_call',toolCallId='tool-'+str(i),name='sessions_send' if scenario=='send' else 'sessions_spawn',title='delegate',status='in_progress',rawInput=dict(task=objective,cwd='/native/project'))
    emit(dict(method='session/update',params=dict(sessionId=sid,update=update)))
    finish=dict(sessionUpdate='tool_call_update',toolCallId='tool-'+str(i),status='completed',rawOutput=output)
    emit(dict(method='session/update',params=dict(sessionId=sid,update=finish)));emit(dict(method='session/update',params=dict(sessionId=sid,update=finish)))
   if scenario in ['hold-prompt','ignore-cancel']:held=frame['id'];continue
   emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='Delegated and admitted; still running.')))))
   emit(dict(id=frame['id'],result=dict(stopReason='end_turn')))
  elif method=='session/cancel' and held is not None and scenario!='ignore-cancel':emit(dict(id=held,result=dict(stopReason='cancelled')));held=None
class Gateway(ThreadingHTTPServer):
 daemon_threads=True
 def __init__(self,root,token):self.root=root;self.token=token;super().__init__(('127.0.0.1',0),Handler)
class Handler(BaseHTTPRequestHandler):
 protocol_version='HTTP/1.1'
 def log_message(self,*args):pass
 def frame(self,value):
  data=json.dumps(value).encode();size=len(data);header=bytes([0x81,size]) if size<126 else bytes([0x81,126])+struct.pack('!H',size) if size<65536 else bytes([0x81,127])+struct.pack('!Q',size);self.connection.sendall(header+data)
 def exact(self,size):
  value=b''
  while len(value)<size:
   data=self.rfile.read(size-len(value))
   if not data:raise EOFError()
   value+=data
  return value
 def receive(self):
  first,second=self.exact(2)
  if first&15==8:raise EOFError()
  assert first&15==1 and second&128
  size=second&127
  if size==126:size=struct.unpack('!H',self.exact(2))[0]
  if size==127:size=struct.unpack('!Q',self.exact(8))[0]
  assert size<=1048576
  mask=self.exact(4);data=self.exact(size);return json.loads(bytes(v^mask[i%4] for i,v in enumerate(data)))
 def do_GET(self):
  self.close_connection=True
  assert self.path=='/' and self.headers.get('Upgrade','').lower()=='websocket'
  key=self.headers['Sec-WebSocket-Key'];accept=base64.b64encode(hashlib.sha1((key+'258EAFA5-E914-47DA-95CA-C5AB0DC85B11').encode()).digest()).decode();self.send_response(101);self.send_header('Upgrade','websocket');self.send_header('Connection','Upgrade');self.send_header('Sec-WebSocket-Accept',accept);self.end_headers();self.connection.settimeout(10)
  root=self.server.root;connected=False;self.frame(dict(type='event',event='connect.challenge',payload=dict(nonce=uuid.uuid4().hex)))
  try:
   while True:
    frame=self.receive();assert frame['type']=='req' and isinstance(frame['id'],str);method=frame['method'];args=frame['params'];record=dict(method=method,params=dict(args));
    if method=='connect':
     assert not connected and args['minProtocol']==3 and args['maxProtocol']==4 and args['client']['id']=='gateway-client' and args['client']['mode']=='backend' and args['scopes']==['operator.read','operator.write','operator.admin'] and args['auth']['token']==self.server.token,args
     record['params']['auth']={'token':'REDACTED'};connected=True;payload=dict(type='hello-ok',protocol=3)
    else:
     assert connected;write(root,'gateway',**record)
     runid=args.get('runId');path=root/(str(runid)+'.json') if runid else None
     if method=='agent.wait':
      assert args['timeoutMs']==30000;item=json.loads(path.read_text())
      if item['scenario']=='disconnect':return
      if item['scenario']=='slow-wait' and not item['aborted']:time.sleep(3)
      if item['aborted']:payload=dict(status='error',error='cancelled_by_user')
      elif item['scenario']=='pending' and not (root/('release-'+runid)).exists():time.sleep(.08);payload=dict(status='pending',providerStarted=False)
      elif item['scenario']=='failed':payload=dict(status='error',error='native child failed')
      elif (root/'release-all').exists() or (root/('release-'+runid)).exists():payload=dict(status='ok')
      else:time.sleep(.08);payload=dict(status='timeout')
      if item['scenario']=='completion-run-mismatch' and payload.get('status')=='ok':payload['runId']='foreign-run'
     elif method=='chat.history':
      assert args['limit']==20;items=[json.loads(p.read_text()) for p in root.glob('run-*.json')];item=next(i for i in items if i['sessionKey']==args['sessionKey']);payload=dict(sessionKey=item['sessionKey'],messages=[dict(role='assistant',content='STALE HISTORY'),dict(role='user',content='untrusted'),dict(role='assistant',content=[dict(type='text',text=item['result']),dict(type='image',url='ignored')])])
      if item['scenario']=='empty':payload['messages']=[]
      if item['scenario']=='foreign-history':payload['sessionKey']='agent:foreign'
      if item['scenario']=='oversized':payload['messages']=[dict(role='assistant',content='z'*70000)]
     elif method=='chat.abort':
      item=json.loads(path.read_text());assert item['sessionKey']==args['sessionKey']
      if item['scenario']=='abort-lost':return
      if item['scenario']=='abort-weak':payload=dict(ok=True)
      elif item['scenario']=='abort-race':(root/('release-'+runid)).touch();payload=dict(ok=True,aborted=False,runIds=[])
      else:
       item['aborted']=True;save(path,item);payload=dict(ok=True,aborted=True,runIds=[runid])
       while item['scenario']=='abort-crash' and (root/'hold-abort-response').exists():time.sleep(.02)
     else:raise AssertionError('Unexpected Gateway method '+method)
    if method=='connect':write(root,'gateway',**record)
    if (root/'wrong-rpc-id').exists():self.frame(dict(type='res',id='foreign-id',ok=True,payload=dict(status='ok')))
    self.frame(dict(type='res',id=frame['id'],ok=True,payload=payload))
    if method!='connect':return
  except (EOFError,ConnectionError,socket.timeout):pass
  except Exception as error:write(root,'error',error=repr(error));raise
if __name__=='__main__':acp()
