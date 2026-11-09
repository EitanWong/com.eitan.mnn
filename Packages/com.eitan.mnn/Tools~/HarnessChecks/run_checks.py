#!/usr/bin/env python3
"""Run the same NUnit behavior cases outside Unity using installed Mono; no model/network required."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse
import json
import os
import shutil
import subprocess
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--package', type=Path, default=PACKAGE_ROOT)
    parser.add_argument('--sample', type=Path, help='Legacy source sample location; resolves its sibling package.')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--test-inputs', type=Path, help='Keep identical test inputs when checking a baseline or rollback copy.')
    options = parser.parse_args()
    package = (options.sample.resolve().parents[2] / 'Packages/com.eitan.mnn') if options.sample else options.package.resolve()
    inputs = (options.test_inputs or package).resolve()
    root = PROJECT_ROOT
    output = (options.output or root / 'TestArtifacts~/SiriAIHarness/Checks').resolve()
    output.mkdir(parents=True, exist_ok=True)
    nunit = root / 'Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom/nunit.framework.dll'
    test_package = inputs if (inputs / 'Tests/Editor/Language/Agent/Harnesses').is_dir() else inputs.parents[2] / 'Packages/com.eitan.mnn'
    core = sorted((package / 'Runtime/Core/Language/Agent/Harnesses').glob('*.cs'))
    sources = sorted((test_package / 'Tests/Editor/Language/Agent/Harnesses').rglob('*.cs'))
    sources += [Path(__file__).parent / 'Program.cs']
    executable = output / 'HarnessChecks.exe'
    library = output / 'MNN.Samples.SiriAI.Harnesses.dll'
    commands = [['mcs', '-langversion:7.2', '-target:library', '-r:System.Runtime.Serialization',
                 '-r:System.Net.Http', '-out:' + str(library), *map(str, core)],
                ['mcs', '-langversion:7.2', '-r:System.Runtime.Serialization', '-r:System.Net.Http',
                 '-r:' + str(library), '-r:' + str(nunit), '-out:' + str(executable), *map(str, sources)]]
    records = []
    for command in commands:
        result = subprocess.run(command, text=True, capture_output=True, timeout=60, cwd=output)
        records.append(dict(command=command, stdout=result.stdout, stderr=result.stderr, exit_code=result.returncode))
        print(result.stdout + result.stderr, end='')
        if result.returncode:
            break
    else:
        shutil.copy2(nunit, output / nunit.name)
        command = ['mono', str(executable)]
        environment = dict(os.environ, SIRIAI_HARNESS_TEST_ARTIFACTS=str(output))
        result = subprocess.run(command, text=True, capture_output=True, timeout=30, cwd=output, env=environment)
        records.append(dict(command=command, stdout=result.stdout, stderr=result.stderr, exit_code=result.returncode))
        print(result.stdout + result.stderr, end='')
    (output / 'results.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n')
    return records[-1]['exit_code']


if __name__ == '__main__':
    raise SystemExit(main())
