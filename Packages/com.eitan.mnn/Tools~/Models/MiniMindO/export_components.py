#!/usr/bin/env python3
"""Export pinned MiniMind-o auxiliary components, without changing upstream or MNN C++.

Requires the reference directory used by install.py (released core + auxiliary).
Intermediate ONNX and logs belong in --work under the project's TestArtifacts~/.
"""
import argparse
import ast
import hashlib
from typing import Optional, Iterable
import importlib
import json
from pathlib import Path
import sys
import types
import numpy as np
import onnx
import onnxruntime as ort
import torch
from torch import nn
from transformers import MimiModel, SiglipVisionModel


def core(reference):
    package = types.ModuleType('minimind_reference')
    package.__path__ = [str(reference)]
    sys.modules[package.__name__] = package
    module = importlib.import_module('minimind_reference.model_omni')
    config = module.OmniConfig(**json.loads((reference / 'config.json').read_text()))
    config.flash_attn = False
    model = module.MiniMindOmni(config)
    model.load_state_dict(torch.load(reference / 'pytorch_model.bin', map_location='cpu', weights_only=True), strict=True)
    for block in list(model.thinker.layers) + list(model.talker.layers):
        block.self_attn.flash = False
    return model.eval()


class Vision(nn.Module):
    def __init__(self, model, encoder):
        super().__init__(); self.projector = model.vision_proj; self.encoder = encoder
    def forward(self, pixels):
        return self.projector(self.encoder(pixel_values=pixels).last_hidden_state)


class MimiDecoder(nn.Module):
    def __init__(self, model):
        super().__init__(); self.model = model
    def forward(self, codes):
        embeddings = self.model.upsample(self.model.quantizer.decode(codes.to(torch.int64)))
        x = embeddings.transpose(1, 2)
        positions = torch.arange(x.shape[1], device=x.device)
        mask = torch.where(positions[:, None] >= positions[None, :], 0.0, torch.finfo(x.dtype).min)[None, None]
        x = self.model.decoder_transformer(x, attention_mask=mask, use_cache=False, return_dict=False)[0]
        return self.model.decoder(x.transpose(1, 2))


class FeatureEmbedding(nn.Module):
    def __init__(self, embedding):
        super().__init__(); self.embedding = embedding
    def forward(self, tokens):
        return self.embedding(tokens) * (1 - self.mask) + self.features * self.mask


class MultimodalPrefill(nn.Module):
    def __init__(self, model):
        super().__init__(); self.model = model
        self.injection = FeatureEmbedding(model.thinker.embed_tokens)
        model.thinker.embed_tokens = self.injection
    def forward(self, tokens, audio_codes, features, feature_mask):
        self.injection.features, self.injection.mask = features, feature_mask
        out = self.model(torch.cat((audio_codes, tokens.unsqueeze(1)), dim=1), use_cache=True, logits_to_keep=1)
        return out.logits, torch.stack(out.audio_logits), torch.stack([torch.stack(pair) for pair in out.past_key_values])


