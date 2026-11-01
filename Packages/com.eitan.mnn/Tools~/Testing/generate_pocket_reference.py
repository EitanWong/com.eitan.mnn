#!/usr/bin/env python3
"""Create independent Pocket tokenizer/ONNX references from existing local assets.

Requires numpy, onnxruntime and sentencepiece in the caller's Python environment.
Does not download anything. The two ONNX graphs are diagnostic references only;
the package's Pocket runtime and fixed install plan use MNN exclusively.
"""
import argparse
import json
import math
from pathlib import Path
import random
import wave

import numpy as np
import onnxruntime as ort
import sentencepiece as sp


def resample(samples, source, target):
    """Independent implementation of the existing managed cosine-windowed sinc."""
    divisor = math.gcd(source, target)
    original, following = source // divisor, target // divisor
    cutoff = min(1, target / source) * .99
    width = math.ceil(6 / cutoff)
    size = 2 * width + 2
    offsets, kernels = [], []
    for phase in range(following):
        center = phase * original / following
        offset = math.floor(center) - width
        t = (offset + np.arange(size) - center) * cutoff
        kernel = np.where(np.abs(t) < 6, np.sinc(t) * np.cos(t * np.pi / 12) ** 2 * cutoff, 0)
        offsets.append(offset)
        kernels.append(kernel.astype(np.float32))
    count = (len(samples) * target + source - 1) // source
    result = np.zeros(count, np.float32)
    for phase in range(following):
        positions = np.arange(phase, count, following)
        starts = positions // following * original + offsets[phase]
        sums = np.zeros(len(positions), np.float64)
        for k, coefficient in enumerate(kernels[phase]):
            indices = starts + k
            valid = (indices >= 0) & (indices < len(samples))
            sums[valid] += samples[indices[valid]].astype(np.float64) * float(coefficient)
        result[positions] = sums
    return result


def main():
    project = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--models", type=Path, default=project / "Assets/StreamingAssets/MNN/Models/Pocket-TTS-MNN")
    parser.add_argument("--output", type=Path, default=project / "TestArtifacts~/ModelDiscovery/Tts")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    tokenizer = sp.SentencePieceProcessor(model_file=str(args.models / "tokenizer.model"))
    texts = ["Hello world.", "The capital of France is Paris.", " a  b ", "  Spaces   remain.",
             "Café déjà vu!", "0123456789", "é😊東京", "hello\tthere\nworld", "<s> text </s>", "test—word", "▁marker"]
    randomizer = random.Random(42)
    words = "The capital of France is Paris hello world speech model music beautiful testing sentence short English".split()
    for _ in range(150):
        texts.append(" ".join(randomizer.choice(words) for _ in range(randomizer.randint(1, 15))) + randomizer.choice([".", "?", "!", ";"]))
    (args.output / "pocket-tokenizer-reference.json").write_text(json.dumps({"cases": [
        {"text": text, "ids": tokenizer.encode(text)} for text in texts]}, ensure_ascii=False, indent=2), encoding="utf-8")
    options = ort.SessionOptions()
    options.intra_op_num_threads = 4
    metadata = json.loads((args.models / "bundle.json").read_text())
    state = {entry["input_name"]: np.full(entry["shape"], np.nan if entry["fill"] == "nan" else 0,
             np.int64 if entry["dtype"] == "int64" else np.float32) for entry in metadata["flow_lm_state_manifest"]}
    session = ort.InferenceSession(str(args.models / "flow_lm_main_int8.onnx"), options, providers=["CPUExecutionProvider"])
    outputs = session.run(None, {"sequence": np.zeros((1, 0, 32), np.float32), "text_embeddings": np.zeros((1, 4, 1024), np.float32), **state})
    for layer in range(6):
        outputs[2 + layer * 3].tofile(args.output / f"pocket-reference-priming-state{layer}.f32")
    with wave.open(str(args.models / "reference_sample.wav")) as recording:
        if recording.getnchannels() != 1 or recording.getsampwidth() != 2:
            raise ValueError("Expected the publisher's mono PCM16 reference WAV.")
        samples = np.frombuffer(recording.readframes(-1), np.int16).astype(np.float32) / 32768
        rate = recording.getframerate()
    samples = resample(samples, rate, 24000) if rate != 24000 else samples
    encoder = ort.InferenceSession(str(args.models / "mimi_encoder.onnx"), options, providers=["CPUExecutionProvider"])
    embeddings = encoder.run(None, {"audio": samples.reshape(1, 1, -1)})[0]
    if not np.isfinite(embeddings).all():
        raise ValueError("Non-finite reference embeddings.")
    embeddings.tofile(args.output / "pocket-reference-voice-embeddings.f32")
    report = {"tokenizerCases": len(texts), "referenceEmbeddingShape": list(embeddings.shape),
              "onnxRuntimeUsedOnlyForIndependentReference": True}
    managed_path = args.output / "pocket-voice-embeddings.f32"
    if managed_path.is_file():
        managed = np.fromfile(managed_path, np.float32).reshape(embeddings.shape)
        report["encoderMaxError"] = float(np.max(np.abs(embeddings - managed)))
        report["encoderRmse"] = float(np.sqrt(np.mean((embeddings - managed) ** 2)))
        report["encoderCorrelation"] = float(np.corrcoef(embeddings.ravel(), managed.ravel())[0, 1])
    (args.output / "pocket-independent-reference.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report))


if __name__ == "__main__":
    main()
