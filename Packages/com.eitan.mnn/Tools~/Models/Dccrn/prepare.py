#!/usr/bin/env python3
"""Reproduce checkpoint-owned Mayavoz DCCRN with the official MNN converter."""
from pathlib import Path
import argparse
import copy
import hashlib
import importlib.util
import json
import os
import subprocess
import sys
import uuid


HERE = Path(__file__).resolve().parent


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def compare(actual, expected):
    import numpy as np
    if actual.shape != expected.shape or not np.isfinite(actual).all():
        raise ValueError('Invalid source waveform')
    return float(np.linalg.norm(actual.astype(np.float64) - expected) / (np.linalg.norm(expected.astype(np.float64)) or 1.))


def source_gate(model, lowered, oracle, output):
    import numpy as np
    import torch
    from networks import Analysis, Encoder, Recurrent, Back
    description = json.loads((oracle / 'cases.json').read_text())
    if description['torch'] != torch.__version__ or description['torch_revision'] != torch.version.git_version:
        raise ValueError('Oracle and export must use the pinned original framework')
    if len(description['cases']) != 22:
        raise ValueError('Supply all22 original physical conditions')
    snapshot = {key: value.clone() for key, value in model.state_dict().items()}
    analysis, encoder, recurrent, decoder = Analysis(lowered), Encoder(lowered), Recurrent(lowered), Back(lowered)
    rows = []
    with torch.inference_mode():
        for item in description['cases']:
            name = item['case']
            raw = np.fromfile(oracle / (name + '-input.f32'), np.float32)
            expected = np.fromfile(oracle / (name + '-target.f32'), np.float32)
            if raw.size != item['samples'] or not 1 <= raw.size <= 48000:
                raise ValueError('Invalid oracle sample contract')
            pcm = torch.from_numpy(np.pad(raw, (0, (-raw.size) % 100)))[None, None]
            model.load_state_dict({key: value.clone() for key, value in snapshot.items()}, strict=True, assign=True)
            original = model(pcm).numpy().reshape(-1)[:raw.size]
            value = lowered(pcm).numpy().reshape(-1)[:raw.size]
            features, magnitude, phase = analysis(pcm)
            skips = encoder(features)
            split = decoder(recurrent(skips[5]), *skips, magnitude, phase).numpy().reshape(-1)[:raw.size]
            row = dict(case=name, original_waveform_nrmse=compare(original, expected),
                       lowered_waveform_nrmse=compare(value, expected), split_waveform_nrmse=compare(split, expected))
            rows.append(row)
            (output / 'source-contract.json').write_text(json.dumps(rows, indent=2) + '\n')
            print(row, flush=True)
            if any(row[key] != 0 for key in ['original_waveform_nrmse', 'lowered_waveform_nrmse', 'split_waveform_nrmse']):
                raise ValueError('Every original, readonly and split source waveform must match bitwise')
    return rows


