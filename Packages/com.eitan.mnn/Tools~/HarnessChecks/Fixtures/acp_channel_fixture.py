#!/usr/bin/env python3
"""Independent ACP Agent and stdio MCP server exercising bidirectional channel RPC."""
import json,os,sys,threading,time,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_CHANNEL_ROOT']);MODE=sys.argv[1];WRITE=threading.Lock();pending={};active={}
def thread_error(args):
 import traceback
 (ROOT/('errors-'+MODE+'.txt')).write_text(''.join(traceback.format_exception(args.exc_type,args.exc_value,args.exc_traceback)))
threading.excepthook=thread_error
def event(kind,**values):
 with (ROOT/'events.jsonl').open('a') as f:f.write(json.dumps(dict(kind=kind,pid=os.getpid(),**values))+'\n')
def emit(value):
 with WRITE:
  if MODE=='agent':
   with (ROOT/'wire.jsonl').open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),direction='agent',frame=value))+'\n')
  print(json.dumps(value,ensure_ascii=MODE!='agent'),flush=True)
def request(method,params,id=None,omit=False):
 id=id if id is not None else 'peer-'+uuid.uuid4().hex;ready=threading.Event();pending[json.dumps(id)]=[ready,None];frame=dict(jsonrpc='2.0',id=id,method=method,params=params)
 if omit:frame.pop('params')
 emit(frame);assert ready.wait(15),'RPC response timed out '+method
 result=pending.pop(json.dumps(id))[1];return result
def result(id,value):emit(dict(jsonrpc='2.0',id=id,result=value))
def tool(frame):
 method=frame['method'];params=frame.get('params');id=frame.get('id')
 if method=='initialize':value=dict(protocolVersion='2024-11-05',capabilities=dict(tools={}),serverInfo=dict(name='channel-native-tool',version='1'))
 elif method=='notifications/initialized':event('initialized');return
 elif method=='tools/list':value=dict(tools=[dict(name='read_file',description='Read independent fixture file',inputSchema=dict(type='object',properties={}))])
 elif method=='notifications/cancelled':
  event('cancel-notification',requestId=params['requestId']);active.get(json.dumps(params['requestId']),threading.Event()).set();return
 elif method=='tools/call':
  name=params['name'];event('tool-start',name=name,id=id)
  if name=='delay':
   wait=threading.Event();active[json.dumps(id)]=wait;wait.wait(12);active.pop(json.dumps(id));value=dict(content=[dict(type='text',text='cancelled' if wait.is_set() else 'delayed')])
  elif name=='callback':
   callback=request('sampling/createMessage',dict(messages=[],maxTokens=8),id='native-callback');assert callback.get('result',{}).get('model')=='fixture-model',callback
   emit(dict(jsonrpc='2.0',method='notifications/message',params=dict(level='info',data='provider-notification')));value=dict(content=[dict(type='text',text='callback-ok')])
  elif name=='callback-cancel':
   replies=[];thread=threading.Thread(target=lambda:replies.append(request('sampling/createMessage',dict(messages=[],maxTokens=9),id='native-cancel')));thread.start();time.sleep(.05)
   emit(dict(jsonrpc='2.0',method='notifications/cancelled',params=dict(requestId='native-cancel')));thread.join(10);assert replies[0]['error']['code']==-32800,replies
   value=dict(content=[dict(type='text',text='reverse-cancel-ok')])
  elif name=='error':emit(dict(jsonrpc='2.0',id=id,error=dict(code=-32042,message='native-tool-error',data=dict(marker='exact-error'))));return
  elif name=='crash':event('crash-side-effect');os._exit(9)
  elif name=='oversize':value=dict(content=[dict(type='text',text='x'*300000)])
  else:
   assert name=='read_file';value=dict(content=[dict(type='text',text=(ROOT/'value.txt').read_text())]);time.sleep(.04 if id=='slow' else 0)
  event('tool-end',name=name,id=id)
 else:raise AssertionError(method)
 if id is not None:result(id,value)
if MODE=='tool':
 assert os.environ.get('TOOL_SECRET')==(ROOT/'secret.txt').read_text()
 for name in ['SIRIAI_CHANNEL_TOKEN','SIRIAI_CHANNEL_SECRET']:assert name not in os.environ
 event('provider-start',cwd=os.getcwd())
