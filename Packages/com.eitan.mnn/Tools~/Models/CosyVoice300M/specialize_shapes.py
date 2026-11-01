#!/usr/bin/env python3
"""Fix batch1/Q=1 projection shapes using official MNN Expression APIs.

The decoder retains dynamic KV length and the original quantized operations.
Only shape inputs and the final single-token Gather index/axis are replaced.
"""
import argparse
from collections import Counter
import copy
import hashlib
import json
from pathlib import Path
import shutil
import subprocess


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['models', 'work', 'converter']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--profile', default='splitbody4out8b64nopost')
    parser.add_argument('--force', action='store_true', help='Regenerate an existing candidate and reset its validation status')
    args = parser.parse_args()
    folder = args.models / 'experimental-decode'
    config_path = folder / 'config.json'
    config = json.loads(config_path.read_text())
    contract = config.get('decode_contracts', {}).get(args.profile, {})
    if (contract.get('state_layout') != 'per_layer_kv' or contract.get('capacity') or
            contract.get('internal_positions') or contract.get('excludes_sentinel') or
            contract.get('fixed_query_length')):
        parser.error('Requires original dynamic split K/V, external positions and batch1/Q=1 projection shapes')
    source = folder / config[args.profile]
    profile = args.profile + 'fixedshape'
    destination = folder / (source.stem + '_fixedshape.mnn')
    weight = Path(str(destination) + '.weight')
    if not args.force and (destination.exists() or weight.exists() or profile in config):
        parser.error('Candidate already exists; use --force to regenerate and reset validation')
    args.work.mkdir(parents=True, exist_ok=True)
    topology_path = args.work / (args.profile + '-fixedshape-source.json')
    with (args.work / (args.profile + '-fixedshape-dump.log')).open('w') as log:
        subprocess.run([str(args.converter.resolve()), '-f', 'MNN', '--modelFile', str(source.resolve()),
                        '--JsonFile', str(topology_path.resolve())], check=True, stdout=log, stderr=subprocess.STDOUT)
    # JSON is a topology map only. The final graph is saved from the original
    # binary, retaining its weights and actual negative-infinity constants.
    data = json.loads(topology_path.read_text().replace('-inf', '-3.4028234e38'))
    producer = {index: op for op in data['oplists'] for index in op.get('outputIndexes', [])}
    embeddings = [op for op in data['oplists'] if op['type'] == 'Input' and op['name'] == 'embeddings']
    if len(embeddings) != 1 or embeddings[0]['main'].get('dims') != [1, 1, 1024]:
        raise ValueError('Expected batch1/Q=1/hidden1024 embedding input')
    changes = []
    for op in data['oplists']:
        if op['type'] != 'Reshape' or len(op.get('inputIndexes', [])) != 2:
            continue
        value_index, shape_index = op['inputIndexes']
        if producer[shape_index]['type'] == 'Const':
            continue
        convert = producer[value_index]
        if (convert['type'] != 'ConvertTensor' or
                convert['main'] != {'source': 'NC4HW4', 'dest': 'NCHW'}):
            raise ValueError('Unexpected dynamic reshape source: ' + op['name'])
        convolution = producer[convert['inputIndexes'][0]]
        if convolution['type'] != 'Convolution':
            raise ValueError('Expected quantized projection before ' + op['name'])
        common = convolution['main']['common']
        if any(common.get(key) != value for key, value in {
                'kernelX': 1, 'kernelY': 1, 'strideX': 1, 'strideY': 1, 'padX': 0, 'padY': 0, 'group': 1}.items()):
            raise ValueError('Expected pointwise projection before ' + op['name'])
        channels = common['outputCount']
        if channels not in [1024, 3072, 4096, 4097] or (channels == 4097) != (op['name'] == 'logits'):
            raise ValueError('Unexpected projection width: ' + op['name'])
        changes.append({'name': data['tensorName'][op['outputIndexes'][0]],
                        'operator': op['name'],
                        'value': data['tensorName'][value_index],
                        'shape': [1, channels] if op['name'] == 'logits' else [1, 1, channels]})
    if len(changes) != 58 or Counter(change['shape'][-1] for change in changes) != {
            1024: 29, 3072: 14, 4096: 14, 4097: 1}:
        raise ValueError('Expected all58 original projection reshapes')
    gathers = [op for op in data['oplists'] if op['type'] == 'GatherV2']
    if len(gathers) != 1 or gathers[0]['name'] != '/Gather_output_0' or len(gathers[0]['inputIndexes']) != 3:
        raise ValueError('Expected one final single-token Gather')
    gather = gathers[0]

    import MNN
    import numpy as np
    variables = MNN.expr.load_as_dict(str(source.resolve()))
    for change in changes:
        shape = change['shape']
        constant = MNN.expr.const(np.array(shape, np.int32), [len(shape)], MNN.expr.NCHW, MNN.expr.int)
        constant.name = change['operator'] + '/q1_shape'
        variables[change['name']].set_inputs([variables[change['value']], constant])
    index = MNN.expr.const(np.array(0, np.int32), [], MNN.expr.NCHW, MNN.expr.int)
    axis = MNN.expr.const(np.array(1, np.int32), [], MNN.expr.NCHW, MNN.expr.int)
    variables[data['tensorName'][gather['outputIndexes'][0]]].set_inputs(
        [variables[data['tensorName'][gather['inputIndexes'][0]]], index, axis])
    MNN.expr.save([variables[name] for name in contract['outputs']], str(destination.resolve()), 0)
    source_weight = Path(str(source) + '.weight')
    weight.unlink(missing_ok=True)
    try:
        weight.hardlink_to(source_weight)
    except OSError:
        shutil.copyfile(source_weight, weight)
    config[profile] = destination.name
    config['decode_contracts'][profile] = copy.deepcopy(contract)
    config['decode_contracts'][profile]['fixed_query_length'] = 1
    config['decode_weights'][profile] = config['decode_weights'][args.profile] + '; weights unchanged, fixed Q=1 shapes'
    config.setdefault('profile_validation', {})[profile] = (
        'EXPERIMENTAL: regenerated with official Expression APIs; Q=1 shape numeric and complete synthesis validation required')
    config_path.write_text(json.dumps(config, indent=2, ensure_ascii=False) + '\n')
    report = {'source_profile': args.profile, 'profile': profile, 'projection_reshapes': changes,
              'final_gather': {'index': 0, 'axis': 1}, 'fixed_batch': 1, 'fixed_query_length': 1,
              'dynamic_kv_length': True, 'api': 'official MNN.expr Var.set_inputs/save',
              'source_sha256': digest(source), 'model_sha256': digest(destination),
              'weight_sha256': digest(weight), 'weight_bytes_unchanged': digest(weight) == digest(source_weight),
              'for_inference_transform': False}
    (args.work / (profile + '-shape-specialization.json')).write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: value for key, value in report.items() if key != 'projection_reshapes'}, indent=2))


if __name__ == '__main__':
    main()
