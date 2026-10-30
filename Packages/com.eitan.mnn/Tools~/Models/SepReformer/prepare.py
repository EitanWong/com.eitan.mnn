#!/usr/bin/env python3
"""Reproduce the selected SepReformer-B WSJ0-2mix FP32 MNN from pinned author assets."""
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
from graph import lower, polynomial_gelu

from original import load, REVISION, CHECKPOINT_SHA


def source_cases(model):
    random = np.random.default_rng(20261031)
    cases = []
    with torch.inference_mode():
        for length in [16, 17, 75, 76, 77, 1003, 8007, 16000, 31999, 32000]:
            value = random.normal(0, .03, (1, length)).astype(np.float32)
            padded = np.pad(value, ((0, 0), (0, (-length) % 4)))
            sources, _ = model(torch.from_numpy(padded.copy()))
            expected = torch.stack([x.reshape(1, -1)[:, :length] for x in sources], dim=-1).numpy()
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
    identity = str(uuid.uuid5(uuid.NAMESPACE_URL, "dmlguq456/SepReformer/6dd3d163/SepReformer_Base_WSJ0/epoch.0180.pth"))
    model.data[position + 4:position + 40] = identity.encode()
    path.write_bytes(model.data)
    return identity


def convert(converter, onnx_path, graph):
    command = [str(converter), "-f", "ONNX", "--modelFile", str(onnx_path), "--MNNModel", str(graph),
               "--bizCode", "SepReformerBaseWSJ0", "--optimizeLevel", "1", "--optimizePrefer", "2", "--useGeluApproximation", "0",
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
    return dict(repository="dmlguq456/SepReformer", revision=REVISION,
                weights="models/SepReformer_Base_WSJ0/log/scratch_weights/epoch.0180.pth",
                graph="sepreformer_fp32.mnn", sample_rate=8000, speakers=2, input="mixture", output="separated",
                normalization="none; original network output before audition file peak scaling",
                padding_multiple=4, minimum_samples=16, maximum_samples=32000)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ["source", "checkpoint", "converter", "output"]:
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    torch.set_num_threads(2)
    model, protocol = load(args.source, args.checkpoint)
    generator = torch.Generator().manual_seed(20261031)
    scalar_inputs = torch.cat([torch.linspace(-12., 12., 200001), torch.randn(200000, generator=generator) * 3])
    scalar_error = float(torch.max(torch.abs(polynomial_gelu(None, scalar_inputs) - torch.nn.functional.gelu(scalar_inputs, approximate="none"))))
    if scalar_error >= 2e-6:
        raise ValueError("GELU scalar source error: " + str(scalar_error))
    cases = source_cases(model)
    onnx_path = args.output / "sepreformer_dynamic.onnx"
    lowering = export_onnx(model, cases, onnx_path)
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    session = ort.InferenceSession(str(onnx_path), options, providers=["CPUExecutionProvider"])
    onnx_parity = [dict(samples=n, nrmse=compare(session.run(["separated"], {"mixture": x})[0], y)) for n, x, y in cases]
    graph = args.output / "sepreformer_fp32.mnn"
    command, identity = convert(args.converter, onnx_path, graph)
    mnn_parity = check_mnn(graph, cases)
    (args.output / "mnn_sepreformer.json").write_text(json.dumps(profile(), indent=2) + "\n")
    report = dict(source_revision=REVISION, source_hashes=json.loads(Path(__file__).with_name("source-hashes.json").read_text()), checkpoint_sha256=CHECKPOINT_SHA,
                  model_args=protocol, torch_version=torch.__version__, scalar_gelu_max_abs_error=scalar_error, lowering=lowering,
                  onnx_parity=onnx_parity, mnn_parity=mnn_parity, converter_arguments=command,
                  canonical_public_uuid=identity, graph_bytes=graph.stat().st_size,
                  graph_sha256=hashlib.sha256(graph.read_bytes()).hexdigest())
    (args.output / "preparation.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
