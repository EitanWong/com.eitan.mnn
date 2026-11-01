#!/usr/bin/env python3
"""Generate independent depth references with the original pinned ONNX graph.

Requires existing numpy, Pillow, onnxruntime packages and local input images.
Never downloads models or dependencies. Output stays under TestArtifacts~/.
"""
import argparse
import hashlib
import json
from pathlib import Path
import time

import numpy as np
import onnxruntime as ort
from PIL import Image


def main():
    root = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--image", action="append", required=True, metavar="NAME=PATH")
    parser.add_argument("--output", type=Path, default=root / "TestArtifacts~/TaskIntegration/Depth")
    args = parser.parse_args()
    output = args.output.resolve()
    if root / "TestArtifacts~" not in output.parents:
        parser.error("References must be written below this project's TestArtifacts~/.")
    model = root / "Assets/StreamingAssets/MNN/Models/DepthAnything--V2-Small/depth-anything-v2-small.onnx"
    model_sha = hashlib.sha256(model.read_bytes()).hexdigest()
    if model_sha != "afb6a5c28f3b6bf1618c6e43f02073ef9dfdc70e937502d51603e57b0a1df10c":
        parser.error("Original ONNX graph does not match the pinned publisher revision.")
    options = ort.SessionOptions()
    options.intra_op_num_threads = 4
    session = ort.InferenceSession(str(model), options, providers=["CPUExecutionProvider"])
    for entry in args.image:
        name, separator, path = entry.partition("=")
        if not separator or not name or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789-_" for c in name):
            parser.error("Use a lowercase NAME=PATH without directory separators in NAME.")
        original = Image.open(path)
        width, height = original.size
        if width * height > 16777216:
            parser.error("Input exceeds the profile limit of 16,777,216 source pixels.")
        source = original.convert("RGB")
        sx, sy = 518 / width, 518 / height
        scale = sx if abs(1 - sx) < abs(1 - sy) else sy
        nw, nh = round(width * scale / 14) * 14, round(height * scale / 14) * 14
        if nw < 14 or nh < 14 or nw // 14 * (nh // 14) > 3072:
            parser.error("Input exceeds the prepared profile's attention geometry limit.")
        resized = source.resize((nw, height), Image.Resampling.BICUBIC).resize((nw, nh), Image.Resampling.BICUBIC)
        if not np.array_equal(np.asarray(resized), np.asarray(source.resize((nw, nh), Image.Resampling.BICUBIC))):
            raise ValueError("Pillow's combined and separable bicubic passes differ for this fixture.")
        values = (np.asarray(resized, dtype=np.float32) * np.float32(1 / 255) - np.array([.485, .456, .406], np.float32)) / np.array([.229, .224, .225], np.float32)
        values = values.transpose(2, 0, 1)[None].copy()
        start = time.monotonic()
        depth = session.run(["predicted_depth"], {"pixel_values": values})[0]
        seconds = time.monotonic() - start
        if depth.shape != (1, nh, nw) or not np.isfinite(depth).all() or depth.min() < 0:
            raise ValueError("Unexpected original ONNX output.")
        destination = output / name
        destination.mkdir(parents=True, exist_ok=True)
        files = {"source.rgb": source.tobytes(), "input.f32": values.astype("<f4").tobytes(), "output.f32": depth.astype("<f4").tobytes()}
        for filename, data in files.items():
            (destination / filename).write_bytes(data)
        source.save(destination / "source.png")
        span = float(depth.max() - depth.min())
        preview = np.zeros((nh, nw), np.uint8) if span == 0 else np.rint((depth[0] - depth.min()) / span * 255).astype(np.uint8)
        Image.fromarray(preview).save(destination / "reference.png")
        report = {"name": name, "width": width, "height": height, "input_width": nw, "input_height": nh,
                  "output_shape": list(depth.shape), "min": float(depth.min()), "max": float(depth.max()),
                  "ort_seconds": seconds, "reference_runtime": ort.__version__, "source_onnx_sha256": model_sha,
                  "files": {filename: hashlib.sha256(data).hexdigest() for filename, data in files.items()},
                  "preprocessing": "DPT minimal scale, multiple 14 ties-to-even; Pillow uint8 antialiased bicubic RGB; CHW float32 ImageNet normalization",
                  "units": "relative inverse depth; larger means closer; no metric unit"}
        (destination / "reference.json").write_text(json.dumps(report, indent=2) + "\n")
        print(json.dumps(report))


if __name__ == "__main__":
    main()
