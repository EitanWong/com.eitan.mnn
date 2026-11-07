#!/usr/bin/env python3
"""Run GC/allocation tests under host Mono and the real NUnit API.

This is a separate runtime measurement. It never clears or replaces Unity skips.
No Unity process, model download, native inference or dependency installation.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
CASES = tuple("MNN.Unity.Tests." + name for name in (
    "AcousticCoreTests.TimelineAndAcousticProcessingAllocateNothingAfterWarmup",
    "AcousticUtilityTests.UtilityProcessingAllocatesNothingAfterWarmup",
    "EchoDelayEstimatorTests.DelaySearchMeetsCpuAndAllocationBudgets(True)",
    "MNNAdaptiveEchoFilterTests.ProcessingMeetsCpuAndAllocationBudgets(0.0f,True)",
    "MNNAdaptiveEchoFilterTests.ProcessingMeetsCpuAndAllocationBudgets(0.25f,True)",
    "MNNAdaptiveEchoFilterTests.ProcessingMeetsCpuAndAllocationBudgets(1.0f,True)",
    "RealtimeTransportTests.CaptureAndRenderTransportAllocateNothingAfterWarmup",
    "SharedFreeRegionSolverTests.RepeatedLayoutSolvesDoNotAllocateManagedMemory",
))
GRAPH_CASES = tuple("MNN.Unity.Tests.AdditionalTtsUnitTests." + name for name in (
    "GraphSpecializationDoesNotDuplicateTheEntireSourceBeforeWritingItsResult",
    "MultipleGraphInputsShareOneResultBuffer",
))
SUITES = {
    "shared": ("MNN.Unity.Tests.dll", CASES),
    "graph-specialization": ("MNN.Unity.Integration.Tests.dll", GRAPH_CASES),
}


def evaluate_results(path, exit_code, suite="shared"):
    assembly_name, expected_cases = SUITES[suite]
    report = {"kind": "host_mono_allocation_gate", "passed": False, "host_mono_allocation_verified": False,
              "unity_allocation_verified": False, "native_inference_verified": False, "errors": [], "cases": 0}
    errors = report["errors"]
    if exit_code:
        errors.append("The host Mono test process failed.")
    try:
        root = ET.parse(path).getroot()
        if root.get("type") != "Assembly" or root.get("name") != assembly_name:
            errors.append("Expected the actual selected test assembly result: " + assembly_name)
        cases = list(root.iter("test-case"))
        report["cases"] = len(cases)
        names = [case.get("fullname") for case in cases]
        if len(names) != len(expected_cases) or set(names) != set(expected_cases):
            errors.append("Exactly the selected allocation cases must execute.")
        for case in cases:
            if case.get("result") != "Passed":
                errors.append("Allocation case failed or did not execute: " + str(case.get("fullname")))
        if root.get("result") != "Passed":
            errors.append("NUnit allocation run did not pass.")
        if any(int(root.get(name, "-1")) != 0 for name in ("failed", "skipped", "inconclusive")):
            errors.append("Allocation results contain failure, skip or inconclusive counters.")
        if int(root.get("total", "-1")) != len(expected_cases) or int(root.get("passed", "-1")) != len(expected_cases):
            errors.append("Allocation case counters are incomplete.")
    except (OSError, ET.ParseError, ValueError):
        errors.append("Allocation NUnit XML is missing or invalid.")
    report["passed"] = report["host_mono_allocation_verified"] = not errors
    return report


def resolve_test_references(project):
    nunit = sorted((Path(project) / "Library/PackageCache").glob("com.unity.ext.nunit*/net35/unity-custom/nunit.framework.dll"))
    runner = Path(project) / "Library/ScriptAssemblies/UnityEngine.TestRunner.dll"
    if not nunit or not runner.is_file():
        raise ValueError("The selected project's NUnit and UnityEngine.TestRunner.dll references are missing.")
    return nunit[-1], runner


def complete_compilation(summary):
    rows = summary.get("assemblies", [])
    return bool(rows) and len(rows) == summary.get("expected_assemblies") and all(row.get("passed") for row in rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", required=True, type=Path, help="Installed Unity used for compiler/reference paths only; never launched")
    parser.add_argument("--output", type=Path, default=ROOT / "TestArtifacts~/MonoAllocationTests")
    parser.add_argument("--suite", choices=tuple(SUITES), default="shared")
    source = parser.add_mutually_exclusive_group()
    source.add_argument("--compiled-assemblies", type=Path, help="Existing check_assembly_compilation output; otherwise compile current package")
    source.add_argument("--unity-project", type=Path, help="Use this already compiled Unity project's actual ScriptAssemblies and package references")
    parser.add_argument("--mono", default=shutil.which("mono"))
    parser.add_argument("--csc", default=shutil.which("csc"), help="Existing host C# compiler for the standalone NUnit entry point")
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "TestArtifacts~") or output == ROOT / "TestArtifacts~":
        parser.error("output must be a child of this repository's TestArtifacts~/")
    if not args.mono or not args.csc:
        parser.error("Provision host Mono and csc; this tool never downloads them.")
    contents = args.unity.resolve().parents[1]
    if not (contents / "Managed/UnityEngine").is_dir():
        parser.error("Provide an installed macOS Unity executable for its managed references.")
    try:
        nunit, test_runner = resolve_test_references(args.unity_project.resolve() if args.unity_project else ROOT)
    except ValueError:
        parser.error("Resolve the selected Unity project's test dependencies first; NUnit and UnityEngine.TestRunner.dll are required.")
    output.mkdir(parents=True, exist_ok=True)
    compiled = (args.unity_project.resolve() / "Library/ScriptAssemblies" if args.unity_project else
                args.compiled_assemblies.resolve() if args.compiled_assemblies else output / "Compilation")
    if args.compiled_assemblies is None and args.unity_project is None:
        with (output / "compilation.log").open("w") as log:
            compilation = subprocess.run([sys.executable, "-B", str(HERE.parent / "Validation/check_assembly_compilation.py"),
                                          "--unity", str(args.unity), "--output", str(compiled)], stdout=log, stderr=subprocess.STDOUT, check=False)
        if compilation.returncode:
            print("Package assembly compilation failed; inspect " + str(output / "compilation.log"))
            return 1
    assembly_name, selected_cases = SUITES[args.suite]
    assembly = compiled / assembly_name
    if not assembly.is_file():
        parser.error("The selected compilation has no actual test assembly: " + assembly_name)
    if args.unity_project is None:
        if not (compiled / "summary.json").is_file() or not complete_compilation(json.loads((compiled / "summary.json").read_text())):
            parser.error("Provide complete successful check_assembly_compilation output, including its summary.")
    case_file, executable, results = output / "cases.txt", output / "MNNMonoAllocationRunner.exe", output / "results.xml"
    case_file.write_text("\n".join(selected_cases) + "\n")
    results.unlink(missing_ok=True)
    with (output / "runner-build.log").open("w") as log:
        build = subprocess.run([args.csc, "-nologo", "-langversion:latest", "-r:" + str(nunit),
                                "-out:" + str(executable), str(HERE / "MNNMonoAllocationRunner.cs")],
                               stdout=log, stderr=subprocess.STDOUT, check=False)
    if build.returncode:
        print("Standalone NUnit entry point compilation failed; inspect " + str(output / "runner-build.log"))
        return 1
    environment = os.environ.copy()
    environment["MONO_PATH"] = os.pathsep.join((str(compiled), str(nunit.parent), str(test_runner.parent),
                                               str(contents / "Managed"), str(contents / "Managed/UnityEngine")))
    environment["MNN_TEST_AFFINE_GRAPH"] = str(ROOT / "Packages/com.eitan.mnn/Tests/Fixtures/affine.mnn")
    environment["MNN_TEST_AFFINE_INPUTS_GRAPH"] = str(ROOT / "Packages/com.eitan.mnn/Tests/Fixtures/affine-inputs.mnn")
    with (output / "runner.log").open("w") as log:
        try:
            completed = subprocess.run([args.mono, str(executable), str(assembly), str(case_file), str(results)],
                                       env=environment, stdout=log, stderr=subprocess.STDOUT, timeout=180, check=False)
            exit_code = completed.returncode
        except subprocess.TimeoutExpired:
            exit_code = 124
    report = evaluate_results(results, exit_code, args.suite)
    report["suite"] = args.suite
    report.update(assembly=str(assembly), assembly_sha256=hashlib.sha256(assembly.read_bytes()).hexdigest(),
                  compilation_summary=str(compiled / "summary.json") if args.unity_project is None else None,
                  actual_unity_project=str(args.unity_project.resolve()) if args.unity_project else None, results=str(results),
                  limitation="Host Mono measurements do not replace Unity, IL2CPP, device or native allocation measurements.")
    (output / "summary.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
