#!/usr/bin/env python3
"""Export the pinned 44.1 kHz denoiser and verify its unmodified source oracle."""
from pathlib import Path
import argparse
import hashlib
import importlib.util
import json
import os
import subprocess
import sys
import uuid
import numpy as np
import torch
from original import load, Spectrum, REVISION, SOURCE_REVISION, CHECKPOINT_SHA
from graph import lower


def nrmse(actual, expected):
    if actual.shape != expected.shape or not np.isfinite(actual).all():
        raise ValueError('Invalid source tensor')
    return float(np.linalg.norm(actual.astype(np.float64) - expected) / (np.linalg.norm(expected.astype(np.float64)) or 1.))


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
    identity = str(uuid.uuid5(uuid.NAMESPACE_URL, 'ResembleAI/resemble-enhance/' + REVISION + '/denoiser_fp32.mnn'))
    model.data[position + 4:position + 40] = identity.encode()
    path.write_bytes(model.data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'checkpoint', 'converter', 'oracle', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    torch.set_num_threads(4)
    model, inference, omitted = load(args.source, args.checkpoint)
    graph = Spectrum(lower(model)).eval()
    cases = json.loads((args.oracle / 'cases.json').read_text())['cases']
    rows = []
    with torch.inference_mode():
        for item in cases:
            name = item['case']
            samples = np.fromfile(args.oracle / (name + '-input.f32'), np.float32)
            expected = np.fromfile(args.oracle / (name + '-target.f32'), np.float32)
            original = inference(model, torch.from_numpy(samples), 44100, 'cpu').numpy()
            features = torch.from_numpy(np.fromfile(args.oracle / (name + '-features.f32'), np.float32).reshape(item['shape']))
            actual = graph(features).numpy()
            spectrum = np.fromfile(args.oracle / (name + '-spectrum.f32'), np.float32).reshape(actual.shape)
            row = dict(case=name, original_waveform_nrmse=nrmse(original, expected), lowered_spectrum_nrmse=nrmse(actual, spectrum))
            if row['original_waveform_nrmse'] != 0 or row['lowered_spectrum_nrmse'] >= 1e-4:
                raise ValueError(row)
            rows.append(row)
        onnx = args.output / 'denoiser_dynamic.onnx'
        sample = torch.from_numpy(np.fromfile(args.oracle / 'real-features.f32', np.float32).reshape(1, 3, 841, -1))
        torch.onnx.export(graph, (sample,), onnx, input_names=['features'], output_names=['spectrum'],
                          dynamic_axes={'features': {3: 'frames'}, 'spectrum': {3: 'frames'}}, opset_version=17, dynamo=False)
    output = args.output / 'denoiser_fp32.mnn'
    command = [str(args.converter.resolve()), '-f', 'ONNX', '--modelFile', str(onnx), '--MNNModel', str(output),
               '--bizCode', 'Resemble-Denoiser-44K-MNN', '--optimizeLevel', '1', '--optimizePrefer', '2', '--useGeluApproximation', '0',
               '--transformerFuseC4', '0', '--transformerFuseQkvProj', '0', '--transformerFuseGateUpProj', '0', '--transformerFuseLnProj', '0']
    with (args.output / 'conversion.log').open('w') as log:
        subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
    normalize_uuid(output)
    window = torch.hann_window(1680).numpy()
    profile = dict(repository='ResembleAI/resemble-enhance', revision=REVISION, source_revision=SOURCE_REVISION,
                   graph=output.name, sample_rate=44100, fft_size=1680, hop_size=420, right_padding=441,
                   minimum_samples=400, maximum_samples=132300, input='features', output='spectrum',
                   coefficient_sha256=hashlib.sha256(window.tobytes()).hexdigest(), analysis_window=window.tolist(), causal=False)
    (args.output / 'mnn_resemble_denoiser.json').write_text(json.dumps(profile, indent=2) + '\n')
    report = dict(source_revision=SOURCE_REVISION, checkpoint_revision=REVISION, checkpoint_sha256=CHECKPOINT_SHA,
                  torch_version=torch.__version__, torch_revision=torch.version.git_version, training_only_mel_buffers_omitted=omitted,
                  lowering=rows, converter_arguments=command, graph_bytes=output.stat().st_size,
                  graph_sha256=hashlib.sha256(output.read_bytes()).hexdigest())
    (args.output / 'preparation.json').write_text(json.dumps(report, indent=2) + '\n')
    environment = os.environ.copy()
    if environment.get('MNN_VALIDATION_PYTHONPATH'):
        environment['PYTHONPATH'] = environment['MNN_VALIDATION_PYTHONPATH']
    subprocess.run([sys.executable, str(Path(__file__).with_name('validate.py')), '--graph', str(output),
                    '--oracle', str(args.oracle), '--output', str(args.output / 'mnn-source-parity.json')], check=True, env=environment)


if __name__ == '__main__':
    main()
