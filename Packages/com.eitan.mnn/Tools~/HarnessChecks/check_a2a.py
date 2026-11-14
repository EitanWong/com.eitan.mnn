#!/usr/bin/env python3
"""Exercises four real HTTP wire profiles and crash semantics against an external independent server."""
from pathlib import Path
import json,os,socket,subprocess,sys,urllib.request
from concurrent.futures import ThreadPoolExecutor
sys.dont_write_bytecode=True
import check_remote_protocols as common
OUT=Path(os.environ.get('SIRIAI_PROTOCOL_CHECK_OUTPUT',str(common.ROOT/'TestArtifacts~/SiriAIHarness/RemoteProtocols')))/'A2aChecks'
OUT.mkdir(parents=True,exist_ok=True);common.OUT=OUT
records=[]
def passed(text):records.append(dict(result='PASS',behavior=text));print('PASS '+text,flush=True)
def run(version,binding):
    with socket.socket() as sock:sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
    url='http://127.0.0.1:'+str(port)
    log=(OUT/('fixture-'+version+'-'+binding+'.log')).open('w')
    peer=subprocess.Popen([sys.executable,'-u',str(common.SAMPLE/'Tools~/HarnessChecks/Fixtures/a2a_fixture.py'),str(port),version,binding,common.TOKEN],stdout=log,stderr=log)
    host=common.Host('a2a',[dict(Id='backend',Protocol='a2a',Endpoint=url,TokenEnvironmentVariable='SIRIAI_REMOTE_CHECK_TOKEN',PollMilliseconds=30)])
    def state():
        try:
            with urllib.request.urlopen(url+'/__state',timeout=2) as r:return json.load(r)
        except OSError:return None
    def finished(identity):
        t=host.task(identity)
        if t['State'] in [3,4,5,6]:return t
    try:
        common.wait(state);host.start(Path(os.environ.get('SIRIAI_REMOTE_BASELINE_HOST',str(common.DLL))))
        first=host.submit('outage','restart');common.wait(lambda:host.task(first['Id']).get('BackendState'))
        # Wait for the confirmed remote identity, not the pre-admission intent.
        common.wait(lambda:json.loads(host.task(first['Id'])['BackendState']).get('RemoteId'))
        host.stop(kill=True);host.start()
        result=common.wait(lambda:finished(first['Id']));assert result['State']==3,result
        assert len(state()['tasks'])==1 and first['Id'] not in state()['tasks']
        assert [p['Kind'] for p in result['Artifacts'][0]['Parts']]==[0,1,2,3],result
        passed(version+' '+binding+' resumes confirmed remote task and preserves four artifact types')
        prompt=host.submit('input','input')
        for expected in ['auth-required','input-required']:
            waiting=common.wait(lambda:(lambda t:t if t['WaitingFor']==1 and expected in t['Activity'] else None)(host.task(prompt['Id'])))
            common.time.sleep(.1)
            assert host.task(prompt['Id'])['RequestId']==waiting['RequestId'], 'Polling timestamp must not replace the pending prompt identity'
            host.call('/v1/tasks/'+prompt['Id']+'/respond',dict(RequestId='stale',Text='confirmed'),409)
            host.call('/v1/tasks/'+prompt['Id']+'/respond',dict(RequestId=waiting['RequestId'],Text='user answer' if expected=='auth-required' else 'confirmed'))
        assert common.wait(lambda:finished(prompt['Id']))['State']==3
        assert len(state()['tasks'])==2
        passed(version+' '+binding+' routes auth and input to the same task/context with exact prompt identity')
        cancelled=host.submit('slow','cancel');common.wait(lambda:json.loads(host.task(cancelled['Id']).get('BackendState') or '{}').get('RemoteId'))
        result=host.call('/v1/tasks/'+cancelled['Id']+'/cancel',{})
        assert result['State']==5 and any(t['state']=='canceled' for t in state()['tasks'].values()),result
        passed(version+' '+binding+' waits for protocol-confirmed cancellation')
        pending=host.submit('slow','cancel-restart');common.wait(lambda:json.loads(host.task(pending['Id']).get('BackendState') or '{}').get('RemoteId'))
        with ThreadPoolExecutor(max_workers=1) as worker:
            cancel=worker.submit(host.call,'/v1/tasks/'+pending['Id']+'/cancel',{})
            common.wait(lambda:any(t['state']=='cancelling' for t in state()['tasks'].values()))
            host.stop(kill=True)
            try:cancel.result(timeout=5)
            except (OSError,AssertionError):pass
        host.start();assert common.wait(lambda:finished(pending['Id']))['State']==5
        passed(version+' '+binding+' resumes persisted cancellation intent after gateway crash')
        message=host.submit('message','message');result=common.wait(lambda:finished(message['Id']))
        assert result['State']==3 and result['Result']=='immediate reply',result
        ambiguous=host.submit('ambiguous','ambiguous');result=common.wait(lambda:finished(ambiguous['Id']))
        assert result['State']==6,result
        before=len(state()['requests']);host.stop(kill=True);host.start()
        assert host.task(ambiguous['Id'])['State']==6 and len(state()['requests'])==before
        assert not state()['errors'],state()['errors']
        passed(version+' '+binding+' accepts direct message and never replays ambiguous admission')
        bad=common.Host('untrusted-card',[dict(Id='backend',Protocol='a2a',Endpoint=url,AgentCardPath='.well-known/agent-card.json?evil=1',TokenEnvironmentVariable='SIRIAI_REMOTE_CHECK_TOKEN',PollMilliseconds=30)])
        before=len(state()['requests'])
        try:
            bad.start();rejected=bad.submit('slow','untrusted')
            result=common.wait(lambda:(lambda t:t if t['State']==6 else None)(bad.task(rejected['Id'])))
            assert 'UnauthorizedAccessException' in result['Result'] and len(state()['requests'])==before,result
        finally:bad.stop()
        passed(version+' '+binding+' rejects card-selected origins before sending credentials or task payload')
        (OUT/(version+'-'+binding+'-wire.json')).write_text(json.dumps(state(),indent=2)+'\n')
    finally:
        (OUT/(version+'-'+binding+'-wire.json')).write_text(json.dumps(state(),indent=2)+'\n')
        host.stop();peer.terminate();peer.wait(timeout=5);log.close()
try:
    for version in ['1.0','0.3']:
        for binding in ['JSONRPC','HTTP+JSON']:run(version,binding)
finally:(OUT/'results.json').write_text(json.dumps(records,indent=2)+'\n')
