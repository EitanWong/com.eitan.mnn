#!/usr/bin/env python3
"""Fuse CFG and Euler update into the official ONNX estimator graph."""
import argparse
import json
from pathlib import Path
import subprocess

import numpy as np
import onnx
from onnx import TensorProto, helper, numpy_helper


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for key in ['models', 'work', 'converter']:
        p.add_argument('--' + key, type=Path, required=True)
    a = p.parse_args()
    source = onnx.load(str(a.models / 'source/flow_estimator.onnx'))
    g = source.graph
    original_output = g.output[0].name
    for node in g.node:
        for i, name in enumerate(node.input):
            if name == 'x':
                node.input[i] = 'cfg_x'
    g.node.insert(0, helper.make_node('Concat', ['x', 'x'], ['cfg_x'], axis=0))
    next(x for x in g.input if x.name == 'x').type.tensor_type.shape.dim[0].dim_value = 1
    g.input.append(helper.make_tensor_value_info('dt', TensorProto.FLOAT, [1]))
    constants = {'cfg_axis': np.array([0], np.int64), 'cfg_begin': np.array([0], np.int64),
                 'cfg_middle': np.array([1], np.int64), 'cfg_end': np.array([2], np.int64),
                 'cfg_scale': np.array([1.7], np.float32), 'cfg_negative_scale': np.array([.7], np.float32)}
    g.initializer.extend(numpy_helper.from_array(x, name) for name, x in constants.items())
    g.node.extend([
        helper.make_node('Slice', [original_output, 'cfg_begin', 'cfg_middle', 'cfg_axis'], ['conditioned']),
        helper.make_node('Slice', [original_output, 'cfg_middle', 'cfg_end', 'cfg_axis'], ['unconditioned']),
        helper.make_node('Mul', ['conditioned', 'cfg_scale'], ['positive_velocity']),
        helper.make_node('Mul', ['unconditioned', 'cfg_negative_scale'], ['negative_velocity']),
        helper.make_node('Sub', ['positive_velocity', 'negative_velocity'], ['guided_velocity']),
        helper.make_node('Mul', ['guided_velocity', 'dt'], ['delta']),
        helper.make_node('Add', ['x', 'delta'], ['next_z']),
    ])
    del g.output[:]
    g.output.append(helper.make_tensor_value_info('next_z', TensorProto.FLOAT, [1, 80, 'length']))
    onnx.checker.check_model(source)
    path = a.work / 'ONNX/flow_euler.onnx'
    onnx.save(source, str(path))
    destination = a.models / 'experimental-flow'
    destination.mkdir(exist_ok=True)
    command = [str(a.converter.resolve()), '-f', 'ONNX', '--modelFile', str(path.resolve()),
               '--MNNModel', str((destination / 'flow_euler.mnn').resolve()), '--bizCode', 'CosyVoice-300M-CFG-Euler',
               '--targetVersion', '3.6.1', '--optimizePrefer', '2', '--saveExternalData', '--fp16']
    (a.work / 'command-flow-euler.json').write_text(json.dumps(command, indent=2))
    with (a.work / 'convert-flow-euler.log').open('w') as log:
        subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
    if 'Converted Success' not in (a.work / 'convert-flow-euler.log').read_text():
        raise RuntimeError('Conversion failed')
    (destination / 'config.json').write_text(json.dumps({'file': 'flow_euler.mnn',
        'inputs': [x.name for x in g.input], 'outputs': ['next_z'], 'cfg': .7,
        'status': 'experimental; numerical and synthesis validation required'}, indent=2))


if __name__ == '__main__':
    main()
