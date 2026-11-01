#!/usr/bin/env python3
"""Use official converter per-operation mixed precision for an existing decode ONNX."""
import argparse
import copy
import json
from pathlib import Path
import subprocess


POLICIES = {
    'ffn4': ('mixed4', ('feed_forward',), 'FFN int4 HQQ; attention/input/head int8 HQQ'),
    'body4': ('body4', ('feed_forward', 'linear_qkv', 'linear_out'),
              'FFN/attention int4 HQQ; input/head int8 HQQ'),
    'body4-out8': ('body4out8', ('feed_forward', 'linear_qkv'),
                   'FFN/QKV int4 HQQ; attention output/input/head int8 HQQ'),
    'body4-qkv8': ('body4qkv8', ('feed_forward', 'linear_out'),
                   'FFN/attention output int4 HQQ; QKV/input/head int8 HQQ'),
}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for key in ['models', 'work', 'converter']:
        p.add_argument('--' + key, type=Path, required=True)
    p.add_argument('--base', default='split', help='Existing split-state ONNX variant name, e.g. split or splitstatic384')
    p.add_argument('--contract-profile', help='Existing profile with the same graph interface; defaults to base + 8')
    p.add_argument('--block', type=int, default=32)
    p.add_argument('--ffn-block', type=int, choices=[16, 32, 64, 128, 256],
                   help='Override only FFN weight groups; other operations keep --block')
    p.add_argument('--qkv-block', type=int, choices=[16, 32, 64, 128, 256],
                   help='Override only QKV weight groups; other operations keep --block')
    p.add_argument('--policy', choices=list(POLICIES), default='ffn4',
                   help='Choose INT4 projections; input/head always remain INT8')
    p.add_argument('--quantizer', choices=['minmax', 'hqq'], default='minmax',
                   help='Default minmax reproduces historical weights; HQQ uses a distinct filename and verified converter banner')
    p.add_argument('--force', action='store_true', help='Regenerate an existing candidate and reset its validation status')
    a = p.parse_args()
    destination = a.models / 'experimental-decode'
    tag, int4_patterns, description = POLICIES[a.policy]
    base_name = 'llm_decode_' + a.base + '_' + tag + '_b' + str(a.block)
    overrides = [('ffn', a.ffn_block), ('qkv', a.qkv_block)]
    suffix = ''.join('_' + key + str(value) for key, value in overrides if value is not None)
    if a.quantizer == 'hqq':
        suffix += '_hqq_enabled'
    name = base_name + suffix
    label = a.base + tag + 'b' + str(a.block) + ''.join(
        key + str(value) for key, value in overrides if value is not None) + ('hqqenabled' if a.quantizer == 'hqq' else '')
    model_config = json.loads((destination / 'config.json').read_text())
    if not a.force and (label in model_config or (destination / (name + '.mnn')).exists() or
                        (destination / (name + '.mnn.weight')).exists()):
        p.error('Candidate already exists; use --force to regenerate and reset validation')
    # Group overrides change the strategy, not the converter's operation names.
    guide = a.work / (base_name + '-guide.json')
    strategy = a.work / (name + '-strategy.json')
    common = [str(a.converter.resolve()), '-f', 'ONNX', '--modelFile',
              str((a.work / 'ONNX' / ('llm_decode_' + a.base + '.onnx')).resolve()),
              '--targetVersion', '3.6.1', '--optimizePrefer', '2', '--saveExternalData',
              '--weightQuantBits', '4', '--weightQuantBlock', str(a.block), '--weightQuantAsymmetric=1']
    if a.quantizer == 'hqq':
        common.append('--hqq')
    # The converter generates its own operation names and quantization guide.
    if not guide.exists():
        command = common + ['--MNNModel', str((destination / (name + '_guide.mnn')).resolve()),
                            '--compressionParamsFile', str(guide.resolve())]
        with (a.work / (name + '-guide.log')).open('w') as log:
            subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
    config = json.loads(guide.read_text())
    config['forGuide'] = True  # Weight compression only; no activation scales supplied.
    layers = next(x['quantParams']['layer'] for x in config['algo'] if 'quantParams' in x)
    for layer in layers:
        quantize = any(pattern in layer['opName'] for pattern in int4_patterns)
        bits = 4 if quantize else 8
        block = a.block
        if a.ffn_block is not None and 'feed_forward' in layer['opName']:
            block = a.ffn_block
        if a.qkv_block is not None and 'linear_qkv' in layer['opName']:
            block = a.qkv_block
        for weight in layer['weight']:
            weight.update(bits=bits, blockSize=block, asymmetric=True)
    if not any(w['bits'] == 4 for layer in layers for w in layer['weight']):
        raise RuntimeError('No FFN operations matched; refuse a mislabeled mixed model')
    strategy.write_text(json.dumps(config, indent=2))
    command = common + ['--MNNModel', str((destination / (name + '.mnn')).resolve()),
                        '--compressionParamsFile', str(strategy.resolve())]
    (a.work / ('command-' + name + '.json')).write_text(json.dumps(command, indent=2))
    with (a.work / ('convert-' + name + '.log')).open('w') as log:
        subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
    log_text = (a.work / ('convert-' + name + '.log')).read_text()
    if 'Converted Success' not in log_text:
        raise RuntimeError('Conversion failed')
    if a.quantizer == 'hqq' and ('Use HQQ to quant weight' not in log_text or 'Disable Hqq' in log_text):
        raise RuntimeError('Converter did not confirm HQQ; refuse to register a mislabeled model')
    model_config = json.loads((destination / 'config.json').read_text())
    model_config[label] = name + '.mnn'
    model_config['decode_contracts'][label] = copy.deepcopy(model_config['decode_contracts'][a.contract_profile or a.base + '8'])
    description = description.replace('HQQ', 'HQQ' if a.quantizer == 'hqq' else 'asymmetric min/max')
    model_config['decode_weights'][label] = description + (
        f'; FFN block{a.ffn_block or a.block}, QKV block{a.qkv_block or a.block}, other block{a.block}'
        if any(value is not None for _, value in overrides) else '')
    model_config.setdefault('quantization_provenance', {})[label] = {
        'quantizer': a.quantizer, 'asymmetric_cli': '--weightQuantAsymmetric=1',
        'hqq_banner_verified': a.quantizer == 'hqq', 'conversion_log': 'convert-' + name + '.log'}
    model_config.setdefault('profile_validation', {})[label] = (
        'EXPERIMENTAL: regenerated by official converter; numeric and full-synthesis quality validation required')
    (destination / 'config.json').write_text(json.dumps(model_config, indent=2))
    # Temporary guide model is reproducible and was only created by this command.
    for suffix in ['_guide.mnn', '_guide.mnn.weight']:
        (destination / (name + suffix)).unlink(missing_ok=True)
    print(label, 'registered', len(layers), 'operations', flush=True)


if __name__ == '__main__':
    main()
