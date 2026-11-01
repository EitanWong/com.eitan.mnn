#!/usr/bin/env python3
"""Reproduce NAMO v1 multilingual from pinned local author-published ONNX/tokenizer files."""
from pathlib import Path
import argparse
import base64
import hashlib
import importlib.util
import json
import subprocess
import sys
import numpy as np
import onnx
import onnxruntime as ort
from graph import tile_attention

sys.dont_write_bytecode = True
REPOSITORY = 'videosdk-live/Namo-Turn-Detector-v1-Multilingual'
REVISION = '59245aac1f0a0f170277ebecc32ddaea418083b4'
SOURCE_HASHES = {
    'model.onnx': 'fa8359326322ecaa388ba1b69b6a8ca7f7d43d06110f6833eb67f5b561052311',
    'tokenizer.json': 'b7cc6d8599efc7f57ee2fe874a7e29b82038c5dfd03a442652c2afea13fe41e5',
    'config.json': 'd698427a3a3ea3b9791ed29261a5645ba1eee9c7bff919abcf0a6902bea104f8',
    'LICENSE': '7ecbb9c5ba3cc22056ffc55eee42feadd7e7b9f185f9cbe1abdd584bdc6e548a',
}
PUBLIC_UUID = '13e372ca-d0c2-4580-903c-1b1309938bee'


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def write_tokenizer(source, output):
    profile = json.loads(source.read_text())
    model = profile['model']
    if model['type'] != 'BPE' or not model['byte_fallback'] or not model['fuse_unk'] or model['ignore_merges']:
        raise ValueError('Unexpected NAMO BPE protocol')
    vocabulary = model['vocab']
    tokens = sorted(vocabulary, key=vocabulary.__getitem__)
    if [vocabulary[token] for token in tokens] != list(range(256000)):
        raise ValueError('Unexpected NAMO vocabulary IDs')
    with output.open('w') as stream:
        stream.write('NAMO_BPE_1\n' + str(len(tokens)) + '\n')
        for token in tokens:
            stream.write(base64.b64encode(token.encode()).decode() + '\n')
        stream.write(str(len(model['merges'])) + '\n')
        for left, right in model['merges']:
            stream.write(f'{vocabulary[left]} {vocabulary[right]} {vocabulary[left + right]}\n')
        stream.write(str(len(profile['added_tokens'])) + '\n')
        for token in profile['added_tokens']:
            stream.write(f'{token["id"]} {int(token["lstrip"])}\n')


def canonical_uuid(path):
    specification = importlib.util.spec_from_file_location('mnn_schema', Path(__file__).resolve().parents[1] / 'deduplicate_mnn_external_weights.py')
    schema = importlib.util.module_from_spec(specification)
    specification.loader.exec_module(schema)
    model = schema.Model(path.read_bytes())
    field = model.field(model.integer(0), 11)
    if not field:
        raise ValueError('Missing public Net.mnn_uuid')
    position = field + model.integer(field)
    if model.integer(position) != 36:
        raise ValueError('Invalid public UUID')
    model.data[position + 4:position + 40] = PUBLIC_UUID.encode()
    path.write_bytes(model.data)


def probabilities(logits):
    exponentials = np.exp(logits - logits.max())
    return exponentials / exponentials.sum()


