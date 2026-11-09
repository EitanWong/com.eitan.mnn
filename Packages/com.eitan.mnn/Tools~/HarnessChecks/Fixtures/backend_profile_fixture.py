#!/usr/bin/env python3
"""Strict independent ACP profile peer; asserts launch argv/environment before protocol."""
import json
import os
import sys
from pathlib import Path

expected = json.loads(Path(os.environ['SIRIAI_PROFILE_EXPECTATION']).read_text())
record = {'argv': sys.argv[1:], 'methods': [], 'environment_ok': True, 'prompted': False}
output = Path(os.environ['SIRIAI_PROFILE_CAPTURE'])
output.write_text(json.dumps(record))
assert sys.argv[1:] == expected['arguments'], (sys.argv[1:], expected['arguments'])
for name in expected.get('present', []):
    assert os.environ.get(name) == 'fixture-' + name, name
for name in expected.get('absent', []):
    assert name not in os.environ, name
for name, value in expected.get('values', {}).items():
    assert os.environ.get(name) == value, name
output = Path(os.environ['SIRIAI_PROFILE_CAPTURE'])

def save():
    output.write_text(json.dumps(record))

def emit(value):
    print(json.dumps(dict(jsonrpc='2.0', **value)), flush=True)

save()
for line in sys.stdin:
    message = json.loads(line)
    method = message.get('method'); record['methods'].append(method); save()
    if method == 'initialize':
        emit(dict(id=message['id'], result=dict(protocolVersion=1, agentCapabilities={})))
    elif method == 'session/new':
        options = [dict(id='permissionMode', type='select', currentValue='default', options=[dict(value='bypassPermissions', name='Full')]),
                   dict(id='mode', type='select', currentValue='default', options=[dict(value='auto', name='Auto')])]
        if expected.get('missing_option'): options = []
        emit(dict(id=message['id'], result=dict(sessionId='profile-session', configOptions=options)))
    elif method == 'session/set_config_option':
        setting = message['params']
        assert [setting['configId'], setting['value']] == expected['setting']
        emit(dict(id=message['id'], result=dict(configOptions=[dict(id=setting['configId'],type='select',currentValue='default' if expected.get('unconfirmed') else setting['value'],options=[dict(value=setting['value'],name='Selected')])])))
    elif method == 'session/prompt':
        record['prompted'] = True; save()
        assert message['params']['prompt'][0]['text'] == 'Profile task'
        if expected.get('request_permission'):
            record['prompt_id'] = message['id']; save()
            emit(dict(id='profile-permission', method='session/request_permission', params=dict(sessionId='profile-session', toolCall=dict(title='Profile fixture action'), options=[dict(optionId='yes',kind='allow_always' if expected.get('always_only') else 'allow_once',name='Yes'),dict(optionId='no',kind='reject_once',name='No')])))
        else:
            emit(dict(method='session/update',params=dict(sessionId='profile-session',update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='profile completed')))))
            emit(dict(id=message['id'],result=dict(stopReason='end_turn')))
    elif message.get('id') == 'profile-permission':
        assert message['result']['outcome']['optionId'] == 'yes'
        record['permission_confirmed'] = True; save()
        emit(dict(method='session/update',params=dict(sessionId='profile-session',update=dict(sessionUpdate='agent_message_chunk',content=dict(type='text',text='profile completed')))))
        emit(dict(id=record['prompt_id'],result=dict(stopReason='end_turn')))
