#!/usr/bin/env python3
"""Deterministic ACP peer for protocol integration tests; never a production backend."""
import json
import sys
import threading
import time

writer = threading.Lock()
cancelled = threading.Event()
permission = threading.Event()
permission_value = None


def emit(value):
    with writer:
        print(json.dumps(dict(jsonrpc='2.0', **value)), flush=True)


def prompt(request):
    text = request['params']['prompt'][0]['text']
    if text == 'permission':
        emit(dict(id='approval-1', method='session/request_permission', params=dict(
            sessionId='fixture-session', toolCall=dict(title='Write a test artifact'),
            options=[dict(optionId='yes', kind='allow_once', name='Allow'),
                     dict(optionId='no', kind='reject_once', name='Reject')])))
        if not permission.wait(10):
            emit(dict(id=request['id'], error=dict(code=-1, message='Permission timed out')))
            return
        text = 'approved' if permission_value == 'yes' else 'rejected'
    duration = 2 if text == 'slow' else 0.05
    deadline = time.monotonic() + duration
    while time.monotonic() < deadline:
        if cancelled.wait(0.01):
            emit(dict(id=request['id'], result=dict(stopReason='cancelled')))
            return
    emit(dict(method='session/update', params=dict(sessionId='fixture-session', update=dict(
        sessionUpdate='agent_message_chunk', content=dict(type='text', text='fixture result: ' + text)))))
    emit(dict(id=request['id'], result=dict(stopReason='end_turn')))


for line in sys.stdin:
    request = json.loads(line)
    method = request.get('method')
    if method == 'initialize':
        emit(dict(id=request['id'], result=dict(protocolVersion=1, agentCapabilities={})))
    elif method == 'session/new':
        emit(dict(id=request['id'], result=dict(sessionId='fixture-session')))
    elif method == 'session/prompt':
        threading.Thread(target=prompt, args=(request,), daemon=True).start()
    elif method == 'session/cancel':
        cancelled.set()
    elif request.get('id') == 'approval-1':
        permission_value = request['result']['outcome'].get('optionId')
        permission.set()
