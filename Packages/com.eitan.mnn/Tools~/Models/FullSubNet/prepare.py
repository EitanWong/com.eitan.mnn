#!/usr/bin/env python3
"""Reproduce official FullSubNet v0.2 CPU and fixed32 Metal MNN graphs locally.

Requires PyTorch, ONNX, the frozen upstream checkout/checkpoint and official
MNNConvert. No download or native source modification is performed. Computation
is adapted from FullSubNet v0.2 under the accompanying MIT license.
"""
from __future__ import annotations
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys

REVISION = '213df1b46d5bc3d61d774a75aebae5b731046bd2'
CHECKPOINT_SHA256 = '482615e9709d2d6c10ee0d24f9844ad2da2dae3c0cf897f014839c365917a2e0'
BLOCK = 32


def load_model(source: Path, checkpoint: Path):
    revision = subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip()
    if revision != REVISION or hashlib.sha256(checkpoint.read_bytes()).hexdigest() != CHECKPOINT_SHA256:
        raise ValueError('source revision/checkpoint does not match official FullSubNet v0.2')
    import torch
    sys.path.insert(0, str(source))
    spec = importlib.util.spec_from_file_location('fullsubnet_original', source / 'recipes/dns_interspeech_2020/fullsubnet/model.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    model = module.Model(num_freqs=257, look_ahead=2, sequence_model='LSTM', fb_num_neighbors=0,
        sb_num_neighbors=15, fb_output_activate_function='ReLU', sb_output_activate_function=False,
        fb_model_hidden_size=512, sb_model_hidden_size=384, weight_init=False,
        norm_type='offline_laplace_norm', num_groups_in_drop_band=2).eval()
    model.load_state_dict(torch.load(checkpoint, map_location='cpu', weights_only=False)['model'], strict=True)
    return model


def gather_frequency(values, num_neighbors):
    import torch
    if num_neighbors <= 0:
        return values.permute(0, 2, 1, 3).unsqueeze(3)
    index = torch.arange(257)[:, None] + torch.arange(-num_neighbors, num_neighbors + 1)[None, :]
    index = torch.where(index < 0, -index, index)
    index = torch.where(index >= 257, 512 - index, index).reshape(-1)
    result = values.index_select(2, index).reshape(values.shape[0], values.shape[1], 257, 2*num_neighbors+1, values.shape[3])
    return result.permute(0, 2, 1, 3, 4).contiguous()


def make_block(net):
    import torch
    class RecurrentBlock(torch.nn.Module):
        def __init__(self):
            super().__init__()
            self.net = net

        def forward(self, features, hidden, cell):
            hidden_out, cell_out = [], []
            recurrent = self.net.sequence_model
            for layer in range(2):
                affine = torch.nn.functional.linear(features, getattr(recurrent, 'weight_ih_l'+str(layer)),
                    getattr(recurrent, 'bias_ih_l'+str(layer)))
                h, c, frames = hidden[layer], cell[layer], []
                for frame in range(BLOCK):
                    gates = affine[:, frame] + torch.nn.functional.linear(h, getattr(recurrent, 'weight_hh_l'+str(layer)),
                        getattr(recurrent, 'bias_hh_l'+str(layer)))
                    i, f, g, o = gates.chunk(4, dim=-1)
                    c = torch.sigmoid(f)*c + torch.sigmoid(i)*torch.tanh(g)
                    h = torch.sigmoid(o)*torch.tanh(c)
                    frames.append(h)
                features = torch.stack(frames, dim=1)
                hidden_out.append(h)
                cell_out.append(c)
            projected = self.net.fc_output_layer(features)
            if self.net.output_activate_function:
                projected = self.net.activate_function(projected)
            return projected, torch.stack(hidden_out), torch.stack(cell_out)
    return RecurrentBlock().eval()


