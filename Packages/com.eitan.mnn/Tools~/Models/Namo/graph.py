"""Equivalent head grouping for the frozen author-published NAMO ONNX graph."""
import numpy as np
import onnx
from onnx import helper, numpy_helper


def tile_attention(model):
    nodes = list(model.graph.node)
    producer = {value: node for node in nodes for value in node.output}
    consumers = {}
    for node in nodes:
        for value in node.input:
            consumers.setdefault(value, []).append(node)
    replacement = {}
    removed = set()
    for softmax in [node for node in nodes if node.op_type == 'Softmax']:
        add = producer[softmax.input[0]]
        assert add.op_type == 'Add'
        score = producer[add.input[0]]
        assert score.op_type == 'MatMul'
        outputs = consumers[softmax.output[0]]
        assert len(outputs) == 1 and outputs[0].op_type == 'MatMul'
        weighted = outputs[0]
        assert all(value == -1 for attr in softmax.attribute if attr.name == 'axis' for value in [attr.i])
        prefix = softmax.name + '/tiled'
        tiled = []
        parts = []
        for group in range(3):
            tag = prefix + '/' + str(group)
            names = {}
            for key, value in [('start', [group * 4]), ('end', [(group + 1) * 4]), ('axis', [1])]:
                name = tag + '/' + key
                model.graph.initializer.append(numpy_helper.from_array(np.asarray(value, np.int64), name))
                names[key] = name
            slices = []
            for key, value in [('query', score.input[0]), ('key', score.input[1]), ('value', weighted.input[1])]:
                name = tag + '/' + key
                tiled.append(helper.make_node('Slice', [value, names['start'], names['end'], names['axis']], [name], name=name))
                slices.append(name)
            tiled.append(helper.make_node('MatMul', slices[:2], [tag + '/score'], name=tag + '/score'))
            tiled.append(helper.make_node('Add', [tag + '/score', add.input[1]], [tag + '/masked'], name=tag + '/masked'))
            tiled.append(helper.make_node('Softmax', [tag + '/masked'], [tag + '/probability'], name=tag + '/probability', axis=-1))
            part = tag + '/weighted'
            tiled.append(helper.make_node('MatMul', [tag + '/probability', slices[2]], [part], name=part))
            parts.append(part)
        tiled.append(helper.make_node('Concat', parts, list(weighted.output), name=prefix + '/concat', axis=1))
        replacement[weighted.name] = tiled
        removed.update(node.name for node in [score, add, softmax, weighted])
    assert len(replacement) == 22
    del model.graph.node[:]
    for node in nodes:
        if node.name in replacement:
            model.graph.node.extend(replacement[node.name])
        elif node.name not in removed:
            model.graph.node.append(node)
    onnx.checker.check_model(model)
    return model
