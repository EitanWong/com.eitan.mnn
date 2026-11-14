#!/usr/bin/env python3
"""Independent MSP stdio peer using the official 1.4.14 wire shapes; no model."""
import hashlib,json,os,re,sys,threading,time,uuid
from pathlib import Path
ROOT=Path(os.environ['MUSE_FIXTURE_ROOT']);ROOT.mkdir(parents=True,exist_ok=True)
MODE=sys.argv[2] if len(sys.argv)>2 else 'durable';LOCK=threading.RLock();STATE=ROOT/'state.json';WIRE=ROOT/'wire.jsonl'
assert sys.argv[1]=='serve'
assert os.environ.get('META_API_KEY')==os.environ['MUSE_FIXTURE_EXPECTED_KEY']
assert 'SIRIAI_MSP_CHECK_TOKEN' not in os.environ and 'SIRIAI_MSP_OTHER_TOKEN' not in os.environ
DATA=json.loads(STATE.read_text()) if STATE.exists() else dict(sessions={},commands={},turns={})
INITIALIZED=False
def save():
 with LOCK:
  p=STATE.with_suffix('.tmp');p.write_text(json.dumps(DATA));p.replace(STATE)
def record(direction,value):
 with LOCK:
  with WIRE.open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),direction=direction,frame=value),ensure_ascii=False)+'\n')
def send(value):
 with LOCK:
  record('server',value);sys.stdout.write(json.dumps(value,ensure_ascii=False)+'\n');sys.stdout.flush()
def result(request,value):send(dict(jsonrpc='2.0',id=request['id'],result=value))
def event(method,params,request=False):
 if method.startswith(('item/','approval/','turn/')) and method!='item/delta':
  stream=params.get('turnId') or params.get('item',{}).get('turnId') or 'session'
  params.setdefault('sourceRange',dict(stream=dict(kind='run',id=stream),first=dict(id=stream,sequence=1),last=dict(id=stream,sequence=1)))
 value=dict(jsonrpc='2.0',method=method,params=params)
 if request:value['id']='receipt-'+uuid.uuid4().hex
 send(value)
def command(request):
 p=request['params'];assert re.fullmatch('[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}',p['commandId']),p
 return p
def item(session,turn,text,revision=1,status='completed',kind='agentMessage',identity='answer'):
 return dict(sessionId=session,viewCursor='cursor-'+str(revision),item=dict(itemId=turn+'-'+identity,turnId=turn,kind=kind,revision=revision,status=status,recordedAt='2026-11-09T00:00:00Z',text=text))
def finish(session,turn,text,terminal='completed'):
 with LOCK:
  current=DATA['turns'][turn]
  if current.get('terminal'):return
  current.update(terminal=terminal,text=text);DATA['sessions'][session]['activeTurnId']=None;save()
 event('item/completed',item(session,turn,text))
 event('turn/completed',dict(sessionId=session,turnId=turn,terminal=terminal,viewCursor='terminal-'+turn))
def execute(session,turn,objective):
 if objective in ['permission','permission-deny','permission-stages']:
  approval(session,turn,0);return
 if objective in ['input','multi-input']:
  request=dict(sessionId=session,turnId=turn,userInputId='input-'+turn,itemId=turn+'-tool',toolCallId='call',toolName='request_user_input',viewCursor='input',questions=[dict(id='folder',header='Folder',question='Which folder?',selection=dict(mode='single'),options=[dict(label='Reports'),dict(label='Notes')])])
  if objective=='multi-input':request['questions'].append(dict(id='name',header='Name',question='Which name?',selection=dict(mode='single'),options=[]))
  DATA['turns'][turn]['input']=request;save();event('userInput/requested',request);event('userInput/request',request,True);return
 if objective=='steer':return
 if objective in ['gap','gap-stalled']:
  DATA['turns'][turn]['gap']=True;save();event('view/gap',dict(sessionId=session,after='initial',next='gap-item'));return
 if objective=='failure':finish(session,turn,'',terminal='failed');return
 if objective=='result-revisions':
  event('item/started',item(session,turn,'draft',revision=1,status='inProgress'))
  event('item/delta',dict(sessionId=session,itemId=turn+'-answer',delta='not duplicated',field='text',viewCursor='delta'))
  event('item/completed',item(session,turn,'final text',revision=3))
  event('item/updated',item(session,turn,'stale text',revision=2))
  DATA['turns'][turn].update(terminal='completed',text='final text');DATA['sessions'][session]['activeTurnId']=None;save()
  event('turn/completed',dict(sessionId=session,turnId=turn,terminal='completed',viewCursor='terminal'));return
 if objective in ['slow','cancel','crash','lost-ack','timeout']:
  time.sleep(1.2 if objective=='slow' else 30)
 finish(session,turn,'fixture MSP answer: '+objective)
