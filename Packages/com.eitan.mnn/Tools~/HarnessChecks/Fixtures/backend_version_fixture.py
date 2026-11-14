#!/usr/bin/env python3
"""Independent local CLI with version/protocol modes. No model or vendor execution."""
from pathlib import Path
import json
import os
import subprocess
import sys
import time

settings = json.loads(Path(__file__).with_suffix('.json').read_text())
event = dict(pid=os.getpid(), argv=sys.argv[1:], mode='version' if '--version' in sys.argv else 'protocol',
             host_token_leaked='VERSION_HOST_TOKEN' in os.environ,
             unrelated_secret_leaked='PRIVATE_PROVIDER_TOKEN' in os.environ,
             forwarded=os.environ.get('VERSION_TEST_FORWARD'))
with Path(settings['events']).open('a') as log:
    log.write(json.dumps(event) + '\n')
if '--version' in sys.argv:
    if settings.get('mode') == 'invalid-utf8':
        sys.stdout.buffer.write(b'qwen 0.21.6\xff\n')
        sys.stdout.buffer.flush()
        sys.exit(0)
    if settings.get('mode') == 'hang':
        child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'])
        with Path(settings['events']).open('a') as log:
            log.write(json.dumps(dict(child_pid=child.pid)) + '\n')
        time.sleep(60)
    if settings.get('mode') == 'flood':
        sys.stdout.write('x' * 100000)
        sys.stdout.flush()
        time.sleep(60)
    output = sys.stderr if settings.get('mode') == 'stderr' else sys.stdout
    output.write(settings['version'] + '\n')
    sys.exit(settings.get('exit', 0))
for line in sys.stdin:
    request = json.loads(line)
    if request.get('method') == 'initialize':
        with Path(settings['events']).open('a') as log:
            log.write(json.dumps(dict(method='initialize', pid=os.getpid())) + '\n')
        result = dict(protocolVersion=1, agentCapabilities={})
        sys.stdout.write(json.dumps(dict(jsonrpc='2.0', id=request['id'], result=result)) + '\n')
        sys.stdout.flush()
    else:
        with Path(settings['events']).open('a') as log:
            log.write(json.dumps(dict(method=request.get('method'), pid=os.getpid())) + '\n')