else:
 assert MODE=='agent'
 for name in ['SIRIAI_CHANNEL_TOKEN','SIRIAI_CHANNEL_SECRET']:assert name not in os.environ
 connections={};cancel=threading.Event();sampling={}
 def rpc(method,params,id=None):
  reply=request(method,params,id);assert 'result' in reply,reply;return reply['result']
 def call(cid,method,params=None,id=None):return request('mcp/message',dict(connectionId=cid,method=method,params=params),id)
 def prompt(frame):
  params=frame['params'];objective=params['prompt'][0]['text'];descriptors=connections[params['sessionId']]
  invalid=request('mcp/connect',None,omit=True);assert invalid['error']['code']==-32602,invalid
  cid=rpc('mcp/connect',dict(serverId=descriptors[0]['serverId']))['connectionId'];event('connected',connectionId=cid)
  assert call(cid,'initialize',dict(protocolVersion='2024-11-05',capabilities={},clientInfo=dict(name='agent',version='1')))['result']['serverInfo']['name']=='channel-native-tool'
  emit(dict(jsonrpc='2.0',method='mcp/message',params=dict(connectionId=cid,method='notifications/initialized')))
  assert call(cid,'tools/list',{})['result']['tools'][0]['name']=='read_file'
  if objective=='cancel':
   thread=threading.Thread(target=lambda:call(cid,'tools/call',dict(name='delay',arguments={}),id='cancel-me'));thread.start();cancel.wait(10);emit(dict(jsonrpc='2.0',method='mcp/message',params=dict(connectionId=cid,method='notifications/cancelled',params=dict(requestId='cancel-me'))));thread.join(10);text='cancelled'
  elif objective in ['crash','oversize']:
   reply=call(cid,'tools/call',dict(name=objective,arguments={}));assert 'error' in reply,reply
   repeated=call(cid,'tools/list',{});assert 'error' in repeated,repeated;text='provider-failed-without-replay'
  else:
   if objective=='hold':
    (ROOT/'held-cid.txt').write_text(cid)
    while not (ROOT/'release').exists():time.sleep(.03)
   if objective=='foreign':
    foreign=call((ROOT/'held-cid.txt').read_text(),'tools/list',{});assert foreign['error']['code']==-32602,foreign
   if objective=='limits':
    extra=[rpc('mcp/connect',dict(serverId=descriptors[0]['serverId']))['connectionId'] for _ in range(7)]
    assert len(set(extra+[cid]))==8
    limit=request('mcp/connect',dict(serverId=descriptors[0]['serverId']));assert limit['error']['code']==-32000,limit
    for connection in extra:rpc('mcp/disconnect',dict(connectionId=connection))
    limit=call(cid,'tools/call',dict(name='read_file',arguments=dict(large='<'*100000)));assert limit['error']['code']==-32602,limit
   replies={}
   def one(id):replies[id]=call(cid,'tools/call',dict(name='read_file',arguments={}),id)
   threads=[threading.Thread(target=one,args=(id,)) for id in ['slow',7,'7','中文']]
   for t in threads:t.start()
   for t in threads:t.join(10)
   assert len(replies)==4 and all(v['result']['content'][0]['text']=='native-channel-value' for v in replies.values()),replies
   assert call(cid,'tools/call',dict(name='callback',arguments={}))['result']['content'][0]['text']=='callback-ok'
   assert call(cid,'tools/call',dict(name='callback-cancel',arguments={}))['result']['content'][0]['text']=='reverse-cancel-ok'
   error=call(cid,'tools/call',dict(name='error',arguments={}))['error'];assert error==dict(code=-32042,message='native-tool-error',data=dict(marker='exact-error')),error
   unknown=request('mcp/message',dict(connectionId='foreign-connection',method='tools/list',params={}));assert unknown['error']['code']==-32602,unknown
   text='channel-native-ok'
  if objective!='idle-exit':
   rpc('mcp/disconnect',dict(connectionId=cid));invalid=call(cid,'tools/list',{});assert invalid['error']['code']==-32602,invalid
  emit(dict(jsonrpc='2.0',method='session/update',params=dict(sessionId=params['sessionId'],update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text)))))
  result(frame['id'],dict(stopReason='cancelled' if objective=='cancel' else 'end_turn'))
  if objective=='idle-exit':time.sleep(.2);os._exit(0)
try:
 for line in sys.stdin:
  frame=json.loads(line)
  if MODE=='agent':
   with (ROOT/'wire.jsonl').open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),direction='client',frame=frame))+'\n')
  if 'method' not in frame:
   item=pending.get(json.dumps(frame['id']))
   if item:item[1]=frame;item[0].set()
   continue
  if MODE=='tool':threading.Thread(target=tool,args=(frame,),daemon=True).start();continue
  method=frame['method'];params=frame.get('params',{})
  if method=='initialize':result(frame['id'],dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,sessionCapabilities=dict(resume={}) if (ROOT/'resume').exists() else {},mcpCapabilities=dict(acp=json.loads((ROOT/'capability.json').read_text()))),agentInfo=dict(name='channel-independent-agent',version='1')))
  elif method in ['session/new','session/load','session/resume']:
   assert len(params['mcpServers'])==1 and params['mcpServers'][0]['type']=='acp'
   assert set(params['mcpServers'][0])=={'type','name','serverId'}
   sid=params.get('sessionId') or 'channel-'+uuid.uuid4().hex;connections[sid]=params['mcpServers'];result(frame['id'],dict(sessionId=sid) if method=='session/new' else {})
  elif method=='session/prompt':cancel.clear();threading.Thread(target=prompt,args=(frame,),daemon=True).start()
  elif method=='session/cancel':cancel.set()
  elif method=='mcp/message':
   if 'id' not in frame:
    if params['method']=='notifications/cancelled':
     original=sampling.pop(params['connectionId']);assert params['params']['requestId']==original
     emit(dict(jsonrpc='2.0',id=original,error=dict(code=-32800,message='sampling-cancelled')))
    event('agent-notification',method=params['method']);continue
   assert params['method']=='sampling/createMessage'
   if params['params']['maxTokens']==9:sampling[params['connectionId']]=frame['id'];continue
   result(frame['id'],dict(role='assistant',content=dict(type='text',text='sampled'),model='fixture-model'))
  else:raise AssertionError(method)
except Exception:
 import traceback
 (ROOT/('errors-'+MODE+'.txt')).write_text(traceback.format_exc());raise
