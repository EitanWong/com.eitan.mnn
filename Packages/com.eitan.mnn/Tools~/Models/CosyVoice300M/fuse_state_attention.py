#!/usr/bin/env python3
"""Represent relative attention as official stateless MNN Attention plus additive bias.

Edits model data through the official converter's JSON interface, never runtime source.
The position term stays explicit; only content attention/softmax/value are fused.
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
    p.add_argument('--concat-relative', action='store_true', help='Encode relative term in doubled Q/K head dimension, avoiding head-specific additive mask')
    a = p.parse_args()
    destination = a.models / 'experimental-decode'
    source = destination / 'llm_decode_split_int8.mnn'
    dump = a.work / 'split8-attention-source.json'
    with (a.work / 'split8-attention-dump.log').open('w') as log:
        subprocess.run([str(a.converter), '-f', 'MNN', '--modelFile', str(source), '--JsonFile', str(dump)],
                       stdout=log, stderr=subprocess.STDOUT, check=True)
    # The converter's diagnostic JSON spells the mask constant as -inf.
    # A finite float32 minimum also becomes -inf in FP16 and underflows softmax.
    data = json.loads(dump.read_text().replace('-inf', '-3.4028234e38'))
    ops = data['oplists']
    producers = {index: op for op in ops for index in op.get('outputIndexes', [])}
    consumers = {}
    for op in ops:
        for index in op.get('inputIndexes', []):
            consumers.setdefault(index, []).append(op)
    replacements, remove = {}, set()

    def fresh(name):
        index = len(data['tensorName'])
        data['tensorName'].append(name)
        return index

    count = 0
    for softmax in [op for op in ops if op['type'] == 'Softmax']:
        masked = producers[softmax['inputIndexes'][0]]
        if masked['type'] != 'Select':
            raise RuntimeError('Unexpected mask pattern')
        condition, minus_inf, scaled_scores = masked['inputIndexes']
        scale = producers[scaled_scores]
        addition = producers[scale['inputIndexes'][0]]
        content = producers[addition['inputIndexes'][0]]
        relative = addition['inputIndexes'][1]
        relative_product = producers[relative]
        post = next(op for op in consumers[softmax['outputIndexes'][0]] if op['type'] == 'Select')
        weighted = next(op for op in consumers[post['outputIndexes'][0]] if op['type'] == 'MatMul')
        transpose = next(op for op in consumers[weighted['outputIndexes'][0]] if op['type'] == 'Permute')
        reshape = next(op for op in consumers[transpose['outputIndexes'][0]] if op['type'] == 'Reshape')
        key_transpose = producers[content['inputIndexes'][1]]
        query, key, value = content['inputIndexes'][0], key_transpose['inputIndexes'][0], weighted['inputIndexes'][1]
        new_ops = []
        if a.concat_relative:
            def op(kind, parameter, main, inputs, name):
                out = fresh(name)
                new_ops.append({'type': kind, 'main_type': parameter, 'main': main,
                                'name': name, 'inputIndexes': inputs, 'outputIndexes': [out]})
                return out

            def constant(values, shape, dtype, name):
                out = fresh(name)
                new_ops.append({'type': 'Const', 'main_type': 'Blob',
                    'main': {'dims': shape, 'dataFormat': 'NCHW', 'dataType': dtype,
                             'float32s' if dtype == 'DT_FLOAT' else 'int32s': values},
                    'name': name, 'outputIndexes': [out]})
                return out

            prefix = f'concat_relative_{count}_'
            rel_query = relative_product['inputIndexes'][0]
            rel_positions = producers[relative_product['inputIndexes'][1]]['inputIndexes'][0]
            query = op('Concat', 'Axis', {'axis': 3}, [query, rel_query], prefix + 'q128')
            # The official CPU Attention uses sqrt(head_dim), independently of
            # attnScale. sqrt(2) compensates 128 vs the original 64 dimensions.
            factor = constant([2 ** .5], [], 'DT_FLOAT', prefix + 'factor')
            query = op('BinaryOp', 'BinaryOp', {'opType': 'MUL', 'T': 'DT_FLOAT'}, [query, factor], prefix + 'scaled_q')
            key = op('Concat', 'Axis', {'axis': 3}, [key, rel_positions], prefix + 'k128')
            zero = constant([0.], [], 'DT_FLOAT', prefix + 'zero')
            zeros = op('BinaryOp', 'BinaryOp', {'opType': 'MUL', 'T': 'DT_FLOAT'}, [value, zero], prefix + 'zero_value')
            value = op('Concat', 'Axis', {'axis': 3}, [value, zeros], prefix + 'v128')
        seq_indices = []
        for kind, index in [('q', query), ('k', key), ('v', value)]:
            out = fresh(f'fused_{count}_{kind}_seq')
            new_ops.append({'type': 'Permute', 'main_type': 'Permute', 'main': {'dims': [0, 2, 1, 3]},
                            'name': data['tensorName'][out], 'inputIndexes': [index], 'outputIndexes': [out]})
            seq_indices.append(out)
        bias_scaled, bias_masked = fresh(f'fused_{count}_relative_scaled'), fresh(f'fused_{count}_relative_masked')
        new_ops.extend([
            {'type': 'BinaryOp', 'main_type': 'BinaryOp', 'main': {'opType': 'MUL', 'T': 'DT_FLOAT'},
             'name': data['tensorName'][bias_scaled], 'inputIndexes': [relative, scale['inputIndexes'][1]], 'outputIndexes': [bias_scaled]},
            {'type': 'Select', 'main_type': 'NONE', 'name': data['tensorName'][bias_masked],
             'inputIndexes': [condition, minus_inf, bias_scaled], 'outputIndexes': [bias_masked]},
            {'type': 'Attention', 'main_type': 'AttentionParam', 'main': {'kv_cache': False, 'attnScale': .125},
             'name': f'relative_attention_{count}', 'inputIndexes': seq_indices + [bias_masked],
             'outputIndexes': reshape['outputIndexes']},
        ])
        if a.concat_relative:
            # Drop additive relative bias nodes; the Q/K dot product includes it.
            new_ops = new_ops[:-3]
            flat = fresh(prefix + 'flat')
            new_ops.append({'type': 'Attention', 'main_type': 'AttentionParam', 'main': {'kv_cache': False},
                            'name': prefix + 'attention', 'inputIndexes': seq_indices + [next(
                                op['outputIndexes'][0] for op in ops if op['type'] == 'Input' and op['name'] == 'mask')],
                            'outputIndexes': [flat]})
            shape = constant([1, 1, 16, 128], [4], 'DT_INT32', prefix + 'shape')
            heads = op('Reshape', 'Reshape', {'dimType': 'NCHW'}, [flat, shape], prefix + 'heads')
            begin = constant([0, 0, 0, 0], [4], 'DT_INT32', prefix + 'begin')
            size = constant([1, 1, 16, 64], [4], 'DT_INT32', prefix + 'size')
            cropped = op('SliceTf', 'NONE', None, [heads, begin, size], prefix + 'cropped')
            final_shape = constant([1, 1, 1024], [3], 'DT_INT32', prefix + 'final_shape')
            new_ops.append({'type': 'Reshape', 'main_type': 'Reshape', 'main': {'dimType': 'NCHW'},
                            'name': prefix + 'output', 'inputIndexes': [cropped, final_shape],
                            'outputIndexes': reshape['outputIndexes']})
        replacements[id(reshape)] = new_ops
        remove.update(id(x) for x in [content, addition, scale, masked, softmax, post, weighted, transpose])
        count += 1
    if count != 14:
        raise RuntimeError(f'Expected 14 attention layers, found {count}')
    rewritten = []
    for op in ops:
        if id(op) in replacements:
            rewritten.extend(replacements[id(op)])
        elif id(op) not in remove:
            rewritten.append(op)
    # Remove operations that no longer contribute to an official model output.
    by_output = {i: op for op in rewritten for i in op.get('outputIndexes', [])}
    needed = set()
    def visit(index):
        op = by_output[index]
        if id(op) in needed:
            return
        needed.add(id(op))
        for i in op.get('inputIndexes', []):
            visit(i)
    for name in data['outputName']:
        visit(data['tensorName'].index(name))
    data['oplists'] = [op for op in rewritten if id(op) in needed]
    for op in data['oplists']:
        if op.get('main') is None:
            op.pop('main', None)
    data['tensorNumber'] = len(data['tensorName'])
    variant = 'relative128' if a.concat_relative else 'attention'
    patched = a.work / ('split8-' + variant + '-fused.json')
    patched.write_text(json.dumps(data))
    name = 'llm_decode_split_' + variant + '_int8'
    with (a.work / ('convert-split-' + variant + '.log')).open('w') as log:
        subprocess.run([str(a.converter), '-f', 'JSON', '--modelFile', str(patched),
                        '--MNNModel', str(destination / (name + '.mnn'))], stdout=log, stderr=subprocess.STDOUT, check=True)
    shutil.copyfile(str(source) + '.weight', destination / (name + '.mnn.weight'))
    config = json.loads((destination / 'config.json').read_text())
    config[variant + '8'] = name + '.mnn'
    config['decode_weights'][variant + '8'] = 'int8'
    config['decode_contracts'][variant + '8'] = copy.deepcopy(config['decode_contracts']['split8'])
    (destination / 'config.json').write_text(json.dumps(config, indent=2))
    print('Replaced', count, 'attention blocks; operations', len(ops), '->', len(data['oplists']), flush=True)


if __name__ == '__main__':
    main()
