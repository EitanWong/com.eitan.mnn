#!/usr/bin/env python3
"""Managed foreground/task checks using installed Unity compiler references; never loads a model."""
from harness_paths import PACKAGE_ROOT, PROJECT_ROOT
import argparse
import json
import os
import subprocess
from pathlib import Path


def main():
    root = PROJECT_ROOT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=root)
    parser.add_argument('--output', type=Path, default=root / 'TestArtifacts~/SiriAIHarness/Foreground/Checks')
    parser.add_argument('--unity', type=Path, default=Path('/Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity'))
    args = parser.parse_args()
    source = args.source_root.resolve()
    output = args.output.resolve()
    if (root / 'TestArtifacts~').resolve() not in output.parents:
        parser.error('Outputs must stay under this project TestArtifacts~/.')
    output.mkdir(parents=True, exist_ok=True)
    contents = args.unity.resolve().parents[1]
    framework = contents / 'NetStandard/ref/2.1.0'
    refs = sorted(framework.glob('*.dll')) + sorted((contents / 'NetStandard/compat/2.1.0/shims/netfx').glob('*.dll'))
    refs += sorted((contents / 'Managed/UnityEngine').glob('UnityEngine*.dll'))
    prefix = [str(contents / 'NetCoreRuntime/dotnet'), str(contents / 'DotNetSdkRoslyn/csc.dll'),
              '-nologo', '-noconfig', '-nostdlib+', '-langversion:9', '-unsafe+', '-warn:0']
    prefix += ['-r:' + str(p) for p in dict.fromkeys(refs)]
    package = source / 'Packages/com.eitan.mnn'
    core_folder = package / 'Runtime/Core/Language/Agent/Harnesses'
    runtime = [p for p in (package / 'Runtime').rglob('*.cs') if 'Burst' not in p.parts and not p.is_relative_to(core_folder)]
    core = sorted(core_folder.glob('*.cs'))
    nunit = root / 'Library/PackageCache/com.unity.ext.nunit@1.0.6/net35/unity-custom/nunit.framework.dll'
    harness_tests = root / 'Packages/com.eitan.mnn/Tests/Editor/Language/Agent/Harnesses'
    tests = [root / 'Packages/com.eitan.mnn/Tests/Editor/Language/Realtime/ConversationEventTests.cs',
             root / 'Packages/com.eitan.mnn/Tests/Editor/Language/Agent/AgentReuseTests.cs',
             root / 'Packages/com.eitan.mnn/Tests/Editor/Language/Agent/AgentLifetimeTests.cs',
             harness_tests / 'SiriAISubmissionJournalTests.cs',
             harness_tests / 'Fixtures/ControlledHarness.cs',
             harness_tests / 'Fixtures/HarnessTestCase.cs',
             Path(__file__).parent / 'ForegroundProgram.cs']
    commands = [prefix + ['-target:library', '-out:' + str(output / 'MNN.Samples.SiriAI.Harnesses.dll')] + list(map(str, core)),
                prefix + ['-target:library', '-out:' + str(output / 'MNN.Unity.dll')] + list(map(str, sorted(runtime))),
                prefix + ['-target:exe', '-r:' + str(nunit), '-r:' + str(output / 'MNN.Unity.dll'),
                          '-r:' + str(output / 'MNN.Samples.SiriAI.Harnesses.dll'),
                          '-out:' + str(output / 'ForegroundChecks.exe')] + list(map(str, tests)),
                ['mono', str(output / 'ForegroundChecks.exe')]]
    environment = dict(os.environ, SIRIAI_HARNESS_TEST_ARTIFACTS=str(output),
                       MONO_PATH=os.pathsep.join([str(output), str(nunit.parent), str(contents / 'Managed/UnityEngine')]))
    records = []
    for command in commands:
        result = subprocess.run(command, capture_output=True, text=True, cwd=output, env=environment, timeout=120)
        records.append(dict(command=command, stdout=result.stdout, stderr=result.stderr, exit_code=result.returncode))
        print(result.stdout + result.stderr, end='')
        if result.returncode:
            break
    (output / 'results.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n')
    return records[-1]['exit_code']


if __name__ == '__main__':
    raise SystemExit(main())
