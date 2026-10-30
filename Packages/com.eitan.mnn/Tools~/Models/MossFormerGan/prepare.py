#!/usr/bin/env python3
"""Reproduce the pinned original-checkpoint MossFormerGAN FP32 MNN neural core.

Inputs are power-compressed complex spectra; original phase restoration remains
in managed DSP. Requires a local frozen upstream checkout and checkpoint. This
tool does not download files or change upstream/native source.
"""
from __future__ import annotations

import argparse
import ast
import hashlib
import importlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import types

sys.dont_write_bytecode = True
REVISION = '6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61'
CHECKPOINT_SHA256 = '04bf00db575389e1ea8b1fcca48d9efcf073e50ad170e6dd5230df3bac98be27'
MODEL_REVISION = 'dcb1b20e1e44d435090072ab9b223c792ea1f624'
GRAPH_UUID = b'4e2da396-741c-412f-a01e-3d0adc8c6da1'


def load_helper(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def load_model(source, checkpoint):
    revision = subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip()
    if revision != REVISION or hashlib.sha256(checkpoint.read_bytes()).hexdigest() != CHECKPOINT_SHA256:
        raise ValueError('source revision/checkpoint differs from pinned MossFormerGAN')
    import torch
    directory = source / 'clearvoice/clearvoice'
    packages = [('clearvoice', directory), ('clearvoice.models', directory/'models'),
        ('clearvoice.models.mossformer_gan_se', directory/'models/mossformer_gan_se')]
    for name, path in packages:
        package = types.ModuleType(name)
        package.__path__ = [str(path)]
        sys.modules[name] = package
    module = importlib.import_module('clearvoice.models.mossformer_gan_se.generator')
    original = module.MossFormerGAN_SE_16K(types.SimpleNamespace(fft_len=400, mode='inference')).model.eval()
    state = torch.load(checkpoint, map_location='cpu', weights_only=True)['model']
    original.load_state_dict({key.removeprefix('module.'): value for key, value in state.items()}, strict=True)
    return original, module


def inputs(reference):
    import numpy as np
    import torch
    if reference:
        for row in json.loads((reference/'cases.json').read_text())['cases']:
            if row['case'] == 'silence':
                continue
            values = np.fromfile(reference/(row['case']+'-features.f32'), '<f4')
            yield row['case'], torch.from_numpy(values.reshape(1, 2, row['frames'], 201).copy())
        return
    generator = torch.Generator().manual_seed(20261030)
    for frames in [4, 42, 162, 322, 596, 1282]:
        yield str(frames), torch.randn(1, 2, frames, 201, generator=generator)


def adapt_shape_arithmetic(original, module, source):
    from rotary_embedding_torch import RotaryEmbedding
    for layer in original.modules():
        if isinstance(layer, RotaryEmbedding):
            layer.cache_if_possible = False
    for block in original.blocks:
        if block.emb_hs != 1:
            raise ValueError('identity ceil arithmetic requires original embedding stride1')
    path = source/'clearvoice/clearvoice/models/mossformer_gan_se/generator.py'
    tree = ast.parse(path.read_text())
    cls = next(node for node in tree.body if isinstance(node, ast.ClassDef) and node.name == 'SyncANetBlock')
    forward = next(node for node in cls.body if isinstance(node, ast.FunctionDef) and node.name == 'forward')

    class IdentityCeil(ast.NodeTransformer):
        def visit_Assign(self, node):
            shape_name = len(node.targets) == 1 and isinstance(node.targets[0], ast.Name) and node.targets[0].id in ('T', 'Q')
            ceil = any(isinstance(value, ast.Attribute) and isinstance(value.value, ast.Name) and
                value.value.id == 'math' and value.attr == 'ceil' for value in ast.walk(node.value))
            if shape_name and ceil:
                node.value = ast.Name(id='old_'+node.targets[0].id, ctx=ast.Load())
            return self.generic_visit(node)

    forward = IdentityCeil().visit(forward)
    ast.fix_missing_locations(forward)
    namespace = module.__dict__.copy()
    exec(compile(ast.Module(body=[forward], type_ignores=[]), str(path), 'exec'), namespace)
    for block in original.blocks:
        block.forward = types.MethodType(namespace['forward'], block)


def export_core(original, module, source, work, reference):
    import torch

    class SpectralCore(torch.nn.Module):
        def __init__(self, net):
            super().__init__()
            self.net = net

        def forward(self, features):
            mag = torch.sqrt(features[:, 0, :, :]**2 + features[:, 1, :, :]**2).unsqueeze(1)
            x = self.net.dense_encoder(torch.cat([mag, features], dim=1))
            for block in self.net.blocks:
                x = block(x)
            return self.net.mask_decoder(x), self.net.complex_decoder(x)

    cases = list(inputs(reference))
    with torch.no_grad():
        expected = {name: [value.clone() for value in original(values)] for name, values in cases}
    adapt_shape_arithmetic(original, module, source)
    core = SpectralCore(original).eval()
    rows = []
    with torch.no_grad():
        for name, x in cases:
            mask, residual = core(x)
            phase = torch.angle(torch.complex(x[:, 0], x[:, 1])).unsqueeze(1)
            mag = torch.sqrt(x[:, 0]**2 + x[:, 1]**2).unsqueeze(1)
            out = [mask*mag*torch.cos(phase)+residual[:, 0:1], mask*mag*torch.sin(phase)+residual[:, 1:2]]
            if not all(torch.equal(a, b) for a, b in zip(out, expected[name])):
                raise ValueError('core/phase/shape adaptation differs from original: '+name)
            rows.append({'case': name, 'frames': x.shape[2], 'original_output_bit_identical': True})
    sample = next((x for _, x in cases if x.shape[2] == 162), cases[0][1])
    path = work/'mossformergan_core_dynamic.onnx'
    with torch.no_grad():
        torch.onnx.export(core, sample, path, opset_version=17, dynamo=False,
            input_names=['compressed_spectrum'], output_names=['mask', 'residual'],
            dynamic_axes={'compressed_spectrum': {2: 'frames'}, 'mask': {2: 'frames'}, 'residual': {2: 'frames'}})
    return path, rows


def convert(onnx_path, converter, work):
    import onnx
    tools = Path(__file__).resolve().parents[1]
    padding = load_helper('pad_canonicalizer', tools/'canonicalize_onnx_padding.py')
    eye = load_helper('eye_canonicalizer', tools/'canonicalize_onnx_eyelike.py')
    model = onnx.load(onnx_path)
    counts = {'negative_pads': padding.canonicalize(model), 'eye_like': eye.canonicalize(model)}
    onnx.checker.check_model(model)
    path = work/'mossformergan_core_canonical.onnx'
    onnx.save(model, path)
    output = work/'mossformergan_fp32.mnn'
    arguments = [str(converter), '-f', 'ONNX', '--modelFile', str(path), '--MNNModel', str(output),
        '--bizCode', 'MossFormerGAN', '--optimizeLevel', '1', '--optimizePrefer', '2', '--transformerFuseC4', '0',
        '--transformerFuseQkvProj', '0', '--transformerFuseGateUpProj', '0', '--transformerFuseLnProj', '0']
    with (work/'convert.log').open('w') as log:
        subprocess.run(arguments, check=True, stdout=log, stderr=subprocess.STDOUT)
    normalize_uuid(output)
    return {**counts, 'graph_sha256': hashlib.sha256(output.read_bytes()).hexdigest(), 'graph_bytes': output.stat().st_size}


def normalize_uuid(path):
    """Pin only public serialized Net.mnn_uuid; operators and weights are untouched."""
    schema = load_helper('mnn_public_schema', Path(__file__).resolve().parents[1]/'deduplicate_mnn_external_weights.py')
    model = schema.Model(path.read_bytes())
    field = model.field(model.integer(0), 11)  # schema/default/MNN.fbs: Net.mnn_uuid
    if not field:
        raise ValueError('missing public Net.mnn_uuid')
    position = field + model.integer(field)
    if model.integer(position) != len(GRAPH_UUID):
        raise ValueError('unexpected Net.mnn_uuid length')
    model.data[position+4:position+4+len(GRAPH_UUID)] = GRAPH_UUID
    path.write_bytes(model.data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--checkpoint', type=Path, required=True)
    parser.add_argument('--converter', type=Path, required=True)
    parser.add_argument('--work-directory', type=Path, required=True)
    parser.add_argument('--reference-directory', type=Path)
    parser.add_argument('--keep-intermediates', action='store_true')
    args = parser.parse_args()
    import torch
    torch.set_num_threads(2)
    source, checkpoint = args.source.resolve(), args.checkpoint.resolve()
    work = args.work_directory.resolve()
    work.mkdir(parents=True, exist_ok=True)
    original, module = load_model(source, checkpoint)
    onnx_path, rows = export_core(original, module, source, work, args.reference_directory)
    report = {'source_revision': REVISION, 'checkpoint_sha256': CHECKPOINT_SHA256, 'cases': rows,
        **convert(onnx_path, args.converter.resolve(), work)}
    profile = {'repository': 'alibabasglab/MossFormerGAN_SE_16K', 'revision': MODEL_REVISION,
        'source_revision': REVISION, 'graph': 'mossformergan_fp32.mnn', 'sample_rate': 16000,
        'fft_size': 400, 'hop_size': 100, 'power_compression': .3, 'normalization': 'rms_utterance',
        'analysis_window': torch.hamming_window(400, periodic=True).tolist()}
    (work/'mnn_mossformergan.json').write_text(json.dumps(profile, indent=2)+'\n')
    if not args.keep_intermediates:
        for path in work.glob('*.onnx'):
            path.unlink()
    (work/'preparation.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
