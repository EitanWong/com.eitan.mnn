#!/usr/bin/env python3
"""Collect real decode activations and export an equivalent channel-scaled graph.

Only Python graph export changes. Official MNN source and converters are unmodified.
This is an AWQ-style calibration experiment, not QAT or a quality guarantee.
"""
import argparse
import json
from pathlib import Path
import sys

import numpy as np
import torch
from torch import nn
from torch.nn import functional as F

from export import CachedDecoder, load_subset
from optimize_decode import SingleTokenDecoder
from optimize_state import SplitStateDecoder


CALIBRATION_TEXTS = [
    '早上好，今天我们来学习如何使用本地语音合成。',
    '我想订一张明天上午去上海的火车票。',
    'Welcome back. Your files are ready, and you can start whenever you like.',
    'Open the application and choose a voice for the next message.',
]


def load_decoder(args):
    sys.path.insert(0, str(args.source_code.resolve()))
    decoder = CachedDecoder().eval()
    load_subset(decoder, torch.load(args.models / 'source/llm.pt', map_location='cpu', weights_only=True))
    return SplitStateDecoder(SingleTokenDecoder(decoder)).eval()


def collect(args):
    from runtime import Pipeline
    module = load_decoder(args)
    samples = {}
    handles = []
    for index, layer in enumerate(module.decoder.llm.encoders):
        for kind, linear in [('qkv', layer.self_attn.linear_qkv), ('out', layer.self_attn.linear_out),
                             ('ff1', layer.feed_forward.w_1), ('ff2', layer.feed_forward.w_2)]:
            key = f'l{index}_{kind}'
            samples[key] = []
            def capture(layer, inputs, key=key):
                if len(samples[key]) < args.samples:
                    samples[key].append(inputs[0].detach().reshape(-1, inputs[0].shape[-1])[0].clone())
            handles.append(linear.register_forward_pre_hook(capture))
    pipeline = Pipeline(args.models, args.work, compute='low', hift_backend=1, flow_backend=1,
                        llm_backend=0, decode_profile='split8', decode_memory=2,
                        prompt_path=args.models / 'common/prompt-1.0-fullspeaker.npz')
    original = pipeline.graphs['llm_decode']
    class Observe:
        step = 0
        def __getattr__(self, name):
            return getattr(original, name)
        def __call__(self, *values, **kwargs):
            if self.step % args.stride == 0 and len(samples['l0_qkv']) < args.samples:
                arrays = [original.array(value) for value in values]
                with torch.inference_mode():
                    module(*(torch.from_numpy(value) for value in arrays))
            self.step += 1
            return original(*values, **kwargs)
    observer = Observe()
    pipeline.graphs['llm_decode'] = observer
    corpus = []
    for seed in [17, 23]:
        for text in CALIBRATION_TEXTS:
            _, metrics = pipeline.synthesize(text, steps=2, seed=seed, flow_guidance=.7, max_token_text_ratio=40)
            corpus.append({'text': text, 'seed': seed, 'speech_tokens': metrics['speech_tokens']})
            print('collected', len(samples['l0_qkv']), text, flush=True)
    for handle in handles:
        handle.remove()
    data = {key: torch.stack(values).numpy() for key, values in samples.items()}
    args.calibration.parent.mkdir(parents=True, exist_ok=True)
    np.savez(args.calibration, **data)
    args.calibration.with_suffix('.json').write_text(json.dumps({
        'texts': corpus, 'stride': args.stride, 'samples': len(data['l0_qkv']),
        'teacher': 'official MNN split8 CPU4 low compute; float PyTorch layer activations on its actual KV/input',
        'reference': 'common/prompt-1.0-fullspeaker.npz', 'purpose': 'calibration only; excludes evaluation texts/seeds',
    }, indent=2, ensure_ascii=False) + '\n')


@torch.inference_mode()
def fake_hqq(weight, block=32):
    # Mirror the public converter's fixed-beta HQQ equations in Python for scale search.
    # Actual deployable weights are produced by the official converter separately.
    shape = weight.shape
    w = weight.reshape(-1, block)
    lower, upper = w.amin(1, keepdim=True), w.amax(1, keepdim=True)
    scale = (upper - lower).div(15).clamp_min(1e-7)
    inv = scale.reciprocal()
    zero = -lower * inv
    for _ in range(20):
        q = (w * inv + zero).round().clamp(0, 15)
        error = w - (q - zero) * scale
        shrink = error.sign() * (error.abs() - .1 * error.abs().clamp_min(1e-8).pow(-.3)).clamp_min(0)
        zero = (q - (w - shrink) * inv).mean(1, keepdim=True)
    return (((w * inv + zero).round().clamp(0, 15) - zero) * scale).reshape(shape)


@torch.inference_mode()
def search(linear, inputs, block=32):
    w = linear.weight.detach()
    inputs = inputs.float()
    target = F.linear(inputs, w)
    grouped = w.reshape(-1, block)
    w_mean = (grouped.abs() / (grouped.abs().amax(1, keepdim=True) + 1e-6)).reshape(w.shape).mean(0)
    x_mean = inputs.abs().mean(0)
    best_scale, best_loss, history = torch.ones(w.shape[1]), float('inf'), []
    for ratio in [None, 0., .25, .5, .75, 1.]:
        scale = torch.ones(w.shape[1]) if ratio is None else (
            x_mean.pow(ratio) / (w_mean.pow(1 - ratio) + 1e-4)).clamp(1e-3, 1e3)
        scale = scale / (scale.max() * scale.min()).sqrt()
        # Include an approximate per-token INT8 activation quantization in this proxy.
        scaled_x = inputs / scale
        amax = scaled_x.abs().amax(1, keepdim=True).clamp_min(1e-8)
        quant_x = (scaled_x * (127 / amax)).round().clamp(-127, 127) * (amax / 127)
        predicted = F.linear(quant_x, fake_hqq(w * scale, block))
        loss = float((predicted - target).square().mean())
        history.append({'ratio': ratio, 'proxy_mse': loss})
        if loss < best_loss:
            best_scale, best_loss = scale.clone(), loss
    return best_scale, history


