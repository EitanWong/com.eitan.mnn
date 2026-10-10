#!/usr/bin/env python3
"""Run every real representative-model test in an existing isolated Unity project.

This is an offline EditMode integration run. It requires the six already
downloaded models and local fixtures, and fails if any representative test is
skipped. It never downloads, copies, or deletes model files.
"""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET


MODELS = (
    "Qwen3.5-0.8B-MNN",
    "SmolVLM-256M-Instruct-MNN",
    "Qwen2.5-Omni-3B-MNN",
    "LFM2.5-Audio-1.5B-MNN",
    "Qwen3-Embedding-0.6B-MNN",
    "Qwen3-Reranker-0.6B-MNN",
)
EXPECTED_TESTS = 30
REPRESENTATIVE_FIXTURES = (
    "MNNLlmIntegrationTests", "VisionModelTests", "OmniModelTests",
    "AudioModelTests", "EmbeddingModelTests", "RerankerModelTests",
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", required=True, type=Path)
    parser.add_argument("--project", required=True, type=Path)
    parser.add_argument("--model-root", required=True, type=Path)
    parser.add_argument("--fixtures", required=True, type=Path)
    args = parser.parse_args()

    repo = Path(__file__).resolve().parents[4]
    project = args.project.resolve()
    model_root = args.model_root.resolve()
    fixtures = args.fixtures.resolve()
    artifacts_root = (repo / "TestArtifacts~/MNNValidation/ModelTests").resolve()
    if repo / "TestArtifacts~" not in project.parents:
        parser.error("Use an isolated Unity project under this repository's TestArtifacts~/.")
    if not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        parser.error("The isolated project is not a Unity project.")
    if not (project / "Packages/com.eitan.mnn").is_dir():
        parser.error("Sync this package into the isolated project's Packages/com.eitan.mnn first.")
    approved_model_root = (repo / "Assets/StreamingAssets/MNN/Models").resolve()
    if model_root != approved_model_root:
        parser.error("Use the existing project model directory: " + str(approved_model_root))
    if not fixtures.is_relative_to((repo / "TestArtifacts~").resolve()):
        parser.error("Fixtures must remain under this repository's TestArtifacts~/.")

    for name in MODELS:
        directory = model_root / name
        if not (directory / "config.json").is_file():
            parser.error("Missing model config: " + str(directory / "config.json"))
        if not any(directory.rglob("*.mnn")):
            parser.error("Missing MNN weights in: " + str(directory))
    for name in ("red.png", "blue.png", "speech.wav"):
        if not (fixtures / name).is_file():
            parser.error("Missing local test input: " + str(fixtures / name))

    run_id = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-" + str(os.getpid())
    output = artifacts_root / run_id
    output.mkdir(parents=True)
    result_file = output / "results.xml"
    command = [str(args.unity.resolve()), "-batchmode", "-projectPath", str(project),
               "-runTests", "-testPlatform", "editmode", "-testCategory", "RepresentativeModel",
               "-testFilter", ";".join("MNN.Unity.Tests." + name for name in REPRESENTATIVE_FIXTURES),
               "-testResults", str(result_file), "-logFile", str(output / "unity.log")]
    env = dict(os.environ,
               MNN_TEST_MODEL_DIRECTORY=str(model_root / "Qwen3.5-0.8B-MNN"),
               MNN_TEST_MODEL_ROOT=str(model_root),
               MNN_TEST_ARTIFACT_ROOT=str(output),
               MNN_TEST_FIXTURE_ROOT=str(fixtures),
               MNN_REQUIRE_MODEL_TESTS="1")
    try:
        completed = subprocess.run(command, env=env, timeout=1800, check=False)
    except subprocess.TimeoutExpired:
        print("Unity model tests timed out. Inspect " + str(output / "unity.log"), file=sys.stderr)
        return 1
    if not result_file.is_file():
        print("Unity produced no test results. Inspect " + str(output / "unity.log"), file=sys.stderr)
        return 1

    root = ET.parse(result_file).getroot()
    cases = list(root.iter("test-case"))
    representative = [case for case in cases if "." in case.get("fullname", "") and any(
        "." + name + "." in case.get("fullname", "") for name in
        REPRESENTATIVE_FIXTURES)]
    passed = len(representative) == EXPECTED_TESTS and all(
        case.get("result") == "Passed" for case in representative)
    success = (completed.returncode == 0 and int(root.get("failed", "0")) == 0 and
               len(representative) == EXPECTED_TESTS and passed)
    summary = {
        "mode": "offline_editmode_representative_models",
        "models": list(MODELS),
        "test_count": len(representative),
        "expected_test_count": EXPECTED_TESTS,
        "passed": sum(case.get("result") == "Passed" for case in representative),
        "failed_or_skipped": [
            {"name": case.get("fullname"), "result": case.get("result"),
             "message": case.findtext("failure/message", "")}
            for case in representative if case.get("result") != "Passed"],
        "unity_process_exit_code": completed.returncode,
        "passed_all_required_model_tests": success,
        "results": str(result_file),
    }
    (output / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0 if success else 1


if __name__ == "__main__":
    raise SystemExit(main())
