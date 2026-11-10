#!/usr/bin/env python3
from pathlib import Path
import json,os,subprocess,sys,time
settings=json.loads(Path(__file__).with_suffix('.json').read_text());root=Path(settings['root']);argv=sys.argv[1:]
with (root/'events.jsonl').open('a') as f:f.write(json.dumps(dict(argv=argv,pid=os.getpid(),cwd=os.getcwd(),host_leaked='SKILL_HOST_TOKEN' in os.environ,private_leaked='PRIVATE_SKILL_SECRET' in os.environ,forwarded=os.environ.get('SKILL_FORWARD'))) + '\n')
assert argv[:2]==['-y',settings.get('package','skills@1.5.22')],argv
args=argv[2:]
if 'hang' in args:
 child=subprocess.Popen([sys.executable,'-c','import time;time.sleep(60)'])
 with (root/'events.jsonl').open('a') as f:f.write(json.dumps(dict(child_pid=child.pid))+'\n')
 time.sleep(60)
if 'flood' in args:sys.stdout.write('x'*100000);sys.stdout.flush();time.sleep(60)
if 'invalid-utf8' in args:sys.stdout.buffer.write(b'\xff\n');sys.exit(0)
if 'failed' in args:print('repository unavailable',file=sys.stderr);sys.exit(7)
if 'hold' in args:
 while not (root/'release').exists():time.sleep(.02)
state=root/'installed.json';installed=json.loads(state.read_text()) if state.exists() else []
if args[0]=='add' and '--list' in args:print('review\npdf-tools\n<b>literal text</b>')
elif args[0]=='add':
 names=[args[i+1] for i,x in enumerate(args) if x=='--skill'];installed=sorted(set(installed+names));state.write_text(json.dumps(installed));print('installed '+','.join(names))
elif args[0]=='remove':installed=[n for n in installed if n!=args[1]];state.write_text(json.dumps(installed));print('removed '+args[1])
elif args[0]=='list':print('skills '+','.join(installed))
elif args[0]=='update':print('updated')
else:raise AssertionError(args)
