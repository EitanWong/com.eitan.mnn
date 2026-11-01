#!/usr/bin/env python3
"""Reproduce the pinned Libri1Mix DCCRNet FP32 waveform MNN."""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import subprocess
import uuid

import numpy as np
import onnxruntime as ort
import torch
from graph import Export
from original import load, CHECKPOINT_SHA
import hybrid


def error(actual, expected):
    value = float(np.linalg.norm(actual - expected) / (np.linalg.norm(expected) or 1.))
    if not np.isfinite(value) or value >= .001:
        raise ValueError("Original waveform differs: " + str(value))
    return value


def canonical_uuid(path, key="JorisCos/DCCRNet_Libri1Mix_enhsingle_16k/00914c64c47932a360b5b4e0c07ab12d305604ba"):
    spec = importlib.util.spec_from_file_location("schema", Path(__file__).resolve().parents[1] / "deduplicate_mnn_external_weights.py")
    schema = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(schema)
    model = schema.Model(path.read_bytes())
    field = model.field(model.integer(0), 11)
    if not field:
        raise ValueError("Missing public Net.mnn_uuid")
    position = field + model.integer(field)
    if model.integer(position) != 36:
        raise ValueError("Invalid public UUID")
    identity = str(uuid.uuid5(uuid.NAMESPACE_URL, key))
    model.data[position + 4:position + 40] = identity.encode()
    path.write_bytes(model.data)
    return identity


def original_cases(model):
    random = np.random.default_rng(20261031)
    cases = []
    with torch.inference_mode():
        for length in [1000, 1099, 1100, 1101, 16007, 32000, 47999, 48000]:
            value = random.normal(0, .03, (1, length)).astype(np.float32)
            expected = model(torch.from_numpy(value.copy())).transpose(1, 2).numpy()
            cases.append((value, expected))
    return cases


def export_onnx(model, cases, path):
    wrapper = Export(model).eval()
    with torch.inference_mode():
        lowering = [dict(samples=x.shape[1], nrmse=error(wrapper(torch.from_numpy(x.copy())).numpy(), y)) for x, y in cases]
        torch.onnx.export(wrapper, torch.zeros(1, 16000), path, opset_version=17, dynamo=False,
                          input_names=["mixture"], output_names=["enhanced"],
                          dynamic_axes={"mixture": {1: "samples"}, "enhanced": {1: "samples"}})
    return lowering


def convert(converter, onnx_path, graph):
    command = [str(converter), "-f", "ONNX", "--modelFile", str(onnx_path), "--MNNModel", str(graph),
               "--bizCode", "DCCRNetLibri1Mix16K", "--optimizeLevel", "1", "--optimizePrefer", "2", "--useOriginRNNImpl",
               "--transformerFuseC4", "0", "--transformerFuseQkvProj", "0", "--transformerFuseGateUpProj", "0", "--transformerFuseLnProj", "0"]
    with (graph.parent / "conversion.log").open("w") as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    return command, canonical_uuid(graph)


def check_mnn(graph, cases):
    import MNN
    interpreter = MNN.Interpreter(str(graph))
    session = interpreter.createSession(dict(backend="CPU", numThread=2, precision="high"))
    rows = []
    for value, expected in cases:
        tensor = interpreter.getSessionInput(session, "mixture")
        interpreter.resizeTensor(tensor, value.shape)
        interpreter.resizeSession(session)
        tensor.copyFrom(MNN.Tensor(value.shape, MNN.Halide_Type_Float, value.copy(), MNN.Tensor_DimensionType_Caffe))
        if interpreter.runSession(session) != 0:
            raise RuntimeError("MNN inference failed")
        output = interpreter.getSessionOutput(session, "enhanced")
        host = MNN.Tensor(output.getShape(), MNN.Halide_Type_Float, np.zeros(output.getShape(), np.float32), MNN.Tensor_DimensionType_Caffe)
        output.copyToHostTensor(host)
        actual = np.asarray(host.getData(), np.float32).reshape(expected.shape)
        rows.append(dict(samples=value.shape[1], nrmse=error(actual, expected)))
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ["source", "filterbanks", "checkpoint", "converter", "output"]:
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    torch.set_num_threads(2)
    model, checkpoint = load(args.checkpoint, args.source, args.filterbanks)
    cases = original_cases(model)
    onnx_path = args.output / "dccrnet_dynamic.onnx"
    lowering = export_onnx(model, cases, onnx_path)
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    session = ort.InferenceSession(str(onnx_path), options, providers=["CPUExecutionProvider"])
    parity = [dict(samples=x.shape[1], nrmse=error(session.run(["enhanced"], {"mixture": x})[0], y)) for x, y in cases]
    graph = args.output / "dccrnet_native_fp32.mnn"
    command, identity = convert(args.converter, onnx_path, graph)
    mnn_parity = check_mnn(graph, cases)
    hybrid_graphs = hybrid.prepare(model, args.converter, args.output, canonical_uuid)
    hybrid_parity = hybrid.check_cpu(args.output, cases, error)
    profile = dict(repository="JorisCos/DCCRNet_Libri1Mix_enhsingle_16k", revision="00914c64c47932a360b5b4e0c07ab12d305604ba",
                   asteroid_version="0.7.0", checkpoint_asteroid_version="0.4.1rc0", filterbanks_version="0.4.0",
                   graph=graph.name, sample_rate=16000, speakers=1, input="mixture", output="enhanced",
                   normalization="none; original forward_wav", minimum_samples=1000, maximum_samples=48000)
    profile.update(encoder_graph="dccrnet_encoder_fp32.mnn", recurrent_graph="dccrnet_recurrent_fp32.mnn", decoder_graph="dccrnet_decoder_fp32.mnn")
    (args.output / "mnn_dccrnet.json").write_text(json.dumps(profile, indent=2) + "\n")
    report = dict(checkpoint_sha256=CHECKPOINT_SHA, source_hashes=json.loads(Path(__file__).with_name("source-hashes.json").read_text()),
                  model_args=checkpoint["model_args"], framework=checkpoint["infos"], torch_version=torch.__version__,
                  torch_compatibility="numpy.int64 constructor input_size converted to builtin int; model math unchanged",
                  lowering=lowering, onnx_parity=parity, mnn_parity=mnn_parity, hybrid_graphs=hybrid_graphs,
                  hybrid_cpu_parity=hybrid_parity, converter_arguments=command,
                  canonical_public_uuid=identity, graph_bytes=graph.stat().st_size,
                  graph_sha256=hashlib.sha256(graph.read_bytes()).hexdigest())
    (args.output / "preparation.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