def convert(lowered, converter, output, contract):
    import torch
    from networks import Analysis, Encoder, Recurrent, Back
    from atan2_export import register
    spec = importlib.util.spec_from_file_location('mnn_schema', HERE.parent / 'deduplicate_mnn_external_weights.py')
    schema = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(schema)
    x = torch.zeros(1, 1, 16000)
    analysis, encoder, recurrent, decoder = Analysis(lowered).eval(), Encoder(lowered).eval(), Recurrent(lowered).eval(), Back(lowered).eval()
    with torch.inference_mode():
        features, magnitude, phase = analysis(x)
        skips = encoder(features)
        r = recurrent(skips[5])
        keys = ['skip' + str(i) for i in range(6)]
        skip_axes = {key: {3: 'frames'} for key in keys}
        register()
        jobs = [
            ('dccrn', lowered, x, ['pcm'], ['enhanced'], {'pcm': {2: 'samples'}, 'enhanced': {2: 'samples'}}, ''),
            ('analysis', analysis, x, ['pcm'], ['features','magnitude','phase'], {'pcm': {2:'samples'}, 'features': {3:'frames'}, 'magnitude': {2:'frames'}, 'phase': {2:'frames'}}, '-analysis'),
            ('encoder', encoder, features, ['features'], keys, {'features': {3:'frames'}, **skip_axes}, '-encoder'),
            ('recurrent', recurrent, skips[5], ['encoded'], ['recurrent'], {'encoded': {3:'frames'}, 'recurrent': {3:'frames'}}, '-recurrent'),
            ('decoder', decoder, (r,*skips,magnitude,phase), ['recurrent',*keys,'magnitude','phase'], ['enhanced'],
             {'recurrent': {3:'frames'}, **skip_axes, 'magnitude': {2:'frames'}, 'phase': {2:'frames'}, 'enhanced': {2:'samples'}}, '-back')]
        sources = []
        for name, network, inputs, inames, onames, axes, biz in jobs:
            onnx, internal = output / (name + '.onnx'), output / (name + '-internal.mnn')
            torch.onnx.export(network, inputs, onnx, input_names=inames, output_names=onames, dynamic_axes=axes, opset_version=17, dynamo=False)
            with (output / (name + '-convert.log')).open('w') as log:
                subprocess.run([str(converter), '-f','ONNX','--modelFile',str(onnx),'--MNNModel',str(internal),
                    '--bizCode','DCCRN-Mayavoz'+biz,'--useOriginRNNImpl','--optimizeLevel','1','--optimizePrefer','2'], check=True, stdout=log, stderr=subprocess.STDOUT)
            external = output / 'external' / (name + '.mnn')
            external.parent.mkdir(exist_ok=True)
            with (output / (name + '-external.log')).open('w') as log:
                subprocess.run([str(converter),'-f','MNN','--modelFile',str(internal),'--MNNModel',str(external),'--saveExternalData'], check=True, stdout=log, stderr=subprocess.STDOUT)
            model = schema.Model(external.read_bytes())
            position = model.follow(model.field(model.follow(0), 11))
            if model.integer(position) != 36:
                raise ValueError('Unexpected official Net UUID field')
            model.data[position+4:position+40] = str(uuid.uuid5(uuid.NAMESPACE_URL,
                contract['repository']+'/'+contract['revision']+'/fp32/'+name)).encode()
            external.write_bytes(model.data)
            sources.append(external)
    return schema.compact_models(sources, output / 'models', 'dccrn.mnn.weight')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ['source','checkpoint','oracle','converter','output','metadata-dependency-path']:
        parser.add_argument('--'+key, type=Path, required=True)
    parser.add_argument('--mnn-pythonpath', required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError('Use a fresh output directory to preserve prior evidence')
    args.output.mkdir(parents=True)
    import torch
    from source import original, CONTRACT
    from networks import freeze
    torch.set_num_threads(1)
    oracle_hashes = {p.name: sha(p) for p in sorted(args.oracle.iterdir()) if p.suffix == '.f32' or p.name == 'cases.json'}
    model, _, _ = original(args.source, args.checkpoint, args.metadata_dependency_path)
    lowered = copy.deepcopy(model)
    freeze(lowered)
    comparisons = source_gate(model, lowered, args.oracle, args.output)
    storage = convert(lowered, args.converter, args.output, CONTRACT)
    env = os.environ.copy()
    env['PYTHONPATH'] = args.mnn_pythonpath
    env['PYTHONDONTWRITEBYTECODE'] = '1'
    validation = args.output / 'waveform-validation.json'
    with (args.output / 'validate.log').open('w') as log:
        subprocess.run([sys.executable,str(HERE/'validate.py'),'--directory',str(args.output/'models'),
                        '--oracle',str(args.oracle),'--output',str(validation)], env=env, check=True, stdout=log, stderr=subprocess.STDOUT)
    if any(sha(args.oracle/name) != expected for name,expected in oracle_hashes.items()):
        raise ValueError('Oracle changed during export')
    report = dict(source=CONTRACT, source_comparisons=comparisons, oracle_sha256=oracle_hashes, storage=storage,
                  torch=torch.__version__, torch_revision=torch.version.git_version, converter_sha256=sha(args.converter),
                  waveform_comparisons=json.loads(validation.read_text()))
    (args.output/'conversion-report.json').write_text(json.dumps(report,indent=2)+'\n')


if __name__ == '__main__':
    main()
