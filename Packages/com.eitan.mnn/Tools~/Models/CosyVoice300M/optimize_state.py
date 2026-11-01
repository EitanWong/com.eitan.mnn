#!/usr/bin/env python3
"""Export per-layer K/V state to avoid concatenating the whole cache each step."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import types

import numpy as np
import torch
from torch import nn

from export import CachedDecoder, load_subset
from optimize_decode import SingleTokenDecoder


class SplitStateDecoder(nn.Module):
    def __init__(self, compact, positions=None, capacity=None):
        super().__init__()
        self.decoder = compact.decoder
        self.register_buffer('position_table', positions)
        self.capacity = capacity

    def forward(self, embeddings, positions, mask, *states):
        x = self.decoder.llm.embed.out(embeddings) * self.decoder.llm.embed.pos_enc.xscale
        updates = []
        for i, layer in enumerate(self.decoder.llm.encoders):
            residual = x
            attention = layer.self_attn
            q, k, v = attention.linear_qkv(layer.norm1(x)).reshape(1, 1, 3, 16, 64).unbind(2)
            q, k, v = q.transpose(1, 2), k.transpose(1, 2), v.transpose(1, 2)
            k = torch.cat([states[2 * i], k], dim=2)
            v = torch.cat([states[2 * i + 1], v], dim=2)
            updates.extend([k[:, :, 1:], v[:, :, 1:]] if self.capacity else [k, v])
            ac = (q + attention.pos_bias_u[None, :, None]) @ k.transpose(-2, -1)
            relative = (torch.narrow(self.position_table[i:i + 1], 2, positions[0], k.shape[2])
                        if self.position_table is not None else positions[i:i + 1])
            bd = (q + attention.pos_bias_v[None, :, None]) @ relative.transpose(-2, -1)
            masked = mask[:, None] == 0
            probabilities = ((ac + bd) / 8).masked_fill(masked, -float('inf')).softmax(-1).masked_fill(masked, 0.)
            context = (probabilities @ v).transpose(1, 2).reshape(1, 1, 1024)
            x = residual + attention.linear_out(context)
            x = x + layer.feed_forward(layer.norm2(x))
        logits = self.decoder.llm_decoder(self.decoder.llm.after_norm(x)[:, -1])
        return (logits, *updates)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for key in ['source-code', 'models', 'work', 'converter']:
        p.add_argument('--' + key, type=Path, required=True)
    p.add_argument('--bake-positions', action='store_true', help='Move position slicing inside the decode module')
    p.add_argument('--static-linear', action='store_true', help='Export Q=1 Linear as explicit fixed-shape 1x1 convolution')
    p.add_argument('--capacity', type=int, help='Fixed per-layer KV capacity; rejects overlong input at runtime')
    a = p.parse_args()
    sys.path.insert(0, str(a.source_code.resolve()))
    torch.set_num_threads(4)
    torch.manual_seed(20261028)
    decoder = CachedDecoder().eval()
    load_subset(decoder, torch.load(a.models / 'source/llm.pt', map_location='cpu', weights_only=True))
    compact = SingleTokenDecoder(decoder).eval()
    if a.static_linear:
        def one_token_linear(layer, x):
            result = torch.nn.functional.conv2d(x.reshape(1, layer.in_features, 1, 1),
                layer.weight.reshape(layer.out_features, layer.in_features, 1, 1), layer.bias)
            # Only the final head consumes rank2 input; all other projections rank3.
            return result.reshape(1, layer.out_features) if x.ndim == 2 else result.reshape(1, 1, layer.out_features)
        for layer in decoder.modules():
            if isinstance(layer, nn.Linear):
                layer.forward = types.MethodType(one_token_linear, layer)
    table = np.load(a.models / 'common/projected_positions.npy', mmap_mode='r')
    middle = table.shape[2] // 2
    if a.capacity is not None and (a.capacity < 64 or a.capacity > 2047 or a.bake_positions):
        p.error('Capacity must be [64,2047], with external positions')
    module = SplitStateDecoder(compact, torch.from_numpy(table[:, :, :middle + 1].copy()) if a.bake_positions else None,
                               a.capacity).eval()
    names = [f'{kind}_{i}' for i in range(14) for kind in ['key', 'value']]
    output_names = ['logits'] + ['next_' + n for n in names]
    report = []
    with torch.inference_mode():
        for past in sorted(set([31, min(199, (a.capacity or 401) - 1), min(400, (a.capacity or 401) - 1)])):
            x = torch.randn(1, 1, 1024)
            cache = torch.randn(14, 16, past, 128) * .1
            positions = torch.from_numpy(table[:, :, middle - past:middle + 1].copy())
            mask = torch.ones(1, 1, past + 1, dtype=torch.int32)
            mask[:, :, 0] = 0
            states = [part.contiguous() for i in range(14) for part in cache[i:i + 1].split(64, -1)]
            position_input = torch.tensor([middle - past], dtype=torch.int32) if a.bake_positions else positions
            expected = compact(x, cache, positions, mask)
            if a.capacity:
                states = [torch.nn.functional.pad(s, (0, 0, a.capacity - past, 0)) for s in states]
                position_input = torch.from_numpy(table[:, :, middle - a.capacity:middle + 1].copy())
                mask = torch.zeros(1, 1, a.capacity + 1, dtype=torch.int32)
                mask[:, :, -past:] = 1
            inputs = (x, position_input, mask, *states)
            actual = module(*inputs)
            packed = torch.cat([torch.cat(actual[1 + i * 2:3 + i * 2], -1) for i in range(14)])
            if a.capacity:
                packed = packed[:, :, -past - 1:]
            errors = [float((actual[0] - expected[0]).abs().max()), float((packed - expected[1]).abs().max())]
            if max(errors) > .001:
                raise RuntimeError(errors)
            report.append({'past': past, 'torch_max_abs_logits_cache': errors})
            print(report[-1], flush=True)
        variant = ('internal' if a.bake_positions else 'split') + ('conv' if a.static_linear else '') + (
            'static' + str(a.capacity) if a.capacity else '')
        path = a.work / ('ONNX/llm_decode_' + variant + '.onnx')
        dynamic = {'mask': {2: 'total'}}
        if not a.bake_positions:
            dynamic['positions'] = {2: 'total'}
        dynamic.update({n: {2: 'past'} for n in names})
        dynamic.update({'next_' + n: {2: 'total'} for n in names})
        torch.onnx.export(module, inputs, str(path), input_names=['embeddings', 'positions', 'mask'] + names,
                          output_names=output_names, dynamic_axes=None if a.capacity else dynamic, opset_version=17, dynamo=False)
    (a.work / ('decode-' + variant + '-torch-validation.json')).write_text(json.dumps(report, indent=2))
    destination = a.models / 'experimental-decode'
    name = 'llm_decode_' + variant + '_int8'
    command = [str(a.converter.resolve()), '-f', 'ONNX', '--modelFile', str(path.resolve()),
               '--MNNModel', str((destination / (name + '.mnn')).resolve()), '--bizCode', 'CosyVoice-300M-split-state',
               '--targetVersion', '3.6.1', '--optimizePrefer', '2', '--saveExternalData',
               '--weightQuantBits', '8', '--weightQuantBlock', '64', '--weightQuantAsymmetric']
    (a.work / ('command-' + name + '.json')).write_text(json.dumps(command, indent=2))
    with (a.work / ('convert-' + name + '.log')).open('w') as log:
        subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
    if 'Converted Success' not in (a.work / ('convert-' + name + '.log')).read_text():
        raise RuntimeError('Conversion failed')
    config = json.loads((destination / 'config.json').read_text())
    label = variant + '8'
    config[label] = name + '.mnn'
    config.setdefault('decode_contracts', {})[label] = {'inputs': ['embeddings', 'positions', 'mask'] + names,
        'outputs': output_names, 'state_layout': 'per_layer_kv', 'internal_positions': a.bake_positions,
        'capacity': a.capacity}
    config.setdefault('decode_weights', {})[label] = 'int8'
    (destination / 'config.json').write_text(json.dumps(config, indent=2))


if __name__ == '__main__':
    main()
