#!/usr/bin/env python3
"""Export experimental fixed-Q verification graphs from the accepted Q=1 binary.

This tool does not implement speculative sampling or a draft model. Graphs retain
the original external quantized weight bytes, with causal multi-query attention
and logits for every input position. Numeric differences require separate checks.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
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
    parser.add_argument('--queries', nargs='+', type=int, choices=[1, 2, 4, 8], default=[1, 2, 4, 8])
    parser.add_argument('--force', action='store_true', help='Regenerate selected graphs and reset their validation status')
    args = parser.parse_args()
    if len(set(args.queries)) != len(args.queries):
        parser.error('Query sizes must be unique')
    config = json.loads((args.models / 'experimental-decode/config.json').read_text())
    source = args.models / 'experimental-decode' / config[SOURCE_PROFILE]
    contract = config['decode_contracts'][SOURCE_PROFILE]
    if (digest(source) != SOURCE_SHA256 or contract.get('fixed_query_length') != 1 or
            contract.get('state_layout') != 'per_layer_kv' or contract.get('capacity') or
            contract.get('internal_positions') or contract.get('excludes_sentinel') or
            len(contract['inputs']) != 31 or len(contract['outputs']) != 29):
        parser.error('Requires the documented accepted fixedshape Q=1 binary and original31/29 split-KV contract')
    destination = args.models / 'experimental-verify'
    config_path = destination / 'config.json'
    report = json.loads(config_path.read_text()) if config_path.exists() else {}
    targets = {q: destination / f'llm_verify_q{q}_body4out8_b64.mnn' for q in args.queries}
    if not args.force and any(p.exists() or Path(str(p) + '.weight').exists() or str(q) in report
                              for q, p in targets.items()):
        parser.error('Selected probe already exists; use --force to regenerate and reset validation')
    args.work.mkdir(parents=True, exist_ok=True)
    topology = args.work / 'multiquery-verify-source.json'
    with (args.work / 'multiquery-verify-source-dump.log').open('w') as log:
        subprocess.run([str(args.converter.resolve()), '-f', 'MNN', '--modelFile', str(source.resolve()),
                        '--JsonFile', str(topology.resolve())], check=True, stdout=log, stderr=subprocess.STDOUT)
    # JSON is used solely to locate topology. Values/weights come from the
    # original binary through official Expression APIs, including real -inf.
    data = json.loads(topology.read_text().replace('-inf', '-3.4028234e38'))
    if len(data['oplists']) != 846:
        raise ValueError('Unexpected source topology')
    names = data['tensorName']
    producer = {index: op for op in data['oplists'] for index in op.get('outputIndexes', [])}
    head_ops = [op for op in data['oplists'] if op['type'] == 'Reshape' and
                producer[op['inputIndexes'][0]]['type'] == 'GatherV2']
    if len(head_ops) != 1:
        raise ValueError('Expected one final token Gather before the head projection')
    source_weight = Path(str(source) + '.weight')
    weight_sha = digest(source_weight)
    destination.mkdir(exist_ok=True)

    import MNN
    import numpy as np
    f = MNN.expr

    def integer(values):
        array = np.array(values, np.int32)
        return f.const(array, list(array.shape), f.NCHW, f.int)

    for query, target in targets.items():
        variables = f.load_as_dict(str(source.resolve()))
        for name, shape, dtype in [('embeddings', [1, query, 1024], f.float),
                                   ('mask', [1, query, -1], f.int)]:
            new = f.placeholder(shape, f.NCHW, dtype)
            new.name = name
            variables[name].replace(new)
        changed = 0
        for op in data['oplists']:
            if op['type'] != 'Const':
                continue
            values = op['main'].get('int32s')
            if values in [[1, 1, 1024], [1, 1, 3072], [1, 1, 4096], [1, 1, 3, 16, 64]]:
                shape = values.copy()
                shape[1] = query
            elif values == [1, 4097]:
                shape = [1, query, 4097]
            else:
                continue
            variables[names[op['outputIndexes'][0]]].replace(integer(shape))
            changed += 1
        if changed != 60:
            raise ValueError('Expected60 projection/context/QKV shape constants')
        head = head_ops[0]
        gather = producer[head['inputIndexes'][0]]
        variables[names[head['outputIndexes'][0]]].set_inputs([
            variables[names[gather['inputIndexes'][0]]], variables[names[head['inputIndexes'][1]]]])
        relative_count = 0
        for op in data['oplists']:
            if op['type'] != 'MatMul':
                continue
            second = producer[op['inputIndexes'][1]]
            if second['type'] != 'Permute':
                continue
            position_slice = producer[second['inputIndexes'][0]]
            if (position_slice['type'] != 'StridedSlice' or
                    names[position_slice['inputIndexes'][0]] != 'positions'):
                continue
            layer = relative_count
            relative_count += 1
            if query == 1:
                continue
            raw = f.matmul(*[variables[names[index]] for index in op['inputIndexes']],
                           transposeA=op['main']['transposeA'], transposeB=op['main']['transposeB'])
            raw.name = op['name'] + '/unshifted'
            total = f.slice(f.shape(variables[f'next_key_{layer}']), [2], [1])
            size = f.concat([integer([1, 16, 1]), total], 0)
            rows = [f.slice(raw, [0, 0, i, query - 1 - i], size) for i in range(query)]
            variables[names[op['outputIndexes'][0]]].replace(f.concat(rows, 2))
        if relative_count != 14:
            raise ValueError('Expected14 relative-position matrix products')
        f.save([variables[name] for name in contract['outputs']], str(target.resolve()), 0)
        weight = Path(str(target) + '.weight')
        weight.unlink(missing_ok=True)
        try:
            weight.hardlink_to(source_weight)
        except OSError:
            shutil.copyfile(source_weight, weight)
        if digest(weight) != weight_sha:
            raise ValueError('External weight bytes changed')
        report[str(query)] = {'file': target.name, 'query': query,
            'inputs': contract['inputs'], 'outputs': contract['outputs'],
            'positions': 'float32[14,16,P+2*Q-1,64]; descending relative lags P+Q-1 through -(Q-1)',
            'mask': 'int32[1,Q,P+Q]; sentinel0 and upper-right future keys masked',
            'logits': 'float32[1,Q,4097]; one next-token distribution per input position',
            'source_profile': SOURCE_PROFILE, 'source_graph_sha256': SOURCE_SHA256,
            'graph_sha256': digest(target), 'weight_sha256': weight_sha,
            'status': 'EXPERIMENTAL: regenerated; numeric and speculative-sampling validation required'}
        config_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n')
        print(json.dumps(report[str(query)]), flush=True)
    (args.work / 'multiquery-verify-conversion.json').write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n')


if __name__ == '__main__':
    main()
