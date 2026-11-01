#!/usr/bin/env python3
"""Check dynamic graph outputs against independent ONNX Runtime references.

Writes measured errors and portable MNN input/output fixtures. It does not
infer audio quality or an end-to-end RTF from component timings.
"""
import argparse
import json
from pathlib import Path

import numpy as np

from runtime import Graph, CONTRACT


def metrics(actual, expected):
    assert actual.shape == expected.shape, (actual.shape, expected.shape)
    assert np.isfinite(actual).all()
    if actual.dtype.kind in 'iu':
        return {'equal_fraction': float(np.mean(actual == expected))}
    x, y = actual.astype(np.float64).ravel(), expected.astype(np.float64).ravel()
    error = x - y
    return {'max_abs': float(np.max(np.abs(error))), 'relative_rmse': float(np.linalg.norm(error) / max(np.linalg.norm(y), 1e-12)),
            'cosine': float(np.dot(x, y) / max(np.linalg.norm(x) * np.linalg.norm(y), 1e-12))}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--models', type=Path, required=True)
    p.add_argument('--work', type=Path, required=True)
    a = p.parse_args()
    rng = np.random.default_rng(2026)
    report = []
    prompt = dict(np.load(a.models / 'common/prompt.npz'))
    front = dict(np.load(a.work / 'Fixtures/frontend.npz'))
    table = np.load(a.models / 'common/projected_positions.npy')
    cases = {
        'text_encoder': [(rng.integers(0, 51866, (1, n), dtype=np.int32), np.array([n], np.int32)) for n in (1, 11, 79)],
        'llm_cached': [(rng.normal(size=(1, q, 1024)).astype(np.float32), np.zeros((14, 16, past, 128), np.float32),
                        table[:, :, table.shape[2] // 2 - (q + past) + 1:table.shape[2] // 2 + (q + past)].copy(),
                        np.concatenate([np.ones((1, q, past), np.int32), np.tri(q, dtype=np.int32)[None]], axis=2))
                       for q, past in [(4, 1), (1, 31), (17, 200)]],
        'flow_encoder': [(rng.integers(0, 4096, (1, n), dtype=np.int32), np.array([n], np.int32)) for n in (11, 119)],
        'flow_regulator': [(rng.normal(size=(1, 80, n)).astype(np.float32),) for n in (31, 128)],
        'flow_estimator': [(rng.normal(size=(2, 80, n)).astype(np.float32), np.ones((2, 1, n), np.float32),
                            rng.normal(size=(2, 80, n)).astype(np.float32), np.array([.1, .1], np.float32),
                            np.zeros((2, 80), np.float32), np.zeros((2, 80, n), np.float32)) for n in (64, 128)],
        'hift_f0': [(prompt['mel'][:, :n].transpose(0, 2, 1).copy(),) for n in (16, 64)],
        'hift_core': [(prompt['mel'][:, :n].transpose(0, 2, 1).copy(), rng.normal(size=(1, 18, n * 64 + 1)).astype(np.float32)) for n in (16, 64)],
        'campplus': [(front['feats'],)],
        'speech_tokenizer': [(front['logmel'], np.array([front['logmel'].shape[-1]], np.int32))],
    }
    fixture_root = a.work / 'Fixtures/MNN'
    fixture_root.mkdir(parents=True, exist_ok=True)
    for name, inputs in cases.items():
        file = 'llm_cached_projected' if name == 'llm_cached' else name
        if name in ['flow_estimator', 'campplus', 'speech_tokenizer']:
            source = a.models / 'source' / {'flow_estimator': 'flow_estimator.onnx', 'campplus': 'campplus.onnx', 'speech_tokenizer': 'speech_tokenizer_v1.onnx'}[name]
        else:
            source = a.work / 'ONNX' / (file + '.onnx')
        reference = Graph(None, name, engine='onnx', onnx=source)
        expected = [reference(*x) for x in inputs]
        del reference
        for precision in (['fp16', 'int8', 'int4'] if name in ['text_encoder', 'llm_cached'] else ['fp16']):
            graph = Graph(a.models / precision / (file + '.mnn'), name)
            for i, (x, y) in enumerate(zip(inputs, expected)):
                actual = graph(*x)
                row = {'graph': name, 'weights': precision, 'case': i, 'shapes': [list(z.shape) for z in x],
                       'outputs': [metrics(v, w) for v, w in zip(actual, y)]}
                if name == 'llm_cached':
                    row['argmax_equal'] = bool(np.argmax(actual[0]) == np.argmax(y[0]))
                    row['top25_overlap'] = len(set(np.argsort(actual[0].ravel())[-25:]) & set(np.argsort(y[0].ravel())[-25:])) / 25
                report.append(row)
                print(json.dumps(row), flush=True)
            # Device fixtures use FP16-vs-ORT, with actual semantic dtypes.
            if precision == 'fp16':
                import MNN
                # The first 16 mel frames of the reference voice are near silence;
                # use 64 frames for meaningful device F0 comparisons.
                fixture_case = 1 if name == 'hift_f0' else 0
                for suffix, tensors, names in [('input', inputs[fixture_case], CONTRACT[name][0]), ('output', expected[fixture_case], CONTRACT[name][1])]:
                    values = []
                    for tensor, tensor_name in zip(tensors, names):
                        value = graph.var(tensor)
                        value.name = tensor_name
                        values.append(value)
                    MNN.expr.save(values, str(fixture_root / (name + '-' + suffix + '.mnn')))
            del graph
        (a.work / 'numeric-validation.json').write_text(json.dumps(report, indent=2))
    print('validation complete', flush=True)


if __name__ == '__main__':
    main()
