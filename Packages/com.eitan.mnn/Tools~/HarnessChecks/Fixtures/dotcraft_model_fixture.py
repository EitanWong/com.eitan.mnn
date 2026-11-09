#!/usr/bin/env python3
"""Independent OpenAI Chat Completions peer for the real DotCraft runtime."""
import json, os, sys, threading, time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

calls = []
lock = threading.Lock()
key = os.environ['SIRIAI_DOTCRAFT_MODEL_KEY']

class Peer(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_GET(self):
        raw = json.dumps(dict(calls=calls)).encode()
        self.send_response(200); self.end_headers(); self.wfile.write(raw)

    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        assert self.path == '/v1/chat/completions', self.path
        assert self.headers.get('Authorization') == 'Bearer ' + key
        with lock:
            calls.append(dict(body=body, authorized=True))
        last_user = max(i for i,m in enumerate(body['messages']) if m['role'] == 'user')
        content = body['messages'][last_user]['content']
        objective = content if isinstance(content, str) else '\n'.join(p.get('text', '') for p in content)
        objective = objective.split('<system-reminder>', 1)[0].strip()
        if objective == 'provider-error':
            self.send_response(400); self.send_header('Content-Type','application/json'); self.end_headers()
            self.wfile.write(json.dumps(dict(error=dict(message='fixture rejected request',type='invalid_request_error'))).encode())
            return
        if 'hold' in objective and not objective.startswith('mcp-'):
            time.sleep(9)
        elif 'slow' in objective:
            time.sleep(1)
        delta = dict(role='assistant', content='DotCraft fixture answer: ' + objective)
        if objective=='provider-overflow': delta['content']='x'*65537
        finish = 'stop'
        if objective.startswith('plugin-'):
            results=[m for m in body['messages'][last_user+1:] if m['role']=='tool']
            if results:delta['content']=str(results[-1]['content'])
            else:
                name='Mutate' if 'mutate' in objective else 'Hold' if 'hold' in objective else 'Echo'
                tool=next((t['function'] for t in body.get('tools',[]) if 'SiriAIPlugin' in t['function']['name'] and t['function']['name'].endswith(name)),None)
                if tool is None:delta['content']='PLUGIN_TOOL_MISSING '+name
                else:
                    delta=dict(role='assistant',tool_calls=[dict(index=0,id='plugin-fixture-1',type='function',function=dict(name=tool['name'],arguments=json.dumps(dict(text=objective))))]);finish='tool_calls'
        if objective.startswith('code-'):
            results=[m for m in body['messages'][last_user+1:] if m['role']=='tool']
            if results:
                pending='Script running with cell ID '
                result=str(results[-1]['content'])
                if pending in result and 'CodeModeWait' in {t.get('function',{}).get('name') for t in body.get('tools',[])}:
                    import re
                    cell=re.search(r'Script running with cell ID (cell_[0-9a-f]+)',result).group(1)
                    delta=dict(role='assistant',tool_calls=[dict(index=0,id='code-wait-'+str(len(results)),type='function',function=dict(name='CodeModeWait',arguments=json.dumps(dict(cell_id=cell,yield_time_ms=1000))))]);finish='tool_calls'
                else:delta['content']='Actual CodeMode result: '+result
            else:
                tool=next((t['function'] for t in body.get('tools',[]) if t.get('function',{}).get('name')=='CodeMode'),None)
                if tool is None:delta['content']='CODEMODE_MISSING'
                else:
                    source='const values=await Promise.all([tools.ReadFile({path:"proof.txt"}),tools.ReadFile({path:"proof.txt"})]);text(values);'
                    if objective=='code-store':source='store("fixture", "SIRIAI_CODEMODE_STORE_OK");text(load("fixture"));'
                    elif objective=='code-load':source='text(load("fixture") ?? "SIRIAI_CODEMODE_STORE_EMPTY");'
                    elif objective=='code-sandbox':source='text({process:typeof process,require:typeof require,fetch:typeof fetch,console:typeof console});'
                    elif objective=='code-invalid':source='await tools.ThisToolDoesNotExist({});'
                    elif objective=='code-deadline':source='// @exec: {"timeout_ms": 100}\nwhile(true){}'
                    elif objective.startswith('code-permission'):
                        arguments=json.dumps(dict(path=os.path.join(os.environ['SIRIAI_DOTCRAFT_WRITE_ROOT'],objective+'.txt'),content='SIRIAI_CODEMODE_APPROVED_WRITE_OK'))
                        source='try { text(await tools.WriteFile('+arguments+')); } catch(e) { text("NESTED_WRITE_REJECTED:"+e.message); }'
                    elif objective=='code-loop':source='// @exec: {"yield_time_ms": 0, "timeout_ms": 60000}\ntext("SIRIAI_CODEMODE_LOOP_STARTED");const t=ALL_TOOLS.find(t=>t.name.includes("proofhttp") && t.name.endsWith("__hold"));await tools[t.name]({text:"code-loop"});store("aborted","MUST_NOT_COMMIT");'
                    elif objective=='code-mcp':source='const t=ALL_TOOLS.find(t=>t.name.includes("proofstdio") && t.name.endsWith("__echo"));text(await tools[t.name]({text:"code-mcp"}));'
                    elif objective=='code-after-cancel':source='text(load("aborted") ?? "NO_ABORTED_STORE");text(await tools.ReadFile({path:"proof.txt"}));'

                    delta=dict(role='assistant',tool_calls=[dict(index=0,id='code-fixture-1',type='function',function=dict(name='CodeMode',arguments=json.dumps(dict(code=source))))]);finish='tool_calls'
        if objective.startswith('mcp-'):
            results=[m for m in body['messages'][last_user+1:] if m['role']=='tool']
            if results:delta['content']='Actual DotCraft MCP tool returned: '+str(results[-1]['content'])
            else:
                server='proofhttp' if objective.startswith('mcp-http-') else 'proofstdio'
                name='hold' if '-hold-' in objective else 'mutate' if '-mutate-' in objective else 'echo'
                tool=next((t['function'] for t in body.get('tools',[]) if server in t['function']['name'] and t['function']['name'].endswith('__'+name)),None)
                if tool is None:delta['content']='MCP_TOOL_MISSING '+server+' '+name
                else:
                    delta=dict(role='assistant',tool_calls=[dict(index=0,id='mcp-fixture-1',type='function',function=dict(name=tool['name'],arguments=json.dumps(dict(text=objective))))]);finish='tool_calls'
        if not objective.startswith(('code-','plugin-')) and any(word in objective for word in ['read-proof', 'permission', 'ask-input']):
            results = [m for m in body['messages'][last_user+1:] if m['role'] == 'tool']
            if results:
                delta['content'] = 'Actual DotCraft tool returned: ' + str(results[-1]['content'])
            else:
                name = 'WriteFile' if 'permission' in objective else 'RequestUserInput' if 'ask-input' in objective else 'ReadFile'
                tool = next(t['function'] for t in body['tools'] if t['function']['name'] == name)
                properties = tool['parameters']['properties']
                if name == 'RequestUserInput':
                    arguments = dict(questions=[dict(id='destination', header='Output', question='Where should the result go?', options=[dict(label='Reports',description='Report folder'),dict(label='Archive',description='Archive folder')])])
                    if objective=='ask-input-three':
                        arguments['questions'].extend([
                            dict(id=' format ', header=' Format ', question='Which format?', isOther=False, options=[dict(label='Markdown',description='Readable text'),dict(label='JSON',description='Structured data')]),
                            dict(id='private-note',header='Private',question='Enter a private note.',isSecret=True,options=[dict(label='No note',description='Skip the note'),dict(label='Team note',description='Shared note')])])
                else:
                    path_key = next(k for k in properties if 'path' in k.lower())
                    arguments = {path_key: os.path.join(os.environ['SIRIAI_DOTCRAFT_WRITE_ROOT'], objective+'.txt') if name=='WriteFile' else 'proof.txt'}
                    if name=='WriteFile': arguments['content']='SIRIAI_APPROVED_WRITE_OK'
                delta = dict(role='assistant', tool_calls=[dict(index=0, id='fixture-tool-1', type='function', function=dict(name=name, arguments=json.dumps(arguments)))])
                finish = 'tool_calls'
        chunk = dict(id='fixture-completion', object='chat.completion.chunk', created=1, model=body['model'], choices=[dict(index=0, delta=delta, finish_reason=None)])
        end = dict(id='fixture-completion', object='chat.completion.chunk', created=1, model=body['model'], choices=[dict(index=0, delta={}, finish_reason=finish)])
        self.send_response(200); self.send_header('Content-Type', 'text/event-stream'); self.end_headers()
        try:
            for value in [chunk, end]:
                self.wfile.write(('data: ' + json.dumps(value) + '\n\n').encode()); self.wfile.flush()
            self.wfile.write(b'data: [DONE]\n\n'); self.wfile.flush()
        except (BrokenPipeError, ConnectionResetError):
            pass

ThreadingHTTPServer(('127.0.0.1', int(sys.argv[1])), Peer).serve_forever()
