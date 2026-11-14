#!/usr/bin/env python3
"""Persistent independent ACP1 peer; records all commands and prior prompt history."""
import fcntl, hashlib, json, os, sys, threading, time, uuid
from contextlib import contextmanager
from pathlib import Path

ROOT=Path(os.environ['SIRIAI_ACP_SESSION_ROOT']);ROOT.mkdir(exist_ok=True)
MODE=sys.argv[1];LOCK=threading.RLock();OUTPUT=threading.Lock();active={};permissions={}
STATE=ROOT/'state.json';WIRE=ROOT/'wire.jsonl';sessions=json.loads(STATE.read_text()) if STATE.exists() else {}
@contextmanager
def storage():
    with LOCK,(ROOT/'state.lock').open('a+') as file:
        fcntl.flock(file,fcntl.LOCK_EX)
        try:
            sessions.clear();sessions.update(json.loads(STATE.read_text()) if STATE.exists() else {})
            yield
        finally:fcntl.flock(file,fcntl.LOCK_UN)
def save():
    temp=STATE.with_suffix('.tmp');temp.write_text(json.dumps(sessions));temp.replace(STATE)
def record(direction,frame):
    with LOCK:
        with WIRE.open('a') as f:f.write(json.dumps(dict(direction=direction,pid=os.getpid(),frame=frame))+'\n')
def emit(frame):
    frame=dict(jsonrpc='2.0',**frame)
    with OUTPUT:
        record('agent',frame);print(json.dumps(frame),flush=True)
def update(session,text):
    emit(dict(method='session/update',params=dict(sessionId=session,update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text=text)))))
def content(session,block):
    emit(dict(method='session/update',params=dict(sessionId=session,update=dict(sessionUpdate='agent_message_chunk',content=block))))
def output(message,cancel):
    sid=message['params']['sessionId'];objective=message['params']['prompt'][0]['text']
    image=dict(type='image',data='AAEC',mimeType='image/png',uri='mcp://reports/%E5%9B%BE.png')
    content(sid,image)
    if objective=='output-mixed':
        content('unrelated-native-session',dict(type='image',data='AAEC',mimeType='image/png',uri='memory://secret/foreign.png'))
        for kind in ['agent_thought_chunk','user_message_chunk']:
            emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate=kind,content=dict(type='image',data='AAEC',mimeType='image/png',uri='memory://secret/private.png')))))
        update(sid,'Files ready. ')
        rid='output-permission-'+uuid.uuid4().hex;event=threading.Event();permissions[rid]=dict(event=event,allow=None)
        emit(dict(id=rid,method='session/request_permission',params=dict(sessionId=sid,toolCall=dict(toolCallId='publish',title='Publish files',status='pending'),options=[dict(optionId='yes',kind='allow_once',name='Allow once'),dict(optionId='no',kind='reject_once',name='Reject once')])))
        time.sleep(.08)
        content(sid,dict(type='resource',resource=dict(uri='memory://reports/notes.txt',mimeType='text/plain',text='原生文件\n内容')))
        while not event.wait(.02):
            if cancel.is_set():break
        assert permissions[rid]['allow']=='yes'
        blocks=[dict(type='audio',data='AwQF',mimeType='audio/wav'),
            dict(type='resource',resource=dict(uri='file:///tmp/result.bin',mimeType='application/octet-stream',blob='BgcI')),
            dict(type='resource_link',uri='https://example.invalid/report.pdf',name='report.pdf',mimeType='application/pdf'),
            dict(type='resource_link',uri='file:///private/report.txt',name='native-reference',description='A native path reference'),
            dict(type='resource_link',uri='data:application/octet-stream;base64,CQoL',name='inline.bin'),
            dict(type='resource',resource=dict(uri='memory://out/empty.txt',mimeType='text/plain',text='')),
            dict(type='resource',resource=dict(uri='memory://out/empty.bin',mimeType='application/octet-stream',blob='')),
            dict(type='resource',resource=dict(uri='memory://out/spaces.txt',mimeType='text/plain',text=' \n '))]
        for block in blocks:content(sid,block)
        update(sid,'Done.')
    elif objective in ['output-cancel','output-crash','output-disconnect']:
        if objective=='output-disconnect':time.sleep(.15);os._exit(7)
        while not cancel.wait(.02):pass
        emit(dict(id=message['id'],result=dict(stopReason='cancelled')));return
    elif objective=='output-duplicate':content(sid,image)
    elif objective=='output-links':
        for block in [dict(type='resource_link',uri='data:application/octet-stream,%FF%00%2B+',name='percent.bin'),
            dict(type='resource_link',uri='data:application/octet-stream;base64,AQI%3D',name='base64-percent.bin'),
            dict(type='resource_link',uri='urn:mnn:report',name='native-urn'),
            dict(type='resource_link',uri='https://example.invalid/a',name='🙂'*256),
            dict(type='resource_link',uri='data:;charset=utf-8,%E4%BD%A0%E5%A5%BD',name='charset.txt')]:content(sid,block)
    elif objective=='output-count-overflow':
        for i in range(32):content(sid,image)
    elif objective=='output-bytes-overflow':
        for i in range(2):content(sid,dict(type='resource',resource=dict(uri='memory://out/large-'+str(i),text='x'*140000)))
    elif objective=='output-escaped-overflow':
        for i in range(2):content(sid,dict(type='resource',resource=dict(uri='memory://out/escaped-'+str(i)+'.txt',text='\x01'*90000)))
    elif objective=='output-invalid-base64':content(sid,dict(type='audio',data='!!!',mimeType='audio/wav'))
    elif objective=='output-missing-mime':content(sid,dict(type='image',data='AAEC'))
    elif objective=='output-unknown':content(sid,dict(type='video',data='AAEC',mimeType='video/mp4'))
    elif objective=='output-invalid-resource':content(sid,dict(type='resource',resource=dict(uri='memory://out/missing')))
    elif objective=='output-long-name':content(sid,dict(type='resource_link',uri='https://example.invalid/a',name='x'*513))
    elif objective=='output-url-credentials':content(sid,dict(type='resource_link',uri='https://user:password@example.invalid/a',name='private'))
    elif objective=='output-invalid-disconnect':
        content(sid,dict(type='audio',data='!!!',mimeType='audio/wav'));time.sleep(.15);os._exit(7)
    emit(dict(id=message['id'],result=dict(stopReason='end_turn')))
