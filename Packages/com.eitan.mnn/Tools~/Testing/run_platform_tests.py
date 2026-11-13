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
import uuid
import xml.etree.ElementTree as ET


def read_player_result(path, build_id):
    """Read only complete NUnit evidence produced by this exact Player build."""
    try:
        data = path.read_bytes()
        result = ET.fromstring(data)
    except (FileNotFoundError, ET.ParseError):
        return None
    if result.tag not in ("test-suite", "test-run") or result.get("mnn-build-id") != build_id:
        return None
    return data


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
    parser.add_argument("--export-only", action="store_true",
                        help="Export an iOS test Xcode project without launching or claiming device inference.")
    parser.add_argument("--require-mobile-llm", action="store_true",
                        help="Require all four staged MobileRealLlm model tests to pass; weights and plan must already be on the device.")
    parser.add_argument("--model-root", type=Path, help="Existing directory containing the six representative models.")
    parser.add_argument("--fixtures", type=Path, help="Existing directory containing red.png, blue.png and speech.wav.")
    parser.add_argument("--bert-root", type=Path, help="Existing root containing the three prepared, pinned BERT embedding profiles.")
    parser.add_argument("--native-bert-root", type=Path, help="Verified local ModelScope TorryJi Chinese BGE directory with tokenizer.mtok.")
    parser.add_argument("--vision-root", type=Path, help="Existing vision root containing prepared YOLO and fourteen OCR profiles including v4/fp16 variants with bus/line/full-page fixtures and the pinned DB detector.")
    parser.add_argument("--document-layout-root", type=Path, help="Require PP-DocLayoutV3 real-page tests; desktop path or assets already staged in mobile persistentDataPath/document-layout.")
    parser.add_argument("--style-root", type=Path, help="Require Candy pixel-reference tests; desktop path or assets already staged in mobile persistentDataPath/style-transfer.")
    parser.add_argument("--text-decision-root", type=Path, help="Require two CPU/Auto English Laya six-reference tests; mobile assets must be staged in persistentDataPath/text-decision.")
    parser.add_argument("--depth-root", type=Path, help="Require four Depth Anything V2 independent preprocessing/depth CPU/Auto tests; desktop path or mobile persistentDataPath/depth.")
    parser.add_argument("--pose-root", type=Path, help="Require MoveNet independent integer input/joint tests for both people; desktop path or assets staged in mobile persistentDataPath/pose.")
    parser.add_argument("--segmentation-root", type=Path, help="Require DeepLab independent pixel/logit tests; desktop path or assets already staged in mobile persistentDataPath/segmentation.")
    parser.add_argument("--test-filter", help="Optional Unity test filter. Reports certify only the selected tests.")
    parser.add_argument("--player-heartbeat-timeout", type=int, default=1800,
                        help="Seconds allowed without a Player heartbeat during synchronous native inference (default: 1800).")
    parser.add_argument("--run-timeout-seconds", type=int, default=1800,
                        help="Total Editor/build/Player deadline, 1–86400 seconds (default: 1800). Independent of heartbeat timeout.")
    parser.add_argument("--kws-root", type=Path, help="Existing prepared Chinese KWS profile with demo WAV and tokenized keywords.")
    parser.add_argument("--smart-turn-root", type=Path, help="Existing prepared Smart Turn model with independent ONNX reference PCM.")
    parser.add_argument("--silero-root", type=Path, help="Existing prepared Silero VAD model and independent ONNX reference PCM/probabilities.")
    parser.add_argument("--paraformer-root", type=Path, help="Existing prepared Paraformer online 8404 weights and independent Chinese WAV fixture.")
    parser.add_argument("--qwen3-asr-root", type=Path, help="Existing prepared Qwen3-ASR 0.6B INT8 weights with ../../Zipformer/Audio fixtures.")
    parser.add_argument("--whisper-root", type=Path, help="Existing pinned Whisper model and feature root, with ../../Zipformer/Audio WAV fixture.")
    parser.add_argument("--sensevoice-root", type=Path, help="Existing SenseVoice weights/feature fixtures with sibling Zipformer/Audio WAV fixtures.")
    parser.add_argument("--asr-root", type=Path, help="Existing Zipformer WAV/reference fixture root.")
    parser.add_argument("--asr-models", type=Path, help="Existing root containing the two verified Zipformer model directories.")
    parser.add_argument("--label-suffix", default="", help="Optional artifact suffix to preserve earlier runs (letters, digits, hyphen or underscore).")
    args = parser.parse_args()
    if args.player_heartbeat_timeout < 1:
        parser.error("--player-heartbeat-timeout must be positive.")
    if not 1 <= args.run_timeout_seconds <= 86400:
        parser.error("--run-timeout-seconds must be between 1 and 86400.")
    if args.label_suffix and not re.fullmatch(r"[A-Za-z0-9_-]+", args.label_suffix):
        parser.error("--label-suffix accepts only letters, digits, hyphen or underscore.")
    root = Path(__file__).resolve().parents[4]
    project = args.project.resolve()
    build_id = uuid.uuid4().hex
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
    if args.platform in ("Android", "iOS") and args.backend != "IL2CPP":
        parser.error("Mobile direct inference requires ARM64 / IL2CPP.")
    if args.export_only and (args.platform != "iOS" or args.contract_only):
        parser.error("--export-only requires iOS inference tests.")
    if bool(args.model_root) != bool(args.fixtures) or (args.contract_only and args.model_root):
        parser.error("Provide --model-root and --fixtures together, only for inference runs.")
    if bool(args.asr_root) != bool(args.asr_models):
        parser.error("Provide --asr-root and --asr-models together.")
    if args.contract_only and (args.depth_root or args.text_decision_root or args.pose_root or args.segmentation_root or args.style_root or args.document_layout_root or args.bert_root or args.native_bert_root or args.vision_root or args.asr_root or args.sensevoice_root or args.whisper_root or args.qwen3_asr_root or args.paraformer_root or args.silero_root or args.smart_turn_root or args.kws_root):
        parser.error("Embedding fixtures require an inference run.")

    label = "-".join(filter(None, [args.platform, args.backend, args.mac_architecture,
                                  "export" if args.export_only else "contract" if args.contract_only else "inference", "bert" if args.bert_root else None, "mtok" if args.native_bert_root else None, "vision" if args.vision_root else None, "asr" if args.asr_root else None, "sensevoice" if args.sensevoice_root else None, "whisper" if args.whisper_root else None, "qwen3-asr" if args.qwen3_asr_root else None, "paraformer" if args.paraformer_root else None, "silero" if args.silero_root else None, "smart-turn" if args.smart_turn_root else None, "kws" if args.kws_root else None, args.label_suffix]))
    output = artifact_root / label
    output.mkdir(parents=True, exist_ok=True)
    (output / "expected-build-id.txt").write_text(build_id + "\n")
    for name in ("configure.log", "unity.log", "player.log"):
        (output / name).unlink(missing_ok=True)
    settings = output / "settings.json"
    settings.write_text(json.dumps({"scriptingBackend": args.backend}), encoding="utf-8")
    if args.platform in ("Android", "iOS", "StandaloneWindows64"):
        resources = project / "Assets/MNNValidation/Resources"
        resources.mkdir(parents=True, exist_ok=True)
        (resources / "MNNValidationBuildId.txt").write_text(build_id + "\n")
    base = [str(args.unity.resolve()), "-ApplePersistenceIgnoreState", "YES", "-batchmode", "-projectPath", str(project)]
    if args.platform != "PlayMode":
        # An isolated project may have last built Android/iOS. Select the target
        # before configuring and running; -testPlatform alone can retain that
        # previous target's extension (.apk) when launching a desktop test build.
        cli_target = {"StandaloneOSX": "OSXUniversal", "StandaloneWindows64": "Win64",
                      "StandaloneLinux64": "Linux64"}.get(args.platform, args.platform)
        base += ["-buildTarget", cli_target]
    if args.platform in ("Android", "iOS"):
        setup = project / "Assets" / "MNNValidation" / "Editor" / "PlatformValidationSetup.cs"
        setup.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(Path(__file__).with_name("PlatformValidationSetup.cs"), setup)
        env = dict(os.environ, MNN_VALIDATION_MOBILE_PLATFORM=args.platform)
        subprocess.run(base + ["-quit", "-executeMethod", "MNNPlatformValidationSetup.ConfigureMobile",
                               "-logFile", str(output / "configure.log")],
                       env=env, check=True, timeout=600)
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
                      "-playerHeartbeatTimeout", str(args.player_heartbeat_timeout),
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
    if args.test_filter:
        command += ["-testFilter", args.test_filter]
    env = dict(os.environ, MNN_INFERENCE_CACHE_DIRECTORY=str(output / "Cache"))
    if args.platform == "StandaloneWindows64":
        env["MNN_PLAYER_RESULTS_DIRECTORY"] = str(output)
    if args.model_root:
        env.update(MNN_PLAYER_MODEL_ROOT=str(args.model_root.resolve()),
                   MNN_PLAYER_FIXTURES=str(args.fixtures.resolve()), MNN_PLAYER_ARTIFACTS=str(output))
    if args.bert_root:
        env["MNN_PLAYER_BERT_ROOT"] = str(args.bert_root.resolve())
    if args.native_bert_root:
        env["MNN_PLAYER_NATIVE_BERT_ROOT"] = str(args.native_bert_root.resolve())
    if args.vision_root:
        env["MNN_PLAYER_VISION_ROOT"] = str(args.vision_root.resolve())
    if args.document_layout_root and args.platform not in ("Android", "iOS"):
        env["MNN_PLAYER_DOCUMENT_LAYOUT_ROOT"] = str(args.document_layout_root.resolve())
    if args.style_root and args.platform not in ("Android", "iOS"):
        env["MNN_PLAYER_STYLE_ROOT"] = str(args.style_root.resolve())
    if args.text_decision_root and args.platform not in ("Android", "iOS"):
        env["MNN_PLAYER_TEXT_DECISION_ROOT"] = str(args.text_decision_root.resolve())
    if args.depth_root and args.platform not in ("Android", "iOS"):
        env["MNN_PLAYER_DEPTH_ROOT"] = str(args.depth_root.resolve())
    if args.pose_root and args.platform not in ("Android", "iOS"):
        env["MNN_PLAYER_POSE_ROOT"] = str(args.pose_root.resolve())
    if args.segmentation_root and args.platform not in ("Android", "iOS"):
        env["MNN_PLAYER_SEGMENTATION_ROOT"] = str(args.segmentation_root.resolve())
    if args.asr_root:
        env["MNN_PLAYER_ASR_ROOT"] = str(args.asr_root.resolve())
        env["MNN_PLAYER_ASR_MODELS"] = str(args.asr_models.resolve())
    if args.kws_root:
        env["MNN_PLAYER_KWS_ROOT"] = str(args.kws_root.resolve())
    if args.smart_turn_root:
        env["MNN_PLAYER_SMART_TURN_ROOT"] = str(args.smart_turn_root.resolve())
    if args.silero_root:
        env["MNN_PLAYER_SILERO_ROOT"] = str(args.silero_root.resolve())
    if args.paraformer_root:
        env["MNN_PLAYER_PARAFORMER_ROOT"] = str(args.paraformer_root.resolve())
    if args.qwen3_asr_root:
        env["MNN_PLAYER_QWEN3_ASR_ROOT"] = str(args.qwen3_asr_root.resolve())
    if args.whisper_root:
        env["MNN_PLAYER_WHISPER_ROOT"] = str(args.whisper_root.resolve())
    if args.sensevoice_root:
        env["MNN_PLAYER_SENSEVOICE_ROOT"] = str(args.sensevoice_root.resolve())
    marker = output / "player-ready.txt"
    marker.unlink(missing_ok=True)
    if args.platform == "StandaloneOSX" or args.export_only:
        env["MNN_VALIDATION_PLAYER_READY"] = str(marker)
    player_process = None
    exported = None
    device_result_retrieved = False
    local_player_result_retrieved = False
    adb = shutil.which("adb") if args.platform == "Android" else None
    device_result_path = "/sdcard/Android/data/com.UnityTestRunner.UnityTestRunner/files/mnn-player-results.xml"
    if adb:
        subprocess.run([adb, "shell", "am", "force-stop", "com.UnityTestRunner.UnityTestRunner"],
                       capture_output=True, check=True, timeout=15)
        subprocess.run([adb, "shell", "rm", "-f", device_result_path], capture_output=True, timeout=15)
    next_device_poll = time.monotonic() + 5
    process = subprocess.Popen(command, env=env)
    try:
        deadline = time.monotonic() + args.run_timeout_seconds
        while process.poll() is None:
            if args.platform == "StandaloneWindows64":
                local_result = read_player_result(output / "mnn-player-results.xml", build_id)
                if local_result is not None:
                    result_path.write_bytes(local_result)
                    local_player_result_retrieved = True
                    break
            if adb and time.monotonic() >= next_device_poll and not result_path.exists():
                next_device_poll = time.monotonic() + 5
                pulled = subprocess.run([adb, "exec-out", "cat", device_result_path],
                                        capture_output=True, timeout=15)
                if pulled.returncode == 0 and pulled.stdout.startswith(b"<test-suite"):
                    device_xml = ET.fromstring(pulled.stdout)
                    if device_xml.get("mnn-build-id") != build_id:
                        continue  # An earlier Player cannot certify this build.
                    result_path.write_bytes(pulled.stdout)
                    device_result_retrieved = True
                    break
            if args.export_only and marker.exists():
                exported = Path(marker.read_text().strip()).resolve()
                if output not in exported.parents or not (exported / "Unity-iPhone.xcodeproj").is_dir():
                    raise RuntimeError("Expected an exported test Xcode project under the artifact directory.")
                break
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
                raise TimeoutError("Unity test run exceeded " + str(args.run_timeout_seconds) + " seconds.")
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
    if args.export_only and exported is not None:
        summary = {"kind": "ios_test_player_export_only", "project": str(exported), "build_id": build_id,
                   "native_link_verified": False, "device_inference_verified": False}
        (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n")
        print(json.dumps(summary, indent=2))
        return 0
    # A locked screen can pause Unity frames while the native test finishes.
    # The Editor heartbeat timeout does not prove the installed Player stopped.
    # Recover only this APK's build id, and only while that device process is live.
    if adb and not result_path.exists():
        # A real transformer can spend several minutes in native CPU calls
        # after the Editor heartbeat times out. Keep the original run deadline
        # while the actual device process is alive; do not shorten it to 120s.
        late_deadline = deadline
        print("Editor ended without results; checking the still-running Android Player for this build.", flush=True)
        while time.monotonic() < late_deadline:
            pulled = subprocess.run([adb, "exec-out", "cat", device_result_path], capture_output=True, timeout=15)
            if pulled.returncode == 0 and pulled.stdout.startswith(b"<test-suite"):
                device_xml = ET.fromstring(pulled.stdout)
                if device_xml.get("mnn-build-id") == build_id:
                    result_path.write_bytes(pulled.stdout)
                    device_result_retrieved = True
                    break
            live = subprocess.run([adb, "shell", "pidof", "com.UnityTestRunner.UnityTestRunner"], capture_output=True, timeout=15)
            if live.returncode != 0 or not live.stdout.strip():
                break
            time.sleep(2)
    if not result_path.exists():
        print("No test results produced. Inspect " + str(output / "unity.log"), file=sys.stderr)
        return 1
    result = ET.parse(result_path).getroot()
    tests = list(result.iter("test-case"))
    native = [test for test in tests if ".PlayerInferenceTests." in test.get("fullname", "")]
    measurement_names = {"SmolLm_RealWeights", "Qwen_RealWeights", "SmolVlm_RealImage", "Lfm_RealAudio"}
    # Accept archived results from before the shared fixture refactor too.
    mobile_llm = [test for test in tests if ".MobileLlmPlayerTests." in test.get("fullname", "") or
                  (".PlayerRepresentativeTests." in test.get("fullname", "") and
                   test.get("name") in measurement_names)]
    representative_names = {"Qwen_TextAndTokenizer_ReturnOwnedResults", "SmolVlm_ImageChangesColorAnswer",
                            "Omni_CombinedInputsAndSpeechRejection", "Lfm_AudioTranscribesParis",
                            "Embedding_RetrievesRelevantDocument", "Reranker_RanksRelevantDocument"}
    representative = [test for test in tests if ".PlayerRepresentativeTests." in test.get("fullname", "") and
                      test.get("name") in representative_names]
    bert = [test for test in tests if ".BertEmbeddingPlayerTests." in test.get("fullname", "")]
    native_bert = [test for test in tests if ".NativeMtokEmbeddingPlayerTests." in test.get("fullname", "")]
    vision = [test for test in tests if ".YoloV8PlayerTests." in test.get("fullname", "")]
    paddle_text = [test for test in tests if ".PaddleTextPlayerTests." in test.get("fullname", "")]
    paddle_page = [test for test in tests if ".PaddleOcrPlayerTests." in test.get("fullname", "")]
    document_layout = [test for test in tests if ".DocumentLayoutPlayerTests." in test.get("fullname", "")]
    style = [test for test in tests if ".StyleTransferPlayerTests." in test.get("fullname", "")]
    text_decision = [test for test in tests if ".TextDecisionPlayerTests." in test.get("fullname", "")]
    depth = [test for test in tests if ".DepthPlayerTests." in test.get("fullname", "")]
    pose = [test for test in tests if ".PosePlayerTests." in test.get("fullname", "")]
    segmentation = [test for test in tests if ".SegmentationPlayerTests." in test.get("fullname", "")]
    asr = [test for test in tests if ".ZipformerPlayerTests." in test.get("fullname", "")]
    kws = [test for test in tests if ".KeywordSpotterPlayerTests." in test.get("fullname", "")]
    smart_turn = [test for test in tests if ".SmartTurnPlayerTests." in test.get("fullname", "")]
    silero = [test for test in tests if ".SileroVadPlayerTests." in test.get("fullname", "")]
    paraformer = [test for test in tests if ".ParaformerPlayerTests." in test.get("fullname", "")]
    qwen3_asr = [test for test in tests if ".Qwen3AsrPlayerTests." in test.get("fullname", "")]
    whisper = [test for test in tests if ".WhisperPlayerTests." in test.get("fullname", "")]
    sensevoice = [test for test in tests if ".SenseVoicePlayerTests." in test.get("fullname", "")]
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
        if test in mobile_llm and not args.require_mobile_llm:
            reason = test.findtext("reason/message", "")
            return reason.startswith(("This suite measures ARM64", "Stage actual mobile LLM", "No plan entries for "))
        if args.contract_only and test.get("name") == "AppleAbi_PublicLayoutsMatchNativeContract":
            return True  # The unsupported ABI layout is deliberately not executed.
        return (not args.depth_root and test in depth or not args.text_decision_root and test in text_decision or not args.pose_root and test in pose or not args.segmentation_root and test in segmentation or not args.style_root and test in style or not args.document_layout_root and test in document_layout or not args.model_root and test in representative or not args.bert_root and test in bert or
                not args.native_bert_root and test in native_bert or not args.asr_root and test in asr or not args.sensevoice_root and test in sensevoice or not args.whisper_root and test in whisper or not args.qwen3_asr_root and test in qwen3_asr or not args.paraformer_root and test in paraformer or not args.silero_root and test in silero or not args.smart_turn_root and test in smart_turn or not args.kws_root and test in kws or not args.vision_root and (test in vision or test in paddle_text or test in paddle_page))

    # Unity 2021 reports the suite as Skipped:Ignored when even one optional
    # test is ignored. Accept only the specific optional cases above.
    success = ((process.returncode == 0 or device_result_retrieved or local_player_result_retrieved) and len(tests) > 0 and
               int(result.get("failed", "0")) == 0 and all(acceptable(test) for test in tests))
    if actual_backend is not None:
        success = success and actual_backend.group(1) == args.backend
    elif not args.test_filter:
        success = False
    if args.mac_architecture in ("arm64", "x64") and (actual_arch is not None or not args.test_filter):
        expected_arch = {"arm64": "Arm64", "x64": "X64"}[args.mac_architecture]
        success = success and actual_arch is not None and actual_arch.group(1) == expected_arch
    if not args.contract_only and (native or not args.test_filter):
        numeric = [test for test in native if not test.get("name", "").startswith("NativeString_")]
        success = success and len(numeric) >= 4 and all(test.get("result") == "Passed" for test in numeric)
    mobile_llm_verified = len(mobile_llm) == 4 and all(test.get("result") == "Passed" for test in mobile_llm)
    if args.require_mobile_llm:
        success = success and mobile_llm_verified
    representative_verified = (bool(args.model_root) and len(representative) == 6 and
                               all(test.get("result") == "Passed" for test in representative))
    if args.model_root:
        success = success and representative_verified
    bert_verified = bool(args.bert_root) and len(bert) == 3 and all(test.get("result") == "Passed" for test in bert)
    if args.bert_root:
        success = success and bert_verified
    native_bert_verified = bool(args.native_bert_root) and len(native_bert) == 1 and all(test.get("result") == "Passed" for test in native_bert)
    if args.native_bert_root:
        success = success and native_bert_verified
    vision_verified = bool(args.vision_root) and len(vision) == 2 and all(test.get("result") == "Passed" for test in vision)
    if args.vision_root:
        success = success and vision_verified
    paddle_text_verified = bool(args.vision_root) and len(paddle_text) == 28 and all(test.get("result") == "Passed" for test in paddle_text)
    if args.vision_root:
        success = success and paddle_text_verified
    paddle_page_verified = bool(args.vision_root) and len(paddle_page) == 28 and all(test.get("result") == "Passed" for test in paddle_page)
    if args.vision_root:
        success = success and paddle_page_verified
    asr_verified = bool(args.asr_root) and len(asr) == 4 and all(test.get("result") == "Passed" for test in asr)
    if args.asr_root:
        success = success and asr_verified
    sensevoice_verified = bool(args.sensevoice_root) and len(sensevoice) == 2 and all(test.get("result") == "Passed" for test in sensevoice)
    if args.sensevoice_root:
        success = success and sensevoice_verified
    whisper_verified = bool(args.whisper_root) and len(whisper) == 6 and all(test.get("result") == "Passed" for test in whisper)
    if args.whisper_root:
        success = success and whisper_verified
    qwen3_asr_verified = bool(args.qwen3_asr_root) and len(qwen3_asr) == 4 and all(test.get("result") == "Passed" for test in qwen3_asr)
    if args.qwen3_asr_root:
        success = success and qwen3_asr_verified
    paraformer_verified = bool(args.paraformer_root and len(paraformer) == 1 and all(c.get("result") == "Passed" for c in paraformer))
    if args.paraformer_root:
        success = success and paraformer_verified
    silero_verified = bool(args.silero_root and len(silero) == 2 and all(c.get("result") == "Passed" for c in silero))
    if args.silero_root:
        success = success and silero_verified
    smart_turn_verified = bool(args.smart_turn_root and len(smart_turn) == 1 and all(c.get("result") == "Passed" for c in smart_turn))
    if args.smart_turn_root:
        success = success and smart_turn_verified
    kws_verified = bool(args.kws_root and len(kws) == 1 and all(test.get("result") == "Passed" for test in kws))
    if args.kws_root:
        success = success and kws_verified
    document_layout_verified = bool(args.document_layout_root and len(document_layout) == 2 and all(test.get("result") == "Passed" for test in document_layout))
    if args.document_layout_root:
        success = success and document_layout_verified
    style_verified = bool(args.style_root and len(style) == 2 and all(test.get("result") == "Passed" for test in style))
    if args.style_root:
        success = success and style_verified
    segmentation_verified = bool(args.segmentation_root and len(segmentation) == 2 and all(test.get("result") == "Passed" for test in segmentation))
    if args.segmentation_root:
        success = success and segmentation_verified
    depth_verified = bool(args.depth_root and len(depth) == 4 and all(test.get("result") == "Passed" for test in depth))
    if args.depth_root:
        success = success and depth_verified
    pose_verified = bool(args.pose_root and len(pose) == 4 and all(test.get("result") == "Passed" for test in pose))
    if args.pose_root:
        success = success and pose_verified
    text_decision_verified = bool(args.text_decision_root and len(text_decision) == 2 and all(test.get("result") == "Passed" for test in text_decision))
    if args.text_decision_root:
        success = success and text_decision_verified
    summary = {
        "english_laya_verified": bool(success and text_decision_verified),
        "depth_anything_v2_verified": bool(success and depth_verified),
        "movenet_pose_verified": bool(success and pose_verified),
        "platform": args.platform, "backend": args.backend,
        "test_filter": args.test_filter,
        "document_layout_verified": bool(success and document_layout_verified),
        "candy_style_verified": bool(success and style_verified),
        "deeplab_segmentation_verified": bool(success and segmentation_verified),
        "mac_architecture": args.mac_architecture, "contract_only": args.contract_only,
        "actual_backend": actual_backend.group(1) if actual_backend else None,
        "actual_architecture": actual_arch.group(1) if actual_arch else None,
        "runtime": runtime,
        "result": result.attrib, "process_exit_code": process.returncode,
        "device_result_retrieved": device_result_retrieved,
        "local_player_result_retrieved": local_player_result_retrieved,
        "build_id": build_id,
        "mobile_llm_four_models_verified": bool(success and mobile_llm_verified),
        "native_inference_tests": len(native),
        "platform_contract_verified": bool(success and args.contract_only),
        "affine_inference_verified": bool(success and not args.contract_only and len(native) >= 4),
        "auto_affine_backend": auto_device.group(1) if auto_device else None,
        "auto_affine_inference_verified": bool(success and auto_test is not None and auto_test.get("result") == "Passed"),
        "six_representative_models_verified": bool(success and representative_verified),
        "bert_embedding_models_verified": bool(success and bert_verified),
        "native_mtok_embedding_verified": bool(success and native_bert_verified),
        "yolov8_detection_verified": bool(success and vision_verified),
        "paddle_english_line_recognition_verified": bool(success and paddle_text_verified),
        "paddle_multilingual_line_recognition_verified": bool(success and paddle_text_verified),
        "paddle_multilingual_page_ocr_verified": bool(success and paddle_page_verified),
        "kws_verified": bool(success and kws_verified),
        "smart_turn_verified": bool(success and smart_turn_verified),
        "silero_vad_verified": bool(success and silero_verified),
        "paraformer_asr_verified": bool(success and paraformer_verified),
        "qwen3_asr_verified": bool(success and qwen3_asr_verified),
        "whisper_asr_verified": bool(success and whisper_verified),
        "sensevoice_asr_verified": bool(success and sensevoice_verified),
        "zipformer_asr_verified": bool(success and asr_verified),
        "all_models_verified": False,
    }
    (output / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    return 0 if success else 1


if __name__ == "__main__":
    sys.exit(main())
