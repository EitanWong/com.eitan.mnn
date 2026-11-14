#!/usr/bin/env python3
"""Run conversation and Studio EditMode checks in an isolated, reusable Unity project."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--package-only', action='store_true', help='Run package tests without the local SiriAI import')
    parser.add_argument('--label', default='optimized')
    parser.add_argument('--platform', choices=['EditMode', 'PlayMode'], default='EditMode')
    parser.add_argument('--graphics', action='store_true', help='Enable the GPU for shader rendering and visual regression tests')
    parser.add_argument('--filter', default='MNN.Unity.Tests.ConversationReuseTests;MNN.Unity.Tests.StudioPerformanceRegressionTests')
    parser.add_argument('--package-source', type=Path, help='Optional package snapshot under TestArtifacts~ for before/after measurement')
    parser.add_argument('--sample-source', type=Path, help='Optional complete SiriAI snapshot under TestArtifacts~ for before/after regression')
    parser.add_argument('--runtime-source', type=Path, help='Optional saved Runtime tree for a before/after benchmark')
    parser.add_argument('--include-package-tests', action='store_true', help='Enable package test assemblies for managed/native integration checks')
    parser.add_argument('--benchmarks-only', action='store_true', help='Compile only benchmarks, without newer regression tests')
    parser.add_argument('--without-apm', action='store_true', help='Verify EasyMic with model AEC and no optional APM package')
    parser.add_argument('--without-easymic', action='store_true', help='Verify the Unity-only installation without EasyMic or APM')
    parser.add_argument('--output', type=Path, help='Reusable isolated project and logs under the repository TestArtifacts~')
    parser.add_argument('--unity', default='/Applications/Unity/Hub/Editor/2021.3.45f2/Unity.app/Contents/MacOS/Unity')
    args = parser.parse_args()
    if not args.label.replace('-', '').replace('_', '').isalnum():
        parser.error('label must contain only letters, digits, hyphens or underscores')
    repo = Path(__file__).resolve().parents[4]
    sample = args.sample_source.resolve() if args.sample_source else repo / 'Assets/Samples/SiriAI'
    if args.sample_source and repo / 'TestArtifacts~' not in sample.parents:
        parser.error('sample-source must be under the repository TestArtifacts~')
    output = args.output.resolve() if args.output else repo / 'TestArtifacts~/ConversationTests'
    if repo / 'TestArtifacts~' not in output.parents:
        parser.error('output must be under the repository TestArtifacts~')
    project = output / 'UnityProject'
    destination = project / 'Assets/Samples/SiriAI'
    destination.mkdir(parents=True, exist_ok=True)
    # Only this runner's private source copy is replaced; preserve its Library cache.
    shutil.rmtree(destination)
    if not args.package_only:
        if not sample.is_dir(): parser.error('Local SiriAI sample is absent; use --package-only')
        shutil.copytree(sample, destination, ignore=shutil.ignore_patterns('*~', '.DS_Store'))
    else:
        destination.mkdir(parents=True)
    if args.runtime_source is not None:
        runtime_source = args.runtime_source.resolve()
        if not runtime_source.is_dir():
            parser.error('runtime-source must be a saved Runtime directory')
        shutil.rmtree(destination / 'Scripts/Runtime')
        shutil.copytree(runtime_source, destination / 'Scripts/Runtime')
    if args.benchmarks_only:
        for source in (destination / 'Scripts/Tests/Editor').rglob('*.cs'):
            if source.name != 'SiriAIPerformanceTests.cs':
                source.unlink()
                Path(str(source) + '.meta').unlink(missing_ok=True)
    package_source = args.package_source.resolve() if args.package_source else repo / 'Packages/com.eitan.mnn'
    if args.package_source and repo / 'TestArtifacts~' not in package_source.parents:
        parser.error('package-source must be under the repository TestArtifacts~')
    (project / 'Packages').mkdir(parents=True, exist_ok=True)
    # A reusable Player project can contain an embedded copy, which takes priority
    # over the manifest's file dependency. Keep that private copy current as well.
    embedded = project / 'Packages/com.eitan.mnn'
    if embedded.is_dir() and not embedded.is_symlink():
        subprocess.run(['rsync', '-a', '--delete', str(package_source) + '/',
                        str(embedded) + '/'], check=True)
    (project / 'ProjectSettings').mkdir(exist_ok=True)
    shutil.copy2(repo / 'ProjectSettings/ProjectVersion.txt', project / 'ProjectSettings/ProjectVersion.txt')
    # Includes the consuming project's APM experimental-feature defines. Set before
    # launching Unity so the source feature bridge compiles for this project.
    shutil.copy2(repo / 'ProjectSettings/ProjectSettings.asset', project / 'ProjectSettings/ProjectSettings.asset')
    dependencies = {'com.eitan.mnn': 'file:' + str(package_source), 'com.unity.ugui': '1.0.0'}
    for package in (['com.unity.test-framework', 'com.unity.ext.nunit'] if args.without_easymic or args.package_only else
                    ['com.eitan.easymic', 'com.unity.test-framework', 'com.unity.ext.nunit']):
        cached = sorted((repo / 'Library/PackageCache').glob(package + '@*'))
        if not cached:
            raise SystemExit('Required cached dependency missing: ' + package)
        dependencies[package] = 'file:' + str(cached[0])
    # EasyMic alone does not contain AEC/ANS/AGC. Keep the installed APM package
    # and its local settings when testing an isolated Player from this project.
    for package in (repo / 'Packages').glob('*'):
        manifest = package / 'package.json'
        if not args.package_only and not args.without_easymic and not args.without_apm and manifest.is_file() and json.loads(manifest.read_text()).get('name') == 'com.eitan.easymic.apm':
            dependencies['com.eitan.easymic.apm'] = 'file:' + str(package.resolve())
    for folder in ['ProjectSettings/EasyMic', 'UserSettings/EasyMic']:
        source = repo / folder
        if source.is_dir():
            shutil.copytree(source, project / folder, dirs_exist_ok=True)
    for module in ['ui', 'imgui', 'audio', 'jsonserialize', 'physics', 'physics2d', 'animation',
                   'imageconversion', 'screencapture', 'unitywebrequest', 'unitywebrequestaudio', 'androidjni']:
        dependencies['com.unity.modules.' + module] = '1.0.0'
    manifest = {'dependencies': dependencies}
    if args.include_package_tests or args.package_only:
        manifest['testables'] = ['com.eitan.mnn']
    (project / 'Packages/manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    result = output / (args.label + '.xml')
    log = output / (args.label + '.log')
    result.unlink(missing_ok=True)
    if not args.package_only:
        progress = output / (args.label + '.progress.txt')
        progress.unlink(missing_ok=True)
        callback = destination / 'Scripts/Tests/Editor/PerformanceProgress.cs'
        callback.parent.mkdir(parents=True, exist_ok=True)
        callback.write_text("""using System.IO;
    using NUnit.Framework.Interfaces;
    using UnityEngine.TestRunner;
    [assembly: TestRunCallback(typeof(PerformanceProgress))]
    public sealed class PerformanceProgress : ITestRunCallback
    {
        private static readonly string Path = @\"""" + str(progress) + """\";
        public void RunStarted(ITest test) { }
        public void RunFinished(ITestResult result) { }
        public void TestStarted(ITest test) { if (!test.IsSuite) File.AppendAllText(Path, \"START \" + test.FullName + \"\\n\"); }
        public void TestFinished(ITestResult result) { if (!result.Test.IsSuite) File.AppendAllText(Path, result.ResultState.Status + \" \" + result.FullName + \"\\n\" + result.Message + \"\\n\" + result.StackTrace + \"\\n\"); }
    }
    """)
    completed = subprocess.run([args.unity, '-batchmode'] + ([] if args.graphics else ['-nographics']) + ['-projectPath', str(project),
                                '-runTests', '-testPlatform', args.platform, '-testFilter', args.filter,
                                '-testResults', str(result), '-logFile', str(log)], check=False)
    if result.exists():
        root = ET.parse(result).getroot()
        if int(root.get('total', '0')) == 0:
            print('No tests matched the filter; compilation alone is not regression validation.')
            return 1
        print(json.dumps(root.attrib, indent=2))
        for case in root.iter('test-case'):
            if case.get('result') == 'Failed':
                print(case.get('fullname'), case.findtext('failure/message'))
    else:
        print('No test result; inspect ' + str(log))
    if not result.exists():
        return completed.returncode or 1
    return completed.returncode or (0 if root.get('failed') == '0' else 1)


if __name__ == '__main__':
    raise SystemExit(main())
