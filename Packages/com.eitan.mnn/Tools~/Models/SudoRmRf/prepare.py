#!/usr/bin/env python3
"""Reproduce the selected SudoRMRF U16/512 WSJ0-2mix FP32 MNN from pinned author assets."""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import subprocess
import sys
import uuid

import numpy as np
import onnxruntime as ort
import torch
from graph import lower

REVISION = "cd00f2e21f7ad6281360cdf24ade36f84b0fbad6"
CHECKPOINT_SHA = "33639f5c5dd846d053c99170078f60f95cab55a0606316b9efb9f5a321e975f2"
SOURCE_SHA = "b531f2b598e517356968df0613be5bb2482669afca8c6fbc1e44c753f2bfbcb6"
PROTOCOL = dict(out_channels=256, in_channels=512, num_blocks=16, upsampling_depth=5,
                enc_kernel_size=21, enc_num_basis=512, num_sources=2)


def require_hash(path, expected):
    if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
        raise ValueError("Pinned input differs: " + str(path))


def load_original(source, checkpoint):
    require_hash(source / "sudo_rm_rf/dnn/models/improved_sudormrf.py", SOURCE_SHA)
    require_hash(checkpoint, CHECKPOINT_SHA)
    sys.path.insert(0, str(source))
    from sudo_rm_rf.dnn.models.improved_sudormrf import SuDORMRF
    # Author checkpoint serializes a Module. Verify its digest before deserializing.
    saved = torch.load(checkpoint, map_location="cpu", weights_only=False)
    actual = {name: getattr(saved, name) for name in PROTOCOL}
    if actual != PROTOCOL:
        raise ValueError("Unexpected original checkpoint architecture")
    model = SuDORMRF(**actual).eval()
    model.load_state_dict(saved.state_dict(), strict=True)
    return model


def source_cases(model):
    random = np.random.default_rng(20261031)
    cases = []
    with torch.inference_mode():
        for length in [2, 319, 320, 321, 8007, 16000, 31999, 32000]:
            value = torch.from_numpy(random.normal(0, .03, (1, length)).astype(np.float32))
            mean = value.mean(-1, keepdim=True)
            std = value.std(-1, keepdim=True)
            normalized = (value - mean) / (std + 1e-9)
            expected = model(normalized.unsqueeze(1)).transpose(1, 2).numpy()
            padded = np.pad(normalized.numpy(), ((0, 0), (0, (-length) % 320)))
            cases.append((length, padded, expected))
    return cases


def compare(actual, expected):
    actual = actual[:, :expected.shape[1]]
    errors = [float(np.linalg.norm(actual[:, :, i] - expected[:, :, i]) /
                    (np.linalg.norm(expected[:, :, i]) or 1.)) for i in range(2)]
    if not np.isfinite(errors).all() or max(errors) >= .001:
        raise ValueError("Original waveform differs: " + str(errors))
    return errors


def export_onnx(model, cases, path):
    graph = lower(model)
    with torch.inference_mode():
        errors = [dict(samples=n, nrmse=compare(graph(torch.from_numpy(x)).numpy(), y)) for n, x, y in cases]
        torch.onnx.export(graph, torch.zeros(1, 8000), path, opset_version=17, dynamo=False,
                          input_names=["mixture"], output_names=["separated"],
                          dynamic_axes={"mixture": {1: "padded_samples"}, "separated": {1: "padded_samples"}})
    return errors


def canonical_uuid(path):
    spec = importlib.util.spec_from_file_location("mnn_schema", Path(__file__).resolve().parents[1] / "deduplicate_mnn_external_weights.py")
    schema = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(schema)
    model = schema.Model(path.read_bytes())
    field = model.field(model.integer(0), 11)
    if not field:
        raise ValueError("Missing public Net.mnn_uuid")
    position = field + model.integer(field)
    if model.integer(position) != 36:
        raise ValueError("Invalid public UUID")
    identity = str(uuid.uuid5(uuid.NAMESPACE_URL, "etzinis/sudo_rm_rf/cd00f2e2/Improved_Sudormrf_U16_Bases512_WSJ02mix"))
    model.data[position + 4:position + 40] = identity.encode()
    path.write_bytes(model.data)
    return identity


