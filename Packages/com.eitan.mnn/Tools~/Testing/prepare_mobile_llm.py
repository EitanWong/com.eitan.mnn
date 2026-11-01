#!/usr/bin/env python3
"""Stage existing real LLM weights/inputs and a mobile validation plan; no downloads.

Copy the resulting directory into Application.persistentDataPath/mobile-llm.
Android uses adb push; iOS uses devicectl appDataContainer Documents/mobile-llm.
The optional weights are excluded from the test Player itself.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil


MODELS = ["SmolLM2-135M-Instruct-MNN", "Qwen3.5-0.8B-MNN",
          "SmolVLM-256M-Instruct-MNN", "LFM2.5-Audio-1.5B-MNN"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--models-root", type=Path, required=True)
    parser.add_argument("--fixtures", type=Path, required=True,
                        help="Existing red.png, blue.png and speech.wav (spoken Paris fixture).")
    parser.add_argument("--backend", choices=["CPU", "Metal"], default="CPU")
    parser.add_argument("--run-id", required=True, help="Unique ID checked against device reports.")
    parser.add_argument("--output", type=Path,
                        default=Path("TestArtifacts~/MobileLlmDirectCpp/DeviceData/mobile-llm"))
    parser.add_argument("--plan-only", action="store_true", help="Keep already staged models; only replace plan.json.")
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[4]
    output = args.output.resolve()
    if repository / "TestArtifacts~" not in output.parents:
        parser.error("Keep staged data under this repository's TestArtifacts~/.")
    for model in MODELS:
        for filename in ["config.json", "llm_config.json", "llm.mnn", "llm.mnn.weight", "tokenizer.txt"]:
            if not (args.models_root / model / filename).is_file():
                parser.error("Missing existing model asset: " + str(args.models_root / model / filename))
    for name in ["red.png", "blue.png", "speech.wav"]:
        if not (args.fixtures / name).is_file():
            parser.error("Missing existing input: " + str(args.fixtures / name))
    output.mkdir(parents=True, exist_ok=True)
    manifest = []
    if not args.plan_only:
        for model in MODELS:
            destination = output / "models" / model
            destination.mkdir(parents=True, exist_ok=True)
            for source in sorted((args.models_root / model).iterdir()):
                if (not source.is_file() or source.name.startswith(".") or
                        source.name.endswith((".meta", ".mnn.json")) or
                        source.name in ("README.md", "LICENSE")):
                    continue
                target = destination / source.name
                shutil.copy2(source, target)
                manifest.append({"path": str(target.relative_to(output)), "bytes": target.stat().st_size,
                                 "sha256": hashlib.sha256(target.read_bytes()).hexdigest()})
        fixtures = output / "fixtures"
        fixtures.mkdir(exist_ok=True)
        for name in ["red.png", "blue.png", "speech.wav"]:
            target = fixtures / name
            shutil.copy2(args.fixtures / name, target)
            manifest.append({"path": str(target.relative_to(output)), "bytes": target.stat().st_size,
                             "sha256": hashlib.sha256(target.read_bytes()).hexdigest()})
        (output.parent / "assets-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    cases = []

    def add(identifier, model, prompt, expected, limit=32, repeats=2, **kwargs):
        cases.append(dict(id=identifier, model=model, backend=args.backend, prompt=prompt,
                          expected=expected, limit=limit, repeats=repeats, **kwargs))

    add("smol-capital", MODELS[0], "What is the capital of France? Answer with just the city name.", "Paris", 24)
    add("qwen-zh", MODELS[1], "中国的首都是哪里？只回答城市名称。", "北京")
    add("qwen-math", MODELS[1], "What is 1 + 1? Answer with just the number.", "2", 16)
    add("qwen-long", MODELS[1], "记录：天空是蓝色的，草地是绿色的。\n" * 32 + "中国的首都是哪里？只回答城市名称。", "北京", repeats=1)
    add("qwen-decode", MODELS[1], "Count from 1 to 20, separated by single spaces. Output only the numbers.",
        " ".join(str(i) for i in range(1, 21)), 96)
    add("qwen-stream", MODELS[1], "My name is Alice. What is my name? Answer only the name.", "Alice", 24, streaming=True)
    for color in ["red", "blue"]:
        add("smolvlm-" + color, MODELS[2], "What is the main color in this image? Answer in one word.",
            color, 24, image="fixtures/" + color + ".png")
    add("lfm-audio", MODELS[3], "Transcribe the spoken sentence in English.", "Paris", 64, audio="fixtures/speech.wav")
    (output / "plan.json").write_text(json.dumps({"runId": args.run_id, "cases": cases}, ensure_ascii=False, indent=2) + "\n")
    print("Prepared " + str(output) + "; 9 cases / 17 generations, backend=" + args.backend)


if __name__ == "__main__":
    main()
