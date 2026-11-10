#!/usr/bin/env python3
"""Exercise local onboarding through the real host with deterministic executables; never installs third-party agents."""
import argparse,json,os,shutil,socket,subprocess,sys,uuid
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args()
out=a.output.resolve();out.mkdir(parents=True,exist_ok=True);run=out/('run-'+uuid.uuid4().hex);run.mkdir();bins=run/'bin space';bins.mkdir();work=run/'project space';work.mkdir()
records=[];passed=[]
runtime=str(Path(shutil.which('dotnet')).resolve());host=str(a.host.resolve())
base=bins/'codex';adapter=bins/'codex-acp';npm=bins/'npm';capture=run/'npm.json';sentinel='fixture-do-not-print-key'
base.write_text('#!/bin/sh\nexit 0\n');base.chmod(0o700)
npm.write_text('#!'+sys.executable+'\nimport json,os,sys\nfrom pathlib import Path\np=Path(os.environ["SETUP_CAPTURE"])\np.write_text(json.dumps(sys.argv[1:]))\na=Path(os.environ["CODEX_ACP_BIN"])\na.write_text("#!/bin/sh\\nexit 0\\n")\na.chmod(0o700)\n');npm.chmod(0o700)
env=dict(os.environ,PATH=str(bins)+os.pathsep+os.environ.get('PATH',''),CODEX_PATH=str(base),CODEX_ACP_BIN=str(adapter),SETUP_CAPTURE=str(capture),OPENAI_API_KEY=sentinel)
def cli(args,code=0):
 command=[runtime,host,*args];r=subprocess.run(command,env=env,text=True,capture_output=True,timeout=30)
 records.append(dict(command=command,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode));(out/'commands.json').write_text(json.dumps(records,indent=2))
 assert r.returncode==code,records[-1]
 assert sentinel not in r.stdout+r.stderr
 return r

def ok(message):passed.append(message);print('PASS '+message,flush=True)
cli(['--backend-install','codex']);assert json.loads(capture.read_text())==['install','-g','@agentclientprotocol/codex-acp@latest'];ok('installs only a missing adapter and preserves the existing CLI')
capture.unlink();cli(['--backend-install','codex']);assert not capture.exists();ok('repeated installation performs no npm operation')
registration=run/'local'/'registration'
args=['--setup-local','--backend','codex','--workspace',str(work),'--directory',str(registration),'--runtime',runtime,'--port','0']
result=json.loads(cli(args).stdout);token=Path(result['TokenPath']).read_text();first_address=result['Address'];assert len(token)==64 and token not in records[-1]['stdout']
config=json.loads((registration/'host.json').read_text());h=config['Harnesses'][0]
assert h['Backend']=='codex' and h['Command']==str(adapter) and h['NativeCommand']==str(base)
assert config['Workspaces']['project']==str(work) and h['PermissionMode']=='native'
assert 'MNN_LOCAL_HARNESS_TOKEN' not in h['ForwardEnvironment']
credentials=json.loads((registration/'credentials.json').read_text());assert credentials['MNN_LOCAL_HARNESS_TOKEN']==token and credentials['OPENAI_API_KEY']==sentinel
if os.name!='nt':assert Path(result['TokenPath']).stat().st_mode&0o777==0o600
ok('prepares private authenticated localhost service with selected backend and project; secrets excluded from stdout and backend host-token forwarding')
second=json.loads(cli(args).stdout);assert second['Address']==first_address and Path(second['TokenPath']).read_text()==token
ok('repreparing keeps the local token and automatically assigned port stable')
cli(args+['--backend','claude'],2);ok('duplicate setup flags rejected')
wrong=args.copy();wrong[wrong.index('codex')]='unknown';cli(wrong,2);ok('unknown backend rejected')
missing=args.copy();missing[missing.index(str(work))]=str(work/'missing');cli(missing,2);ok('nonexistent project rejected')
invalid=args.copy();invalid[-1]='1';cli(invalid,2);ok('privileged port rejected')
(out/'summary.json').write_text(json.dumps(dict(passed=passed,third_party_installations=0),indent=2))