def source_cases():
    cases = []
    generator = np.random.default_rng(20261031)
    for length in [2, 3, 127, 128, 129, 512, 1024, 2048, 4096, 8192]:
        tokens = generator.integers(260, 256000, (1, length), dtype=np.int64)
        tokens[0, 0], tokens[0, -1] = 2, 1
        cases.append(dict(input_ids=tokens, attention_mask=np.ones_like(tokens)))
    mask = np.ones((1, 129), np.int64)
    mask[0, -5:] = 0
    cases.append(dict(input_ids=cases[4]['input_ids'].copy(), attention_mask=mask))
    return cases


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['source', 'converter', 'output']:
        parser.add_argument('--' + name, required=True, type=Path)
    args = parser.parse_args()
    for name, expected in SOURCE_HASHES.items():
        if digest(args.source / name) != expected:
            raise ValueError('Pinned author source differs: ' + name)
    args.output.mkdir(parents=True, exist_ok=True)
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    cases = source_cases()
    original = ort.InferenceSession(str(args.source / 'model.onnx'), options, providers=['CPUExecutionProvider'])
    expected = [original.run(['logits'], feed)[0] for feed in cases]
    del original
    graph = tile_attention(onnx.load(args.source / 'model.onnx'))
    onnx_path = args.output / 'namo_tiled.onnx'
    onnx.save(graph, onnx_path)
    del graph
    lowered = ort.InferenceSession(str(onnx_path), options, providers=['CPUExecutionProvider'])
    errors = []
    for feed, source in zip(cases, expected):
        actual = lowered.run(['logits'], feed)[0]
        error = float(abs(source - actual).max())
        if not np.isfinite(actual).all() or error >= .001:
            raise ValueError('Head decomposition changes original logits: ' + str(error))
        errors.append(dict(tokens=feed['input_ids'].shape[1], maximum_logit_error=error))
    del lowered
    output = args.output / 'namo_tiled_fp16.mnn'
    command = [str(args.converter), '-f', 'ONNX', '--modelFile', str(onnx_path), '--MNNModel', str(output), '--bizCode', 'NAMOTurnDetector', '--fp16']
    with (args.output / 'conversion.log').open('w') as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    canonical_uuid(output)
    write_tokenizer(args.source / 'tokenizer.json', args.output / 'tokenizer.txt')
    import MNN
    model = MNN.Interpreter(str(output))
    session = model.createSession(dict(backend='CPU', numThread=2, precision='high'))
    parity = []
    for feed, source in zip(cases, expected):
        for key in feed:
            model.resizeTensor(model.getSessionInput(session, key), feed[key].shape)
        model.resizeSession(session)
        for key, value in feed.items():
            model.getSessionInput(session, key).copyFrom(MNN.Tensor(value.shape, MNN.Halide_Type_Int, value.astype(np.int32), MNN.Tensor_DimensionType_Caffe))
        code = model.runSession(session)
        if code:
            raise RuntimeError('MNN run failed: ' + str(code))
        tensor = model.getSessionOutput(session, 'logits')
        host = MNN.Tensor(tensor.getShape(), MNN.Halide_Type_Float, np.zeros(tensor.getShape(), np.float32), MNN.Tensor_DimensionType_Caffe)
        tensor.copyToHostTensor(host)
        actual = np.asarray(host.getData(), np.float32).reshape(1, 2)
        error = float(abs(probabilities(source) - probabilities(actual)).max())
        row = dict(tokens=feed['input_ids'].shape[1], probability_absolute_error=error)
        print(row, flush=True)
        if not np.isfinite(actual).all() or error >= .001:
            raise ValueError('Selected MNN differs from original FP32 ONNX: ' + str(row))
        parity.append(row)
    profile = dict(repository=REPOSITORY, revision=REVISION, source='author-published FP32 ONNX', graph=output.name, tokenizer='tokenizer.txt', maximum_tokens=8192, end_of_turn_class=1, input_ids='input_ids', attention_mask='attention_mask', output='logits')
    (args.output / 'mnn_namo_turn_detector.json').write_text(json.dumps(profile, indent=2) + '\n')
    report = dict(repository=REPOSITORY, revision=REVISION, source_sha256=SOURCE_HASHES, reference='author-published FP32 ONNX, not original PyTorch checkpoint', head_groups=3, heads_per_group=4, onnx_parity=errors, selected_mnn_parity=parity, canonical_public_uuid=PUBLIC_UUID, converter_arguments=command, graph_bytes=output.stat().st_size, graph_sha256=digest(output), tokenizer_sha256=digest(args.output / 'tokenizer.txt'))
    (args.output / 'preparation.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