def prompt(message):
    p=message['params'];sid=p['sessionId'];objective=p['prompt'][0]['text'];cancel=threading.Event();active[sid]=cancel
    with storage():
        assert sid in sessions and sessions[sid]['cwd']==os.getcwd()
        prior=len(sessions[sid]['prompts']);sessions[sid]['prompts'].append(objective);save()
    if objective.startswith('output-'):
        output(message,cancel);active.pop(sid,None);return
    if objective=='disconnect': os._exit(7)
    if objective in ['permission','parallel-permissions','permission-cancel']:
        requests=[]
        for index in range(2 if objective=='parallel-permissions' else 1):
            event=threading.Event();rid='permission-'+uuid.uuid4().hex;permissions[rid]=dict(event=event,allow=None);requests.append(rid)
            emit(dict(id=rid,method='session/request_permission',params=dict(sessionId=sid,toolCall=dict(toolCallId='tool-'+str(index),title='Write fixture file '+str(index),status='pending'),options=[dict(optionId='yes',kind='allow_once',name='Allow once'),dict(optionId='no',kind='reject_once',name='Reject once')])))
        emit(dict(method='session/update',params=dict(sessionId=sid,update=dict(sessionUpdate='tool_call_update',toolCallId='tool-0',status='in_progress',title='Approval is still pending'))))
        for rid in requests:
            while not permissions[rid]['event'].wait(.02):
                if cancel.is_set():break
        objective='allowed' if all(permissions[r]['allow']=='yes' for r in requests) else 'denied'
    duration=12 if objective in ['crash','hold','cancel-race'] else 1 if objective=='slow' else .03
    end=time.monotonic()+duration
    while time.monotonic()<end:
        if cancel.wait(.02) and MODE!='ignore-cancel':break
    if cancel.is_set() and objective!='cancel-race' and MODE!='ignore-cancel':
        emit(dict(id=message['id'],result=dict(stopReason='cancelled')))
    else:
        if objective=='inspect-content':
            data=json.dumps(p['prompt'][1:],ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()
            update(sid,'native content: '+hashlib.sha256(data).hexdigest())
        else:update(sid,'native result: '+objective+'; prior='+str(prior))
        emit(dict(id=message['id'],result=dict(stopReason='end_turn')))
    active.pop(sid,None)
    if objective=='idle-exit':
        time.sleep(.08);os._exit(0)

try:
    for line in sys.stdin:
        frame=json.loads(line);record('client',frame);method=frame.get('method');p=frame.get('params',{})
        if method=='initialize':
            assert p['protocolVersion']==1
            capabilities=dict(loadSession=MODE not in ['none','resume','capacity-none'] and not (MODE=='capability-drop' and (ROOT/'drop-capability').exists()),sessionCapabilities=dict(resume={}) if MODE=='resume' else {})
            if (ROOT/'capabilities.json').exists():capabilities['promptCapabilities']=json.loads((ROOT/'capabilities.json').read_text())
            emit(dict(id=frame['id'],result=dict(protocolVersion=1,agentCapabilities=capabilities,agentInfo=dict(name='independent-session-peer',version='1'))))
        elif method=='session/new':
            assert p==dict(cwd=os.getcwd(),mcpServers=[])
            sid='fixture-'+uuid.uuid4().hex
            with storage():sessions[sid]=dict(cwd=p['cwd'],prompts=[]);save()
            if MODE=='admission-lost':time.sleep(20);continue
            emit(dict(id=frame['id'],result=dict(sessionId=sid)))
        elif method in ['session/load','session/resume']:
            assert method==('session/resume' if MODE=='resume' else 'session/load')
            assert p['cwd']==os.getcwd() and p['mcpServers']==[] and p['sessionId'] in sessions
            if MODE=='load-fails':
                emit(dict(id=frame['id'],error=dict(code=-32000,message='native session unavailable')));continue
            if method=='session/load':
                for objective in sessions[p['sessionId']]['prompts']:
                    update(p['sessionId'],'HISTORY ONLY: '+objective+'\n')
                if MODE=='output':content(p['sessionId'],dict(type='resource',resource=dict(uri='memory://history/old.txt',text='HISTORY ARTIFACT ONLY')))
            emit(dict(id=frame['id'],result={}))
        elif method=='session/prompt':threading.Thread(target=prompt,args=(frame,),daemon=True).start()
        elif method=='session/cancel':
            if p['sessionId'] in active:active[p['sessionId']].set()
        elif frame.get('id') in permissions:
            value=permissions[frame['id']];value['allow']=frame['result']['outcome'].get('optionId');value['event'].set()
        else:raise AssertionError('Unexpected method '+str(method))
except Exception:
    import traceback
    (ROOT/'errors.txt').write_text(traceback.format_exc());raise
