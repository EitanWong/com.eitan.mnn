#!/usr/bin/env python3
"""Independent native ACP coordinator/worker with actual HTTP MCP calls."""
import json,os,sys,threading,time,urllib.request,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_COORD_ROOT']);MODE=sys.argv[1];WRITE=threading.Lock();sessions={};cancels={}
def emit(frame):
 frame=dict(jsonrpc='2.0',**frame)
 with WRITE:
  with (ROOT/'wire.jsonl').open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),direction='agent',frame=frame))+'\n')
  print(json.dumps(frame),flush=True)
def event(**values):
 with (ROOT/'events.jsonl').open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),**values))+'\n')
def mcp(server,method,params):
 frame=dict(jsonrpc='2.0',id=uuid.uuid4().hex,method=method,params=params);headers={h['name']:h['value'] for h in server['headers']};headers.update({'Content-Type':'application/json','Accept':'application/json,text/event-stream'})
 with urllib.request.urlopen(urllib.request.Request(server['url'],json.dumps(frame).encode(),headers=headers),timeout=12) as response:result=json.loads(response.read())
 with (ROOT/'mcp-wire.jsonl').open('a') as f:
  f.write(json.dumps(dict(direction='client',frame=frame))+'\n');f.write(json.dumps(dict(direction='server',frame=result))+'\n')
 return result['result']
def prompt(frame):
 try:
  params=frame['params'];sid=params['sessionId'];objective=params['prompt'][0]['text'];cancel=threading.Event();cancels[sid]=cancel
  if MODE=='coordinator':
   server=sessions[sid][0];mcp(server,'initialize',dict(protocolVersion='2025-06-18',capabilities={},clientInfo=dict(name='independent-coordinator',version='1')))
   tools=mcp(server,'tools/list',{})['tools'];assert len(tools)==5
   operation=json.loads(objective);hold=operation.pop('hold_after_admission',False);name='siriai_session_'+operation.pop('op');name='siriai_sessions_list' if name=='siriai_session_list' else name
   response=mcp(server,'tools/call',dict(name=name,arguments=operation));text=json.dumps(response)
   if hold:
    (ROOT/'held-admission.json').write_text(json.dumps(response))
    while not (ROOT/'release-coordinator').exists():time.sleep(.03)
  else:
   path=ROOT/('history-'+sid+'.json');history=json.loads(path.read_text()) if path.exists() else [];event(kind='worker-start',sid=sid,objective=objective,prior=len(history))
   if objective=='wait':cancel.wait(20)
   else:time.sleep(.2)
   if cancel.is_set():emit(dict(id=frame['id'],result=dict(stopReason='cancelled')));return
   text='worker:'+objective+'|prior='+str(len(history));history.append(objective);path.write_text(json.dumps(history));event(kind='worker-end',sid=sid,objective=objective)
  emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text)))));emit(dict(id=frame['id'],result=dict(stopReason='end_turn')))
 except Exception:
  import traceback
  (ROOT/('errors-'+MODE+'.txt')).write_text(traceback.format_exc());emit(dict(id=frame['id'],error=dict(code=-32000,message='fixture operation failed')))
for line in sys.stdin:
 frame=json.loads(line)
 with (ROOT/'wire.jsonl').open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),direction='client',frame=frame))+'\n')
 method=frame.get('method');params=frame.get('params',{})
 if method=='initialize':emit(dict(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True,mcpCapabilities=dict(http=True)),agentInfo=dict(name='independent-'+MODE,version='1'))))
 elif method in ['session/new','session/load','session/resume']:
  sid=params.get('sessionId') or 'native-'+uuid.uuid4().hex;sessions[sid]=params['mcpServers'];emit(dict(id=frame['id'],result=dict(sessionId=sid) if method=='session/new' else {}))
 elif method=='session/prompt':threading.Thread(target=prompt,args=(frame,),daemon=True).start()
 elif method=='session/cancel':cancels[params['sessionId']].set()
 else:raise AssertionError(method)
