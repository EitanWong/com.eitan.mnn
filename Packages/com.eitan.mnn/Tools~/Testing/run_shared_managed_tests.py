#!/usr/bin/env python3
"""Run the complete offline shared EditMode scope in an isolated package-only project.

The fixed network category has a separately runnable scope. No user case filters.
Any failed, unreviewed skipped, inconclusive, missing or inconsistent result blocks the gate.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[4]
ASSEMBLY = "MNN.Unity.Tests"
REQUIRED_FIXTURES = tuple("MNN.Unity.Tests." + name for name in (
    "AgentLifetimeTests", "AgentReuseTests", "ConversationCompositionTests", "LocalConversationProviderTests",
    "OpenAIStreamingTests", "TaskSpecificCloudSpeechTests", "ConversationReuseTests", "ConversationBoundaryTests",
    "ConversationCommandTests", "ConversationTranscriptStoreTests", "ConversationSharedStateTests",
    "AcousticFrontendTests", "AcousticSpeakerSelectionTests", "ModelReuseServicesTests", "PinnedArchiveTests",
))
NETWORK_CASES = tuple("MNN.Unity.Tests." + name for name in (
    "ModelDiscoveryTests.LiveDiscovery_AuditsBothPublicProviders",
    "ModelRepositoryTests.LiveCatalog_AllRepositoriesHaveDownloadableFiles",
    "ModelDownloadServiceTests.DownloadRepositoryFileAsync_ModelScopeLfsRedirect_DownloadsCompleteFile",
    "ModelDownloadServiceTests.ModelScopeMobileLlmDownloadsOnlyItsRuntimeProfile",
    "ModelDownloadServiceTests.ModelScopeSmolLlmDownloadsOnlyItsRuntimeProfile",
    "ModelDownloadServiceTests.ModelScopeQwen25SmallDownloadsOnlyItsRuntimeProfile",
    "ModelDownloadServiceTests.ModelScopeQwen3SmallDownloadsOnlyItsRuntimeProfile",
))
LOCAL_EXPLICIT_CASES = tuple("MNN.Unity.Tests." + name for name in (
    "ModelCatalogCacheTests.MeasureExistingCatalogCacheReadsWithoutNetwork", "ModelClassificationTests.AuditCachedCatalog",
    "SpeechModelPackagingTests.AvMossFormer2TargetSpeakerExtractionExecutesWithFiniteOutput",
    "SpeechModelPackagingTests.AvMossFormer2TargetSpeakerExtractionExecutesWithMetal",
    "SpeechModelPackagingTests.ConvertedSpeechGraphsExecuteWithFiniteOutputs",
    "SpeechModelPackagingTests.DeepFilterNetStudioPreservesInputWaveSampleRate",
    "SpeechModelPackagingTests.FrcrnStudioPreservesInputWaveSampleRate",
    "StudioCatalogTests.ClickingCatalogWithInstalledModelsKeepsTheEditorResponsive",
    "StudioStartupTests.OpeningWithInstalledModelsDoesNotBlockTheEditor",
    "StudioInferenceTests.OfficialHunyuanRepositoryLoadsAndRunsThroughStudioChat",
    "StudioInferenceTests.OfficialMobileLlmRepositoryLoadsAndRunsThroughStudioChat",
    "StudioInferenceTests.OfficialSmolLlmRepositoryAnswersThroughStudioTextGeneration",
    "StudioInferenceTests.OfficialQwen25SmallAnswersThroughStudioChat",
    "StudioInferenceTests.OfficialQwen25SmallMeasuresAutomaticAndCpuGeneration",
    "StudioInferenceTests.OfficialQwen3SmallAnswersThroughStudioChat",
))
OPTIONAL_IMPORTED_SAMPLE_CASES = tuple(
    'MNN.Unity.Tests.MNNComponentMenuTests.ImportedSampleHasMatchingComponentAndHierarchyMenus("' + name + '","' + menu + '")'
    for name, menu in (
        ("MNN.Examples.MNNBasicExample", "MNN/Examples/Basic Status Example"),
        ("MNN.Examples.MNNInferenceExample", "MNN/Examples/Inference Example"),
        ("MNN.Examples.MNNUnifiedExample", "MNN/Examples/Unified Inference Example"),
        ("SiriAIRenderer", "MNN/Examples/Siri.AI/Renderer"),
    ))


def evaluate_results(path, exit_code, required_fixtures=REQUIRED_FIXTURES, external_network_cases=NETWORK_CASES, scope="offline"):
    report = {"kind": "complete_" + scope + "_shared_regression", "assembly": ASSEMBLY, "scope": scope, "passed": False,
              "unity_exit_code": exit_code, "cases": 0, "errors": [], "native_inference_verified": False,
              "coverage_measured": False, "complexity_measured": False, "duplication_measured": False,
              "mutation_measured": False, "unimported_sample_cases": [],
              "not_selected_network_cases": [], "unresolved_cases": [], "passed_cases": 0,
              "external_network_cases": list(external_network_cases) if scope == "offline" else [],
              "discovered_cases": 0}
    errors = report["errors"]
    if exit_code != 0:
        errors.append("Unity did not exit successfully.")
    path = Path(path)
    if not path.is_file():
        errors.append("Unity result XML is missing; compilation is not test execution.")
        return report
    if path.stat().st_size > 64 * 1024 * 1024:
        errors.append("Result XML exceeds the 64 MiB inspection budget.")
        return report
    try:
        root = ET.parse(path).getroot()
        if root.tag != "test-run":
            raise ValueError("Expected NUnit test-run root.")
        cases = list(root.iter("test-case"))
        report["cases"] = len(cases)
        if not cases:
            errors.append("No tests executed.")
        if int(root.get("total", "-1")) != len(cases):
            errors.append("NUnit total does not match the leaf case count.")
        if root.get("result") not in ("Passed", "Skipped", "Skipped:Explicit", "Skipped:Ignored"):
            errors.append("NUnit test run is not Passed or solely a reviewed scope/optional skip.")
        for name in ("failed", "inconclusive"):
            if int(root.get(name, "-1")) != 0:
                errors.append("NUnit reports nonzero or absent " + name + ".")
        assemblies = [suite for suite in root.iter("test-suite") if suite.get("type") == "Assembly"]
        if len(assemblies) != 1 or assemblies[0].get("name") not in (ASSEMBLY, ASSEMBLY + ".dll"):
            errors.append("Expected only the complete MNN.Unity.Tests assembly.")
        elif len(list(assemblies[0].iter("test-case"))) != len(cases):
            errors.append("Cases outside the shared test assembly were reported.")
        else:
            report["discovered_cases"] = int(assemblies[0].get("testcasecount", "-1"))
            expected = len(cases) if scope == "offline" else report["discovered_cases"]
            if report["discovered_cases"] != expected or report["discovered_cases"] < len(cases):
                errors.append("Discovered cases do not equal the complete assembly's reported leaf inventory.")
        names = set()
        for case in cases:
            name = case.get("fullname", "")
            if not name or name in names:
                errors.append("Missing or duplicate test case identity: " + name)
            names.add(name)
            sample = (name in OPTIONAL_IMPORTED_SAMPLE_CASES and case.get("result") == "Skipped"
                      and case.get("label") == "Ignored" and case.findtext("reason/message") ==
                      "This sample has not been imported into this project.")
            excluded_network = (scope == "offline" and name in external_network_cases)
            explicit_nonselection = (case.get("result") == "Skipped" and case.get("label") == "Explicit"
                                     and case.get("runstate") == "Explicit" and case.find("failure") is None)
            report["passed_cases"] += case.get("result") == "Passed"
            if excluded_network and explicit_nonselection:
                report["not_selected_network_cases"].append(name)
            elif excluded_network:
                errors.append("External network case was not excluded from offline execution: " + name)
            elif sample and scope == "offline":
                report["unimported_sample_cases"].append(name)
            elif case.get("result") != "Passed":
                errors.append("Incomplete case: " + name + " (" + str(case.get("result")) + ")")
            if (excluded_network and not explicit_nonselection) or (not excluded_network and not sample and case.get("result") != "Passed"):
                report["unresolved_cases"].append({"name": name, "result": case.get("result"), "label": case.get("label"),
                                                   "runstate": case.get("runstate"), "reason": case.findtext("reason/message")})
        if scope == "offline":
            for name in sorted(set(external_network_cases) - names):
                errors.append("Declared external network case is missing from the complete assembly inventory: " + name)
        if scope == "network" and names != set(NETWORK_CASES):
            errors.append("The network scope must execute exactly every declared external network case.")
        skipped = len(report["unimported_sample_cases"]) + len(report["not_selected_network_cases"])
        if root.get("result") != "Passed" and skipped == 0:
            errors.append("NUnit run did not pass and no reviewed scope/optional skip explains its status.")
        if int(root.get("passed", "-1")) != len(cases) - skipped or int(root.get("skipped", "-1")) != skipped:
            errors.append("Not every required case passed or the skip counters are inconsistent.")
        for fixture in required_fixtures if scope == "offline" else ():
            if not any(name.startswith(fixture + ".") for name in names):
                errors.append("Required migration fixture did not execute: " + fixture)
    except (ET.ParseError, ValueError, OSError):
        errors.append("Result XML is malformed or has invalid result counters.")
    report["passed"] = not errors
    return report


def unity_command(unity, project, results, log, graphics, scope="offline"):
    # NUnit NotFilter does not explicitly select local Explicit tests. A positive
    # full-name expression selects every offline leaf while omitting exactly the
    # reviewed network scope. Unity's assembly-only filter otherwise selects all Explicit tests.
    selection = ["-testCategory", "RequiresNetwork"] if scope == "network" else [
        "-testFilter", "^(?!(?:" + "|".join(re.escape(name) for name in NETWORK_CASES) + ")$).*"]
    return [str(unity), "-batchmode"] + ([] if graphics else ["-nographics"]) + [
        "-projectPath", str(project), "-runTests", "-testPlatform", "EditMode",
        "-assemblyNames", ASSEMBLY] + selection + ["-testResults", str(results), "-logFile", str(log)]


def audit_suite_sources(package):
    """Inventory declarations, preserving assembly ownership and opt-in requirements."""
    package = Path(package)
    declarations = []
    pattern = re.compile(r'(?P<attributes>(?:\s*\[(?:[^\]"]|"(?:\\.|[^"])*")*\]\s*)+)'
                         r'(?P<signature>(?:public|internal|private|protected)\s+(?:[\w<>,.\[\]]+\s+)+(?P<name>\w+)\s*(?P<kind>\(|\{|:))')
    for path in sorted((package / "Tests").rglob("*.cs")):
        source = path.read_text(encoding="utf-8")
        namespace = re.search(r'\bnamespace\s+([\w.]+)', source)
        fixture = re.search(r'\bclass\s+(\w+)', source)
        if not namespace or not fixture:
            continue
        owner = path.parent
        while owner != package and not list(owner.glob("*.asmdef")):
            owner = owner.parent
        assembly_files = list(owner.glob("*.asmdef"))
        assembly = json.loads(assembly_files[0].read_text())["name"] if len(assembly_files) == 1 else "unknown"
        for match in pattern.finditer(source):
            attributes = match["attributes"]
            explicit = bool(re.search(r'\bExplicit\s*(?:\(|\]|,)', attributes))
            network = 'Category("RequiresNetwork")' in attributes
            if not explicit and not network:
                continue
            target = namespace[1] + "." + fixture[1]
            if match["kind"] == "(":
                target += "." + match["name"]
            declarations.append({"name": target, "assembly": assembly, "explicit": explicit, "requires_network": network,
                                 "source": path.relative_to(package).as_posix(), "line": source[:match.start()].count("\n") + 1,
                                 "file_environment_keys": sorted(set(re.findall(r'GetEnvironmentVariable\("([A-Z0-9_]+)"', source)))})
    shared = [item for item in declarations if item["assembly"] == ASSEMBLY]
    network = sorted(item["name"] for item in shared if item["requires_network"])
    explicit = sorted(item["name"] for item in shared if item["explicit"])
    errors = []
    if network != sorted(NETWORK_CASES):
        errors.append("RequiresNetwork source declarations differ from the exact reviewed seven-case scope.")
    if explicit != sorted(NETWORK_CASES + LOCAL_EXPLICIT_CASES):
        errors.append("Shared Explicit source declarations differ from the reviewed complete scope inventory.")
    return {"passed": not errors, "errors": errors, "network_cases": network, "shared_explicit_cases": explicit,
            "all_explicit_declarations": [item for item in declarations if item["explicit"]]}


def source_digest(package):
    digest = hashlib.sha256()
    files = sorted(path for directory in ("Runtime", "Editor", "Tests")
                   for path in (package / directory).rglob("*") if path.suffix in (".cs", ".asmdef"))
    for path in files:
        digest.update(path.relative_to(package).as_posix().encode())
        digest.update(b"\0")
        digest.update(path.read_bytes())
    return digest.hexdigest()


def link_models(project, models):
    models = Path(models).resolve()
    if not models.is_dir():
        raise ValueError("The existing model assets directory is missing.")
    destination = Path(project) / "Assets/StreamingAssets/MNN/Models"
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.is_symlink() and destination.resolve() == models:
        return
    if destination.exists() or destination.is_symlink():
        raise ValueError("The isolated project already has a different Models owner; use a new output directory.")
    destination.symlink_to(models, target_is_directory=True)


def read_reference_environment(path):
    if path is None:
        return {}
    if path.stat().st_size > 256 * 1024:
        raise ValueError("Oversized reference environment")
    document = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(document, dict):
        raise ValueError("Reference environment must be a JSON object")
    for name, value in document.items():
        sensitive = {"KEY", "APIKEY", "TOKEN", "SECRET", "PASSWORD", "CREDENTIAL", "CREDENTIALS"}
        if (not re.fullmatch(r"MNN_TEST_[A-Z0-9_]+", name) or sensitive.intersection(name.split("_")) or not isinstance(value, str)
                or len(value) > 4096 or "\0" in value):
            raise ValueError("Only nonsecret MNN_TEST_* model/reference settings are allowed.")
    return document


def network_download_environment(project):
    directory = Path(project).resolve() / "TestArtifacts~/NetworkDownloads"
    return {"MNN_TEST_" + key + "_LLM_ROOT": str(directory / name) for key, name in (
        ("MOBILE", "MobileLLM"), ("SMOL", "SmolLM2"), ("QWEN25", "Qwen2.5"), ("QWEN3", "Qwen3"))}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", type=Path, help="Activated Unity executable; required to run tests")
    parser.add_argument("--output", type=Path, default=ROOT / "TestArtifacts~/SharedManagedTests")
    parser.add_argument("--graphics", action="store_true", help="Keep graphics enabled for the complete suite")
    parser.add_argument("--timeout-seconds", type=int, default=2700)
    parser.add_argument("--scope", choices=("offline", "network"), default="offline",
                        help="offline runs every local case, including native Explicit; network explicitly runs all seven external download/discovery tests")
    parser.add_argument("--prepare-only", action="store_true", help="Prepare the project without claiming a test pass")
    parser.add_argument("--models-root", type=Path, help="Link the runner's existing deployed Models directory; never copy or download weights")
    parser.add_argument("--reference-environment", type=Path, help="Nonsecret JSON object of MNN_TEST_* model/reference settings")
    parser.add_argument("--check-results", type=Path, help="Check an existing complete shared result without launching Unity")
    parser.add_argument("--unity-exit-code", type=int, default=0, help="Actual Unity exit code for --check-results")
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "TestArtifacts~") or output == ROOT / "TestArtifacts~":
        parser.error("output must be a child of this repository's TestArtifacts~/")
    if not 1 <= args.timeout_seconds <= 7200:
        parser.error("timeout-seconds must be between 1 and 7200")
    output.mkdir(parents=True, exist_ok=True)
    scope_inventory = audit_suite_sources(ROOT / "Packages/com.eitan.mnn")
    (output / "suite-inventory.json").write_text(json.dumps(scope_inventory, indent=2) + "\n", encoding="utf-8")
    if not scope_inventory["passed"]:
        print(json.dumps(scope_inventory, indent=2))
        return 1
    if args.check_results:
        report = evaluate_results(args.check_results, args.unity_exit_code, scope=args.scope)
        report["results"] = str(args.check_results.resolve())
    else:
        if not args.prepare_only and (not args.unity or not args.unity.is_file()):
            parser.error("provide an existing licensed Unity executable with --unity")
        project = output / "UnityProject"
        subprocess.run([sys.executable, "-B", str(Path(__file__).with_name("prepare_ci_project.py")),
                        "--project", str(project)], check=True)
        try:
            reference_environment = read_reference_environment(args.reference_environment)
            if args.scope == "network":
                reference_environment.update(network_download_environment(project))
            if args.models_root:
                link_models(project, args.models_root)
        except (ValueError, OSError):
            parser.error("Provide existing model assets and a valid nonsecret MNN_TEST_* reference environment; the isolated Models owner cannot change.")
        if args.prepare_only:
            print(json.dumps({"prepared": True, "tests_executed": False, "project": str(project), "scope": args.scope,
                              "suite_inventory": str(output / "suite-inventory.json")}, indent=2))
            return 0
        results, log = output / "results.xml", output / "Unity.log"
        results.unlink(missing_ok=True)
        command = unity_command(args.unity, project, results, log, args.graphics, args.scope)
        try:
            completed = subprocess.run(command, timeout=args.timeout_seconds, check=False,
                                       env={**os.environ, **reference_environment})
            exit_code = completed.returncode
        except subprocess.TimeoutExpired:
            exit_code = 124
        report = evaluate_results(results, exit_code, scope=args.scope)
        report.update(command=command, results=str(results), log=str(log),
                      source_sha256=source_digest(project / "Packages/com.eitan.mnn"))
        report["model_assets"] = str(args.models_root.resolve()) if args.models_root else None
        report["reference_environment_keys"] = sorted(reference_environment)
    (output / "summary.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
