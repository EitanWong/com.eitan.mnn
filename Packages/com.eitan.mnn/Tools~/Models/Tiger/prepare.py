#!/usr/bin/env python3
"""Strictly verify and export pinned TIGER or DAVE audio separation to MNN.

Original sources/checkpoints/oracles stay in the supplied scratch directories.
This tool adds no native code and does not change original model sources.
"""
from pathlib import Path
import argparse
import copy
import hashlib
import importlib
import importlib.util
import json
import os
import subprocess
import sys
import types
import uuid


HERE = Path(__file__).resolve().parent


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def original(source, checkpoint, contract, dave):
    import torch
    for name, expected in contract['source_sha256'].items():
        if digest(source / name) != expected:
            raise ValueError('Original source SHA mismatch: ' + name)
    if digest(checkpoint) != contract['checkpoint_sha256']:
        raise ValueError('Original checkpoint SHA mismatch')
    root = source / ('src' if dave else 'look2hear')
    for suffix in ['', '.models', '.layers']:
        name = 'tiger_source' + suffix
        module = types.ModuleType(name)
        module.__path__ = [str(root / suffix.removeprefix('.'))]
        sys.modules[name] = module
    if dave:
        module = types.ModuleType('tiger_source.models.layers')
        module.__path__ = [str(root / 'models/layers')]
        sys.modules[module.__name__] = module
    network = importlib.import_module('tiger_source.models.tiger').TIGER(**contract['configuration'])
    if dave:
        state = torch.load(checkpoint, map_location='cpu', weights_only=True)
        state = state.get('state_dict', state)
    else:
        from safetensors.torch import load_file
        state = load_file(str(checkpoint))
    network.load_state_dict(state, strict=True)
    return network.eval().requires_grad_(False)


def error(actual, expected):
    import numpy as np
    if actual.shape != expected.shape or not np.isfinite(actual).all():
        raise ValueError('Invalid source spectrum')
    return float(np.linalg.norm(actual.astype(np.float64) - expected) /
                 (np.linalg.norm(expected.astype(np.float64)) or 1.))


def export(network, oracle, output):
    import numpy as np
    import torch
    import torch.nn.functional as functional
    from spectral import Spectrum, adaptive_pool
    description = json.loads((oracle / 'cases.json').read_text())
    if not description['torch'].startswith('2.8.') or description['torch_revision'] != torch.version.git_version:
        raise ValueError('The original oracle framework differs from the pinned export framework')
    cases = description['cases']
    if len(cases) != 25:
        raise ValueError('Supply the complete25-condition original source oracle')
    graph = Spectrum(copy.deepcopy(network)).eval()
    rows = []
    with torch.inference_mode():
        for item in cases:
            name = item['case']
            features = torch.from_numpy(np.fromfile(oracle / (name + '-features.f32'), np.float32).reshape(item['shape']))
            expected = np.fromfile(oracle / (name + '-spectrum.f32'), np.float32).reshape(1, 4, 321, -1)
            native = graph(features).numpy()
            source_error = error(native, expected)
            if source_error != 0:
                raise ValueError('Original spectral wrapper is not bitwise identical: ' + name)
            original_pool = functional.adaptive_avg_pool1d
            functional.adaptive_avg_pool1d = adaptive_pool
            try:
                lowered_error = error(graph(features).numpy(), expected)
            finally:
                functional.adaptive_avg_pool1d = original_pool
            row = dict(case=name, original_spectrum_nrmse=source_error, lowered_spectrum_nrmse=lowered_error)
            rows.append(row)
            (output / 'source-contract.json').write_text(json.dumps(rows, indent=2) + '\n')
            print(row, flush=True)
            if lowered_error != 0:
                raise ValueError('Source-ordered pooling must preserve the original spectrum exactly')
        sample = torch.from_numpy(np.fromfile(oracle / 'real-features.f32', np.float32).reshape(1, 2, 321, -1))
        original_pool = functional.adaptive_avg_pool1d
        functional.adaptive_avg_pool1d = adaptive_pool
        try:
            torch.onnx.export(graph, sample, output / 'spectrum.onnx', input_names=['spectrum'], output_names=['separated'],
                              dynamic_axes={'spectrum': {3: 'frames'}, 'separated': {3: 'frames'}}, opset_version=17, dynamo=False)
        finally:
            functional.adaptive_avg_pool1d = original_pool
    return rows


