#!/usr/bin/env python3
"""Export pinned original 3D-Speaker ERes2NetV2 or CAM++ to FP32 MNN.

Use an existing local checkpoint/source checkout and a TestArtifacts~/ work
folder. Requires PyTorch, ONNX, ONNX Runtime and unmodified official MNNConvert.
No downloads, native code changes or deployment installation are performed.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys

SOURCE_REVISION = '065629c313eaf1a01c65c640c46d77e61e9607b4'
PRESETS = {
    'eres2netv2': {
        'checkpoint': '0eb4057106b2573dd7b132cf0c36273ab29afd192c1610f80baa9c556dbb963c',
        'module': 'speakerlab.models.eres2net.ERes2NetV2', 'class': 'ERes2NetV2',
        'arguments': {'feat_dim': 80, 'embedding_size': 192, 'baseWidth': 26, 'scale': 2, 'expansion': 2},
        'sources': {
            'eres2net/ERes2NetV2.py': '1f7713af4981efea38c70de11ae470a7b31fdd295204f10ca655abf4494eb821',
            'eres2net/fusion.py': 'b76cfbf8adb99336f10ea43953e90228bda85a56de97cda122d9c436a4c94c53',
            'eres2net/pooling_layers.py': '46c6b480ffdd284af90da90ac1bbf9a0e00bd46229e567c0828d7ebe0e178a0a',
        },
    },
    'campplus': {
        'checkpoint': '3388cf5fd3493c9ac9c69851d8e7a8badcfb4f3dc631020c4961371646d5ada8',
        'module': 'speakerlab.models.campplus.DTDNN', 'class': 'CAMPPlus',
        'arguments': {'feat_dim': 80, 'embedding_size': 192},
        'sources': {'campplus/DTDNN.py': '52272359361ef9f03a8b4198f50139301ce712833e777396940a5b437177cd34', 'campplus/layers.py': 'b3d28ad94d14b30fcd20c96d1d83516d4e44de733959460b358e90d97476adb8'},
    },
}


def verify_sources(checkpoint: Path, source: Path, preset: dict):
    if hashlib.sha256(checkpoint.read_bytes()).hexdigest() != preset['checkpoint']:
        raise ValueError('expected frozen official iic speaker checkpoint')
    for name, digest in preset['sources'].items():
        path = source/'speakerlab/models'/name
        if hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise ValueError(f'{name}: expected frozen official 3D-Speaker source')


def verify_onnx(model, graph: Path):
    import numpy as np
    import onnxruntime as ort
    import torch
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    session = ort.InferenceSession(str(graph), options, providers=['CPUExecutionProvider'])
    rng = np.random.default_rng(20261030)
    rows = []
    with torch.no_grad():
        for frames in (9, 200, 370, 798):
            values = rng.normal(0, 1, (1, frames, 80)).astype(np.float32)
            expected = model(torch.from_numpy(values.copy())).numpy().reshape(-1)
            actual = session.run(['embedding'], {'x': values})[0].reshape(-1)
            expected /= np.linalg.norm(expected)
            actual /= np.linalg.norm(actual)
            error = float(np.linalg.norm(actual - expected))
            if not np.isfinite(error) or error > .0001:
                raise ValueError(f'ONNX differs from original PyTorch: {frames} frames, L2={error}')
            rows.append({'frames': frames, 'normalized_embedding_l2': error})
    return rows


def prepare(model_name: str, checkpoint: Path, source: Path, converter: Path, output: Path):
    preset = PRESETS[model_name]
    verify_sources(checkpoint, source, preset)
    import torch
    sys.path.insert(0, str(source))
    import importlib
    model_type = getattr(importlib.import_module(preset['module']), preset['class'])
    output.mkdir(parents=True, exist_ok=True)
    torch.set_num_threads(2)
    model = model_type(**preset['arguments']).eval()
    model.load_state_dict(torch.load(checkpoint, map_location='cpu', weights_only=True), strict=True)
    onnx = output/(model_name+'.onnx')
    # The unmodified official forward unsqueezes its input in-place.
    torch.onnx.export(model, torch.zeros(1, 200, 80), onnx, input_names=['x'], output_names=['embedding'],
                      dynamic_axes={'x': {1: 'frames'}}, opset_version=17, dynamo=False)
    parity = verify_onnx(model, onnx)
    target = output/(model_name+'_fp32.mnn')
    command = [str(converter), '-f', 'ONNX', '--modelFile', str(onnx), '--MNNModel', str(target),
               '--bizCode', preset['class'], '--optimizeLevel', '1', '--optimizePrefer', '2', '--transformerFuseC4', '0']
    with (output/'conversion.log').open('w') as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    report = {'model': model_name, 'checkpoint_sha256': preset['checkpoint'], 'source_revision': SOURCE_REVISION,
              'onnx_source_parity': parity, 'graph_bytes': target.stat().st_size,
              'graph_sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
    (output/'preparation.json').write_text(json.dumps(report, indent=2)+'\n')
    return report


def main(default_model=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', choices=tuple(PRESETS), default=default_model, required=default_model is None)
    parser.add_argument('--checkpoint', required=True, type=Path)
    parser.add_argument('--source-directory', required=True, type=Path)
    parser.add_argument('--converter', required=True, type=Path)
    parser.add_argument('--work-directory', required=True, type=Path)
    args = parser.parse_args()
    print(json.dumps(prepare(args.model, args.checkpoint.resolve(), args.source_directory.resolve(),
                             args.converter.resolve(), args.work_directory.resolve()), indent=2))


if __name__ == '__main__':
    main()
