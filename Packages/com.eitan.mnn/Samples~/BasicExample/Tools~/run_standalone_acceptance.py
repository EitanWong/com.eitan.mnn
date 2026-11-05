#!/usr/bin/env python3
"""Run explicit installed-model acceptance or a parameterized soak in an existing isolated Unity project.

The project must import the whole BasicExample sample. No downloads, model copies,
native modifications, or project edits. Output is confined to this repository's
TestArtifacts~. Simulated rendering does not prove real microphone/speaker quality.
"""

import argparse
import json
from pathlib import Path
import os
import subprocess
import sys
import xml.etree.ElementTree as ET


def main():
    root = Path(__file__).resolve().parents[5]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", type=Path, required=True)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--asr", type=Path, required=True)
    parser.add_argument("--tts", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=root / "TestArtifacts~/SiriAICompletion/Standalone/LocalModels")
    parser.add_argument("--seconds", type=int, default=0, help="0: one real-model turn; 3600: one hour of repeated sessions.")
    parser.add_argument("--turns-per-session", type=int, default=5)
    parser.add_argument("--max-reply-tokens", type=int, default=32)
    parser.add_argument("--max-turn-seconds", type=int, default=300)
    parser.add_argument("--backend", default="CPU")
    parser.add_argument("--rss-growth-mib", type=int, default=0, help="0 reports measurements without a memory-budget verdict.")
    parser.add_argument("--managed-growth-mib", type=int, default=0)
    parser.add_argument("--prepare-only", action="store_true", help="Validate inputs and write the invocation; do not launch Unity.")
    args = parser.parse_args()
    output, project = args.output.resolve(), args.project.resolve()
    artifact_root = (root / "TestArtifacts~").resolve()
    if artifact_root not in output.parents or artifact_root not in project.parents:
        parser.error("Use an isolated project and output underneath this repository's TestArtifacts~/.")
    for path in (args.unity, project / "ProjectSettings/ProjectVersion.txt"):
        if not path.is_file():
            parser.error("Required file is missing: " + str(path))
    for path in (args.model, args.asr, args.tts):
        if not path.is_dir():
            parser.error("Installed model directory is missing: " + str(path))
    tests = [path for path in (project / "Assets").rglob("*.asmdef")
             if json.loads(path.read_text()).get("name") == "MNN.Unity.Samples.BasicExample.Tests"]
    if len(tests) != 1:
        parser.error("Import exactly one complete BasicExample sample, including Tests/Runtime, into the isolated project's Assets/.")
    if not 0 <= args.seconds <= 86400 or not 1 <= args.turns_per_session <= 1000 or not 1 <= args.max_reply_tokens <= 8192:
        parser.error("Choose 0–86400 seconds, 1–1000 turns per session and 1–8192 reply tokens.")
    if not 1 <= args.max_turn_seconds <= 1800 or not 0 <= args.rss_growth_mib <= 65536 or not 0 <= args.managed_growth_mib <= 65536:
        parser.error("Choose a bounded turn deadline and nonnegative bounded memory budgets.")
    output.mkdir(parents=True, exist_ok=True)
    environment = {
        "MNN_STANDALONE_MODEL": str(args.model.resolve()), "MNN_STANDALONE_ASR": str(args.asr.resolve()),
        "MNN_STANDALONE_TTS": str(args.tts.resolve()), "MNN_STANDALONE_ARTIFACTS": str(output),
        "MNN_STANDALONE_ARTIFACT_ROOT": str(artifact_root),
        "MNN_STANDALONE_SOAK_SECONDS": str(max(1, args.seconds)), "MNN_STANDALONE_TURNS_PER_SESSION": str(args.turns_per_session),
        "MNN_STANDALONE_MAX_REPLY_TOKENS": str(args.max_reply_tokens), "MNN_STANDALONE_BACKEND": args.backend,
        "MNN_STANDALONE_MAX_TURN_SECONDS": str(args.max_turn_seconds),
        "MNN_STANDALONE_RSS_GROWTH_MIB": str(args.rss_growth_mib), "MNN_STANDALONE_MANAGED_GROWTH_MIB": str(args.managed_growth_mib),
    }
    method = "LocalTextProducesSpeechAndPresentedReply" if args.seconds == 0 else "RepeatedSessionsMeasureLocalModelSoak"
    command = [str(args.unity.resolve()), "-batchmode", "-nographics", "-projectPath", str(project),
               "-runTests", "-testPlatform", "PlayMode", "-testFilter", "MNN.Examples.Tests.MNNStandaloneLocalModelTests." + method,
               "-testResults", str(output / "results.xml"), "-logFile", str(output / "unity.log")]
    invocation = {"command": command, "environment": environment, "scope": "Real local text/TTS; simulated audio render, ASR preparation only.",
                  "memory_budgets_supplied": bool(args.rss_growth_mib or args.managed_growth_mib), "prepare_only": args.prepare_only}
    (output / "invocation.json").write_text(json.dumps(invocation, indent=2) + "\n")
    print(json.dumps(invocation, indent=2), flush=True)
    if args.prepare_only:
        return 0
    for name in ("results.xml", "summary.json"):
        (output / name).unlink(missing_ok=True)
    result = subprocess.run(command, cwd=root, env={**os.environ, **environment}, timeout=args.seconds + 1800,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    (output / "process.log").write_text(result.stdout)
    if result.returncode != 0:
        return result.returncode
    xml = output / "results.xml"
    if not xml.is_file():
        print("Unity returned without test results.", file=sys.stderr)
        return 1
    cases = [case for case in ET.parse(xml).getroot().iter("test-case") if method in case.get("fullname", "")]
    if len(cases) != 1 or cases[0].get("result") != "Passed":
        print("Expected one passing explicitly selected local-model test; inspect results.xml and unity.log.", file=sys.stderr)
        return 1
    summary = output / "summary.json"
    if not summary.is_file() or json.loads(summary.read_text()).get("status") != "passed":
        print("The test did not write a passing measurement summary.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
