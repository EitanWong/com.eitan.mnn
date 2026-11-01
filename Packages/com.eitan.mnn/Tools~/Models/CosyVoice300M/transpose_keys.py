#!/usr/bin/env python3
"""Store dynamic Q=1 Keys as B,H,D,P through official MNN Expression APIs.

Only the Key layout changes. Values, projected positions, weights, sampling and
attention math retain their original contracts. Complete TTS needs validation.
"""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess

SOURCE_PROFILE = 'splitbody4out8b64nopostfixedshape'
SOURCE_SHA256 = '6bbc40f1b86667635921613602c9b316f3b509e3a141858e9f49aed21d326891'


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
    args = parser.parse_args()
    folder = args.models / 'experimental-decode'
    config_path = folder / 'config.json'
    config = json.loads(config_path.read_text())
    source = folder / config[SOURCE_PROFILE]
    contract = config['decode_contracts'][SOURCE_PROFILE]
    if (digest(source) != SOURCE_SHA256 or contract.get('state_layout') != 'per_layer_kv' or
            contract.get('fixed_query_length') != 1 or contract.get('capacity') or
            contract.get('internal_positions') or contract.get('excludes_sentinel') or
            contract.get('key_layout') or len(contract['inputs']) != 31 or len(contract['outputs']) != 29):
        parser.error('Requires the documented fixedshape binary and original dynamic split-KV contract')
    profile = SOURCE_PROFILE + 'keyt'
    destination = source.with_name(source.stem + '_keyt.mnn')
    weight = Path(str(destination) + '.weight')
    if destination.exists() or weight.exists() or profile in config:
        parser.error('Candidate already exists; preserve existing artifacts')
    args.work.mkdir(parents=True, exist_ok=True)
    topology = args.work / 'keyt-source.json'
    with (args.work / 'keyt-source-dump.log').open('w') as log:
        subprocess.run([str(args.converter.resolve()), '-f', 'MNN', '--modelFile', str(source.resolve()),
                        '--JsonFile', str(topology.resolve())], check=True, stdout=log, stderr=subprocess.STDOUT)
    data = json.loads(topology.read_text().replace('-inf', '-3.4028234e38'))
    names = data['tensorName']
    producers = {index: op for op in data['oplists'] for index in op.get('outputIndexes', [])}
    if len(data['oplists']) != 846:
        raise ValueError('Unexpected source topology')
    import MNN
    f = MNN.expr
    variables = f.load_as_dict(str(source.resolve()))
    for layer in range(14):
        name, output = f'key_{layer}', f'next_key_{layer}'
        input_op = producers[names.index(name)]
        concat = producers[names.index(output)]
        if (input_op['type'] != 'Input' or input_op['main']['dims'] != [1, 16, -1, 64] or
                concat['type'] != 'Concat' or concat['main']['axis'] != 2 or
                names[concat['inputIndexes'][0]] != name):
            raise ValueError('Unexpected Key cache topology')
        key = f.placeholder([1, 16, 64, -1], f.NCHW, f.float)
        key.name = name
        variables[name].replace(key)
        token = variables[names[concat['inputIndexes'][1]]]
        update = f.concat([variables[name], f.transpose(token, [0, 1, 3, 2])], 3)
        update.name = output
        variables[output].replace(update)
    changed = 0
    for op in data['oplists']:
        if op['type'] != 'MatMul':
            continue
        left, right = op['inputIndexes']
        permute = producers[right]
        if permute['type'] != 'Permute' or permute['main']['dims'] != [0, 1, 3, 2]:
            continue
        before = permute['inputIndexes'][0]
        if not names[before].startswith('next_key_'):
            continue
        if op['main']['transposeA'] or op['main']['transposeB']:
            raise ValueError('Unexpected MatMul transpose flags')
        # set_inputs shares the actual updated Key output with attention.
        variables[names[op['outputIndexes'][0]]].set_inputs([variables[names[left]], variables[names[before]]])
        changed += 1
    if changed != 14:
        raise ValueError('Expected14 Key attention inputs')
    f.save([variables[name] for name in contract['outputs']], str(destination.resolve()), 0)
    # Fail instead of making an unnecessary full weight copy on another volume.
    weight.hardlink_to(Path(str(source) + '.weight'))
    config[profile] = destination.name
    config['decode_contracts'][profile] = copy.deepcopy(contract)
    config['decode_contracts'][profile]['key_layout'] = 'B,H,D,P'
    config['decode_weights'][profile] = config['decode_weights'][SOURCE_PROFILE] + '; unchanged weights, transposed dynamic Keys'
    config['profile_validation'][profile] = 'EXPERIMENTAL: requires numeric, full synthesis, content and device validation'
    config_path.write_text(json.dumps(config, indent=2, ensure_ascii=False) + '\n')
    report = {'profile': profile, 'source_profile': SOURCE_PROFILE, 'source_sha256': digest(source),
              'graph_sha256': digest(destination), 'weight_sha256': digest(weight),
              'key_input_shape': [1, 16, 64, 'P'], 'key_output_shape': [1, 16, 64, 'P+1'],
              'value_layout_unchanged': True, 'position_layout_unchanged': True, 'matmul_inputs_changed': changed,
              'api': 'official MNN.expr placeholder/transpose/concat/Var.replace/set_inputs/save',
              'for_inference_transform': False, 'official_source_modified': False}
    (args.work / 'keyt-export.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
