#!/usr/bin/env python3
"""Export equivalent single-token decoders: fused QKV and optional fixed KV capacity.

Only Python export graphs change; the official MNN converter/runtime are unmodified.
The existing dynamic graph still handles prefill. Experimental files are separate.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import types

import numpy as np
import torch
from torch import nn

from export import CachedDecoder, load_subset, projected_attention


class SingleTokenDecoder(nn.Module):
    def __init__(self, decoder, capacity=None):
        super().__init__()
        self.decoder = decoder
        self.capacity = capacity
        for layer in decoder.llm.encoders:
            attention = layer.self_attn
            if not hasattr(attention, 'linear_qkv'):
                attention.linear_qkv = nn.Linear(1024, 3072)
                with torch.no_grad():
                    attention.linear_qkv.weight.copy_(torch.cat([attention.linear_q.weight, attention.linear_k.weight,
                                                                 attention.linear_v.weight]))
                    attention.linear_qkv.bias.copy_(torch.cat([attention.linear_q.bias, attention.linear_k.bias,
                                                               attention.linear_v.bias]))

    def forward(self, embeddings, cache, positions, mask):
        x = self.decoder.llm.embed.out(embeddings) * self.decoder.llm.embed.pos_enc.xscale
        updates = []
        for i, layer in enumerate(self.decoder.llm.encoders):
            residual = x
            normalized = layer.norm1(x)
            attention = layer.self_attn
            q, k, v = attention.linear_qkv(normalized).reshape(1, 1, 3, 16, 64).unbind(2)
            q, k, v = q.transpose(1, 2), k.transpose(1, 2), v.transpose(1, 2)
            old_k, old_v = cache[i:i + 1].split(64, dim=-1)
            k, v = torch.cat([old_k, k], dim=2), torch.cat([old_v, v], dim=2)
            new_cache = torch.cat([k, v], dim=-1)
            updates.append(new_cache[:, :, 1:] if self.capacity else new_cache)
            # At Q=1, the official relative shift is exactly the first P+1
            # entries. Negative positions and rel_shift are unnecessary.
            ac = (q + attention.pos_bias_u[None, :, None]) @ k.transpose(-2, -1)
            bd = (q + attention.pos_bias_v[None, :, None]) @ positions[i:i + 1].transpose(-2, -1)
            scores = (ac + bd) / 8
            masked = mask[:, None] == 0
            probabilities = scores.masked_fill(masked, -float('inf')).softmax(-1).masked_fill(masked, 0.)
            context = (probabilities @ v).transpose(1, 2).reshape(1, 1, 1024)
            x = residual + attention.linear_out(context)
            x = x + layer.feed_forward(layer.norm2(x))
        logits = self.decoder.llm_decoder(self.decoder.llm.after_norm(x)[:, -1])
        return logits, torch.cat(updates)


def quantize_decoders(a):
    from runtime import Graph
    from validate import metrics
    destination = a.models / 'experimental-decode'
    config = json.loads((destination / 'config.json').read_text())
    table = np.load(a.models / 'common/projected_positions.npy', mmap_mode='r')
    middle = table.shape[2] // 2
    rng = np.random.default_rng(20261028)
    report = []
    for profile in a.quant_profiles:
        original = Path(config[profile]).stem
        suffix = (('' if a.quant_block == 64 else '_b' + str(a.quant_block)) +
                  ('_sym' if a.quant_symmetric else '') + ('_hqq' if a.quant_hqq else ''))
        label = profile + str(a.quant_bits) + suffix.replace('_', '')
        name = original + '_int' + str(a.quant_bits) + suffix
        command = [str(a.converter.resolve()), '-f', 'ONNX', '--modelFile',
            str((a.work / 'ONNX' / (original + '.onnx')).resolve()), '--MNNModel',
            str((destination / (name + '.mnn')).resolve()), '--bizCode', 'CosyVoice-300M-decode-quantized',
            '--targetVersion', '3.6.1', '--optimizePrefer', '2', '--saveExternalData',
            '--weightQuantBits', str(a.quant_bits), '--weightQuantBlock', str(a.quant_block)]
        if not a.quant_symmetric:
            command.append('--weightQuantAsymmetric')
        if a.quant_hqq:
            command.append('--hqq')
        (a.work / ('command-' + name + '.json')).write_text(json.dumps(command, indent=2))
        with (a.work / ('convert-' + name + '.log')).open('w') as log:
            subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
        if 'Converted Success' not in (a.work / ('convert-' + name + '.log')).read_text():
            raise RuntimeError(name + ' conversion failed')
        config[label] = name + '.mnn'
        config.setdefault('decode_weights', {})[label] = 'int' + str(a.quant_bits)
        (destination / 'config.json').write_text(json.dumps(config, indent=2))
        capacity = config['capacity'] if profile == 'static' else None
        cases = []
        for past in [31, 199, min(400, config['capacity'] - 1)]:
            cache = rng.standard_normal((14, 16, past, 128), dtype=np.float32) * .1
            x = rng.standard_normal((1, 1, 1024), dtype=np.float32)
            if capacity:
                cache = np.pad(cache, ((0, 0), (0, 0), (capacity - past, 0), (0, 0)))
                positions = table[:, :, middle - capacity:middle + 1].copy()
                mask = np.zeros((1, 1, capacity + 1), np.int32)
                mask[:, :, -past:] = 1
            else:
                positions = table[:, :, middle - past:middle + 1].copy()
                mask = np.ones((1, 1, past + 1), np.int32)
                mask[:, :, 0] = 0
            cases.append((past, (x, cache, positions, mask)))
        reference = Graph(None, 'llm_cached', engine='onnx', onnx=a.work / 'ONNX' / (original + '.onnx'))
        expected = [reference(*values) for _, values in cases]
        del reference
        for memory in [0, 2]:
            graph = Graph(destination / (name + '.mnn'), 'llm_cached', precision='low', memory=memory)
            for (past, values), target in zip(cases, expected):
                actual = graph(*values)
                finite = all(np.isfinite(x).all() for x in actual)
                row = {'profile': label, 'memory': memory, 'past': past, 'finite': finite}
                if finite:
                    row['outputs'] = [metrics(x, y) for x, y in zip(actual, target)]
                    row['top25_overlap'] = len(set(np.argsort(actual[0].ravel())[-25:]) &
                                               set(np.argsort(target[0].ravel())[-25:])) / 25
                report.append(row)
                print(json.dumps(row), flush=True)
                report_name = 'decode-int' + str(a.quant_bits) + suffix + '-numeric-validation.json'
                (a.work / report_name).write_text(json.dumps(report, indent=2))
            del graph


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source-code', type=Path, required=True)
    p.add_argument('--models', type=Path, required=True)
    p.add_argument('--work', type=Path, required=True)
    p.add_argument('--converter', type=Path, required=True)
    p.add_argument('--capacity', type=int, default=512)
    p.add_argument('--quantize-only', action='store_true', help='Convert existing decode ONNX graphs to experimental INT8')
    p.add_argument('--quant-bits', type=int, choices=[4, 8], default=8)
    p.add_argument('--quant-block', type=int, default=64)
    p.add_argument('--quant-symmetric', action='store_true')
    p.add_argument('--quant-hqq', action='store_true')
    p.add_argument('--quant-profiles', nargs='+', choices=['compact', 'static'], default=['compact', 'static'])
    a = p.parse_args()
    if a.capacity < 256 or a.capacity > 2047:
        p.error('capacity must be in [256, 2047]')
    sys.path.insert(0, str(a.source_code.resolve()))
    torch.set_num_threads(4)
    if a.quantize_only:
        quantize_decoders(a)
        return
    torch.manual_seed(20261028)
    destination = a.models / 'experimental-decode'
    destination.mkdir(exist_ok=True)
    onnx_root = a.work / 'ONNX'
    decoder = CachedDecoder().eval()
    state = torch.load(a.models / 'source/llm.pt', map_location='cpu', weights_only=True)
    load_subset(decoder, state)
    del state
    decoder.projected = True
    for layer in decoder.llm.encoders:
        layer.self_attn.forward = types.MethodType(projected_attention, layer.self_attn)
    table = np.load(a.models / 'common/projected_positions.npy', mmap_mode='r')
    middle = table.shape[2] // 2
    cases = []
    with torch.inference_mode():
        for past in [31, 199, min(400, a.capacity - 1)]:
            embeddings = torch.randn(1, 1, 1024)
            cache = torch.randn(14, 16, past, 128) * .1
            full_positions = torch.from_numpy(table[:, :, middle - past:middle + past + 1].copy())
            mask = torch.ones(1, 1, past + 1, dtype=torch.int32)
            mask[:, :, 0] = 0
            expected = tuple(x.clone() for x in decoder(embeddings, cache, full_positions, mask))
            positive = full_positions[:, :, :past + 1].contiguous()
            cases.append((past, (embeddings, cache, positive, mask), expected))
    report = []
    for capacity in [None, a.capacity]:
        name = 'llm_decode_compact' if capacity is None else 'llm_decode_static' + str(capacity)
        module = SingleTokenDecoder(decoder, capacity).eval()
        prepared = []
        with torch.inference_mode():
            for past, inputs, expected in cases:
                if capacity:
                    x, cache, _, _ = inputs
                    cache = torch.nn.functional.pad(cache, (0, 0, capacity - past, 0))
                    positions = torch.from_numpy(table[:, :, middle - capacity:middle + 1].copy())
                    mask = torch.zeros(1, 1, capacity + 1, dtype=torch.int32)
                    mask[:, :, -past:] = 1  # Exclude the sentinel, include the new key.
                    values = (x, cache, positions, mask)
                else:
                    values = inputs
                result = module(*values)
                cache_actual = result[1][:, :, -past - 1:]
                errors = [float((result[0] - expected[0]).abs().max()),
                          float((cache_actual - expected[1]).abs().max())]
                if max(errors) > .001:
                    raise ValueError((name, past, errors))
                report.append({'graph': name, 'past': past, 'torch_max_abs_logits_cache': errors})
                prepared.append((past, values, expected))
            onnx_path = onnx_root / (name + '.onnx')
            dynamic = None if capacity else {'cache': {2: 'past'}, 'positions': {2: 'total'},
                                            'mask': {2: 'total'}, 'updated_cache': {2: 'total'}}
            torch.onnx.export(module, prepared[0][1], str(onnx_path),
                              input_names=['embeddings', 'cache', 'positions', 'mask'],
                              output_names=['logits', 'updated_cache'], dynamic_axes=dynamic,
                              opset_version=17, dynamo=False)
        command = [str(a.converter.resolve()), '-f', 'ONNX', '--modelFile', str(onnx_path.resolve()),
                   '--MNNModel', str((destination / (name + '.mnn')).resolve()),
                   '--bizCode', 'CosyVoice-300M-single-token', '--targetVersion', '3.6.1',
                   '--optimizePrefer', '2', '--saveExternalData', '--fp16']
        (a.work / ('command-' + name + '.json')).write_text(json.dumps(command, indent=2))
        with (a.work / ('convert-' + name + '.log')).open('w') as log:
            subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
        if 'Converted Success' not in (a.work / ('convert-' + name + '.log')).read_text():
            raise RuntimeError(name + ' conversion failed')
        from runtime import Graph
        from validate import metrics
        for precision in ['high', 'low']:
            graph = Graph(destination / (name + '.mnn'), 'llm_cached', precision=precision)
            for past, inputs, expected in prepared:
                actual = graph(*(x.numpy() for x in inputs))
                actual[1] = actual[1][:, :, -past - 1:]
                row = {'graph': name, 'past': past, 'compute': precision,
                       'outputs': [metrics(x, y.numpy()) for x, y in zip(actual, expected)],
                       'top25_overlap': len(set(np.argsort(actual[0].ravel())[-25:]) &
                                            set(np.argsort(expected[0].numpy().ravel())[-25:])) / 25}
                report.append(row)
                print(json.dumps(row), flush=True)
            del graph
        (a.work / 'decode-numeric-validation.json').write_text(json.dumps(report, indent=2))
    (destination / 'config.json').write_text(json.dumps({'capacity': a.capacity, 'weights': 'fp16',
        'status': 'experimental; full audio quality and RTF need separate validation',
        'prefill': 'fp16/llm_cached_projected.mnn',
        'compact': 'llm_decode_compact.mnn', 'static': 'llm_decode_static' + str(a.capacity) + '.mnn'}, indent=2))


if __name__ == '__main__':
    main()
