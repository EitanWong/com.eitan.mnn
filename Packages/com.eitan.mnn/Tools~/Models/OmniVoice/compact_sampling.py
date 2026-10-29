"""Build an optional packed-CFG sampling graph from the pinned OmniVoice ONNX export.

Requires onnx and numpy, plus official MNN 3.6.1's MNNConvert/mnnconvert.
All intermediate files belong in TestArtifacts~; installation copies only the graph.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import shutil
import subprocess

import numpy as np
import onnx
from onnx import TensorProto, helper, numpy_helper


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def export(source, work):
    model = onnx.load(source, load_external_data=False)
    nodes = model.graph.node
    heads = [node for node in nodes if node.name == '/audio_heads/MatMul']
    shapes = [node for node in nodes if node.name in ('/Shape_4', '/Shape_5')]
    if len(heads) != 1 or len(shapes) != 2 or len(model.graph.input) != 4 or len(model.graph.output) != 1 or model.graph.output[0].name != 'logits':
        raise ValueError('Expected the original AFun9 OmniVoice export; refusing an unknown or already transformed graph.')
    head = heads[0]
    hidden = head.input[0]
    if any(node.input[0] != hidden for node in shapes):
        raise ValueError('Unexpected output-head shape dependencies.')
    # Select both target branches before the expensive vocabulary projection.
    model.graph.input.append(helper.make_tensor_value_info('target_indices', TensorProto.INT64, ['target_count']))
    index = min(list(nodes).index(node) for node in shapes)
    nodes.insert(index, helper.make_node('Gather', [hidden, 'target_indices'], ['selected_hidden'], axis=1, name='select_target_hidden'))
    head.input[0] = 'selected_hidden'
    for node in shapes:
        node.input[0] = 'selected_hidden'
    constants = {'guidance_cond': np.array(3, np.float32), 'guidance_uncond': np.array(2, np.float32),
                 'slice_start': np.array([0], np.int64), 'slice_end': np.array([1024], np.int64),
                 'last_axis': np.array([-1], np.int64)}
    for name, value in constants.items():
        model.graph.initializer.append(numpy_helper.from_array(value, name))
    # Match managed sampling: argmax excludes mask 1024, confidence includes it.
    nodes.extend([
        helper.make_node('Split', ['logits'], ['cond_logits', 'uncond_logits'], axis=2),
        helper.make_node('Mul', ['cond_logits', 'guidance_cond'], ['cond_scaled']),
        helper.make_node('Mul', ['uncond_logits', 'guidance_uncond'], ['uncond_scaled']),
        helper.make_node('Sub', ['cond_scaled', 'uncond_scaled'], ['guided']),
        helper.make_node('Slice', ['guided', 'slice_start', 'slice_end', 'last_axis'], ['allowed']),
        helper.make_node('ArgMax', ['allowed'], ['codes'], axis=-1, keepdims=0),
        helper.make_node('ReduceMax', ['allowed'], ['best'], axes=[-1], keepdims=1),
        helper.make_node('ReduceMax', ['guided'], ['maximum'], axes=[-1], keepdims=1),
        helper.make_node('Sub', ['guided', 'maximum'], ['stable']),
        helper.make_node('Exp', ['stable'], ['exponents']),
        helper.make_node('ReduceSum', ['exponents', 'last_axis'], ['sum'], keepdims=1),
        helper.make_node('Log', ['sum'], ['log_sum']),
        helper.make_node('Sub', ['best', 'maximum'], ['best_stable']),
        helper.make_node('Sub', ['best_stable', 'log_sum'], ['confidence_4d']),
        helper.make_node('Squeeze', ['confidence_4d', 'last_axis'], ['confidence']),
    ])
    del model.graph.output[:]
    model.graph.output.extend([
        helper.make_tensor_value_info('codes', TensorProto.INT64, ['batch', 8, 'frames']),
        helper.make_tensor_value_info('confidence', TensorProto.FLOAT, ['batch', 8, 'frames']),
    ])
    # ONNX rejects symlinks/hardlinks. Prefer an APFS clone, else copy.
    locations = {entry.value for value in model.graph.initializer for entry in value.external_data if entry.key == 'location'}
    for location in locations:
        relative = Path(location)
        if relative.is_absolute() or '..' in relative.parts:
            raise ValueError('External tensor paths must stay inside the source directory.')
        original, target = source.parent / relative, work / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        if target.is_symlink() or target.exists() and target.stat().st_nlink > 1:
            target.unlink()
        if target.exists():
            if digest(target) != digest(original):
                raise ValueError('Existing ONNX sidecar does not match source.')
        else:
            try:
                if sys.platform != 'darwin':
                    raise OSError('APFS cloning unavailable')
                subprocess.run(['cp', '-c', str(original), str(target)], check=True)
            except (OSError, subprocess.CalledProcessError):
                shutil.copyfile(original, target)
    destination = work / 'model.onnx'
    onnx.save_model(model, destination, save_as_external_data=False)
    onnx.checker.check_model(str(destination))
    return destination


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--work-dir', type=Path, required=True)
    parser.add_argument('--converter', type=Path, required=True)
    parser.add_argument('--reference-weights', type=Path, required=True)
    parser.add_argument('--install-dir', type=Path)
    args = parser.parse_args()
    work = args.work_dir.resolve()
    work.mkdir(parents=True, exist_ok=True)
    if work == args.source.resolve().parent:
        raise ValueError('Work directory must differ from source directory.')
    for name in ('model.onnx', 'omnivoice_lm_compact.mnn', 'omnivoice_lm_compact.mnn.weight'):
        if (work / name).is_symlink():
            raise ValueError('Refusing to overwrite a symlink in conversion outputs.')
    transformed = export(args.source.resolve(), work)
    graph = work / 'omnivoice_lm_compact.mnn'
    command = [str(args.converter.resolve()), '-f', 'ONNX', '--modelFile', str(transformed),
               '--MNNModel', str(graph), '--saveExternalData']
    with (work / 'conversion.log').open('w') as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    external = Path(str(graph) + '.weight')
    shared_hash = digest(args.reference_weights)
    if digest(external) != shared_hash:
        raise ValueError('Converted external weights differ from installed baseline. Refusing shared-weight installation.')
    manifest = {'source': str(args.source.resolve()), 'source_sha256': digest(args.source),
                'graph_sha256': digest(graph), 'shared_weights_sha256': shared_hash,
                'command': command, 'precision': 'High', 'packed_cfg': True, 'guidance': 2,
                'scope': 'Conversion provenance; validate full synthesis on the target backend.'}
    if args.install_dir:
        installed_weights = args.install_dir / 'omnivoice_lm.mnn.weight'
        if digest(installed_weights) != shared_hash:
            raise ValueError('Installation baseline weights do not match.')
        destination = args.install_dir / graph.name
        temporary = destination.with_suffix('.mnn.tmp')
        try:
            shutil.copyfile(graph, temporary)
            temporary.replace(destination)
        finally:
            temporary.unlink(missing_ok=True)
        manifest['installed_graph'] = str(destination.resolve())
    (work / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps(manifest, indent=2))


if __name__ == '__main__':
    main()
