#!/usr/bin/env python3
"""Pool the final LiveKit hidden token before the vocabulary projection.

For an accelerator, --fixed-length 128 also right-pads the causal context and
introduces last_token_index: int64[1]. The caller supplies the actual final token
position. Future padding cannot affect that token through causal attention.
Requires onnx; use the original floating model, not a dynamically quantized one.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
import onnx
from onnx import helper, numpy_helper


def constant(model, name):
    for value in model.graph.initializer:
        if value.name == name:
            return numpy_helper.to_array(value)
    for node in model.graph.node:
        if node.op_type == "Constant" and name in node.output:
            for attribute in node.attribute:
                if attribute.name == "value":
                    return numpy_helper.to_array(attribute.t)
    raise ValueError(f"Expected a constant: {name}")


def pool_last_hidden(model):
    if any("mnn_last_hidden" in node.output for node in model.graph.node):
        return
    producers = {output: node for node in model.graph.node for output in node.output}
    probability = producers["prob"]
    softmax = producers[probability.input[0]]
    pool = producers[softmax.input[0]]
    projection = producers[pool.input[0]]
    axis = next((attribute.i for attribute in pool.attribute if attribute.name == "axis"), 0)
    if (probability.op_type != "Gather" or softmax.op_type != "Softmax" or
            pool.op_type != "Gather" or projection.op_type != "MatMul" or
            axis != 1 or constant(model, pool.input[1]).shape != () or
            int(constant(model, pool.input[1])) != -1):
        raise ValueError("Expected the floating LiveKit last-token vocabulary head.")
    hidden = projection.input[0]
    projection.input[0] = "mnn_last_hidden"
    projection.output[0] = pool.output[0]
    pool.input[0] = hidden
    pool.output[0] = "mnn_last_hidden"
    model.graph.node.remove(pool)
    model.graph.node.insert(list(model.graph.node).index(projection), pool)


def fix_context(model, length):
    if length < 1 or length > 128:
        raise ValueError("LiveKit context length must be between 1 and 128.")
    if any(value.name == "last_token_index" for value in model.graph.input):
        dimension = model.graph.input[0].type.tensor_type.shape.dim[1]
        if dimension.dim_value != length:
            raise ValueError("This model already has a different fixed context.")
        return
    inputs = [value for value in model.graph.input if value.name == "input_ids"]
    if len(inputs) != 1 or len(inputs[0].type.tensor_type.shape.dim) != 2:
        raise ValueError("Expected input_ids with a batch and sequence axis.")
    dimension = inputs[0].type.tensor_type.shape.dim[1]
    dimension.ClearField("dim_param")
    dimension.dim_value = length
    model.graph.input.append(helper.make_tensor_value_info("last_token_index", onnx.TensorProto.INT64, [1]))
    pool = next(node for node in model.graph.node if "mnn_last_hidden" in node.output)
    pool.input[1] = "last_token_index"
    pool.output[0] = "mnn_last_hidden_unsqueezed"
    axes = "mnn_pool_squeeze_axis"
    model.graph.initializer.append(numpy_helper.from_array(np.array([1], dtype=np.int64), axes))
    squeeze = helper.make_node("Squeeze", [pool.output[0], axes], ["mnn_last_hidden"], name="mnn_squeeze_pool")
    model.graph.node.insert(list(model.graph.node).index(pool) + 1, squeeze)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--fixed-length", type=int)
    args = parser.parse_args()
    model = onnx.load(args.source)
    pool_last_hidden(model)
    if args.fixed_length is not None:
        fix_context(model, args.fixed_length)
    onnx.checker.check_model(model)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    onnx.save(model, args.output)


if __name__ == "__main__":
    main()