@torch.inference_mode()
def export_calibrated(args):
    module = load_decoder(args)
    rng = np.random.default_rng(20261029)
    table = np.load(args.models / 'common/projected_positions.npy', mmap_mode='r')
    middle = table.shape[2] // 2
    checks = []
    for past in [31, 199, 400]:
        x = torch.from_numpy(rng.standard_normal((1, 1, 1024), dtype=np.float32))
        states = [torch.from_numpy(rng.standard_normal((1, 16, past, 64), dtype=np.float32) * .1) for _ in range(28)]
        positions = torch.from_numpy(table[:, :, middle - past:middle + 1].copy())
        mask = torch.ones(1, 1, past + 1, dtype=torch.int32); mask[:, :, 0] = 0
        inputs = (x, positions, mask, *states)
        checks.append((past, inputs, tuple(value.clone() for value in module(*inputs))))
    data = np.load(args.calibration)
    report = []
    scales = {}
    for index, layer in enumerate(module.decoder.llm.encoders):
        targets = [('qkv', layer.norm1, layer.self_attn.linear_qkv),
                   ('ff1', layer.norm2, layer.feed_forward.w_1)]
        if args.scale_ff2:
            targets.append(('ff2', layer.feed_forward.w_1, layer.feed_forward.w_2))
        for kind, previous, linear in targets:
            key = f'l{index}_{kind}'
            scale, history = search(linear, torch.from_numpy(data[key]), args.block)
            if isinstance(previous, nn.LayerNorm):
                previous.weight.div_(scale); previous.bias.div_(scale)
            else:
                if not isinstance(layer.feed_forward.activation, nn.ReLU):
                    raise ValueError('Positive channel scaling requires a homogeneous ReLU activation')
                previous.weight.div_(scale[:, None]); previous.bias.div_(scale)
            linear.weight.mul_(scale[None])
            scales[key] = scale.numpy()
            row = {'layer': index, 'kind': kind, 'block': args.block,
                   'scale_min': float(scale.min()), 'scale_max': float(scale.max()),
                   'history': history, 'best_proxy_mse': min(h['proxy_mse'] for h in history)}
            report.append(row)
            print(json.dumps(row), flush=True)
            (args.work / (args.variant + '-search.json')).write_text(json.dumps(report, indent=2))
    parity = []
    for past, inputs, expected in checks:
        actual = module(*inputs)
        errors = [float((a - b).abs().max()) for a, b in zip(actual, expected)]
        parity.append({'past': past, 'max_abs_logits': errors[0], 'max_abs_state': max(errors[1:])})
        if max(errors) > .002:
            raise RuntimeError('Float equivalence failed: ' + str(parity[-1]))
    (args.work / (args.variant + '-float-parity.json')).write_text(json.dumps(parity, indent=2))
    np.savez(args.work / (args.variant + '-scales.npz'), **scales)
    names = [f'{kind}_{index}' for index in range(14) for kind in ['key', 'value']]
    outputs = ['logits'] + ['next_' + name for name in names]
    dynamic = {'positions': {2: 'total'}, 'mask': {2: 'total'}}
    dynamic.update({name: {2: 'past'} for name in names})
    dynamic.update({'next_' + name: {2: 'total'} for name in names})
    path = args.work / ('ONNX/llm_decode_' + args.variant + '.onnx')
    torch.onnx.export(module, inputs, str(path), input_names=['embeddings', 'positions', 'mask'] + names,
                      output_names=outputs, dynamic_axes=dynamic, opset_version=17, dynamo=False)
    print('exported', path, parity, flush=True)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for name in ['models', 'work', 'source-code']:
        p.add_argument('--' + name, type=Path, required=True)
    p.add_argument('--stage', choices=['collect', 'export'], required=True)
    p.add_argument('--calibration', type=Path)
    p.add_argument('--samples', type=int, default=192)
    p.add_argument('--stride', type=int, default=8)
    p.add_argument('--block', type=int, choices=[16, 32, 64, 128], default=32,
                   help='HQQ proxy group size; match the subsequent official conversion')
    p.add_argument('--variant', default='splitawqsafe', help='Distinct output profile name for each scaling experiment')
    p.add_argument('--scale-ff2', action='store_true',
                   help='Experimental ReLU-output scaling; changes the upstream FF1 weight distribution as well')
    args = p.parse_args()
    args.calibration = args.calibration or args.work / 'Calibration/decode-inputs.npz'
    if args.samples < 8 or args.stride < 1:
        p.error('samples >= 8 and stride >= 1 required')
    if not args.variant.isalnum() or not args.variant.startswith('split'):
        p.error('variant must start with split and contain only letters/digits')
    torch.set_num_threads(4)
    torch.manual_seed(20261029)
    if args.stage == 'collect':
        collect(args)
    else:
        export_calibrated(args)


if __name__ == '__main__':
    main()