def convert(converter, onnx_path, graph):
    command = [str(converter), "-f", "ONNX", "--modelFile", str(onnx_path), "--MNNModel", str(graph),
               "--bizCode", "SudoRMRF_WSJO2Mix", "--optimizeLevel", "1", "--optimizePrefer", "2",
               "--transformerFuseC4", "0", "--transformerFuseQkvProj", "0", "--transformerFuseGateUpProj", "0",
               "--transformerFuseLnProj", "0"]
    with (graph.parent / "conversion.log").open("w") as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    return command, canonical_uuid(graph)


def check_mnn(graph, cases):
    import MNN
    interpreter = MNN.Interpreter(str(graph))
    session = interpreter.createSession(dict(backend="CPU", numThread=2, precision="high"))
    rows = []
    for length, value, expected in cases:
        tensor = interpreter.getSessionInput(session, "mixture")
        interpreter.resizeTensor(tensor, value.shape)
        interpreter.resizeSession(session)
        tensor.copyFrom(MNN.Tensor(value.shape, MNN.Halide_Type_Float, value.copy(), MNN.Tensor_DimensionType_Caffe))
        if interpreter.runSession(session) != 0:
            raise RuntimeError("MNN run failed")
        output = interpreter.getSessionOutput(session, "separated")
        host = MNN.Tensor(output.getShape(), MNN.Halide_Type_Float, np.zeros(output.getShape(), np.float32), MNN.Tensor_DimensionType_Caffe)
        output.copyToHostTensor(host)
        actual = np.asarray(host.getData(), np.float32).reshape(1, -1, 2)
        rows.append(dict(samples=length, nrmse=compare(actual, expected)))
    return rows


def profile():
    return dict(repository="etzinis/sudo_rm_rf", revision=REVISION,
                weights="10.5281/zenodo.6299852/Improved_Sudormrf_U16_Bases512_WSJ02mix.pt",
                graph="sudormrf_fp32.mnn", sample_rate=8000, speakers=2, input="mixture", output="separated",
                normalization="mean and unbiased std + 1e-9; restore std and mean to each speaker",
                padding_multiple=320, minimum_samples=2, maximum_samples=32000)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ["source", "checkpoint", "converter", "output"]:
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    torch.set_num_threads(2)
    model = load_original(args.source, args.checkpoint)
    cases = source_cases(model)
    onnx_path = args.output / "sudormrf_dynamic.onnx"
    lowering = export_onnx(model, cases, onnx_path)
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    session = ort.InferenceSession(str(onnx_path), options, providers=["CPUExecutionProvider"])
    onnx_parity = [dict(samples=n, nrmse=compare(session.run(["separated"], {"mixture": x})[0], y)) for n, x, y in cases]
    graph = args.output / "sudormrf_fp32.mnn"
    command, identity = convert(args.converter, onnx_path, graph)
    mnn_parity = check_mnn(graph, cases)
    (args.output / "mnn_sudormrf.json").write_text(json.dumps(profile(), indent=2) + "\n")
    report = dict(source_revision=REVISION, source_sha256=SOURCE_SHA, checkpoint_sha256=CHECKPOINT_SHA,
                  model_args=PROTOCOL, torch_version=torch.__version__, lowering=lowering,
                  onnx_parity=onnx_parity, mnn_parity=mnn_parity, converter_arguments=command,
                  canonical_public_uuid=identity, graph_bytes=graph.stat().st_size,
                  graph_sha256=hashlib.sha256(graph.read_bytes()).hexdigest())
    (args.output / "preparation.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
