#!/usr/bin/env python3
"""Prepare or explicitly run live acceptance using the actual package C# providers.

The plan is nonsecret. Credential values are inherited in named environment
variables and are never copied into the project, command line, plan or report.
Default mode prepares the fixture only; --execute runs the bounded real requests.
"""

import argparse
import copy
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
ASSEMBLY = "MNN.Unity.CloudAcceptance.Tests"


def load_preflight():
    spec = importlib.util.spec_from_file_location("cloud_preflight", HERE / "preflight_cloud_acceptance.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def inject_fixture(project, plan, plan_directory, output):
    folder = Path(project) / "Assets/CloudAcceptance/Editor"
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(HERE / "MNNCloudAcceptanceTests.cs", folder / "MNNCloudAcceptanceTests.cs")
    assembly = {"name": ASSEMBLY, "references": ["MNN.Unity"], "includePlatforms": ["Editor"],
                "optionalUnityReferences": ["TestAssemblies"], "autoReferenced": False}
    (folder / (ASSEMBLY + ".asmdef")).write_text(json.dumps(assembly, indent=2) + "\n")
    prepared = copy.deepcopy(plan)
    for scenario in prepared["scenarios"]:
        if scenario.get("audio"):
            scenario["audio"] = str((Path(plan_directory) / scenario["audio"]).resolve())
    path = Path(output) / "prepared-plan.json"
    path.write_text(json.dumps(prepared, indent=2) + "\n")
    return path


def evaluate_results(path, exit_code, scenarios):
    report = {"kind": "real_shared_cloud_acceptance", "passed": False, "acceptance_verified": False,
              "expected_scenarios": scenarios, "executed_scenarios": 0, "errors": [], "native_inference_verified": False}
    errors = report["errors"]
    if exit_code:
        errors.append("Unity did not exit successfully.")
    try:
        root = ET.parse(path).getroot()
        cases = list(root.iter("test-case"))
        report["executed_scenarios"] = len(cases)
        if len(cases) != scenarios or scenarios < 1:
            errors.append("Not every planned cloud scenario executed.")
        if root.get("result") != "Passed" or int(root.get("failed", "-1")) != 0:
            errors.append("Cloud NUnit run did not pass.")
        assemblies = [suite for suite in root.iter("test-suite") if suite.get("type") == "Assembly"]
        if len(assemblies) != 1 or assemblies[0].get("name") != ASSEMBLY + ".dll":
            errors.append("Results do not belong to the actual shared-provider cloud acceptance assembly.")
        for counter in ("skipped", "inconclusive"):
            if int(root.get(counter, "-1")) != 0:
                errors.append("Cloud results report incomplete scenarios.")
        names = set()
        for case in cases:
            name = case.get("fullname", "")
            if not name.startswith("MNN.Unity.CloudAcceptance.MNNCloudAcceptanceTests.") or name in names:
                errors.append("Cloud scenario identity is missing, duplicated or belongs to another fixture.")
            names.add(name)
            if case.get("result") != "Passed":
                errors.append("Scenario failed or did not run: " + case.get("name", "unknown"))
        if int(root.get("total", "-1")) != len(cases) or int(root.get("passed", "-1")) != len(cases):
            errors.append("Cloud result counters are incomplete.")
    except (OSError, ValueError, ET.ParseError):
        errors.append("Cloud test XML is missing or invalid.")
    report["passed"] = report["acceptance_verified"] = not errors
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", required=True, type=Path)
    parser.add_argument("--models", type=Path, help="Existing service /models inventory; blocks unadvertised model ids")
    parser.add_argument("--output", type=Path, default=ROOT / "TestArtifacts~/CloudAcceptance")
    parser.add_argument("--unity", type=Path)
    parser.add_argument("--execute", action="store_true", help="Run the plan's real, potentially billable provider requests")
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "TestArtifacts~") or output == ROOT / "TestArtifacts~":
        parser.error("output must be a child of the repository's TestArtifacts~/")
    output.mkdir(parents=True, exist_ok=True)
    try:
        if args.config.stat().st_size > 256 * 1024:
            raise ValueError("Oversized plan")
        plan = json.loads(args.config.read_text(encoding="utf-8"))
    except (ValueError, OSError):
        parser.error("Provide a valid nonsecret acceptance plan, at most 256 KiB.")
    preflight = load_preflight()
    try:
        report = preflight.preflight(plan, args.config.resolve().parent, os.environ, preflight.read_advertised_models(args.models))
    except (ValueError, OSError):
        parser.error("Provide a valid existing service model inventory.")
    (output / "preflight.json").write_text(json.dumps(report, indent=2) + "\n")
    if not report["ready"]:
        print(json.dumps(report, indent=2))
        return 2
    if args.execute and (not args.unity or not args.unity.is_file()):
        parser.error("--execute requires an existing licensed --unity executable")
    project = output / "UnityProject"
    subprocess.run([sys.executable, "-B", str(HERE / "prepare_ci_project.py"), "--project", str(project)], check=True)
    prepared = inject_fixture(project, plan, args.config.resolve().parent, output)
    if not args.execute:
        print(json.dumps({"prepared": True, "acceptance_verified": False, "network_requests": 0,
                          "project": str(project), "plan": str(prepared)}, indent=2))
        return 0
    results, log = output / "results.xml", output / "Unity.log"
    results.unlink(missing_ok=True)
    environment = os.environ.copy()
    environment["MNN_CLOUD_ACCEPTANCE_PLAN"] = str(prepared)
    environment["MNN_CLOUD_ACCEPTANCE_EXECUTE"] = "1"
    try:
        completed = subprocess.run([str(args.unity), "-batchmode", "-nographics", "-projectPath", str(project),
                                    "-runTests", "-testPlatform", "EditMode", "-assemblyNames", ASSEMBLY,
                                    "-testResults", str(results), "-logFile", str(log)], env=environment,
                                   timeout=300 + sum(item["timeout_seconds"] for item in plan["scenarios"]), check=False)
        exit_code = completed.returncode
    except subprocess.TimeoutExpired:
        exit_code = 124
    summary = evaluate_results(results, exit_code, len(plan["scenarios"]))
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
    print(json.dumps(summary, indent=2))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
