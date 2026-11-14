#!/usr/bin/env python3
"""Real host-to-host HTTP delegation and restart recovery; deterministic ACP peer only."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
from pathlib import Path
import json,os,socket,subprocess,sys,time,urllib.request,urllib.error,uuid
SAMPLE=PACKAGE_ROOT
ROOT=PROJECT_ROOT
OUT=Path(os.environ.get('SIRIAI_PROTOCOL_CHECK_OUTPUT',str(ROOT/'TestArtifacts~/SiriAIHarness/RemoteProtocols')))/'HttpChecks'
OUT.mkdir(parents=True,exist_ok=True)
DLL=Path(os.environ.get('SIRIAI_PROTOCOL_CHECK_DLL',str(ROOT/'TestArtifacts~/SiriAIHarness/Host/bin/Debug/net8.0/SiriAI.HarnessHost.dll')))
TOKEN=uuid.uuid4().hex+uuid.uuid4().hex
ENV=dict(os.environ,SIRIAI_REMOTE_CHECK_TOKEN=TOKEN)
records=[]

def wait(test,timeout=15):
    end=time.monotonic()+timeout
    while time.monotonic()<end:
        value=test()
        if value:return value
        time.sleep(.03)
    raise AssertionError('Timed out waiting for observed remote state')

class Host:
    def __init__(self,name,harnesses):
        with socket.socket() as sock:
            sock.bind(('127.0.0.1',0));port=sock.getsockname()[1]
        self.url='http://127.0.0.1:'+str(port)
        self.path=OUT/(name+'-'+uuid.uuid4().hex);self.path.mkdir()
        self.config=self.path/'host.json'
        self.config.write_text(json.dumps(dict(Listen=self.url,DataDirectory=str(self.path/'data'),Identities=[dict(OwnerId=name,TokenEnvironmentVariable='SIRIAI_REMOTE_CHECK_TOKEN')],Workspaces=dict(test=str(self.path)),Harnesses=harnesses)))
        self.process=None
    def call(self,path,data=None,expected=200):
        req=urllib.request.Request(self.url+path,None if data is None else json.dumps(data).encode(),headers={'Authorization':'Bearer '+TOKEN,'Content-Type':'application/json'})
        try:
            with urllib.request.urlopen(req,timeout=10) as response:code,body=response.status,response.read()
        except urllib.error.HTTPError as error:code,body=error.code,error.read()
        assert code==expected,(path,code,body)
        return json.loads(body) if body else None
    def start(self,dll=DLL):
        self.log=(self.path/('host-'+uuid.uuid4().hex+'.log')).open('w')
        self.process=subprocess.Popen(['dotnet',str(dll),'--config',str(self.config)],env=ENV,stdout=self.log,stderr=self.log)
        def ready():
            if self.process.poll() is not None:raise AssertionError('Host exited '+str(self.process.returncode)+': '+str(self.path))
            try:return self.call('/v1/tasks') is not None
            except urllib.error.URLError:return False
        wait(ready)
    def stop(self,kill=False):
        if self.process is None:return
        self.process.kill() if kill else self.process.terminate()
        try:self.process.wait(timeout=10)
        except subprocess.TimeoutExpired:self.process.kill();self.process.wait(timeout=5)
        self.process=None;self.log.close()
    def submit(self,text,key):
        return self.call('/v1/tasks',dict(ConversationId='frontend',SubmissionKey=key,HarnessId='backend',WorkspaceKey='test',Objective=text),202)
    def task(self,identity):return self.call('/v1/tasks/'+identity)

def passed(text):
    records.append(dict(result='PASS',behavior=text));print('PASS '+text,flush=True)

def main():
    down=Host('remote',[dict(Id='backend',Protocol='acp',Command=sys.executable,Arguments=['-u',str(SAMPLE/'Tools~/HarnessChecks/Fixtures/acp_fixture.py')])])
    up=Host('gateway',[dict(Id='backend',Protocol='http',Endpoint=down.url,RemoteHarnessId='backend',RemoteWorkspace='test',TokenEnvironmentVariable='SIRIAI_REMOTE_CHECK_TOKEN',RemoteCapabilities=5,PollMilliseconds=50)])
    try:
        down.start();up.start(Path(os.environ.get('SIRIAI_REMOTE_BASELINE_HOST',str(DLL))))
        first=up.submit('slow','reconnect')
        remote=wait(lambda:down.call('/v1/tasks'))[0]
        assert first['Id']!=remote['Id'] and remote['WorkspaceKey']==str(down.path)
        assert 'gateway' not in remote['ConversationId']
        wait(lambda:up.task(first['Id']).get('BackendState'))
        up.stop(kill=True);up.start()
        finished=wait(lambda: (lambda t:t if t['State']==3 else None)(up.task(first['Id'])))
        assert finished['Result']=='fixture result: slow'
        assert len(down.call('/v1/tasks'))==1
        passed('HTTP delegation survives frontend disconnect and gateway crash without replaying remote work')
        prompt=up.submit('permission','input')
        waiting=wait(lambda:(lambda t:t if t['WaitingFor']==2 else None)(up.task(prompt['Id'])))
        up.call('/v1/tasks/'+prompt['Id']+'/respond',dict(RequestId='wrong',Text='Allow',Allow=True),409)
        up.call('/v1/tasks/'+prompt['Id']+'/respond',dict(RequestId=waiting['RequestId'],Text='Allow once',Allow=True))
        result=wait(lambda:(lambda t:t if t['State']==3 else None)(up.task(prompt['Id'])))
        assert result['Result']=='fixture result: approved'
        passed('HTTP permission response preserves the exact downstream prompt and rejects stale identity')
        cancelled=up.submit('slow','cancel')
        wait(lambda:up.task(cancelled['Id']).get('BackendState'))
        result=up.call('/v1/tasks/'+cancelled['Id']+'/cancel',{})
        assert result['State']==5,result
        remote=down.call('/v1/tasks')[-1]
        assert remote['State']==5,remote
        passed('HTTP cancellation waits for downstream backend confirmation')
    finally:
        up.stop();down.stop()
        (OUT/'results.json').write_text(json.dumps(records,indent=2)+'\n')

if __name__=='__main__':main()
