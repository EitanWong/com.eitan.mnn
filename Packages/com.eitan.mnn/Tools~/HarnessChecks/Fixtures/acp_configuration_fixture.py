#!/usr/bin/env python3
"""Independent ACP session-configuration peer; native persistent state and exact wire are observable."""
import json,os,sys,uuid
from pathlib import Path
ROOT=Path(os.environ['SIRIAI_CONFIG_ROOT']);VARIANT=sys.argv[1] if len(sys.argv)>1 else 'normal';SID=None;state=None

def record(name,data):
 with (ROOT/name).open('a') as f:f.write(json.dumps(dict(pid=os.getpid(),variant=VARIANT,**data),ensure_ascii=False)+'\n')
def emit(**frame):
 frame=dict(jsonrpc='2.0',**frame);record('wire.jsonl',dict(direction='agent',frame=frame));print(json.dumps(frame),flush=True)
def save():(ROOT/(SID+'.json')).write_text(json.dumps(state,ensure_ascii=False))
def update(value,sid=None):emit(method='session/update',params=dict(sessionId=sid or SID,update=value))
def initial():
 options=[dict(id='engine',name='Model selector',category='model',type='select',currentValue='small',options=[dict(group='local',name='Available models',options=[dict(value='small',name='Small'),dict(value='reasoner-v2',name='Reasoner Plus')])]),dict(id='mode',name='Task behavior',type='select',currentValue='ask',options=[dict(value='ask',name='Ask'),dict(value='auto',name='Automatic')]),dict(id='thinking',name='Reasoning',type='boolean',currentValue=False)]
 if VARIANT=='fallback':options[0].pop('category');options[0]['id']='models'
 if VARIANT=='ambiguous':options[0]['options'][0]['options'].append(dict(value='another',name='Reasoner Plus'))
 if VARIANT in ['missing','no-settings']:options=[]
 if VARIANT=='unknown-type':options[0]['type']='future'
 return dict(configOptions=options,modes=dict(currentModeId='ask',availableModes=[dict(id='ask',name='Ask'),dict(id='auto',name='Automatic')]))
for line in sys.stdin:
 frame=json.loads(line);record('wire.jsonl',dict(direction='client',frame=frame));method=frame.get('method');p=frame.get('params',{})
 if method=='initialize':emit(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=dict(loadSession=True),agentInfo=dict(name='independent-config-peer',version='1')))
 elif method in ['session/new','session/load']:
  SID=p.get('sessionId') or uuid.uuid4().hex
  state=json.loads((ROOT/(SID+'.json')).read_text()) if method=='session/load' else initial();save()
  emit(id=frame['id'],result=dict(state,**({'sessionId':SID} if method=='session/new' else {})))
 elif method=='session/set_config_option':
  assert p['sessionId']==SID;record('sets.jsonl',dict(sid=SID,params=p))
  if VARIANT=='disconnect':sys.exit(0)
  if VARIANT=='reject':emit(id=frame['id'],error=dict(code=-32602,message='Independent peer explicitly rejected setting'));continue
  option=next(x for x in state['configOptions'] if x['id']==p['configId'])
  if option['type']=='boolean':assert p.get('type')=='boolean' and type(p['value']) is bool
  else:assert 'type' not in p and type(p['value']) is str
  if VARIANT!='unconfirmed':option['currentValue']=p['value']
  save();emit(id=frame['id'],result={} if VARIANT=='missing-response' else dict(configOptions=state['configOptions']))
 elif method=='session/set_mode':
  assert p['sessionId']==SID;record('sets.jsonl',dict(sid=SID,params=p));state['modes']['currentModeId']=p['modeId'];save()
  if VARIANT!='silent-mode':update(dict(sessionUpdate='current_mode_update',currentModeId=p['modeId']))
  emit(id=frame['id'],result={})
 elif method=='session/prompt':
  assert p['sessionId']==SID;case=json.loads(p['prompt'][0]['text']);record('prompts.jsonl',dict(sid=SID,params=p,state=state))
  if case.get('updates'):
   state['configOptions'][0]['currentValue']='small';save();update(dict(sessionUpdate='config_option_update',configOptions=state['configOptions']))
   update(dict(sessionUpdate='current_mode_update',currentModeId='ask'))
   update(dict(sessionUpdate='config_option_update',configOptions=[]),sid='foreign')
  update(dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=json.dumps(state,ensure_ascii=False))))
  emit(id=frame['id'],result=dict(stopReason='end_turn'))
 elif method=='session/cancel':pass
 else:raise AssertionError(method)
