#!/usr/bin/env python3
"""Build a package-only consumer and verify durable results across actual process exits."""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import time
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', required=True, type=Path)
    parser.add_argument('--dotnet', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    output = args.output.resolve()
    if 'TestArtifacts~' not in output.parts:
        parser.error('Outputs must remain under TestArtifacts~/.')
    output.mkdir(parents=True, exist_ok=True)
    run = output / ('run-' + uuid.uuid4().hex)
    run.mkdir()
    package = args.source_root.resolve() / 'Packages/com.eitan.mnn'
    isolated = run / 'Packages/com.eitan.mnn'
    for relative in ['Runtime/Core/Language/Agent/Harnesses', 'Tools~/HarnessCore']:
        source = package / relative
        if source.exists():
            shutil.copytree(source, isolated / relative)
    # Identical fixture input is supplied by this checker, including when checking an old source copy.
    fixture = Path(__file__).parent / 'Fixtures/PackageConsumer'
    consumer = run / 'Consumer'
    shutil.copytree(fixture, consumer)
    assert not (run / 'Assets').exists()
    environment = dict(os.environ, DOTNET_CLI_HOME=str(output / 'cli'),
                       DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_GENERATE_ASPNET_CERTIFICATE='false')
    build = [str(args.dotnet), 'build', str(consumer / 'Consumer.csproj'), '--nologo', '--no-incremental',
             '-p:HarnessPackageRoot=' + str(isolated), '-p:HarnessCoreArtifactsRoot=' + str(run / 'CoreBuild') + '/']
    commands = [build] + [[str(args.dotnet), str(consumer / 'bin/Debug/net10.0/Consumer.dll'), phase, str(run / 'data')]
                          for phase in ['run', 'recover', 'verify']]
    records = []
    for command in commands:
        started = time.monotonic()
        result = subprocess.run(command, env=environment, cwd=run, text=True, capture_output=True, timeout=120)
        record = dict(command=command, stdout=result.stdout, stderr=result.stderr, exit_code=result.returncode,
                      elapsed_seconds=time.monotonic() - started)
        records.append(record)
        (output / 'commands.json').write_text(json.dumps(records, indent=2) + '\n')
        print(result.stdout + result.stderr, end='', flush=True)
        if result.returncode:
            return result.returncode
    assert sum(record['stdout'].count('PASS ') for record in records) == 5
    (output / 'results.json').write_text(json.dumps(dict(run=str(run), source_root=str(args.source_root),
        package_only=True, assets_directory_exists=False, passed=5, commands=records), indent=2) + '\n')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