def approval(session,turn,stage):
 request=dict(sessionId=session,turnId=turn,approvalId='approval-'+turn,itemId=turn+'-tool',toolCallId='call',taskId='task',toolName='shell',judgeEscalated=False,protectedWrite=False,rawArgs='{}',viewCursor='approval',currentRequirementId=dict(approvalId='approval-'+turn,sourceIndex=stage),subject=dict(kind='shell',command='write report'),availableChoices=[dict(choiceId='once',decision='approved',scope='once',label='Allow once'),dict(choiceId='persistent',decision='approved',scope='localPersistent',label='Always'),dict(choiceId='deny',decision='denied',scope='once',label='Deny')])
 if stage:request['change']=dict(kind='stageResolved',requirementId=dict(approvalId='approval-'+turn,sourceIndex=stage-1),choiceId='once',decision='approved')
 request['sourceRange']=dict(stream=dict(kind='run',id=turn),first=dict(id=turn,sequence=1),last=dict(id=turn,sequence=1));DATA['turns'][turn]['approval']=request;save()
 event('approval/requested' if stage==0 else 'approval/updated',request);event('approval/request',request,True)
 event('item/updated',item(session,turn,'',kind='toolCall',identity='tool',status='inProgress'))
def handle(request):
 global INITIALIZED
 method=request.get('method')
 if method is None:return
 p=request.get('params',{})
 if method=='initialize':
  assert not INITIALIZED and p['clientInfo']['name']=='siriai_harness_host'
  assert p.get('capabilities',{}).get('userInputDialogs') is True
  value=dict(serverInfo=dict(name='msp-fixture',version='1.4.14'),userAgent='fixture',museHome=str(ROOT),platformFamily='unix',platformOs='macos',grantedCapabilities=[],experimentalApi=False,sessionDurability='ephemeral' if MODE=='ephemeral' else 'durable',schema=dict(version=1,fingerprint='sha256:c94c3d1f2f51e6424074b9ec43eac3eedcf532d3dcd77351ec1383a87d3049c6'))
  if MODE=='missing-schema':value.pop('schema')
  if MODE=='schema-mismatch':value['schema']['fingerprint']='sha256:'+'0'*64
  result(request,value);return
 if method=='initialized':INITIALIZED=True;return
 assert INITIALIZED,'traffic before initialized'
 if method=='session/start':
  command(request);assert p['workspaceRoot'] and p.get('modelId')=='fixture-msp-model'
  sid=str(uuid.uuid4());session=dict(sessionId=sid,workspaceRoot=p['workspaceRoot'],modelId=p['modelId'],providerId=None,activeTurnId=None,approvalMode=dict(mode=p.get('approvalMode','onRequest'),source='startup',lastCommandId=p['commandId']),createdAt='2026-11-09T00:00:00Z',updatedAt='2026-11-09T00:00:00Z',forkedFrom=None,path=str(ROOT/sid),status='idle',turnCount=0)
  if MODE=='unconfirmed-full':session['approvalMode']['mode']='onRequest'
  DATA['sessions'][sid]=session;save();result(request,dict(session=session,viewCursor='initial'));return
 if method in ['session/resume','session/read']:
  if method=='session/resume':command(request)
  sid=p['sessionId'];assert sid in DATA['sessions'];history=[item(sid,t,v.get('text',''),status='completed' if v.get('terminal') else 'inProgress')['item'] for t,v in DATA['turns'].items() if v['session']==sid and v.get('terminal')]
  turns=[(t,v) for t,v in DATA['turns'].items() if v['session']==sid];last=turns[-1] if turns else None
  value=dict(session=DATA['sessions'][sid],history=dict(mode='inline',items=history,snapshot=None),pendingRequests=[],viewCursor='resume')
  if last and last[1].get('terminal'):value['lastTurn']=dict(turnId=last[0],terminal=last[1]['terminal'])
  result(request,value)
  if method=='session/resume' and last and not last[1].get('terminal') and not last[1].get('approval') and not last[1].get('input'):threading.Thread(target=execute,args=(sid,last[0],'slow'),daemon=True).start()
  return
 if method=='turn/start':
  command(request);sid=p['sessionId'];assert sid in DATA['sessions'] and p['ifBusy']=='queue'
  assert set(p)<=set(['commandId','sessionId','input','displayText','ifBusy'])
  assert p['input'][0]['type']=='text' and p['input'][0]['text']==p['displayText']
  objective=p['input'][0]['text'];signature=hashlib.sha256(json.dumps(p,sort_keys=True).encode()).hexdigest();previous=DATA['commands'].get(p['commandId'])
  if objective=='image':assert p['input'][1]==dict(type='image',mediaType='image/png',base64Data='aW1hZ2U=')
  if previous:assert previous['signature']==signature;result(request,previous['ack']);return
  turn=str(uuid.uuid4());ack=dict(commandId=p['commandId'],status='accepted',turnId=turn,startedNewTurn=True,disposition='started')
  DATA['commands'][p['commandId']]=dict(signature=signature,ack=ack);DATA['turns'][turn]=dict(session=sid,objective=objective);DATA['sessions'][sid]['activeTurnId']=turn;save()
  if objective=='lost-ack':return
  if objective=='before-ack':finish(sid,turn,'fixture MSP answer: '+objective)
  result(request,ack)
  if objective!='before-ack':threading.Thread(target=execute,args=(sid,turn,objective),daemon=True).start()
  return
 if method=='turn/cancel':
  command(request);sid=p['sessionId'];turn=p['turnId'];assert DATA['turns'][turn]['session']==sid
  result(request,dict(commandId=p['commandId'],status='accepted',turnId=turn));time.sleep(.2);finish(sid,turn,'cancelled by MSP turn','cancelled');return
 if method=='approval/decide':
  command(request);sid=p['sessionId'];turn=next(t for t in DATA['turns'] if p['approvalId']=='approval-'+t);stage=p['requirementId']['sourceIndex'];assert p['choiceId'] in ['once','deny']
  assert p['requirementId']['approvalId']==p['approvalId'];assert 'feedback' not in p
  multiple=DATA['turns'][turn]['objective']=='permission-stages' and stage==0
  result(request,dict(commandId=p['commandId'],status='accepted',approvalId=p['approvalId'],terminal=not multiple))
  if multiple:approval(sid,turn,1)
  else:finish(sid,turn,'permission '+p['choiceId'])
  return
 if method in ['userInput/answer','userInput/clarify','userInput/cancel']:
  command(request);sid=p['sessionId'];turn=next(t for t in DATA['turns'] if p['userInputId']=='input-'+t)
  if method=='userInput/answer':assert p['answers']==[dict(questionId='folder',selectedLabel='Reports')]
  if method=='userInput/cancel':assert p['reason']=='Please stop asking'
  if method=='userInput/clarify':assert p['clarification']==dict(format='text',content='Reports and Final')
  result(request,dict(commandId=p['commandId'],status='accepted',userInputId=p['userInputId']))
  finish(sid,turn,'input accepted');return
 if method=='turn/steer':
  command(request);sid=p['sessionId'];turn=p['expectedTurnId'];assert DATA['turns'][turn]['session']==sid and p['input']==[dict(type='text',text='Focus on report')]
  result(request,dict(commandId=p['commandId'],status='accepted',turnId=turn));finish(sid,turn,'steered result');return
 if method=='view/subscribe':result(request,dict(viewCursor='subscribed'));return
 if method=='view/page':
  sid=p['sessionId'];turn=DATA['sessions'][sid]['activeTurnId'];objective=DATA['turns'][turn]['objective']
  if objective=='gap-stalled':result(request,dict(events=[],nextCursor=p['cursor']));return
  payload=item(sid,turn,'gap recovered result');payload['viewCursor']='gap-item';payload['sourceRange']=dict(stream=dict(kind='run',id=turn),first=dict(id=turn,sequence=1),last=dict(id=turn,sequence=1))
  result(request,dict(events=[dict(method='item/completed',params=payload)],nextCursor=None))
  DATA['turns'][turn].update(terminal='completed',text='gap recovered result');DATA['sessions'][sid]['activeTurnId']=None;save()
  event('item/completed',payload);event('turn/completed',dict(sessionId=sid,turnId=turn,terminal='completed',viewCursor='gap-terminal'));return
 if method=='approval/listPending':
  current=[v for v in DATA['turns'].values() if v['session']==p['sessionId'] and not v.get('terminal')]
  result(request,dict(approvals=[v['approval'] for v in current if v.get('approval')],userInputs=[v['input'] for v in current if v.get('input')]));return
 if method=='session/leave':result(request,dict(commandId=command(request)['commandId'],status='accepted'));return
 raise AssertionError('unexpected method '+str(method))
for line in sys.stdin:
 try:
  request=json.loads(line);record('client',request);handle(request)
 except Exception as error:
  with (ROOT/'errors.txt').open('a') as f:f.write(type(error).__name__+': '+str(error)+'\n')
  if 'id' in request:send(dict(jsonrpc='2.0',id=request['id'],error=dict(code=-32602,message='fixture refused frame',data=dict(kind='invalidParams'))))
