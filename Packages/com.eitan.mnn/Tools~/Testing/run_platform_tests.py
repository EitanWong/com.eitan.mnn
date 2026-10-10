#!/usr/bin/env python3
"""Run runtime tests in an existing isolated Unity project, without downloads.

PlatformContract success verifies guards only. NativeInference success verifies
the bundled affine graph, including its selected Auto device, not every model/backend.
"""
import argparse
import json
import os
import re
from pathlib import Path
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", required=True, type=Path)
    parser.add_argument("--project", required=True, type=Path)
    parser.add_argument("--platform", required=True,
                        choices=["PlayMode", "StandaloneOSX", "StandaloneWindows64",
                                 "StandaloneLinux64", "Android", "iOS", "WebGL"])
    parser.add_argument("--backend", choices=["Mono2x", "IL2CPP"], default="Mono2x")
    parser.add_argument("--mac-architecture", choices=["arm64", "x64", "universal"])
    parser.add_argument("--contract-only", action="store_true",
                        help="Verify platform rejection policy only; does not certify inference.")
    parser.add_argument("--model-root", type=Path, help="Existing directory containing the six representative models.")
    parser.add_argument("--fixtures", type=Path, help="Existing directory containing red.png, blue.png and speech.wav.")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[4]
    project = args.project.resolve()
    artifact_root = root / "TestArtifacts~" / "MNNValidation" / "PlatformValidation"
    if root / "TestArtifacts~" not in project.parents:
        parser.error("Use an existing isolated project under this repository's TestArtifacts~.")
    if not (project / "ProjectSettings" / "ProjectVersion.txt").is_file():
        parser.error("The isolated Unity project must already exist.")
    if not (project / "Packages" / "com.eitan.mnn").is_dir():
        parser.error("Sync the package into the isolated project's Packages/com.eitan.mnn first.")
    if args.mac_architecture and args.platform != "StandaloneOSX":
        parser.error("--mac-architecture applies only to StandaloneOSX.")
    if args.platform == "PlayMode" and args.backend == "IL2CPP":
        parser.error("Editor PlayMode uses Mono. Use a Player target to test IL2CPP.")
    if bool(args.model_root) != bool(args.fixtures) or (args.contract_only and args.model_root):
        parser.error("Provide --model-root and --fixtures together, only for inference runs.")

    label = "-".join(filter(None, [args.platform, args.backend, args.mac_architecture,
                                  "contract" if args.contract_only else "inference"]))
    output = artifact_root / label
    output.mkdir(parents=True, exist_ok=True)
    for name in ("configure.log", "unity.log", "player.log"):
        (output / name).unlink(missing_ok=True)
    settings = output / "settings.json"
    settings.write_text(json.dumps({"scriptingBackend": args.backend}), encoding="utf-8")
    base = [str(args.unity.resolve()), "-batchmode", "-projectPath", str(project)]
    if args.mac_architecture:
        setup = project / "Assets" / "MNNValidation" / "Editor" / "PlatformValidationSetup.cs"
        setup.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(Path(__file__).with_name("PlatformValidationSetup.cs"), setup)
        env = dict(os.environ, MNN_VALIDATION_MAC_ARCH=args.mac_architecture)
        subprocess.run(base + ["-quit", "-executeMethod", "MNNPlatformValidationSetup.Configure",
                               "-logFile", str(output / "configure.log")],
                       env=env, check=True, timeout=600)

    result_path = output / "results.xml"
    # Never accept stale results from an earlier successful invocation.
    for path in [result_path, output / "summary.json"]:
        path.unlink(missing_ok=True)
    command = base + ["-runTests", "-testPlatform", args.platform,
                      "-assemblyNames", "MNN.Unity.Runtime.Tests",
                      "-testSettingsFile", str(settings),
                      "-playerHeartbeatTimeout", "180",
                      "-testResults", str(result_path),
                      "-logFile", str(output / "unity.log")]
    if args.platform != "PlayMode":
        player = output / "Player"
        # Fresh inodes avoid macOS retaining stale executable signatures after
        # Unity's incremental build overwrites a previously launched Player.
        if player.exists():
            shutil.rmtree(player)
        player.mkdir(exist_ok=True)
        # Unity Test Framework expects a directory and creates PlayerWithTests
        # below it. Passing an .app path creates a misleading nested bundle.
        command += ["-buildPlayerPath", str(player)]
    if args.contract_only:
        command += ["-testCategory", "PlatformContract"]
    env = dict(os.environ)
    if args.model_root:
        env.update(MNN_PLAYER_MODEL_ROOT=str(args.model_root.resolve()),
                   MNN_PLAYER_FIXTURES=str(args.fixtures.resolve()), MNN_PLAYER_ARTIFACTS=str(output))
    marker = output / "player-ready.txt"
    marker.unlink(missing_ok=True)
    if args.platform == "StandaloneOSX":
        env["MNN_VALIDATION_PLAYER_READY"] = str(marker)
    player_process = None
    process = subprocess.Popen(command, env=env)
    try:
        deadline = time.monotonic() + 1800
        while process.poll() is None:
            if args.platform == "StandaloneOSX" and marker.exists() and player_process is None:
                bundle = Path(marker.read_text().strip()).resolve()
                if output not in bundle.parents:
                    raise RuntimeError("Player build escaped the validation output directory.")
                executables = [p for p in (bundle / "Contents/MacOS").iterdir() if p.is_file()]
                if len(executables) != 1:
                    raise RuntimeError("Expected exactly one Player executable.")
                player_process = subprocess.Popen([str(executables[0]), "-batchmode",
                                                   "-logFile", str(output / "player.log")],
                                                  env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            if (player_process is not None and player_process.poll() not in (None, 0)
                    and not result_path.exists()):
                raise RuntimeError("Test Player exited with code " + str(player_process.returncode) +
                                   ". Inspect player.log.")
            if time.monotonic() > deadline:
                raise TimeoutError("Unity test run exceeded 30 minutes.")
            time.sleep(1)
    finally:
        for child in (player_process, process):
            if child is not None and child.poll() is None:
                child.terminate()
                try:
                    child.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    child.kill()
                    child.wait()
    if not result_path.exists():
        print("No test results produced. Inspect " + str(output / "unity.log"), file=sys.stderr)
        return 1
    result = ET.parse(result_path).getroot()
    tests = list(result.iter("test-case"))
    native = [test for test in tests if ".PlayerInferenceTests." in test.get("fullname", "")]
    representative = [test for test in tests if ".PlayerRepresentativeTests." in test.get("fullname", "")]
    runtime = next((test.findtext("output", "") for test in tests if
                    test.get("name") == "CurrentRuntime_RejectsBeforeResolvingNativeSymbols"), "")
    actual_backend = re.search(r"Backend=([^;]+);", runtime)
    actual_arch = re.search(r"Architecture=([^;]+);", runtime)
    auto_test = next((test for test in native if test.get("name") ==
                      "AutoSession_UsesPreferredDeviceAndRunsOnWorker"), None)
    auto_device = re.search(r"Actual Player backend=(\w+)", auto_test.findtext("output", "")) if auto_test is not None else None
    def acceptable(test):
        if test.get("result") == "Passed":
            return True
        if not test.get("result", "").startswith("Skipped"):
            return False
        if args.contract_only and test.get("name") == "AppleAbi_PublicLayoutsMatchNativeContract":
            return True  # The unsupported ABI layout is deliberately not executed.
        return not args.model_root and test in representative

    # Unity 2021 reports the suite as Skipped:Ignored when even one optional
    # test is ignored. Accept only the specific optional cases above.
    success = (process.returncode == 0 and len(tests) > 0 and
               int(result.get("failed", "0")) == 0 and all(acceptable(test) for test in tests))
    success = success and actual_backend is not None and actual_backend.group(1) == args.backend
    if args.mac_architecture in ("arm64", "x64"):
        expected_arch = {"arm64": "Arm64", "x64": "X64"}[args.mac_architecture]
        success = success and actual_arch is not None and actual_arch.group(1) == expected_arch
    if not args.contract_only:
        success = success and len(native) >= 4 and all(test.get("result") == "Passed" for test in native)
    representative_verified = (bool(args.model_root) and len(representative) == 6 and
                               all(test.get("result") == "Passed" for test in representative))
    if args.model_root:
        success = success and representative_verified
    summary = {
        "platform": args.platform, "backend": args.backend,
        "mac_architecture": args.mac_architecture, "contract_only": args.contract_only,
        "actual_backend": actual_backend.group(1) if actual_backend else None,
        "actual_architecture": actual_arch.group(1) if actual_arch else None,
        "runtime": runtime,
        "result": result.attrib, "process_exit_code": process.returncode,
        "native_inference_tests": len(native),
        "platform_contract_verified": bool(success and args.contract_only),
        "affine_inference_verified": bool(success and not args.contract_only),
        "auto_affine_backend": auto_device.group(1) if auto_device else None,
        "auto_affine_inference_verified": bool(success and auto_test is not None and auto_test.get("result") == "Passed"),
        "six_representative_models_verified": bool(success and representative_verified),
        "all_models_verified": False,
    }
    (output / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0 if success else 1


if __name__ == "__main__":
    sys.exit(main())
