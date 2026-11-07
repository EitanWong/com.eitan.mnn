#!/usr/bin/env python3
"""Compile production task APIs and run them under Docker Mono or Wine Mono.

The standalone adapters only replace Unity host services. This is native task
interop evidence, not a Unity Editor/Player test or a Windows hardware result.
No packages, models, toolchains, or images are downloaded by this runner.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(command, directory, name):
    previous = [directory / (name + '.' + suffix) for suffix in ('command.json', 'stdout', 'stderr', 'status')]
    if any(p.is_file() for p in previous):
        history = directory / 'History' / datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d-%H%M%S-%f')
        history.mkdir(parents=True)
        for p in previous:
            if p.is_file(): shutil.copy2(p, history / p.name)
    (directory / (name + '.command.json')).write_text(json.dumps(command, indent=2) + '\n')
    result = subprocess.run(command, capture_output=True, text=True)
    (directory / (name + '.stdout')).write_text(result.stdout)
    (directory / (name + '.stderr')).write_text(result.stderr)
    (directory / (name + '.status')).write_text(str(result.returncode) + '\n')
    with (directory / 'actions.jsonl').open('a') as output:
        output.write(json.dumps({'at': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                                 'action': name, 'command': command, 'status': result.returncode}) + '\n')
    if result.returncode or name != 'compile':
        print(result.stdout, end='')
        print(result.stderr, end='', file=sys.stderr)
    print(name.upper() + '_STATUS=' + str(result.returncode), flush=True)
    return result.returncode


def under(path, parent):
    try:
        path.relative_to(parent)
        return True
    except ValueError:
        return False


def default_mono_references():
    mono = shutil.which('mono')
    candidates = [Path('/usr/lib/mono/4.5')]
    if mono:
        candidates.insert(0, Path(mono).resolve().parents[1] / 'lib/mono/4.5')
    return next((p for p in candidates if (p / 'mscorlib.dll').is_file()), None)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--unity', type=Path, required=True, help='Installed Unity executable')
    parser.add_argument('--mono-references', type=Path, default=default_mono_references())
    parser.add_argument('--platform', choices=('Linux', 'Windows'), required=True)
    parser.add_argument('--task', choices=('express', 'llm', 'embedding', 'reranker', 'omni'), required=True)
    parser.add_argument('--model', type=Path, help='Existing model directory; required except Express')
    parser.add_argument('--linux-image', default='codex-mnn-linux-gcc13')
    parser.add_argument('--wine-image', default='codex-mnn-wine-mono:ubuntu24.04-wine9')
    parser.add_argument('--wine-prefix', type=Path, help='Existing initialized Wine/Mono prefix')
    parser.add_argument('--compile-only', action='store_true')
    args = parser.parse_args()
    args.package = args.package.resolve()
    args.project = args.project.resolve()
    output = args.output.resolve()
    if not under(output, args.project / 'TestArtifacts~'):
        parser.error('--output must be inside the project TestArtifacts~/ directory')
    if args.model and under(output, args.model.resolve()):
        parser.error('--output must not be inside a model directory')
    if not args.mono_references or not (args.mono_references / 'mscorlib.dll').is_file():
        parser.error('Provide existing Mono framework assemblies with --mono-references')
    if not args.compile_only and args.task != 'express' and not args.model:
        parser.error('--model is required for this task')
    if not args.compile_only and args.platform == 'Windows' and not args.wine_prefix:
        parser.error('--wine-prefix must identify an existing initialized Wine/Mono prefix')
    output.mkdir(parents=True, exist_ok=True)
    unity = args.unity.resolve()
    editor = unity.parent.parent if sys.platform == 'darwin' else unity.parent
    dotnet = editor / 'NetCoreRuntime' / ('dotnet.exe' if os.name == 'nt' else 'dotnet')
    compiler = editor / 'DotNetSdkRoslyn/csc.dll'
    # macOS Unity executable sits in Contents/MacOS; its compiler is in Contents.
    if not dotnet.is_file() or not compiler.is_file():
        parser.error('Could not find the installed Unity Roslyn compiler beside --unity')
    fixtures = Path(__file__).resolve().parent / 'DesktopMonoTasks'
    catalog = json.loads((fixtures / 'sources.json').read_text())
    sources = catalog['sources'] + [name for name in catalog['optional_sources'] if (args.package / name).is_file()]
    source_paths = [args.package / name for name in sources]
    command = [str(dotnet), str(compiler), '-nologo', '-noconfig', '-nostdlib+', '-unsafe+',
               '-langversion:9', '-target:exe', '-define:UNITY_STANDALONE_' +
               ('WIN' if args.platform == 'Windows' else 'LINUX') + ';ENABLE_MONO',
               '-out:' + str(output / 'AdvancedSmoke.exe')]
    command += ['-r:' + str(args.mono_references / (name + '.dll'))
                for name in ('mscorlib', 'System', 'System.Core')]
    command += [str(p) for p in source_paths]
    command += [str(fixtures / name) for name in ('UnityAdapters.cs', 'AdvancedSmoke.cs')]
    native_directory = args.package / 'Runtime/Plugins' / args.platform / 'x86_64'
    native = native_directory / ('MNN.dll' if args.platform == 'Windows' else 'libMNN.so')
    manifest = {'package': str(args.package), 'platform': args.platform, 'task': args.task,
                'sources': {str(p): digest(p) for p in source_paths},
                'harness': {name: digest(fixtures / name) for name in ('AdvancedSmoke.cs', 'UnityAdapters.cs', 'sources.json')},
                'native': {'path': str(native), 'sha256': digest(native)},
                'evidence_scope': 'Production C# and native symbols under host Mono; Unity APIs adapted'}
    (output / 'inputs.json').write_text(json.dumps(manifest, indent=2) + '\n')
    status = run(command, output, 'compile')
    if status or args.compile_only:
        return status
    shutil.copy2(args.package / 'Tests/Fixtures/affine.mnn', output / 'affine.mnn')
    for p in native_directory.glob('*.dll' if args.platform == 'Windows' else '*.so'):
        shutil.copy2(p, output / p.name)
    command = ['docker', 'run', '--rm', '--platform', 'linux/amd64', '-v', str(output) + ':/work', '-w', '/work']
    if args.model:
        command += ['-v', str(args.model.resolve()) + ':/model:ro']
    if args.platform == 'Linux':
        command += ['-e', 'LD_LIBRARY_PATH=/work', args.linux_image, 'mono', '--interpreter',
                    'AdvancedSmoke.exe', args.task, '/model']
    else:
        command += ['-e', 'LANG=C.UTF-8', '-e', 'LC_ALL=C.UTF-8', '-v',
                    str(args.wine_prefix.resolve()) + ':/wineprefix', args.wine_image,
                    r'Z:\work\AdvancedSmoke.exe', args.task, r'Z:\model']
    return run(command, output, args.task)


if __name__ == '__main__':
    raise SystemExit(main())
