#!/usr/bin/env python3
"""Rewrite constant ONNX negative padding as an equivalent Slice + positive Pad.

MNN 3.6.1 miscomputes the mixed positive/negative token-shift pads exported by
AV MossFormer2. This changes the interchange graph, never the MNN runtime.
Requires numpy and onnx; output is deterministic and applying it twice is safe.
"""
from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
import onnx
from onnx import helper, numpy_helper


def constant_value(name, producers, values):
    if name in values:
        return values[name]
    node = producers.get(name)
    if node is None:
        raise ValueError("Dynamic value: " + name)
    attrs = {attr.name: helper.get_attribute_value(attr) for attr in node.attribute}
    if node.op_type == "Constant":
        result = numpy_helper.to_array(attrs["value"])
    elif node.op_type == "ConstantOfShape":
        shape = constant_value(node.input[0], producers, values)
        fill = numpy_helper.to_array(attrs["value"]) if "value" in attrs else np.array([0], np.float32)
        result = np.full(tuple(shape), fill.item(), dtype=fill.dtype)
    else:
        inputs = [constant_value(item, producers, values) for item in node.input if item]
        if node.op_type == "Concat":
            result = np.concatenate(inputs, axis=attrs["axis"])
        elif node.op_type == "Reshape":
            result = np.reshape(inputs[0], tuple(inputs[1]))
        elif node.op_type == "Transpose":
            result = np.transpose(inputs[0], attrs.get("perm"))
        elif node.op_type == "Cast":
            result = inputs[0].astype(helper.tensor_dtype_to_np_dtype(attrs["to"]))
        elif node.op_type == "Slice":
            axes = inputs[3] if len(inputs) > 3 else range(len(inputs[1]))
            steps = inputs[4] if len(inputs) > 4 else np.ones(len(inputs[1]), np.int64)
            slices = [slice(None)] * inputs[0].ndim
            for start, end, axis, step in zip(inputs[1], inputs[2], axes, steps):
                slices[int(axis)] = slice(int(start), int(end), int(step))
            result = inputs[0][tuple(slices)]
        else:
            raise ValueError("Nonconstant or unsupported expression: " + node.op_type)
    values[name] = result
    return result


def canonicalize(model):
    producers = {out: node for node in model.graph.node for out in node.output}
    values = {item.name: numpy_helper.to_array(item) for item in model.graph.initializer}
    names = set(producers) | set(values)
    rewritten = []
    count = 0
    for node in model.graph.node:
        if node.op_type == "Pad" and len(node.input) > 1:
            try:
                pads = constant_value(node.input[1], producers, values).astype(np.int64)
            except ValueError:
                pads = None
            if pads is not None and np.any(pads < 0):
                attrs = {attr.name: helper.get_attribute_value(attr) for attr in node.attribute}
                if attrs.get("mode", b"constant") != b"constant" or len(node.input) > 3:
                    raise ValueError("Negative padding currently requires constant mode without an axes input.")
                rank = pads.size // 2
                prefix = node.output[0] + "_mnn_crop"
                while prefix in names:
                    prefix += "_"
                starts = np.maximum(-pads[:rank], 0)
                ends = np.where(pads[rank:] < 0, pads[rank:], np.iinfo(np.int64).max)
                data = [starts, ends, np.arange(rank, dtype=np.int64), np.maximum(pads, 0)]
                suffixes = ["_starts", "_ends", "_axes", "_pads"]
                for suffix, array in zip(suffixes, data):
                    tensor_name = prefix + suffix
                    if tensor_name in names:
                        raise ValueError("Generated tensor name collision: " + tensor_name)
                    model.graph.initializer.append(numpy_helper.from_array(array, tensor_name))
                    names.add(tensor_name)
                rewritten.append(helper.make_node("Slice", [node.input[0], prefix + "_starts", prefix + "_ends", prefix + "_axes"], [prefix], name=prefix))
                node.input[0] = prefix
                node.input[1] = prefix + "_pads"
                names.add(prefix)
                count += 1
        rewritten.append(node)
    del model.graph.node[:]
    model.graph.node.extend(rewritten)
    return count


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    model = onnx.load(args.source)
    count = canonicalize(model)
    onnx.save(model, args.destination)
    print(f"Rewrote {count} negative Pad nodes: {args.destination}")


if __name__ == "__main__":
    main()
