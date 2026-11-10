#!/usr/bin/env python3
"""Real package Host TLS plus the production C# remote client, without changing system trust."""
import argparse
import json
import os
from pathlib import Path
import shutil
import socket
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.request
import uuid
from xml.sax.saxutils import escape


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--dotnet', required=True, type=Path)
    parser.add_argument('--openssl', default='openssl')
    parser.add_argument('--dotcraft', action='store_true', help='Verify the .NET10 DotCraft Host using the same production entry point.')
    args = parser.parse_args()
    output = args.output.resolve()
    if 'TestArtifacts~' not in output.parts:
        parser.error('Outputs must remain under TestArtifacts~/.')
    output.mkdir(parents=True, exist_ok=True)
    run = output / ('run-' + uuid.uuid4().hex)
    run.mkdir()
    package = run / 'Packages/com.eitan.mnn'
    source = args.source_root.resolve() / 'Packages/com.eitan.mnn'
    relatives = ['Runtime/Core/Language/Agent/Harnesses', 'Tools~/HarnessCore', 'Tools~/HarnessHost']
    if args.dotcraft:
        relatives.append('Tools~/DotCraftHost')
    for relative in relatives:
        shutil.copytree(source / relative, package / relative)
    fixture_root = Path(__file__).resolve().parent / 'Fixtures'
    token = uuid.uuid4().hex * 2
    password = uuid.uuid4().hex
    env = dict(os.environ, MNN_HTTPS_TEST_TOKEN=token, MNN_HTTPS_PFX_PASSWORD=password,
               DOTNET_CLI_HOME=str(output / 'cli'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
               DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    records, passed = [], []
    host = log = None

    def command(values, expected=0):
        cmd = [str(value) for value in values]
        p = subprocess.run(cmd, env=env, cwd=run, text=True, capture_output=True, timeout=120)
        record = dict(command=cmd, stdout=p.stdout, stderr=p.stderr, exit_code=p.returncode)
        records.append(record)
        assert token not in p.stdout + p.stderr and password not in p.stdout + p.stderr
        if p.returncode != expected:
            raise AssertionError('Command failed: ' + json.dumps(record))
        return p.stdout

    def ok(message):
        passed.append(message)
        print('PASS ' + message, flush=True)

    def stop():
        nonlocal host, log
        if host is not None:
            host.terminate()
            try:
                host.wait(timeout=10)
            except subprocess.TimeoutExpired:
                host.kill()
                host.wait(timeout=5)
            host = None
            log.close()
            log = None

    try:
        # Test CA and DNS identity stay within this run; no keychain, root store or permission changes.
        (run / 'root.cnf').write_text('[req]\ndistinguished_name=dn\nx509_extensions=ca\nprompt=no\n'
            '[dn]\nCN=SiriAI HTTPS Test CA\n[ca]\nbasicConstraints=critical,CA:TRUE\n'
            'keyUsage=critical,keyCertSign,cRLSign\n')
        (run / 'server.cnf').write_text('[req]\ndistinguished_name=dn\nprompt=no\n'
            '[dn]\nCN=localhost\n[server]\nbasicConstraints=critical,CA:FALSE\n'
            'keyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\n'
            'subjectAltName=DNS:localhost\n')
        for name in ('root', 'wrong-root'):
            command([args.openssl, 'req', '-x509', '-newkey', 'rsa:2048', '-nodes', '-days', '2',
                     '-keyout', run / (name + '.key'), '-out', run / (name + '.pem'), '-config', run / 'root.cnf'])
        command([args.openssl, 'req', '-newkey', 'rsa:2048', '-nodes', '-keyout', run / 'server.key',
                 '-out', run / 'server.csr', '-config', run / 'server.cnf'])
        command([args.openssl, 'x509', '-req', '-in', run / 'server.csr', '-CA', run / 'root.pem',
                 '-CAkey', run / 'root.key', '-CAcreateserial', '-days', '2', '-out', run / 'server.pem',
                 '-extfile', run / 'server.cnf', '-extensions', 'server'])
        command([args.openssl, 'pkcs12', '-export', '-out', run / 'server.pfx', '-inkey', run / 'server.key',
                 '-in', run / 'server.pem', '-certfile', run / 'root.pem', '-passout', 'env:MNN_HTTPS_PFX_PASSWORD'])
        for path in [*run.glob('*.key'), run / 'server.pfx']:
            path.chmod(0o600)
        host_name = 'DotCraftHost' if args.dotcraft else 'HarnessHost'
        assembly = 'SiriAI.DotCraftHost' if args.dotcraft else 'SiriAI.HarnessHost'
        framework = 'net10.0' if args.dotcraft else 'net8.0'
        command([args.dotnet, 'build', package / ('Tools~/' + host_name + '/' + assembly + '.csproj'),
                 '--nologo', '-p:HarnessArtifactsRoot=' + str(run / 'HostBuild') + '/'])
        consumer = run / 'Consumer'
        consumer.mkdir()
        shutil.copyfile(fixture_root / 'HttpsConsumer/Program.cs', consumer / 'Program.cs')
        project = consumer / 'Consumer.csproj'
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>' + framework + '</TargetFramework><ImplicitUsings>enable</ImplicitUsings>'
            '<RestorePackagesPath>' + escape(str(run / 'packages')) + '</RestorePackagesPath></PropertyGroup>'
            '<ItemGroup><ProjectReference Include="' + escape(str(package / 'Tools~/HarnessCore/MNN.HarnessCore.csproj'))
            + '" /></ItemGroup></Project>\n')
        command([args.dotnet, 'build', project, '--nologo', '-p:HarnessArtifactsRoot=' + str(run / 'ConsumerBuild') + '/'])
        fixture = run / 'independent-acp.py'
        shutil.copyfile(fixture_root / 'acp_fixture.py', fixture)
        workspace = run / 'workspace'
        workspace.mkdir()
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0))
            port = sock.getsockname()[1]
        url = 'https://localhost:' + str(port)
        config = run / 'host.json'
        config.write_text(json.dumps(dict(Listen='https://127.0.0.1:' + str(port), DataDirectory=str(run / 'data'),
            Identities=[dict(OwnerId='https-owner', TokenEnvironmentVariable='MNN_HTTPS_TEST_TOKEN')],
            Workspaces=dict(test=str(workspace)), Harnesses=[dict(Id='acp', Protocol='acp', Command=sys.executable,
            Arguments=['-u', str(fixture)])])))
        host_env = dict(env, ASPNETCORE_Kestrel__Certificates__Default__Path=str(run / 'server.pfx'),
                        ASPNETCORE_Kestrel__Certificates__Default__Password=password)
        context = ssl.create_default_context(cafile=str(run / 'root.pem'))

        def start():
            nonlocal host, log
            path = run / ('host-' + uuid.uuid4().hex + '.log')
            log = path.open('w')
            cmd = [str(args.dotnet), str(run / ('HostBuild/bin/Debug/' + framework + '/' + assembly + '.dll')), '--config', str(config)]
            host = subprocess.Popen(cmd, env=host_env, cwd=run, stdout=log, stderr=log)
            print('HOST', host.pid, flush=True)
            deadline = time.monotonic() + 15
            while time.monotonic() < deadline:
                if host.poll() is not None:
                    log.flush()
                    records.append(dict(command=cmd, exit_code=host.returncode, stdout=path.read_text(), stderr=''))
                    raise AssertionError('HTTPS Host exited before readiness: ' + str(host.returncode))
                try:
                    request = urllib.request.Request(url + '/v1/health', headers={'Authorization': 'Bearer ' + token})
                    with urllib.request.urlopen(request, context=context, timeout=2) as response:
                        health = json.load(response)
                    records.append(dict(phase='https-readiness', endpoint=url, health=health))
                    return
                except urllib.error.URLError:
                    time.sleep(.05)
            raise AssertionError('HTTPS Host did not become ready.')

        def front(mode, last=None):
            cmd = [args.dotnet, consumer / ('bin/Debug/' + framework + '/Consumer.dll'), mode, url, run / 'root.pem']
            if last is not None:
                cmd.append(last)
            return command(cmd)

        start()
        ok('package-only Host starts native HTTPS with an explicitly configured certificate')
        boundaries = front('boundaries', run / 'wrong-root.pem')
        assert boundaries.count('PASS ') == 6, boundaries
        ok('production C# client validates TLS/authentication, preserves HTTP errors and durable admission semantics')
        submitted = json.loads(front('submit'))
        foreground_exit = time.time_ns()
        # The submitting C# process and its HttpClient are gone throughout backend work.
        time.sleep(2.5)
        assert host.poll() is None
        stop()
        start()
        assert 'PASS HTTPS reconnect' in front('present', submitted['Id'])
        ok('backend completes after C# foreground process exit; restart delivers exact result over TLS')
        stop()
        start()
        assert 'PASS HTTPS Host restart' in front('empty', submitted['Id'])
        ok('second Host restart preserves the text receipt and does not redeliver it')
        records.append(dict(foreground_exit_ns=foreground_exit, task_id=submitted['Id'],
                            physical_playback_tested=False, actual_vendor_tested=False))
        return 0
    finally:
        stop()
        (output / 'commands.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n')
        (output / 'results.json').write_text(json.dumps(dict(run=str(run), passed=passed,
            package_only=True, system_trust_changed=False, physical_playback_tested=False,
            mobile_device_tested=False, actual_vendor_tested=False, dotcraft_host=args.dotcraft), indent=2) + '\n')


if __name__ == '__main__':
    raise SystemExit(main())
