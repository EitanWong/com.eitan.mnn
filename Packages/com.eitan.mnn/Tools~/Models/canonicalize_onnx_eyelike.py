#!/usr/bin/env python3
"""Replace ONNX EyeLike with exact dynamic Range/Equal operators.

ONNX Runtime lacks a boolean EyeLike kernel. This preserves diagonal offsets,
rectangular/dynamic shapes and declared types without changing native code.
Requires numpy and onnx. Applying the rewrite twice is idempotent.
"""
from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
import onnx
from onnx import helper, numpy_helper


def canonicalize(model):
    implicit = [node for node in model.graph.node if node.op_type == 'EyeLike' and
        not any(attribute.name == 'dtype' for attribute in node.attribute)]
    input_types = {}
    if implicit:
        inferred = onnx.shape_inference.infer_shapes(model)
        for value in [*inferred.graph.input, *inferred.graph.value_info, *inferred.graph.output]:
            if value.type.HasField('tensor_type') and value.type.tensor_type.elem_type:
                input_types[value.name] = value.type.tensor_type.elem_type
        input_types.update({value.name: value.data_type for value in inferred.graph.initializer})
    nodes = []
    count = 0
    for node in model.graph.node:
        if node.op_type != 'EyeLike':
            nodes.append(node)
            continue
        attrs = {attribute.name: helper.get_attribute_value(attribute) for attribute in node.attribute}
        dtype = attrs.get('dtype', input_types.get(node.input[0]))
        if dtype is None:
            raise ValueError('Cannot infer EyeLike input type: '+node.input[0])
        prefix = node.name + '_portable'
        offset = attrs.get('k', 0)
        constants = [('zero', np.array(0, np.int64)), ('one', np.array(1, np.int64)),
            ('row_axis', np.array([1], np.int64)), ('col_axis', np.array([0], np.int64)),
            ('row_dim', np.array(0, np.int64)), ('col_dim', np.array(1, np.int64))]
        for tag, value in constants:
            model.graph.initializer.append(numpy_helper.from_array(value, prefix+'_'+tag))

        def add(op, inputs, name, **kwargs):
            nodes.append(helper.make_node(op, inputs, [prefix+'_'+name], name=prefix+'_'+name, **kwargs))

        add('Shape', [node.input[0]], 'shape')
        add('Gather', [prefix+'_shape', prefix+'_row_dim'], 'nrow', axis=0)
        add('Gather', [prefix+'_shape', prefix+'_col_dim'], 'ncol', axis=0)
        add('Range', [prefix+'_zero', prefix+'_nrow', prefix+'_one'], 'rows')
        add('Range', [prefix+'_zero', prefix+'_ncol', prefix+'_one'], 'cols')
        add('Unsqueeze', [prefix+'_rows', prefix+'_row_axis'], 'row')
        add('Unsqueeze', [prefix+'_cols', prefix+'_col_axis'], 'col')
        row = prefix+'_row'
        if offset:
            model.graph.initializer.append(numpy_helper.from_array(np.array(offset, np.int64), prefix+'_offset'))
            add('Add', [row, prefix+'_offset'], 'offsetrow')
            row = prefix+'_offsetrow'
        add('Equal', [row, prefix+'_col'], 'bool')
        nodes.append(helper.make_node('Identity' if dtype == onnx.TensorProto.BOOL else 'Cast',
            [prefix+'_bool'], list(node.output), name=node.name,
            **({} if dtype == onnx.TensorProto.BOOL else {'to': dtype})))
        count += 1
    del model.graph.node[:]
    model.graph.node.extend(nodes)
    return count


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path)
    args = parser.parse_args()
    model = onnx.load(args.source)
    count = canonicalize(model)
    onnx.checker.check_model(model)
    onnx.save(model, args.destination)
    print(f'Rewrote {count} EyeLike nodes: {args.destination}')


if __name__ == '__main__':
    main()
