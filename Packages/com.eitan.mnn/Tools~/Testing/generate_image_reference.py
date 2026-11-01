#!/usr/bin/env python3
"""Generate independent image references from existing pinned source files.

No downloads. Requires Pillow/numpy and ONNX Runtime (Candy/MoveNet) or
ai-edge-litert (DeepLab/MoveNet comparison) in the invoking Python environment.
The runtime package uses MNN only.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image


PROFILES = {
    "candy": {"folder": "DefTruth--FastStyleTransfer-Candy", "model": "style-candy-8.onnx",
              "sha": "e3c6f442e12014c5fec12b4307c14ca303df185cda19f67522afeef40c4d22b8",
              "artifact": "StyleTransfer", "photo": "TestArtifacts~/TaskIntegration/StyleTransfer/style.jpg",
              "photo_sha": "f1d583fd322c68de4d116408dcfcee3a137b53206fe7251313ec02ca8aa6d27c",
              "size": 224, "input": "reference-input.f32", "output": "reference-output.f32"},
    "deeplab": {"folder": "TensorFlow--DeepLabV3-MobileNetV2", "model": "deeplabv3_257_mv_gpu.tflite",
                "sha": "68a539782c2c6a72f8aac3724600124a85ed977162b44e84cbae5db717c933c6",
                "artifact": "Segmentation", "photo": "TestArtifacts~/ModelDiscovery/Vision/bus.jpg",
                "photo_sha": "c02019c4979c191eb739ddd944445ef408dad5679acab6fd520ef9d434bfbc63",
                "size": 257, "input": "input.f32", "output": "output.f32"},
    "movenet": {"folder": "TensorFlow--MoveNet-SinglePose-Lightning", "model": "movenet-lightning.onnx",
                "sha": "1ad4f8d6c2f776a9967db3993c9ca740bc350104f9d37c151dc183fc29a464ad",
                "artifact": "Pose", "photo": "TestArtifacts~/ModelDiscovery/Vision/bus.jpg",
                "photo_sha": "c02019c4979c191eb739ddd944445ef408dad5679acab6fd520ef9d434bfbc63", "size": 192},
}


def generate_pose(profile, model, photo, output):
    import onnxruntime as ort
    from ai_edge_litert.interpreter import Interpreter
    from importlib.metadata import version
    from PIL import ImageDraw
    official = model.parent / "movenet-lightning-f16.tflite"
    official_sha = "0fac2226112d0371903ca86e3853cec24ef603a0b2f96f589b180f0ebdd135ab"
    if not official.is_file() or hashlib.sha256(official.read_bytes()).hexdigest() != official_sha:
        raise ValueError("Place the official Kaggle SinglePose Lightning float16 v1 4.tflite as " + str(official))
    session = ort.InferenceSession(str(model), providers=["CPUExecutionProvider"])
    lite = Interpreter(model_path=str(official)); lite.allocate_tensors()
    original = Image.open(photo).convert("RGB")
    for name, box in (("person-a", (45, 380, 250, 930)), ("person-b", (205, 390, 370, 890))):
        source = original.crop(box)
        w, h = source.size
        longer = max(w, h)
        nw, nh = max(1, (w * 192 + longer // 2) // longer), max(1, (h * 192 + longer // 2) // longer)
        left, top = (192 - nw) // 2, (192 - nh) // 2
        canvas = Image.new("RGB", (192, 192))
        # The shared managed sampler rounds each separable pass to uint8.
        # Pillow 13's combined resize can retain precision between passes;
        # request the two passes explicitly to pin the public pixel contract.
        resized = source.resize((nw, h), Image.Resampling.BILINEAR).resize((nw, nh), Image.Resampling.BILINEAR)
        canvas.paste(resized, (left, top))
        values = np.asarray(canvas, dtype=np.int32)[None]
        result = session.run(["output_0"], {"input": values})[0]
        lite.set_tensor(lite.get_input_details()[0]["index"], values.astype(np.uint8)); lite.invoke()
        official_result = lite.get_tensor(lite.get_output_details()[0]["index"])
        if result.shape != (1, 1, 17, 3) or not np.isfinite(result).all():
            raise ValueError("Invalid independent pose output.")
        # Different float32/float16 publisher exports can choose a different
        # heatmap cell and score. This is a secondary geometry comparison, not
        # proof of identical weights or confidence. MNN correctness is measured
        # strictly against this exact ONNX source by the native integration tests.
        if np.max(abs(result[..., :2] - official_result[..., :2])) * 192 >= 1:
            raise ValueError("ONNX/official TFLite joint geometry differs by at least one input pixel.")
        files = {"source.rgb": source.tobytes(), "input.i32": values.astype("<i4").tobytes(),
                 "output.f32": result.astype("<f4").tobytes(), "official-tflite-output.f32": official_result.astype("<f4").tobytes()}
        directory = output / name; directory.mkdir(parents=True, exist_ok=True)
        for filename, data in files.items(): (directory / filename).write_bytes(data)
        source.save(directory / "source.png")
        points = result[0, 0].copy()
        points[:, 1] = (points[:, 1] * 192 - left) * w / nw
        points[:, 0] = (points[:, 0] * 192 - top) * h / nh
        draw = ImageDraw.Draw(source)
        for y, x, score in points:
            if score >= .2 and 0 <= x < w and 0 <= y < h:
                draw.ellipse((x - 3, y - 3, x + 3, y + 3), fill=(255, 0, 0))
        source.save(directory / "reference.png")
        manifest = {"profile": "movenet", "crop": box, "width": w, "height": h,
                    "resized_width": nw, "resized_height": nh, "left": left, "top": top,
                    "source_model_sha256": profile["sha"], "official_tflite_sha256": official_sha,
                    "photo_sha256": profile["photo_sha"], "reference_runtime": "ONNX Runtime " + ort.__version__,
                    "comparison_runtime": "ai-edge-litert " + version("ai-edge-litert"),
                    "onnx_vs_official_tflite_max": float(np.max(abs(result - official_result))),
                    "onnx_vs_official_tflite_mean": float(np.mean(abs(result - official_result))),
                    "onnx_vs_official_tflite_coordinate_max_input_pixels": float(np.max(abs(result[..., :2] - official_result[..., :2])) * 192),
                    "onnx_vs_official_tflite_score_max": float(np.max(abs(result[..., 2] - official_result[..., 2]))),
                    "files": {filename: hashlib.sha256(data).hexdigest() for filename, data in files.items()},
                    "preprocessing": "Pillow RGB antialiased bilinear horizontal/vertical passes, each uint8; rounded aspect-preserving 192 black letterbox; NHWC int32 raw 0..255; not TensorFlow resize_with_pad",
                    "keypoints_y_x_score": points.tolist()}
        (directory / "reference.json").write_text(json.dumps(manifest, indent=2) + "\n")
        print(json.dumps(manifest, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--profile", choices=PROFILES, default="candy")
    args = parser.parse_args()
    profile = PROFILES[args.profile]
    root = Path(__file__).resolve().parents[4]
    output = root / "TestArtifacts~/TaskIntegration" / profile["artifact"]
    model = root / "Assets/StreamingAssets/MNN/Models" / profile["folder"] / profile["model"]
    photo = root / profile["photo"]
    for path, digest in ((model, profile["sha"]), (photo, profile["photo_sha"])):
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError("Missing or changed pinned source: " + str(path))
    if args.profile == "movenet":
        generate_pose(profile, model, photo, output)
        return
    source = Image.open(photo).convert("RGB")
    size = profile["size"]
    image = source.resize((size, size), Image.Resampling.BILINEAR)
    pixels = np.asarray(image, dtype=np.float32)
    if args.profile == "candy":
        import onnxruntime as ort
        values = pixels.transpose(2, 0, 1)[None]
        options = ort.SessionOptions()
        options.log_severity_level = 3
        session = ort.InferenceSession(str(model), options, providers=["CPUExecutionProvider"])
        result = session.run(["output1"], {"input1": values})[0]
        if result.shape != (1, 3, size, size):
            raise ValueError("Independent ONNX output does not match the Candy contract.")
        rgb = np.rint(np.clip(result[0].transpose(1, 2, 0), 0, 255)).astype(np.uint8)
        runtime = "ONNX Runtime " + ort.__version__
        normalization = "CHW RGB float32 raw0-255"
    else:
        from ai_edge_litert.interpreter import Interpreter
        from importlib.metadata import version
        values = ((pixels - 127.5) / 127.5)[None]
        session = Interpreter(model_path=str(model))
        session.allocate_tensors()
        session.set_tensor(session.get_input_details()[0]["index"], values)
        session.invoke()
        result = session.get_tensor(session.get_output_details()[0]["index"])
        if result.shape != (1, size, size, 21):
            raise ValueError("Independent LiteRT output does not match the DeepLab contract.")
        classes = result[0].argmax(-1).astype(np.uint8)
        palette = np.zeros((21, 3), dtype=np.uint8)
        for c in range(21):
            for bit in range(3):
                for channel in range(3):
                    palette[c, channel] |= ((c >> (3 * bit + channel)) & 1) << (7 - bit)
        rgb = palette[classes]
        runtime = "ai-edge-litert " + version("ai-edge-litert")
        normalization = "NHWC RGB float32 (pixel-127.5)/127.5"
    if not np.isfinite(result).all():
        raise ValueError("Non-finite independent output.")
    files = {"source.rgb": source.tobytes(), profile["input"]: values.astype("<f4").tobytes(),
             profile["output"]: result.astype("<f4").tobytes()}
    if args.profile == "deeplab":
        files["classes.u8"] = classes.tobytes()
    output.mkdir(parents=True, exist_ok=True)
    for name, data in files.items():
        (output / name).write_bytes(data)
    Image.fromarray(rgb).save(output / "reference.png")
    manifest = {"profile": args.profile, "width": source.width, "height": source.height,
                "files": {name: hashlib.sha256(data).hexdigest() for name, data in files.items()},
                "source_model_sha256": profile["sha"], "photo_sha256": profile["photo_sha"],
                "reference_runtime": runtime, "input_shape": list(values.shape), "output_shape": list(result.shape),
                "preprocessing": "Pillow RGB bilinear antialiased resize" + str(size) + "; " + normalization}
    (output / "reference.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
