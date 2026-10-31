#!/usr/bin/env python3
"""Export the pinned 16 kHz SEG YGD network and verify independent source cases."""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import subprocess
import sys
import uuid
import numpy as np
import torch
from graph import lower
from original import load, source_forward, REVISION, SOURCE_REVISION, CHECKPOINT_SHA


def nrmse(actual, expected):
    if actual.shape != expected.shape or not np.isfinite(actual).all():
        raise ValueError('Invalid source output shape or values')
    error = float(np.linalg.norm(actual - expected) / (np.linalg.norm(expected) or 1.))
    if not np.isfinite(error) or error >= .001:
        raise ValueError('Original source waveform NRMSE: ' + str(error))
    return error


def normalize_uuid(path):
    spec = importlib.util.spec_from_file_location('mnn_schema', Path(__file__).resolve().parents[1] / 'deduplicate_mnn_external_weights.py')
    schema = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(schema)
    model = schema.Model(path.read_bytes())
    field = model.field(model.integer(0), 11)
    if not field:
        raise ValueError('Missing public MNN Net UUID')
    position = field + model.integer(field)
    if model.integer(position) != 36:
        raise ValueError('Invalid public MNN Net UUID')
    identity = str(uuid.uuid5(uuid.NAMESPACE_URL, 'alibabasglab/log_YGD_gesture_seg_2spk/' + REVISION + '/seg_fp32.mnn'))
    model.data[position + 4:position + 40] = identity.encode()
    path.write_bytes(model.data)


def profile():
    return dict(repository='alibabasglab/log_YGD_gesture_seg_2spk', revision=REVISION, source_revision=SOURCE_REVISION,
                graph='seg_fp32.mnn', sample_rate=16000, gesture_fps=15, gesture_features=30, minimum_samples=1067,
                maximum_samples=160000, audio_input='mixture', gesture_input='gestures', audio_output='target',
                normalization='none; synchronized frame-major gesture features', causal=False)


def verify_lowering(original, lowered, reference):
    cases = json.loads((reference / 'cases.json').read_text())['cases']
    rows = []
    for row in cases:
        name = row['case']
        audio = np.fromfile(reference / (name + '-input.f32'), np.float32).reshape(1, row['samples'])
        gestures = np.fromfile(reference / (name + '-gesture.f32'), np.float32).reshape(1, row['frames'], 30)
        oracle = np.fromfile(reference / (name + '-target.f32'), np.float32).reshape(1, -1)
        expected = source_forward(original, torch.from_numpy(audio), torch.from_numpy(gestures)).numpy()
        actual = lowered(torch.from_numpy(audio), torch.from_numpy(gestures)).numpy()
        rows.append(dict(case=name, original_oracle_nrmse=nrmse(expected, oracle), lowered_original_nrmse=nrmse(actual, expected)))
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'checkpoint', 'config', 'converter', 'oracle', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    torch.set_num_threads(2)
    model, config = load(args.source, args.checkpoint, args.config)
    lowered = lower(model)
    onnx = args.output / 'seg_dynamic.onnx'
    with torch.inference_mode():
        rows = verify_lowering(model, lowered, args.oracle)
        torch.onnx.export(lowered, (torch.zeros(1, 16000), torch.zeros(1, 15, 30)), onnx,
                          input_names=['mixture', 'gestures'], output_names=['target'], opset_version=17, dynamo=False,
                          dynamic_axes={'mixture': {1: 'samples'}, 'gestures': {1: 'frames'}, 'target': {1: 'samples'}})
    graph = args.output / 'seg_fp32.mnn'
    command = [str(args.converter.resolve()), '-f', 'ONNX', '--modelFile', str(onnx), '--MNNModel', str(graph),
               '--bizCode', 'SEG-YGD-16K-MNN', '--optimizeLevel', '1', '--optimizePrefer', '2', '--useOriginRNNImpl']
    with (args.output / 'conversion.log').open('w') as log:
        subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
    normalize_uuid(graph)
    (args.output / 'mnn_seg.json').write_text(json.dumps(profile(), indent=2) + '\n')
    report = dict(source_revision=SOURCE_REVISION, checkpoint_revision=REVISION, checkpoint_sha256=CHECKPOINT_SHA,
                  torch_version=torch.__version__, original_config=config, source_device_port='CUDA integer index -> CPU only',
                  lowering=rows, converter_arguments=command, graph_bytes=graph.stat().st_size,
                  graph_sha256=hashlib.sha256(graph.read_bytes()).hexdigest(), mnn_source_validation='Run validate.py separately')
    (args.output / 'preparation.json').write_text(json.dumps(report, indent=2) + '\n')
    subprocess.run([sys.executable, str(Path(__file__).with_name('validate.py')), '--graph', str(graph),
                    '--oracle', str(args.oracle), '--output', str(args.output / 'mnn-source-parity.json')], check=True)


if __name__ == '__main__':
    main()
