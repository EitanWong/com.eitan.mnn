#!/usr/bin/env python3
"""Specialize Q=1 attention masks with official MNN Expression APIs.

Each query must have at least one valid key. These graphs cannot replace
general attention that permits all-masked rows.
"""
import argparse
import copy
import json
from pathlib import Path
import shutil
import subprocess


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for key in ['models', 'work', 'converter']:
        p.add_argument('--' + key, type=Path, required=True)
    p.add_argument('--profile', default='splitbody4out8b64')
    p.add_argument('--mode', choices=['drop-post-mask', 'drop-sentinel'], required=True)
    a = p.parse_args()
    destination = a.models / 'experimental-decode'
    config_path = destination / 'config.json'
    config = json.loads(config_path.read_text())
    contract = config['decode_contracts'][a.profile]
    if (contract['state_layout'] != 'per_layer_kv' or contract.get('capacity') or
            contract.get('internal_positions') or contract.get('excludes_sentinel')):
        p.error('Requires dynamic split K/V, external positions and the original sentinel')
    source = destination / config[a.profile]
    suffix = 'nopost' if a.mode == 'drop-post-mask' else 'nosentinel'
    profile = a.profile + suffix
    filename = source.stem + '_' + suffix + '.mnn'
    dump = a.work / (a.profile + '-mask-source.json')
    with (a.work / (a.profile + '-mask-dump.log')).open('w') as log:
        subprocess.run([str(a.converter.resolve()), '-f', 'MNN', '--modelFile', str(source.resolve()),
                        '--JsonFile', str(dump.resolve())], stdout=log, stderr=subprocess.STDOUT, check=True)
    # Normalize -inf only to parse diagnostic topology. The final graph is
    # saved from the original binary through Expression, retaining its -inf.
    data = json.loads(dump.read_text().replace('-inf', '-3.4028234e38'))
    ops = data['oplists']
    producers = {index: op for op in ops for index in op.get('outputIndexes', [])}
    consumers = {}
    for op in ops:
        for index in op.get('inputIndexes', []):
            consumers.setdefault(index, []).append(op)
    replace = {}
    remove = set()
    def condition_source(index):
        while producers[index]['type'] == 'Cast':
            index = producers[index]['inputIndexes'][0]
        return index

    softmaxes = [op for op in ops if op['type'] == 'Softmax']
    if len(softmaxes) != 14:
        raise RuntimeError('Expected 14 independent attention softmax operations')
    for softmax in softmaxes:
        masked = producers[softmax['inputIndexes'][0]]
        if masked['type'] != 'Select':
            raise RuntimeError('Unexpected pre-softmax masking pattern')
        post = [op for op in consumers[softmax['outputIndexes'][0]] if op['type'] == 'Select']
        if (len(post) != 1 or condition_source(post[0]['inputIndexes'][0]) !=
                condition_source(masked['inputIndexes'][0])):
            raise RuntimeError('Unexpected post-softmax masking pattern')
        post = post[0]
        negative = producers[masked['inputIndexes'][1]]
        zero = producers[post['inputIndexes'][1]]
        if (post['inputIndexes'][2] != softmax['outputIndexes'][0] or
                negative['type'] != 'Const' or zero['type'] != 'Const' or
                zero['main'].get('float32s') != [0.] or
                len(negative['main'].get('float32s', [])) != 1 or
                negative['main']['float32s'][0] >= -3e38):
            raise RuntimeError('Expected negative-infinity pre-mask and zero post-mask')
        # Each generated step has valid keys. exp(-inf)=0, so the post-mask
        # leaves every probability unchanged, including the sentinel zero.
        replace[post['outputIndexes'][0]] = softmax['outputIndexes'][0]
        remove.add(id(post))
        if a.mode == 'drop-sentinel':
            # Runtime removes the initial masked key/value after prefill.
            # Every remaining key is valid; no Q=1 causal mask is necessary.
            replace[masked['outputIndexes'][0]] = masked['inputIndexes'][2]
            remove.add(id(masked))
    for op in ops:
        op['inputIndexes'] = [replace.get(index, index) for index in op.get('inputIndexes', [])]
    rewritten = [op for op in ops if id(op) not in remove]
    by_output = {index: op for op in rewritten for index in op.get('outputIndexes', [])}
    needed = set()

    def visit(index):
        op = by_output[index]
        if id(op) in needed:
            return
        needed.add(id(op))
        for input_index in op.get('inputIndexes', []):
            visit(input_index)

    for output in data['outputName']:
        visit(data['tensorName'].index(output))
    data['oplists'] = [op for op in rewritten if id(op) in needed]
    patched = a.work / (profile + '-mask-specialized.json')
    patched.write_text(json.dumps(data))
    # A no-op diagnostic JSON roundtrip changed numeric outputs in controls.
    # Use it only as a topology map; preserve binary ops through official APIs.
    import MNN
    variables = MNN.expr.load_as_dict(str(source.resolve()))
    for output, input_index in replace.items():
        variables[data['tensorName'][output]].replace(variables[data['tensorName'][input_index]])
    MNN.expr.save([variables[output] for output in data['outputName']],
                  str((destination / filename).resolve()), 0)
    (a.work / (profile + '-expression-transform.json')).write_text(json.dumps({
        'source': str(source), 'destination': filename, 'api': 'official MNN.expr Var.replace/save',
        'for_inference_transform': False,
        'replacements': {data['tensorName'][out]: data['tensorName'][inp] for out, inp in replace.items()},
    }, indent=2))
    # These graphs share unchanged external quantized bytes. A hard link saves
    # local disk; publishing/copying the graph still requires its .weight file.
    target_weight = destination / (filename + '.weight')
    target_weight.unlink(missing_ok=True)
    try:
        target_weight.hardlink_to(Path(str(source) + '.weight'))
    except OSError:
        shutil.copyfile(Path(str(source) + '.weight'), target_weight)
    updated_contract = copy.deepcopy(contract)
    updated_contract['requires_valid_key'] = True
    if a.mode == 'drop-sentinel':
        updated_contract['inputs'].remove('mask')
        updated_contract['excludes_sentinel'] = True
    config[profile] = filename
    config['decode_contracts'][profile] = updated_contract
    config['decode_weights'][profile] = config['decode_weights'][a.profile] + '; weights unchanged, ' + a.mode
    config.setdefault('profile_validation', {})[profile] = (
        'EXPERIMENTAL: regenerated with official Expression APIs; numeric and complete synthesis validation required')
    config_path.write_text(json.dumps(config, indent=2) + '\n')
    report = {'source_profile': a.profile, 'profile': profile, 'mode': a.mode,
              'op_count_before': len(ops), 'op_count_after': len(data['oplists']),
              'weights_unchanged': True, 'inputs': updated_contract['inputs']}
    (a.work / (profile + '-specialization.json')).write_text(json.dumps(report, indent=2))
    print(json.dumps(report), flush=True)


if __name__ == '__main__':
    main()
