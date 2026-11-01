#!/usr/bin/env python3
"""Specialize the official Silero v5.1 floating ONNX for fixed mono16k frames.

Requires ONNX, ONNX Runtime and official MNNConvert. Input is a local pinned
source file; this tool performs no download and does not modify native code.
"""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess

SOURCE_SHA256 = '2623a2953f6ff3d2c1e61740c6cdb7168133479b267dfef114a4a3cc5bdd788f'
REVISION = '84768cefdf5a3852400e9d8237f7315d14b64a08'


def prepare(source: Path, converter: Path, output: Path) -> dict:
    if hashlib.sha256(source.read_bytes()).hexdigest() != SOURCE_SHA256:
        raise ValueError('expected official snakers4/silero-vad v5.1 floating ONNX')
    import numpy as np
    import onnx
    import onnxruntime as ort
    output.mkdir(parents=True, exist_ok=True)
    model = onnx.load(source)
    condition = onnx.numpy_helper.to_array(model.graph.node[0].attribute[0].t)
    if condition.item() != 16000:
        raise ValueError('unexpected source sample-rate branch')
    branch = next(a.g for node in model.graph.node if node.op_type == 'If'
                  for a in node.attribute if a.name == 'then_branch')
    nodes = list(copy.deepcopy(branch.node))
    nodes.extend(onnx.helper.make_node('Identity', [original.name], [name])
                 for original, name in zip(branch.output, ['output', 'stateN']))
    graph = onnx.helper.make_graph(nodes, 'silero_v5_16k', [
        onnx.helper.make_tensor_value_info('input', onnx.TensorProto.FLOAT, [1,576]),
        onnx.helper.make_tensor_value_info('state', onnx.TensorProto.FLOAT, [2,1,128])], [
        onnx.helper.make_tensor_value_info('output', onnx.TensorProto.FLOAT, [1,1]),
        onnx.helper.make_tensor_value_info('stateN', onnx.TensorProto.FLOAT, [2,1,128])],
        list(model.graph.initializer) + list(branch.initializer))
    fixed = onnx.helper.make_model(graph, opset_imports=copy.deepcopy(model.opset_import), ir_version=model.ir_version)
    onnx.checker.check_model(fixed)
    stage = output/'silero_v5_16k.onnx'
    onnx.save(fixed, stage)
    options = ort.SessionOptions()
    options.intra_op_num_threads = 1
    options.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_BASIC
    optimized = output/'silero_v5_16k_static.onnx'
    options.optimized_model_filepath = str(optimized)
    specialized = ort.InferenceSession(str(stage), options, providers=['CPUExecutionProvider'])
    simplified = onnx.load(optimized)
    if any(node.op_type == 'If' or node.domain not in ('', 'ai.onnx') for node in simplified.graph.node):
        raise ValueError('fixed shape specialization retained control flow or nonstandard operators')
    options.optimized_model_filepath = ''
    original = ort.InferenceSession(str(source), options, providers=['CPUExecutionProvider'])
    rng = np.random.default_rng(20261030)
    state = np.zeros((2,1,128), np.float32)
    fixed_state = state.copy()
    context = np.zeros((1,64), np.float32)
    max_probability = max_state = 0
    for _ in range(64):
        chunk = rng.normal(0,.03,(1,512)).astype(np.float32)
        values = np.concatenate([context,chunk], axis=1)
        probability, state = original.run(None, {'input':values, 'state':state, 'sr':np.array(16000,np.int64)})
        actual, fixed_state = specialized.run(None, {'input':values, 'state':fixed_state})
        max_probability = max(max_probability, float(abs(probability-actual).max()))
        max_state = max(max_state, float(abs(state-fixed_state).max()))
        context = chunk[:,-64:]
    if max_probability > 1e-6 or max_state > 1e-4:
        raise ValueError('specialized ONNX differs from original source')
    target = output/'silero_v5_16k_fp32.mnn'
    command = [str(converter), '-f', 'ONNX', '--modelFile', str(optimized), '--MNNModel', str(target),
               '--bizCode', 'Silero', '--optimizeLevel', '1', '--optimizePrefer', '2', '--useOriginRNNImpl']
    with (output/'conversion.log').open('w') as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    report = {'source_revision':REVISION, 'source_sha256':SOURCE_SHA256,
              'specialization_max_abs_probability':max_probability, 'specialization_max_abs_state':max_state,
              'graph_sha256':hashlib.sha256(target.read_bytes()).hexdigest(), 'graph_bytes':target.stat().st_size}
    (output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n')
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-onnx', required=True, type=Path)
    parser.add_argument('--converter', required=True, type=Path)
    parser.add_argument('--work-directory', required=True, type=Path)
    args = parser.parse_args()
    print(json.dumps(prepare(args.source_onnx.resolve(), args.converter.resolve(), args.work_directory.resolve()), indent=2))


if __name__ == '__main__':
    main()