def convert(converter, output, contract):
    schema_spec = importlib.util.spec_from_file_location('mnn_schema', HERE.parent / 'deduplicate_mnn_external_weights.py')
    schema = importlib.util.module_from_spec(schema_spec)
    schema_spec.loader.exec_module(schema)
    def run(label, flags):
        with (output / (label + '.log')).open('w') as log:
            subprocess.run([str(converter), *map(str, flags)], check=True, stdout=log, stderr=subprocess.STDOUT)
    inline, external, graph = [output / name for name in ['inline.mnn', 'external.mnn', 'tiger_fp32.mnn']]
    run('convert', ['-f', 'ONNX', '--modelFile', output / 'spectrum.onnx', '--MNNModel', inline,
                    '--bizCode', 'TIGER', '--optimizeLevel', '1', '--optimizePrefer', '2', '--useGeluApproximation', '0',
                    '--transformerFuseC4', '0', '--transformerFuseQkvProj', '0', '--transformerFuseGateUpProj', '0', '--transformerFuseLnProj', '0'])
    run('external', ['-f', 'MNN', '--modelFile', inline, '--MNNModel', external, '--saveExternalData'])
    report = schema.compact_model(external, graph)
    model = schema.Model(graph.read_bytes())
    field = model.field(model.integer(0), 11)
    if not field:
        raise ValueError('Missing public MNN Net UUID')
    position = model.follow(field)
    if model.integer(position) != 36:
        raise ValueError('Invalid public MNN Net UUID')
    identity = str(uuid.uuid5(uuid.NAMESPACE_URL, contract['repository'] + '/' + contract['revision'] + '/tiger_fp32.mnn'))
    model.data[position + 4:position + 40] = identity.encode()
    graph.write_bytes(model.data)
    report['sha256'] = digest(graph)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', choices=['tiger', 'dave'], required=True)
    for name in ['source', 'checkpoint', 'oracle', 'converter', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--io-dependency-path', type=Path)
    parser.add_argument('--mnn-pythonpath', required=True)
    args = parser.parse_args()
    # Bind and check the original Torch before adding optional I/O dependencies.
    import torch
    if not torch.__version__.startswith('2.8.') or torch.version.git_version != 'a1cb3cc05d46d198467bebbb6e8fba50a325d4e7':
        raise ValueError('Use the original pinned Torch2.8/a1cb3cc environment without an overriding PYTHONPATH')
    if args.io_dependency_path:
        sys.path.append(str(args.io_dependency_path.resolve()))
    torch.set_num_threads(1)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    contract = json.loads((HERE / 'contracts.json').read_text())[args.profile]
    oracle = args.oracle.resolve()
    oracle_hashes = {p.name: digest(p) for p in sorted(oracle.iterdir()) if p.suffix in ['.f32', '.json']}
    network = original(args.source.resolve(), args.checkpoint.resolve(), contract, args.profile == 'dave')
    rows = export(network, oracle, output)
    conversion = convert(args.converter.resolve(), output, contract)
    # Validation owns an independent NumPy/MNN process; Torch cannot override its runtime.
    env = os.environ.copy()
    env['PYTHONPATH'] = args.mnn_pythonpath
    env['PYTHONDONTWRITEBYTECODE'] = '1'
    with (output / 'validate.log').open('w') as log:
        subprocess.run([sys.executable, str(HERE / 'validate.py'), '--graph', str(output / 'tiger_fp32.mnn'),
                        '--oracle', str(oracle), '--output', str(output / 'waveform-validation.json'), '--threads', '2', '4'],
                       check=True, env=env, stdout=log, stderr=subprocess.STDOUT)
    if oracle_hashes != {p.name: digest(p) for p in sorted(oracle.iterdir()) if p.suffix in ['.f32', '.json']}:
        raise ValueError('Oracle changed during conversion')
    report = dict(profile=args.profile, source=contract, oracle_sha256=oracle_hashes,
                  spectral_comparisons=rows, conversion=conversion,
                  waveform_comparisons=json.loads((output / 'waveform-validation.json').read_text()))
    (output / 'conversion-report.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(conversion), flush=True)


if __name__ == '__main__':
    main()
