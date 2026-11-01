#!/usr/bin/env python3
"""Export the original 50 Hz CosyVoice-300M; no native source changes.

Requires the pinned upstream Python source, torch, numpy, scipy and onnx.
Neural HiFT is separated from exact STFT/ISTFT DSP, avoiding a dense DFT graph.
"""
import argparse
import json
from pathlib import Path
import sys
import types

import numpy as np
import torch
from torch import nn
from torch.nn import functional as F


def conformer(size, output, heads, units, causal=False):
    from cosyvoice.transformer.encoder import ConformerEncoder
    return ConformerEncoder(input_size=size, output_size=output, attention_heads=heads,
                            linear_units=units, num_blocks=6, input_layer='linear',
                            pos_enc_layer_type='rel_pos_espnet', selfattention_layer_type='rel_selfattn',
                            use_cnn_module=False, macaron_style=False, static_chunk_size=1 if causal else 0)


class TextEncoder(nn.Module):
    def __init__(self):
        super().__init__()
        self.text_embedding = nn.Embedding(51866, 512)
        self.text_encoder = conformer(512, 1024, 16, 4096, True)
        self.text_encoder_affine_layer = nn.Linear(1024, 1024)

    def forward(self, tokens, lengths):
        # Vectorized causal mask: upstream subsequent_chunk_mask contains a
        # Python loop that tracing would freeze at the example text length.
        e = self.text_encoder
        mask = torch.arange(tokens.shape[1], device=tokens.device)[None, :] < lengths[:, None]
        mask = mask[:, None, :] & torch.ones((tokens.shape[1], tokens.shape[1]), dtype=torch.bool, device=tokens.device).tril()[None]
        x, positions, _ = e.embed(self.text_embedding(tokens), mask)
        for layer in e.encoders:
            x, _, _, _ = layer(x, mask, positions)
        x = e.after_norm(x)
        return self.text_encoder_affine_layer(x)


class CachedDecoder(nn.Module):
    def __init__(self):
        super().__init__()
        from cosyvoice.transformer.encoder import TransformerEncoder
        self.llm = TransformerEncoder(input_size=1024, output_size=1024, attention_heads=16,
                                     linear_units=4096, num_blocks=14, input_layer='linear_legacy',
                                     pos_enc_layer_type='rel_pos_espnet', selfattention_layer_type='rel_selfattn',
                                     static_chunk_size=1)
        self.llm_decoder = nn.Linear(1024, 4097)
        self.projected = False

    def forward(self, embeddings, cache, positions, mask):
        # Same official layers and relative-position shift as forward_chunk.
        # A masked sentinel key avoids zero-sized native tensors during prefill.
        x = self.llm.embed.out(embeddings) * self.llm.embed.pos_enc.xscale
        updates = []
        for i, layer in enumerate(self.llm.encoders):
            pos = positions[i:i + 1] if self.projected else positions
            x, _, updated, _ = layer(x, mask, pos, att_cache=cache[i:i + 1])
            updates.append(updated)
        return self.llm_decoder(self.llm.after_norm(x)[:, -1]), torch.cat(updates, dim=0)


def projected_attention(self, query, key, value, mask, pos_emb, cache):
    # Official relative attention algebra; linear_pos is already computed.
    q, k, v = self.forward_qkv(query, key, value)
    key_cache, value_cache = torch.split(cache, 64, dim=-1)
    k = torch.cat([key_cache, k], dim=2)
    v = torch.cat([value_cache, v], dim=2)
    updated = torch.cat([k, v], dim=-1)
    q = q.transpose(1, 2)
    ac = torch.matmul((q + self.pos_bias_u).transpose(1, 2), k.transpose(-2, -1))
    bd = torch.matmul((q + self.pos_bias_v).transpose(1, 2), pos_emb.transpose(-2, -1))
    bd = self.rel_shift(bd)
    return self.forward_attention(v, (ac + bd) / 8, mask), updated


class FlowEncoder(nn.Module):
    def __init__(self):
        super().__init__()
        self.input_embedding = nn.Embedding(4096, 512)
        self.encoder = conformer(512, 512, 8, 2048)
        self.encoder_proj = nn.Linear(512, 80)

    def forward(self, tokens, lengths):
        x, _ = self.encoder(self.input_embedding(tokens), lengths)
        return self.encoder_proj(x)


