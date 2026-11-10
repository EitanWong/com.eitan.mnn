#!/usr/bin/env python3
"""Independent skills installer and ACP peer; all effects are in the controller's fixture root."""
from pathlib import Path
import json, os, sys, time, uuid

metadata = json.loads(Path(sys.argv[0]).with_suffix('.json').read_text())
root = Path(metadata['root'])
home = root / 'home'
directories = {'qwen-code': '.qwen/skills', 'claude-code': '.claude/skills', 'codex': '.agents/skills',
    'opencode': '.agents/skills', 'kimi-code-cli': '.agents/skills', 'openclaw': '.openclaw/skills',
    'qoder': '.qoder/skills', 'hermes-agent': '.hermes/skills', 'codebuddy': '.codebuddy/skills', 'pi': '.pi/agent/skills'}
def event(**fields):
    with (root / 'events.jsonl').open('a') as f:
        f.write(json.dumps(dict(pid=os.getpid(), time_ns=time.time_ns(), **fields)) + '\n')

if metadata['mode'] == 'installer':
    argv = sys.argv[1:]
    event(kind='installer-start', argv=argv, host_token_leaked=bool(os.environ.get('STARTUP_HOST_TOKEN')))
    operation = argv[2]
    source = argv[3] if operation == 'add' else None
    if source in ['hold-source', 'hang-source']:
        while not (root / 'release-installer').exists(): time.sleep(.02)
    if source == 'failed-source':
        print('source no longer contains this skill', file=sys.stderr); sys.exit(9)
    if source == 'flood-source':
        print('x' * 20000); sys.exit(0)
    if operation == 'add' and '--list' not in argv and source != 'no-files':
        names = [argv[i+1] for i, v in enumerate(argv) if v == '--skill']
        targets = [argv[i+1] for i, v in enumerate(argv) if v == '-a']
        for target in targets:
            for name in names:
                p = home / directories[target] / name / 'SKILL.md'
                p.parent.mkdir(parents=True, exist_ok=True); p.write_text('fixture ' + source + '/' + name)
    if operation == 'remove':
        lock = home / '.agents/.skill-lock.json'; values = json.loads(lock.read_text())
        values['skills'].pop(argv[3], None); lock.write_text(json.dumps(values))
        for directory in set(directories.values()):
            (home / directory / argv[3] / 'SKILL.md').unlink(missing_ok=True)
    if source == 'rewrite-lock':
        lock = home / '.agents/.skill-lock.json'; values = json.loads(lock.read_text())
        values['installerGeneration'] = values.get('installerGeneration', 0) + 1
        lock.write_text(json.dumps(values))
    event(kind='installer-end', argv=argv)
    print('installer observed filesystem effects'); sys.exit(0)

scan = {directory: sorted(p.parent.name for p in (home / directory).glob('*/SKILL.md')) for directory in set(directories.values())}
event(kind='backend-scan', files=scan)
session = uuid.uuid4().hex
for line in sys.stdin:
    request = json.loads(line); method = request.get('method')
    if method == 'initialize': result = dict(protocolVersion=1, agentCapabilities={})
    elif method == 'session/new': result = dict(sessionId=session)
    elif method == 'session/prompt':
        objective = ''.join(p.get('text', '') for p in request['params']['prompt'])
        event(kind='backend-prompt', objective=objective)
        if objective == 'hold-backend':
            while not (root / 'release-backend').exists(): time.sleep(.02)
        message = dict(jsonrpc='2.0', method='session/update', params=dict(sessionId=session,
            update=dict(sessionUpdate='agent_message_chunk', content=dict(type='text', text=json.dumps(scan)))))
        print(json.dumps(message), flush=True)
        result = dict(stopReason='end_turn')
    else: result = {}
    if 'id' in request: print(json.dumps(dict(jsonrpc='2.0', id=request['id'], result=result)), flush=True)