def export(module, args, path, inputs, outputs, axes, checks):
    module.eval()
    with torch.inference_mode():
        expected = module(*args)
        torch.onnx.export(module, args, str(path), input_names=inputs, output_names=outputs,
                          dynamic_axes=axes, opset_version=17, dynamo=False)
    onnx.checker.check_model(str(path))
    options = ort.SessionOptions(); options.intra_op_num_threads = 4
    session = ort.InferenceSession(str(path), options, providers=['CPUExecutionProvider'])
    rows = []
    for case in [args] + checks:
        with torch.inference_mode(): ref = module(*case)
        if isinstance(ref, torch.Tensor): ref = (ref,)
        got = session.run(None, {name: value.numpy() for name, value in zip(inputs, case)})
        row = {'inputs': [list(t.shape) for t in case]}
        for name, x, y in zip(outputs, got, ref):
            y = y.numpy(); error = float(np.max(np.abs(x - y)))
            np.testing.assert_allclose(x, y, atol=3e-3, rtol=3e-3)
            row[name] = {'shape': list(x.shape), 'max_abs_error': error}
        rows.append(row)
    path.with_suffix('.checks.json').write_text(json.dumps(rows, indent=2) + '\n')
    print('VERIFIED_ONNX', path, json.dumps(rows), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--work', type=Path, required=True)
    parser.add_argument('--stage', choices=['vision', 'mimi', 'prefill', 'audio'], required=True)
    parser.add_argument('--sensevoice-source', type=Path)
    args = parser.parse_args(); reference = args.reference.resolve(); work = args.work.resolve()
    work.mkdir(parents=True, exist_ok=True); torch.set_num_threads(4); torch.manual_seed(42)
    if args.stage == 'mimi':
        model = MimiModel.from_pretrained(reference / 'auxiliary/mimi', attn_implementation='eager').float().eval()
        model.config.use_cache = False
        module = MimiDecoder(model)
        values = torch.randint(0, 2048, (1, 8, 8), dtype=torch.int32)
        with torch.inference_mode():
            torch.testing.assert_close(module(values), model.decode(values.long()).audio_values)
        print('RELEASED_MIMI_PARITY=PASS', flush=True)
        export(module, (values,), work/'mimi_decoder.onnx' , ['codes'], ['pcm'],
               {'codes': {2: 'frames'}, 'pcm': {2: 'samples'}}, [(values[:, :, :n],) for n in [1, 4, 5, 6, 7]])
    elif args.stage == 'vision':
        net = core(reference)
        model = SiglipVisionModel.from_pretrained(reference/'auxiliary/siglip2-base-p32-256-ve', attn_implementation='eager').float().eval()
        export(Vision(net, model), (torch.rand(1, 3, 256, 256)*2-1,), work/'vision_encoder.onnx', ['pixels'], ['features'], {}, [])
    elif args.stage == 'audio':
        if args.sensevoice_source is None: parser.error('--sensevoice-source is required for audio')
        source = args.sensevoice_source.read_bytes()
        if hashlib.sha256(source).hexdigest() != "b885b209bfd3bfe24adea0fc6194afbf2394d8a2b765c0281a670fcff5e16134":
            raise ValueError('SenseVoice source differs from the pinned FunASR implementation')
        tree = ast.parse(source)
        selected = []
        for node in tree.body:
            if isinstance(node, (ast.ClassDef, ast.FunctionDef)):
                node.decorator_list = []
                selected.append(node)
                if node.name == 'SenseVoiceEncoderSmall': break
        scope = dict(torch=torch, nn=nn, np=np, F=torch.nn.functional, Tensor=torch.Tensor,
                     Optional=Optional, Iterable=Iterable)
        exec(compile(ast.Module(body=selected, type_ignores=[]), str(args.sensevoice_source), 'exec'), scope)
        encoder = scope['SenseVoiceEncoderSmall'](input_size=560, output_size=512, num_blocks=50,
            tp_blocks=20, attention_heads=4, linear_units=2048, kernel_size=11).eval()
        state = torch.load(reference/'auxiliary/SenseVoiceSmall/model.pt', map_location='cpu', weights_only=True)
        encoder.load_state_dict({k[len('encoder.'):]:v for k,v in state.items() if k.startswith('encoder.')}, strict=True)
        net = core(reference)
        class Audio(nn.Module):
            def __init__(self): super().__init__(); self.encoder=encoder; self.projector=net.audio_proj
            def forward(self, fbank):
                lengths = torch.ones((1,), dtype=torch.int64, device=fbank.device) * fbank.shape[1]
                hidden, _ = self.encoder(fbank.clone(), lengths)
                return self.projector(hidden)
        values=torch.randn(1, 24, 560)
        export(Audio(), (values,), work/'audio_encoder.onnx', ['fbank'], ['features'],
               {'fbank':{1:'frames'},'features':{1:'frames'}}, [(values[:,:7],)])
    else:
        from transformers import AutoTokenizer
        net = core(reference)
        tokenizer = AutoTokenizer.from_pretrained(reference)
        ids = tokenizer('你好<|audio_pad|><|audio_pad|>'+ '<|image_pad|>'*64, return_tensors='pt').input_ids
        codes = torch.full((1, 8, ids.shape[1]), 2049, dtype=torch.int64)
        features = torch.randn(1, ids.shape[1], 768)
        mask = ((ids == 16) | (ids == 12)).float().unsqueeze(-1)
        # Compare feature injection to the original released forward before installing the export embedding.
        net.encode_audio_inputs = lambda *a, **kw: [features[0, ids[0] == 16]]
        net.encode_image_inputs = lambda *a, **kw: features[:, ids[0] == 12]
        with torch.inference_mode():
            ref = net(torch.cat((codes, ids.unsqueeze(1)), 1), audio_inputs=torch.ones(1, 1, 560),
                      pixel_values=torch.ones(1, 3, 256, 256), use_cache=True, logits_to_keep=1)
        module = MultimodalPrefill(net)
        with torch.inference_mode(): actual = module(ids, codes, features, mask)
        for a, b in zip(actual, [ref.logits, torch.stack(ref.audio_logits), torch.stack([torch.stack(p) for p in ref.past_key_values])]):
            torch.testing.assert_close(a, b)
        print('RELEASED_FEATURE_INJECTION_PARITY=PASS', flush=True)
        axes = {'tokens':{1:'sequence'},'audio_codes':{2:'sequence'},'features':{1:'sequence'},'feature_mask':{1:'sequence'},'present':{3:'sequence'}}
        export(module, (ids, codes, features, mask), work/'multimodal_prefill.onnx',
               ['tokens','audio_codes','features','feature_mask'], ['text_logits','audio_logits','present'],axes,
               [(ids[:, :5], codes[:, :, :5], features[:, :5], mask[:, :5])])

if __name__ == '__main__': main()
