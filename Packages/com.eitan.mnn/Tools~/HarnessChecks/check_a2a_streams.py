#!/usr/bin/env python3
from pathlib import Path
import json,os,socket,subprocess,sys,urllib.request
sys.dont_write_bytecode=True
import check_remote_protocols as common
OUT=Path(os.environ.get('SIRIAI_PROTOCOL_CHECK_OUTPUT',str(common.ROOT/'TestArtifacts~/SiriAIHarness/Streaming')))/'StreamChecks'
OUT.mkdir(parents=True,exist_ok=True);common.OUT=OUT
records=[]
def passed(text):records.append(dict(result='PASS',behavior=text));print('PASS '+text,flush=True)
def run(version,binding):
    with socket.socket() as s:s.bind(('127.0.0.1',0));port=s.getsockname()[1]
    url='http://127.0.0.1:'+str(port);label=version+' '+binding
    log=(OUT/(version+'-'+binding+'.log')).open('w')
    peer=subprocess.Popen([sys.executable,'-u',str(common.SAMPLE/'Tools~/HarnessChecks/Fixtures/a2a_stream_fixture.py'),str(port),version,binding,common.TOKEN],stdout=log,stderr=log)
    host=common.Host('stream',[dict(Id='backend',Protocol='a2a',Endpoint=url,TokenEnvironmentVariable='SIRIAI_REMOTE_CHECK_TOKEN',PollMilliseconds=30)])
    def state():
        try:
            with urllib.request.urlopen(url+'/__state',timeout=2) as r:return json.load(r)
        except OSError:return None
    def release(identity):
        with urllib.request.urlopen(url+'/__release?id='+identity,timeout=2) as r:r.read()
    def finished(identity):
        t=host.task(identity)
        if t['State'] in [3,4,5,6]:return t
    def partial(identity):
        t=host.task(identity)
        if t['State'] in [4,5,6]:raise AssertionError(t)
        if t.get('Artifacts') and len(t['Artifacts'][0]['Parts'])==2:return t
    def remote(identity):return json.loads(host.task(identity).get('BackendState') or '{}').get('RemoteId')
    def submit(text,key,attachments=None):
        return host.call('/v1/tasks',dict(ConversationId='frontend',SubmissionKey=key,HarnessId='backend',WorkspaceKey='test',Objective=text,Attachments=attachments),202)
    try:
        common.wait(state);host.start(Path(os.environ.get('SIRIAI_REMOTE_BASELINE_HOST',str(common.DLL))))
        parts=[dict(Kind=0,Payload='附加文本',MediaType='text/plain'),dict(Kind=1,Payload='https://example.org/input.png',MediaType='image/png'),dict(Kind=2,Payload='{"key":42}',MediaType='application/json'),dict(Kind=3,Payload='aGk=',MediaType='text/plain',FileName='input.txt')]
        first=submit('stream','first',parts);progress=common.wait(lambda:partial(first['Id']))
        assert progress['State']==1 and not progress['ResultPending']
        assert [p['Payload'] for p in progress['Artifacts'][0]['Parts']]==['first','第二段']
        assert host.call('/v1/tasks',dict(ConversationId='frontend',SubmissionKey='first',HarnessId='backend',WorkspaceKey='test',Objective='stream',Attachments=parts),202)['Id']==first['Id']
        altered=[dict(parts[0],Payload='different')]+parts[1:]
        host.call('/v1/tasks',dict(ConversationId='frontend',SubmissionKey='first',HarnessId='backend',WorkspaceKey='test',Objective='stream',Attachments=altered),400)
        remoteid=remote(first['Id']);received=state()['tasks'][remoteid]['inputs'];assert len(received)==5,received
        assert '附加文本' in json.dumps(received,ensure_ascii=False)
        if not (version=='0.3' and binding=='HTTP+JSON'):assert 'input.txt' in json.dumps(received)
        release(remoteid);done=common.wait(lambda:finished(first['Id']));assert done['State']==3 and done['Result']=='已完成',done
        assert len(done['Artifacts'][0]['Parts'])==2
        passed(label+' streams fragmented UTF-8/multiline SSE and persists artifact append before terminal status; attachments retain identity')
        restart=submit('restart','restart');common.wait(lambda:partial(restart['Id']));remoteid=remote(restart['Id'])
        assert json.loads(host.task(restart['Id'])['BackendState'])['ContextId']==state()['tasks'][remoteid]['context'], 'Context supplied by a later stream delta must be durably adopted'
        host.stop(kill=True);host.start();common.wait(lambda:any(c['op']=='subscribe' for c in state()['calls']))
        release(remoteid);done=common.wait(lambda:finished(restart['Id']));assert done['State']==3,done
        assert len([c for c in state()['calls'] if c['op']=='stream'])==2
        passed(label+' reattaches known task via native subscription after host crash without resending work')
        silent=submit('silent','silent');common.wait(lambda:partial(silent['Id']))
        before=len([c for c in state()['calls'] if c['op']=='subscribe'])
        host.stop(kill=True);host.start();common.wait(lambda:len([c for c in state()['calls'] if c['op']=='subscribe'])>before)
        req=urllib.request.Request(host.url+'/v1/tasks/'+silent['Id']+'/cancel',b'{}',headers={'Authorization':'Bearer '+common.TOKEN,'Content-Type':'application/json'})
        with urllib.request.urlopen(req,timeout=2) as response:assert json.load(response)['State']==5
        passed(label+' cancels a recovered task while its native subscription only sends heartbeats')
        gap=submit('gap','gap');common.wait(lambda:partial(gap['Id']));remoteid=remote(gap['Id'])
        assert host.task(gap['Id'])['State']==1
        release(remoteid);assert common.wait(lambda:finished(gap['Id']))['State']==3
        passed(label+' EOF/lastChunk do not complete a running task; confirmed-ID polling recovers final state')
        cancel=submit('cancel','cancel');common.wait(lambda:partial(cancel['Id']))
        assert host.call('/v1/tasks/'+cancel['Id']+'/cancel',{})['State']==5
        passed(label+' accepts confirmed cancellation while the event stream stays open')
        prompt=submit('input','input');waiting=common.wait(lambda:(lambda t:t if t['WaitingFor']==1 else None)(host.task(prompt['Id'])))
        assert waiting['State']==1 and not waiting['ResultPending']
        host.call('/v1/tasks/'+prompt['Id']+'/respond',dict(RequestId=waiting['RequestId'],Text='Reports'))
        release(remote(prompt['Id']));assert common.wait(lambda:finished(prompt['Id']))['State']==3
        passed(label+' final input-required event keeps the same task active through explicit answer')
        for objective in ['malformed','oversized','mismatch']:
            invalid=submit(objective,objective);result=common.wait(lambda:finished(invalid['Id']))
            assert result['State']==6,result
        passed(label+' rejects malformed/oversized SSE and unrelated task events without false success')
        direct=submit('message','message');done=common.wait(lambda:finished(direct['Id']))
        assert done['State']==3 and done['Result']=='直接回复',done
        assert not state()['errors'],state()['errors']
        passed(label+' accepts a direct streamed Message without inventing a remote Task')
        gateway=common.Host('delegate',[dict(Id='backend',Protocol='http',Endpoint=host.url,RemoteHarnessId='backend',RemoteWorkspace='test',TokenEnvironmentVariable='SIRIAI_REMOTE_CHECK_TOKEN',RemoteCapabilities=19,PollMilliseconds=30)])
        try:
            gateway.start()
            delegated=gateway.call('/v1/tasks',dict(ConversationId='frontend',SubmissionKey='delegated',HarnessId='backend',WorkspaceKey='test',Objective='stream',Attachments=parts),202)
            common.wait(lambda:(lambda t:t if t.get('Artifacts') and len(t['Artifacts'][0]['Parts'])==2 else None)(gateway.task(delegated['Id'])))
            downstream=[t for t in host.call('/v1/tasks') if t['SubmissionKey']==delegated['Id']][0]
            assert downstream['Id']!=delegated['Id']
            fields=['Kind','Payload','MediaType','FileName']
            assert [[p.get(k) for k in fields] for p in downstream['Attachments']]==[[p.get(k) for k in fields] for p in parts]
            release(remote(downstream['Id']))
            done=common.wait(lambda:(lambda t:t if t['State']==3 else None)(gateway.task(delegated['Id'])))
            assert len(done['Artifacts'][0]['Parts'])==2 and done['Result']=='已完成'
            passed(label+' HTTP delegation forwards input attachments and durable partial/final stream artifacts')
        finally:gateway.stop()
    finally:
        (OUT/(version+'-'+binding+'-wire.json')).write_text(json.dumps(state(),ensure_ascii=False,indent=2)+'\n')
        host.stop();peer.terminate();peer.wait(timeout=5);log.close()
try:
    for version in ['1.0','0.3']:
        for binding in ['JSONRPC','HTTP+JSON']:run(version,binding)
finally:(OUT/'results.json').write_text(json.dumps(records,indent=2)+'\n')