class HiFTCore(nn.Module):
    def __init__(self, hift):
        super().__init__()
        self.hift = hift

    def forward(self, mel, source_stft):
        h = self.hift
        x = h.conv_pre(mel)
        for i in range(h.num_upsamples):
            x = h.ups[i](F.leaky_relu(x, h.lrelu_slope))
            if i == h.num_upsamples - 1:
                x = h.reflection_pad(x)
            x = x + h.source_resblocks[i](h.source_downs[i](source_stft))
            x = sum(h.resblocks[i * h.num_kernels + j](x) for j in range(h.num_kernels)) / h.num_kernels
        return h.conv_post(F.leaky_relu(x))


def load_subset(module, state, prefixes=None):
    wanted = module.state_dict()
    selected = {k: state[k] for k in wanted}
    module.load_state_dict(selected, strict=True)


def export(module, args, path, names, outputs, axes):
    module.eval()
    if path.exists():
        return
    print('export', path.name, flush=True)
    with torch.inference_mode():
        torch.onnx.export(module, args, str(path), input_names=names, output_names=outputs,
                          dynamic_axes=axes, opset_version=17, dynamo=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-code', type=Path, required=True)
    parser.add_argument('--source-model', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--onnx', type=Path, required=True)
    parser.add_argument('--stage', choices=['llm', 'flow', 'hift'], required=True)
    parser.add_argument('--project-positions', action='store_true')
    parser.add_argument('--max-context', type=int, default=2048)
    parser.add_argument('--force', action='store_true', help='Regenerate ONNX graphs after changing exporter or source weights')
    a = parser.parse_args()
    sys.path.insert(0, str(a.source_code.resolve()))
    a.output.mkdir(parents=True, exist_ok=True)
    a.onnx.mkdir(parents=True, exist_ok=True)
    if a.force:
        stage_files = {'llm': ['text_encoder', 'llm_cached_projected' if a.project_positions else 'llm_cached'],
                       'flow': ['flow_encoder', 'flow_regulator'], 'hift': ['hift_f0', 'hift_core']}
        for name in stage_files[a.stage]:
            (a.onnx / (name + '.onnx')).unlink(missing_ok=True)
    torch.manual_seed(1234)
    torch.set_num_threads(4)
    state = torch.load(a.source_model / (a.stage + '.pt'), map_location='cpu', weights_only=True)
    if a.stage == 'llm':
        text = TextEncoder().eval()
        load_subset(text, state)
        export(text, (torch.tensor([[1, 2, 3, 4]]), torch.tensor([4])), a.onnx / 'text_encoder.onnx',
               ['tokens', 'lengths'], ['encoded'], {'tokens': {1: 'text'}, 'encoded': {1: 'text'}})
        del text
        decoder = CachedDecoder().eval()
        load_subset(decoder, state)
        # Prove sentinel prefill and cached step against official forward_chunk.
        e = torch.randn(1, 4, 1024)
        zero = torch.zeros(14, 16, 1, 128)
        pe = decoder.llm.embed.pos_enc
        positions = pe.position_encoding(0, 5)
        mask = torch.cat([torch.zeros(1, 4, 1, dtype=torch.bool), torch.ones(1, 4, 4, dtype=torch.bool).tril()], dim=2)
        with torch.inference_mode():
            logits, cache = decoder(e, zero, positions, mask)
            y, c, _ = decoder.llm.forward_chunk(e, 0, -1, att_mask=torch.ones(1, 4, 4, dtype=torch.bool).tril())
            error = float((logits - decoder.llm_decoder(y[:, -1])).abs().max())
            cache_error = float((cache[:, :, 1:] - c).abs().max())
        (a.output / 'cache-reference.json').write_text(json.dumps({'prefill_logits_max_abs': error, 'cache_max_abs': cache_error}, indent=2))
        if error > 0.001 or cache_error > 0.001:
            raise ValueError('Sentinel cache parity failed')
        name = 'llm_cached'
        axes = {'embeddings': {1: 'query'}, 'cache': {2: 'past'}, 'positions': {1: 'relative'},
                'mask': {1: 'query', 2: 'total'}, 'updated_cache': {2: 'total'}}
        if a.project_positions:
            projected = []
            with torch.inference_mode():
                pe.extend_pe(torch.zeros(1, a.max_context, 1024))
                table = pe.position_encoding(0, a.max_context)
                for layer in decoder.llm.encoders:
                    projected.append(layer.self_attn.linear_pos(table).view(1, -1, 16, 64).transpose(1, 2))
                    layer.self_attn.forward = types.MethodType(projected_attention, layer.self_attn)
                positions = torch.cat(projected, dim=0)
                decoder.projected = True
                np.save(a.output / 'projected_positions.npy', positions.numpy())
                middle = positions.shape[2] // 2
                positions = positions[:, :, middle - 4:middle + 5].contiguous()
                optimized, optimized_cache = decoder(e, zero, positions, mask)
                assert (optimized - logits).abs().max() < 0.001
                assert (optimized_cache - cache).abs().max() < 0.001
            name = 'llm_cached_projected'
            axes['positions'] = {2: 'relative'}
        export(decoder, (e, zero, positions, mask), a.onnx / (name + '.onnx'),
               ['embeddings', 'cache', 'positions', 'mask'], ['logits', 'updated_cache'],
               axes)
        np.savez(a.output / 'llm_embeddings.npz', **{k.replace('.', '_'): v.numpy() for k, v in state.items()
                 if k.startswith(('llm_embedding.', 'speech_embedding.', 'spk_embed_affine_layer.'))})
    elif a.stage == 'flow':
        encoder = FlowEncoder()
        load_subset(encoder, state)
        export(encoder, (torch.tensor([[1, 2, 3, 4]]), torch.tensor([4])), a.onnx / 'flow_encoder.onnx',
               ['tokens', 'lengths'], ['encoded'], {'tokens': {1: 'tokens'}, 'encoded': {1: 'tokens'}})
        from cosyvoice.flow.length_regulator import InterpolateRegulator
        regulator = InterpolateRegulator(80, [1, 1, 1, 1]).model
        regulator.load_state_dict({k.removeprefix('length_regulator.model.'): v for k, v in state.items() if k.startswith('length_regulator.model.')})
        export(regulator, (torch.randn(1, 80, 16),), a.onnx / 'flow_regulator.onnx',
               ['features'], ['mu'], {'features': {2: 'mel'}, 'mu': {2: 'mel'}})
        np.savez(a.output / 'flow_speaker.npz', **{k.replace('.', '_'): v.numpy() for k, v in state.items() if k.startswith('spk_embed_affine_layer.')})
    else:
        from cosyvoice.hifigan.generator import HiFTGenerator
        from cosyvoice.hifigan.f0_predictor import ConvRNNF0Predictor
        h = HiFTGenerator(f0_predictor=ConvRNNF0Predictor()).eval()
        # Training checkpoint prefixes the generator.
        if all(k.startswith('generator.') for k in state):
            state = {k.removeprefix('generator.'): v for k, v in state.items()}
        h.load_state_dict(state, strict=True)
        for module in h.modules():
            if hasattr(module, 'weight_g'):
                torch.nn.utils.remove_weight_norm(module)
        export(h.f0_predictor, (torch.randn(1, 80, 16),), a.onnx / 'hift_f0.onnx',
               ['mel'], ['f0'], {'mel': {2: 'mel'}, 'f0': {1: 'mel'}})
        export(HiFTCore(h), (torch.randn(1, 80, 16), torch.randn(1, 18, 1025)), a.onnx / 'hift_core.onnx',
               ['mel', 'source_stft'], ['spectrum'], {'mel': {2: 'mel'}, 'source_stft': {2: 'stft'}, 'spectrum': {2: 'stft'}})
        np.savez(a.output / 'hift_source.npz', weight=h.m_source.l_linear.weight.detach().numpy(), bias=h.m_source.l_linear.bias.detach().numpy())
    print('completed', a.stage, flush=True)


if __name__ == '__main__':
    main()
