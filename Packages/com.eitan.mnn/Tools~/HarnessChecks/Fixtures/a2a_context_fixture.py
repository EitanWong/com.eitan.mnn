#!/usr/bin/env python3
"""Independent A2A peer: observes opaque context and task identity on the HTTP wire."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, unquote
import json
import sys
import threading
import uuid

port, version, binding, streaming = int(sys.argv[1]), sys.argv[2], sys.argv[3], sys.argv[4] == 'stream'
schema = version == '0.3' and binding == 'JSONRPC'
lock = threading.Lock()
release = threading.Event()
finish = threading.Event()
requests, tasks, errors = [], {}, []


def snapshot(identity, complete=False):
    item = tasks[identity]
    state = 'completed' if item['text'] != 'discover' or finish.is_set() else 'working'
    if item['text'] == 'input':
        state = 'input-required' if not item.get('answered') else 'completed' if finish.is_set() else 'working'
    result = dict(id=identity, status=dict(state=state if schema else 'TASK_STATE_' + state.upper()))
    if item['context']:
        result['contextId'] = item['context']
    if schema:
        result['kind'] = 'task'
    result['status']['message'] = dict(messageId='report-' + identity, role='agent' if schema else 'ROLE_AGENT',
                                     parts=[dict(text=item['text'], **({'kind': 'text'} if schema else {}))])
    if version == '0.3' and not schema:
        result['status']['message']['content'] = result['status']['message'].pop('parts')
    return result


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def send(self, value, code=200):
        data = json.dumps(value).encode()
        self.send_response(code)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(data)))
        self.send_header('Connection', 'close')
        self.end_headers()
        self.wfile.write(data)
        self.close_connection = True

    def do_GET(self):
        self.handle_call()

    def do_POST(self):
        self.handle_call()

    def body(self):
        data = b''
        if self.headers.get('Transfer-Encoding') == 'chunked':
            while True:
                size = int(self.rfile.readline().split(b';')[0], 16)
                if not size:
                    self.rfile.readline()
                    break
                data += self.rfile.read(size)
                self.rfile.read(2)
        else:
            data = self.rfile.read(int(self.headers.get('Content-Length', 0)))
        return json.loads(data or b'{}')

    def handle_call(self):
        try:
            path = urlparse(self.path).path
            if path == '/__state':
                with lock:
                    self.send(dict(requests=requests, tasks=tasks, errors=errors))
                return
            if path == '/__release':
                release.set()
                self.send(dict(ok=True))
                return
            if path == '/__finish':
                finish.set()
                self.send(dict(ok=True))
                return
            if path == '/__reset':
                release.clear()
                finish.clear()
                self.send(dict(ok=True))
                return
            if path == '/.well-known/agent-card.json':
                url = 'http://127.0.0.1:' + str(port) + ('/rpc/' if binding == 'JSONRPC' else '')
                card = dict(capabilities=dict(streaming=streaming), name='Context protocol peer')
                if version == '0.3':
                    card.update(protocolVersion='0.3.0', url=url, preferredTransport=binding)
                else:
                    card['supportedInterfaces'] = [dict(url=url, protocolVersion='1.0', protocolBinding=binding)]
                self.send(card)
                return
            assert self.headers.get('A2A-Version') == version
            body = self.body()
            params = body['params'] if binding == 'JSONRPC' else body
            operation = body.get('method', path)
            if operation in ['SendMessage', 'SendStreamingMessage', 'message/send', 'message/stream'] or '/message:' in path:
                message = params['message']
                text = message['content' if version == '0.3' and not schema else 'parts'][0]['text']
                if message.get('taskId'):
                    identity = message['taskId']
                    assert text == 'answer' and tasks[identity]['text'] == 'input'
                    assert message['contextId'] == tasks[identity]['context']
                    tasks[identity]['answered'] = True
                    result = snapshot(identity)
                    if not schema:
                        result = dict(task=result)
                    if binding == 'JSONRPC':
                        result = dict(jsonrpc='2.0', id=body['id'], result=result)
                    self.send(result)
                    return
                context = message.get('contextId') or 'server-' + uuid.uuid4().hex
                if text == 'no-context':
                    context = None
                if text == 'mismatch':
                    context = 'unexpected-' + uuid.uuid4().hex
                identity = 'task-' + uuid.uuid4().hex
                with lock:
                    requests.append(dict(message=message, id=identity, context=context))
                    tasks[identity] = dict(context=context, text=text)
                if text in ['discover', 'discover-fail']:
                    # Hold the first response until the checker observes its concurrent waiter.
                    while not release.wait(.02):
                        if getattr(self.server, 'stopping', False):
                            return
                    if text == 'discover-fail':
                        self.close_connection = True
                        self.connection.shutdown(2)
                        return
                if text == 'message' or text == 'no-context':
                    reply = dict(messageId=identity, role='agent' if schema else 'ROLE_AGENT',
                                 **({'kind': 'message'} if schema else {}))
                    reply['content' if version == '0.3' and not schema else 'parts'] = [dict(text=text)]
                    if context:
                        reply['contextId'] = context
                    result = reply if schema else dict(message=reply)
                else:
                    result = snapshot(identity)
                    if not schema:
                        result = dict(task=result)
            else:
                identity = params['id'] if binding == 'JSONRPC' else unquote(path.split('/tasks/')[1].split(':')[0])
                result = snapshot(identity, complete=True)
            if binding == 'JSONRPC':
                result = dict(jsonrpc='2.0', id=body['id'], result=result)
            if streaming and ('stream' in path.lower() or 'subscribe' in path or
                              operation in ['SendStreamingMessage', 'message/stream', 'SubscribeToTask', 'tasks/resubscribe']):
                data = ('data: ' + json.dumps(result) + '\n\n').encode()
                self.send_response(200)
                self.send_header('Content-Type', 'text/event-stream')
                self.send_header('Content-Length', str(len(data)))
                self.end_headers()
                self.wfile.write(data)
                return
            self.send(result)
        except (BrokenPipeError, ConnectionResetError):
            pass
        except Exception as error:
            with lock:
                errors.append(repr(error))
            self.send(dict(error=repr(error)), 400)


ThreadingHTTPServer(('127.0.0.1', port), Handler).serve_forever()
