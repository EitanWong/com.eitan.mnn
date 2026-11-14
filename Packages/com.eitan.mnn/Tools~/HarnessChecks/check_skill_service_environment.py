from pathlib import Path
import argparse,json,os,shutil,subprocess,uuid
p=argparse.ArgumentParser();p.add_argument('--host',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();O=a.output.resolve()/('service-skills-'+uuid.uuid4().hex);O.mkdir(parents=True)
config=O/'input.json';directory=O/'registration';token=uuid.uuid4().hex*2;env=dict(os.environ,SKILL_SERVICE_TOKEN=token,SKILL_SERVICE_FORWARD='explicit-skill-setting')
config.write_text(json.dumps(dict(DataDirectory=str(O/'data'),Identities=[dict(OwnerId='alice',TokenEnvironmentVariable='SKILL_SERVICE_TOKEN')],Skills=dict(Command=shutil.which('npx'),AllowedOwners=['alice'],ForwardEnvironment=['SKILL_SERVICE_FORWARD'],WorkingDirectory=str(O)))))
cmd=['dotnet',str(a.host),'--service','prepare','--id','skills-environment','--config',str(config),'--runtime',str(Path(shutil.which('dotnet')).resolve()),'--directory',str(directory)]
r=subprocess.run(cmd,env=env,capture_output=True,text=True,timeout=30)
assert r.returncode==0,r.stdout+r.stderr
credentials=json.loads((directory/'credentials.json').read_text())
(a.output/'skill-service-command.json').write_text(json.dumps(dict(command=cmd,stdout=r.stdout,stderr=r.stderr,exit_code=r.returncode,forwarded_environment_present='SKILL_SERVICE_FORWARD' in credentials),indent=2)+'\n')
assert credentials.get('SKILL_SERVICE_FORWARD')=='explicit-skill-setting','SKILL_SERVICE_ENVIRONMENT_MISSING: service preparation dropped the skills installer environment'
assert json.loads((directory/'host.json').read_text())['Skills']['WorkingDirectory']==str(O)
print('PASS service prepare preserves skills explicit environment and normalized host paths without starting a service or installer')