def export_graphs(model, work: Path) -> dict:
    import torch
    torch.set_num_threads(1)
    torch.manual_seed(20261030)
    features = torch.rand(1, 1, 257, 63)
    with torch.inference_mode():
        expected = model(features)
    model.unfold = gather_frequency
    with torch.inference_mode():
        if not torch.equal(expected, model(features)):
            raise ValueError('frequency Gather differs from original Unfold')
    cpu = work / 'fullsubnet_dynamic.onnx'
    torch.onnx.export(model, features, cpu, input_names=['magnitude'], output_names=['crm_mask'], opset_version=17,
        dynamo=False, dynamic_axes={'magnitude': {3: 'frames'}, 'crm_mask': {3: 'frames'}})
    errors = {}
    for name, net, batch, channels, hidden in [('fullband', model.fb_model, 1, 257, 512), ('subband', model.sb_model, 257, 32, 384)]:
        block = make_block(net)
        x = torch.rand(batch, BLOCK, channels)
        h = torch.rand(2, batch, hidden)*.01
        c = torch.rand_like(h)*.01
        with torch.inference_mode():
            sequence, (h_ref, c_ref) = net.sequence_model(x, (h, c))
            projected_ref = net.fc_output_layer(sequence)
            if net.output_activate_function:
                projected_ref = net.activate_function(projected_ref)
            actual = block(x, h, c)
            references = [projected_ref, h_ref, c_ref]
            maximum = max(float(torch.max(torch.abs(a-b))) for a, b in zip(actual, references))
            if maximum > 1e-4:
                raise ValueError(name + ' block differs from original LSTM: ' + str(maximum))
            errors[name] = maximum
        torch.onnx.export(block, (x, h, c), work / ('fullsubnet_'+name+'_32.onnx'),
            input_names=['features', 'hidden', 'cell'], output_names=['projected', 'hidden_out', 'cell_out'],
            opset_version=17, dynamo=False)
    return errors


def convert(converter: Path, work: Path, keep: bool) -> dict:
    spec = importlib.util.spec_from_file_location('external_compactor', Path(__file__).resolve().parents[1] / 'deduplicate_mnn_external_weights.py')
    compactor = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(compactor)
    def run(name, arguments):
        with (work / (name+'.log')).open('w') as log:
            subprocess.run([str(converter), *map(str, arguments)], check=True, stdout=log, stderr=subprocess.STDOUT)
    common = ['--bizCode', 'FullSubNet', '--optimizeLevel', '1', '--optimizePrefer', '2']
    run('convert-cpu', ['-f', 'ONNX', '--modelFile', work/'fullsubnet_dynamic.onnx', '--MNNModel', work/'fullsubnet_fp32.mnn', *common, '--useOriginRNNImpl'])
    report = {}
    for part in ['fullband', 'subband']:
        stem = 'fullsubnet_'+part+'_32'
        inline = work / (stem+'_inline.mnn')
        external = work / (stem+'_external.mnn')
        run('convert-'+part, ['-f', 'ONNX', '--modelFile', work/(stem+'.onnx'), '--MNNModel', inline, *common,
            '--transformerFuseC4', '0', '--transformerFuseQkvProj', '0', '--transformerFuseGateUpProj', '0', '--transformerFuseLnProj', '0'])
        run('external-'+part, ['-f', 'MNN', '--modelFile', inline, '--MNNModel', external, '--saveExternalData'])
        report[part] = compactor.compact_model(external, work/(stem+'_fp32.mnn'))
        if not keep:
            for path in [inline, external, Path(str(external)+'.weight'), work/(stem+'.onnx')]:
                path.unlink()
    if not keep:
        (work/'fullsubnet_dynamic.onnx').unlink()
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', required=True, type=Path)
    parser.add_argument('--checkpoint', required=True, type=Path)
    parser.add_argument('--converter', required=True, type=Path)
    parser.add_argument('--work-directory', required=True, type=Path)
    parser.add_argument('--keep-intermediates', action='store_true')
    args = parser.parse_args()
    work = args.work_directory.resolve()
    work.mkdir(parents=True, exist_ok=True)
    model = load_model(args.source.resolve(), args.checkpoint.resolve())
    errors = export_graphs(model, work)
    report = {'source_revision': REVISION, 'checkpoint_sha256': CHECKPOINT_SHA256,
        'source_block_max_abs': errors, 'storage': convert(args.converter.resolve(), work, args.keep_intermediates)}
    (work/'preparation.json').write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
