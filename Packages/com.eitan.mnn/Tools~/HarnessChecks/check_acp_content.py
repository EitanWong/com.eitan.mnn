#!/usr/bin/env python3
"""Exercise negotiated ACP prompt content through a real external C# host and peer."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse, base64, hashlib, json, os, socket, subprocess, sys, time, urllib.error, urllib.request, uuid
from pathlib import Path

SAMPLE = PACKAGE_ROOT
parser = argparse.ArgumentParser()
parser.add_argument('--host', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
OUT = args.output.resolve(); OUT.mkdir(parents=True, exist_ok=True)
RUN = OUT / ('run-' + uuid.uuid4().hex); RUN.mkdir()
PEER = RUN / 'peer'; PEER.mkdir()
TOKEN = uuid.uuid4().hex * 2
ENV = dict(os.environ, SIRIAI_ACP_CONTENT_TOKEN=TOKEN, SIRIAI_ACP_SESSION_ROOT=str(PEER))
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0)); PORT = sock.getsockname()[1]
URL = 'http://127.0.0.1:' + str(PORT)
CONFIG = RUN / 'host.json'; REG = RUN / 'registration'
CONFIG.write_text(json.dumps(dict(Listen=URL, DataDirectory=str(RUN / 'data'),
    Identities=[dict(OwnerId='alice', TokenEnvironmentVariable='SIRIAI_ACP_CONTENT_TOKEN')],
    Workspaces=dict(test=str(RUN)), Harnesses=[dict(Id='acp', Protocol='acp', Command=sys.executable,
        Arguments=['-u', str(SAMPLE / 'Tools~/HarnessChecks/Fixtures/acp_session_fixture.py'), 'load'],
        ForwardEnvironment=['SIRIAI_ACP_SESSION_ROOT'], TimeoutMilliseconds=10000)])))
process = None; log = None; passed = []; records = []

def ok(text):
    passed.append(text); print('PASS ' + text, flush=True)

def wait(test):
    end = time.monotonic() + 15
    while time.monotonic() < end:
        value = test()
        if value: return value
        time.sleep(.03)
    raise AssertionError('Timed out observing native ACP content')

def call(path, body=None, expected=200):
    req = urllib.request.Request(URL + path, None if body is None else json.dumps(body).encode(),
        headers={'Authorization': 'Bearer ' + TOKEN, 'Content-Type': 'application/json'})
    try:
        with urllib.request.urlopen(req, timeout=15) as response:
            code, raw = response.status, response.read()
    except urllib.error.HTTPError as error: code, raw = error.code, error.read()
    assert code == expected, (path, code, raw)
    assert TOKEN.encode() not in raw
    return json.loads(raw) if raw else None

def start(capabilities):
    global process, log
    (PEER / 'capabilities.json').write_text(json.dumps(capabilities))
    log = (RUN / ('host-' + uuid.uuid4().hex + '.log')).open('w')
    process = subprocess.Popen(['dotnet', str(args.host), '--service', 'run', '--directory', str(REG)],
        stdout=log, stderr=log, env=os.environ)
    def ready():
        if process.poll() is not None: raise AssertionError('Host exited ' + str(process.returncode))
        try: return call('/v1/harnesses')
        except urllib.error.URLError: return None
    return wait(ready)

def stop():
    global process
    if process is not None:
        process.terminate()
        try: process.wait(timeout=10)
        except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=5)
        process = None; log.close()

def wire():
    p = PEER / 'wire.jsonl'
    return [json.loads(line) for line in p.read_text().splitlines()] if p.exists() else []

def prompts(): return [f['frame']['params'] for f in wire() if f['direction'] == 'client' and f['frame'].get('method') == 'session/prompt']
def submit(parts, conversation='content', key=None, expected=202, objective='inspect-content'):
    return call('/v1/tasks', dict(HarnessId='acp', WorkspaceKey='test', ConversationId=conversation,
        SubmissionKey=key or uuid.uuid4().hex, Objective=objective, Attachments=parts), expected)
def terminal(task):
    result = call('/v1/tasks/' + task['Id'])
    return result if result['State'] in [3, 4, 5, 6] else None
def part(kind, payload, media=None, name=None):
    return dict(Kind=kind, Payload=payload, MediaType=media, FileName=name)
def b64(data): return base64.b64encode(data).decode()

try:
    cmd = ['dotnet', str(args.host), '--service', 'prepare', '--id', 'acp-content', '--config', str(CONFIG),
        '--runtime', str(Path('/opt/homebrew/bin/dotnet').resolve()), '--directory', str(REG)]
    result = subprocess.run(cmd, env=ENV, text=True, capture_output=True, timeout=40)
    records.append(dict(command=cmd, stdout=result.stdout, stderr=result.stderr, exit_code=result.returncode))
    assert result.returncode == 0, records[-1]
    catalog = start(dict(image=True, audio=True, embeddedContext=True))
    parts = [part(0, '你好\ncontext', 'text/plain', '说明.txt'),
        part(2, '{"answer":42}', None, 'data.json'),
        part(3, b64(b'image-bytes'), 'image/png', 'one.png'),
        part(3, b64(b'audio-bytes'), 'audio/wav', 'one.wav'),
        part(3, b64('编码文本'.encode()), 'text/plain', 'utf8.txt'),
        part(3, b64(bytes([0, 255, 16])), 'application/octet-stream', 'bytes.bin'),
        part(1, 'https://example.invalid/remote.bin', 'application/octet-stream', 'remote.bin')]
    key = uuid.uuid4().hex; item = submit(parts, key=key)
    assert submit(parts, key=key)['Id'] == item['Id']
    value = wait(lambda: terminal(item)); assert value['State'] == 3, value
    received = prompts()[-1]['prompt']; assert received[0] == dict(type='text', text='inspect-content')
    expected = [
        dict(type='resource', resource=dict(uri='siriai://input/1/%E8%AF%B4%E6%98%8E.txt', mimeType='text/plain', text='你好\ncontext')),
        dict(type='resource', resource=dict(uri='siriai://input/2/data.json', mimeType='application/json', text='{"answer":42}')),
        dict(type='image', data=b64(b'image-bytes'), mimeType='image/png', uri='siriai://input/3/one.png'),
        dict(type='audio', data=b64(b'audio-bytes'), mimeType='audio/wav'),
        dict(type='resource', resource=dict(uri='siriai://input/5/utf8.txt', mimeType='text/plain', text='编码文本')),
        dict(type='resource', resource=dict(uri='siriai://input/6/bytes.bin', mimeType='application/octet-stream', blob=b64(bytes([0, 255, 16])))),
        dict(type='resource_link', uri='https://example.invalid/remote.bin', name='remote.bin', mimeType='application/octet-stream')]
    assert received[1:] == expected, received
    digest = hashlib.sha256(json.dumps(expected, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    assert value['Result'] == 'native content: ' + digest and len(prompts()) == 1
    assert catalog['configured'][0]['capabilities'] & 16
    ok('all seven typed attachments reach the native prompt exactly, including Unicode, image/audio bytes, embedded text/blob and a reference without downloading it')
    changed = [dict(parts[0], Payload='changed')]; submit(changed, key=key, expected=400)
    assert len(prompts()) == 1
    ok('attachment identity participates in durable idempotency and changed payload cannot reuse a submission key')
    before = len(call('/v1/tasks'))
    submit([part(3, b64(bytes([255])), 'text/plain', 'invalid.txt')], expected=400)
    submit([part(0, '<' * 200000, 'text/plain', 'escaped.txt')], expected=422)
    assert len(call('/v1/tasks')) == before and len(prompts()) == 1
    ok('invalid UTF8 text and escaped wire overflow fail before admission or native execution')
    stop()
    for name, cap, attachment in [('image', dict(audio=True, embeddedContext=True), parts[2]),
            ('audio', dict(image=True, embeddedContext=True), parts[3]),
            ('embeddedContext', dict(image=True, audio=True), parts[0]),
            ('nonboolean', dict(image='true', audio=True, embeddedContext=True), parts[2])]:
        start(cap); count = len(prompts()); item = submit([attachment], conversation=name)
        value = wait(lambda: terminal(item)); assert value['State'] == 4, value
        assert len(prompts()) == count and 'capabilit' in value['Result'].lower(), value
        text = submit([], conversation=name, objective='text-followup'); assert wait(lambda: terminal(text))['State'] == 3
        stop()
    ok('missing or nonboolean negotiated capabilities reject attachments before prompt and leave the confirmed session usable for a text follow-up')
    start({}); count = len(prompts()); link = submit([parts[-1]], conversation='baseline-link')
    assert wait(lambda: terminal(link))['State'] == 3 and prompts()[-1]['prompt'][1] == expected[-1]
    ok('resource references use baseline ACP capability without claiming image/audio/embedded support')
    stop(); start(dict(image=True, audio=True, embeddedContext=True))
    item = submit(parts); value = wait(lambda: terminal(item)); assert value['State'] == 3 and value['Result'] == 'native content: ' + digest
    assert any(x['frame'].get('method') == 'session/load' for x in wire())
    assert call('/v1/tasks/' + link['Id'])['Attachments'] == [parts[-1]]
    ok('host restart loads native context and retains exact durable attachment payloads without replaying old input')
    assert not (PEER / 'errors.txt').exists()
finally:
    transcript = wire(); stop()
    (OUT / 'results.json').write_text(json.dumps(passed, indent=2) + '\n')
    (OUT / 'wire.json').write_text(json.dumps(transcript, indent=2) + '\n')
    (OUT / 'commands.json').write_text(json.dumps(records, indent=2) + '\n')
