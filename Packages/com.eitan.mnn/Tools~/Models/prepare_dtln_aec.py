#!/usr/bin/env python3
"""Partition pinned official DTLN-AEC TFLites for CPU normalization/GPU cores.

Requires ai-edge-litert, flatbuffers, numpy and official MNNConvert. Inputs must
already exist locally. No native code or original operator/weight is modified.
The output is a work directory, not a deployment/install directory.
"""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
from pathlib import Path
import subprocess

REVISION = '9d24e128b4f409db18227b8babb343016625921f'
SOURCE_SHA256 = {
    (128, 1): '8d241b3a732af8ca140b2e30043e56a6c3c7800c46e22c26f4eea2f70974ad1e',
    (128, 2): '350bb01a1152ae3cabe09fe5e868ef2f7d8b988a9f22aae44f140195f6493126',
    (256, 1): '4a3a588b69fd79d837bc068b579a26faa92cac39dddbb00001d2dc1c3d869d60',
    (256, 2): 'fa2590243aad1bf893c5be45b20709e8c50feec65e3604d1d52bae6eeddc23d3',
    (512, 1): '569f7c3cfac96b1e093229c3ca10b5d892f5b1906105b644e457f8245b4f7383',
    (512, 2): 'fb423d867ab25d5f4716bd369c7126b6c84926175c019b832e71bf21de0e9907',
}


def partition(model):
    import numpy as np
    from ai_edge_litert import schema_py_generated as schema
    graph = model.subgraphs[0]
    def opcode(op):
        value = model.operatorCodes[op.opcodeIndex]
        return max(value.builtinCode, value.deprecatedBuiltinCode)
    cut = next(i for i, op in enumerate(graph.operators)
               if opcode(op) == schema.BuiltinOperator.CONCATENATION)
    front, core = graph.operators[:cut+1], graph.operators[cut+1:]
    produced = {int(t) for op in front for t in op.outputs}
    core_used = {int(t) for op in core for t in op.inputs if t >= 0}
    front_used = {int(t) for op in front for t in op.inputs if t >= 0}
    boundary = sorted(produced & core_used)
    front_inputs = [int(t) for t in graph.inputs if int(t) in front_used]
    core_inputs = [int(t) for t in graph.inputs if int(t) in core_used] + boundary
    parts = []
    for ops, inputs, outputs in [(front, front_inputs, boundary),
                                (core, core_inputs, list(map(int, graph.outputs)))]:
        modified = copy.deepcopy(model)
        target = modified.subgraphs[0]
        target.operators = copy.deepcopy(ops)
        target.inputs = np.array(inputs, np.int32)
        target.outputs = np.array(outputs, np.int32)
        for i, tensor in enumerate(boundary):
            target.tensors[tensor].name = f'front_{i}'.encode()
        parts.append(modified)
    return parts


def save(model, path: Path):
    import flatbuffers
    builder = flatbuffers.Builder(0)
    builder.Finish(model.Pack(builder), file_identifier=b'TFL3')
    path.write_bytes(bytes(builder.Output()))


def interpreter(path: Path):
    from ai_edge_litert.interpreter import Interpreter, OpResolverType
    result = Interpreter(model_path=str(path), num_threads=1,
                         experimental_op_resolver_type=OpResolverType.BUILTIN_WITHOUT_DEFAULT_DELEGATES)
    result.allocate_tensors()
    return result


def verify_partition(source: Path, front_path: Path, core_path: Path, traces: Path, stem: str):
    import numpy as np
    original, front, core = [interpreter(p) for p in (source, front_path, core_path)]
    inputs = original.get_input_details()
    source_inputs = {row['name']: np.fromfile(traces/f'trace-{stem}-{row["name"]}.f32', '<f4')
                     .reshape((-1, *row['shape'])) for row in inputs}
    maximum = 0.0
    for frame in range(32):
        values = {name: data[frame] for name, data in source_inputs.items()}
        for row in inputs:
            original.set_tensor(row['index'], values[row['name']])
        original.invoke()
        for row in front.get_input_details():
            front.set_tensor(row['index'], values[row['name']])
        front.invoke()
        values.update({row['name']: front.get_tensor(row['index']) for row in front.get_output_details()})
        for row in core.get_input_details():
            core.set_tensor(row['index'], values[row['name']])
        core.invoke()
        expected = {row['name']: original.get_tensor(row['index']) for row in original.get_output_details()}
        for row in core.get_output_details():
            error = float(abs(expected[row['name']] - core.get_tensor(row['index'])).max())
            maximum = max(maximum, error)
    if maximum != 0:
        raise ValueError(f'{stem}: split changed builtin float32 outputs/states by {maximum}')
    return {'frames': 32, 'max_abs_output_and_state': maximum, 'oracle': 'LiteRT builtin, without default delegates'}


def convert(source: Path, target: Path, converter: Path):
    command = [str(converter), '-f', 'TFLITE', '--modelFile', str(source),
               '--MNNModel', str(target), '--bizCode', 'DTLN-AEC',
               '--optimizeLevel', '1', '--optimizePrefer', '2']
    with target.with_suffix('.log').open('w') as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    return {'file': target.name, 'bytes': target.stat().st_size,
            'sha256': hashlib.sha256(target.read_bytes()).hexdigest()}


def prepare(source_directory: Path, converter: Path, output: Path, traces: Path):
    from ai_edge_litert import schema_py_generated as schema
    output.mkdir(parents=True, exist_ok=True)
    rows = []
    for (units, stage), checksum in SOURCE_SHA256.items():
        stem = f'dtln_aec_{units}_{stage}'
        source = source_directory/(stem+'.tflite')
        if hashlib.sha256(source.read_bytes()).hexdigest() != checksum:
            raise ValueError(f'{source.name}: expected frozen official source')
        model = schema.ModelT.InitFromObj(schema.Model.GetRootAsModel(source.read_bytes(), 0))
        paths = [output/(stem+'_'+part+'.tflite') for part in ('front', 'core')]
        for part, path in zip(partition(model), paths):
            save(part, path)
        parity = verify_partition(source, *paths, traces, f'{units}-{stage}')
        graphs = [convert(source, output/(stem+'.mnn'), converter)]
        graphs.extend(convert(path, path.with_suffix('.mnn'), converter) for path in paths)
        rows.append({'units': units, 'stage': stage, 'source_sha256': checksum,
                     'partition_parity': parity, 'graphs': graphs})
    report = {'source': 'breizhn/DTLN-aec', 'revision': REVISION, 'stages': rows}
    (output/'preparation.json').write_text(json.dumps(report, indent=2)+'\n')
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-directory', required=True, type=Path)
    parser.add_argument('--converter', required=True, type=Path)
    parser.add_argument('--work-directory', required=True, type=Path)
    parser.add_argument('--reference-directory', required=True, type=Path)
    args = parser.parse_args()
    print(json.dumps(prepare(args.source_directory.resolve(), args.converter.resolve(),
                             args.work_directory.resolve(), args.reference_directory.resolve()), indent=2))


if __name__ == '__main__':
    main()
