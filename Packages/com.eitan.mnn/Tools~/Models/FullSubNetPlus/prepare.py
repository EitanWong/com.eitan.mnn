#!/usr/bin/env python3
"""Reproduce pinned FullSubNet+ FP32 CPU and partitioned Metal graphs locally.

Requires the original upstream checkout/checkpoint, PyTorch, ONNX, ONNX Runtime
and official MNNConvert. No downloads or native source modifications are made.
The shared FullSubNet recurrent exporter retains the original LSTM arithmetic.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tomllib

sys.dont_write_bytecode = True

REVISION = "b115416d6e2a983c3724fc77d0cdc3d8896a2855"
CHECKPOINT_SHA256 = "cb628b2f3fcf8e078dd720ff04ae9fe14ef9b605ace349e9e8bf1bc1dee15032"
INPUTS = ["magnitude", "real", "imag"]


def helper(name: str, path: Path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def load_model(source: Path, checkpoint: Path):
    revision = subprocess.check_output(["git", "-C", str(source), "rev-parse", "HEAD"], text=True).strip()
    if revision != REVISION or hashlib.sha256(checkpoint.read_bytes()).hexdigest() != CHECKPOINT_SHA256:
        raise ValueError("source revision/checkpoint does not match pinned FullSubNet+")
    import torch
    sys.path[:0] = [str(source), str(source / "speech_enhance")]
    from fullsubnet_plus.model.fullsubnet_plus import FullSubNet_Plus
    config = tomllib.loads((source / "config/inference.toml").read_text())
    model = FullSubNet_Plus(**config["model"]["args"]).eval()
    model.load_state_dict(torch.load(checkpoint, map_location="cpu", weights_only=False)["model"], strict=True)
    return model


def reference_inputs(directory: Path | None):
    import numpy as np
    import torch
    if directory is not None:
        for row in json.loads((directory / "cases.json").read_text())["cases"]:
            yield row["case"], [torch.from_numpy(np.fromfile(directory / (row["case"]+"-"+key+".f32"), "<f4")
                .reshape(1, 1, 257, row["frames"]).copy()) for key in INPUTS]
        return
    from audio_zen.acoustics.feature import stft
    generator = torch.Generator().manual_seed(20261030)
    for count in [1792, 4096, 16037, 59444, 128013]:
        spectrum = stft(torch.randn(1, count, generator=generator)*.05, 512, 256, 512)
        yield str(count), [value[:, None] for value in [spectrum.abs(), spectrum.real, spectrum.imag]]


def export_graphs(model, work: Path, reference: Path | None) -> dict:
    import numpy as np
    import onnx
    import onnxruntime as ort
    import torch
    shared = helper("fullsubnet_export", Path(__file__).resolve().parents[1] / "FullSubNet/prepare.py")
    cases = list(reference_inputs(reference))
    captured = {}
    hook = model.sb_model.register_forward_pre_hook(lambda module, args: captured.update(features=args[0].detach().clone()))
    originals = {}
    with torch.no_grad():
        for name, inputs in cases:
            originals[name] = (model(*inputs).clone(), captured["features"].clone())
    hook.remove()
    model.unfold = lambda values, num_neighbor: shared.gather_frequency(values, num_neighbor)
    with torch.no_grad():
        for name, inputs in cases:
            if not torch.equal(model(*inputs), originals[name][0]):
                raise ValueError("frequency Gather differs from original Unfold: "+name)
    # Use the production export shape; all time dimensions remain dynamic.
    sample = next((inputs for _, inputs in cases if inputs[0].shape[-1] == 63), cases[0][1])
    dynamic = work / "fullsubnetplus_dynamic.onnx"
    torch.onnx.export(model, tuple(sample), dynamic, input_names=INPUTS, output_names=["crm_mask"],
        opset_version=17, dynamo=False, dynamic_axes={**{key: {3: "frames"} for key in INPUTS}, "crm_mask": {3: "frames"}})
    front = work / "fullsubnetplus_front.onnx"
    onnx.utils.extract_model(str(dynamic), str(front), INPUTS, ["/Reshape_18_output_0"])
    graph = onnx.load(front)
    previous = graph.graph.output[0].name
    graph.graph.output[0].name = "sb_features"
    graph.graph.node.append(onnx.helper.make_node("Identity", [previous], ["sb_features"], name="sb_features"))
    onnx.save(graph, front)
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    session = ort.InferenceSession(str(front), options, providers=["CPUExecutionProvider"])
    block = shared.make_block(model.sb_model)
    rows = []
    with torch.no_grad():
        for name, inputs in cases:
            mask, features = originals[name]
            actual = session.run(None, {key: value.numpy() for key, value in zip(INPUTS, inputs)})[0]
            if actual.shape != tuple(features.shape):
                raise ValueError("unexpected subband feature shape: "+name)
            h = torch.zeros(2, 257, 384)
            c = torch.zeros_like(h)
            outputs = []
            for start in range(0, features.shape[-1], 32):
                x = features[:, :, start:start+32].permute(0, 2, 1)
                count = x.shape[1]
                y, h, c = block(torch.nn.functional.pad(x, (0, 0, 0, 32-count)), h, c)
                outputs.append(y[:, :count])
            projected = torch.cat(outputs, dim=1).permute(2, 0, 1).unsqueeze(0)[..., 2:]
            error = float((projected-mask).abs().max())
            if error >= 1e-4:
                raise ValueError("primitive block differs from original LSTM: "+name)
            rows.append({"case": name, "primitive_original_mask_max_abs": error,
                "front_onnx_nrmse": float(np.linalg.norm(actual-features.numpy()) / max(np.linalg.norm(features.numpy()), 1e-30))})
        torch.manual_seed(20261030)
        x = torch.rand(257, 32, 34)
        h = torch.rand(2, 257, 384)*.01
        c = torch.rand_like(h)*.01
        sequence, (hr, cr) = model.sb_model.sequence_model(x, (h, c))
        references = [model.sb_model.fc_output_layer(sequence), hr, cr]
        maximum = max(float((a-b).abs().max()) for a, b in zip(block(x, h, c), references))
        if maximum >= 1e-4:
            raise ValueError("primitive block recurrent state differs from original LSTM")
    torch.onnx.export(block, (x, h, c), work / "fullsubnetplus_subband_32.onnx",
        input_names=["features", "hidden", "cell"], output_names=["projected", "hidden_out", "cell_out"],
        opset_version=17, dynamo=False)
    return {"gather_bit_identical": True, "primitive_original_state_max_abs": maximum, "cases": rows}


def convert(converter: Path, work: Path, keep: bool) -> dict:
    compactor = helper("external_compactor", Path(__file__).resolve().parents[1] / "deduplicate_mnn_external_weights.py")
    def run(label, arguments):
        with (work / (label+".log")).open("w") as log:
            subprocess.run([str(converter), *map(str, arguments)], check=True, stdout=log, stderr=subprocess.STDOUT)
    common = ["--bizCode", "FullSubNetPlus", "--optimizeLevel", "1", "--optimizePrefer", "2", "--transformerFuseC4", "0"]
    run("convert-cpu", ["-f", "ONNX", "--modelFile", work/"fullsubnetplus_dynamic.onnx", "--MNNModel",
        work/"fullsubnetplus_fp32.mnn", *common, "--useOriginRNNImpl"])
    for part in ["front", "subband_32"]:
        run("convert-"+part, ["-f", "ONNX", "--modelFile", work/("fullsubnetplus_"+part+".onnx"),
            "--MNNModel", work/("fullsubnetplus_"+part+"_fp32.mnn"), *common,
            "--transformerFuseQkvProj", "0", "--transformerFuseGateUpProj", "0", "--transformerFuseLnProj", "0"])
    inline = work / "fullsubnetplus_subband_32_fp32.mnn"
    external = work / "subband_external.mnn"
    run("external-subband", ["-f", "MNN", "--modelFile", inline, "--MNNModel", external, "--saveExternalData"])
    compact = work / "subband_compact.mnn"
    report = compactor.compact_model(external, compact)
    compact.replace(inline)
    Path(str(compact)+".weight").replace(Path(str(inline)+".weight"))
    if not keep:
        for path in [*work.glob("*.onnx"), external, Path(str(external)+".weight")]:
            path.unlink()
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--checkpoint", required=True, type=Path)
    parser.add_argument("--converter", required=True, type=Path)
    parser.add_argument("--work-directory", required=True, type=Path)
    parser.add_argument("--reference-directory", type=Path)
    parser.add_argument("--keep-intermediates", action="store_true")
    args = parser.parse_args()
    import torch
    torch.set_num_threads(2)
    work = args.work_directory.resolve()
    work.mkdir(parents=True, exist_ok=True)
    model = load_model(args.source.resolve(), args.checkpoint.resolve())
    report = {"source_revision": REVISION, "checkpoint_sha256": CHECKPOINT_SHA256,
        **export_graphs(model, work, args.reference_directory)}
    report["storage"] = convert(args.converter.resolve(), work, args.keep_intermediates)
    (work / "preparation.json").write_text(json.dumps(report, indent=2)+"\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
