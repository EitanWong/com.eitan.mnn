#!/usr/bin/env python3
"""Validate a converted SEG graph against complete original-source waveforms."""
from pathlib import Path
import argparse
import gc
import json
import numpy as np
import MNN


def run(model, session, audio, gestures):
    for name, value in [('mixture', audio), ('gestures', gestures)]:
        model.resizeTensor(model.getSessionInput(session, name), value.shape)
    model.resizeSession(session)
    for name, value in [('mixture', audio), ('gestures', gestures)]:
        model.getSessionInput(session, name).copyFrom(MNN.Tensor(value.shape, MNN.Halide_Type_Float, value.copy(), MNN.Tensor_DimensionType_Caffe))
    if model.runSession(session) != 0:
        raise RuntimeError('SEG inference failed')
    tensor = model.getSessionOutput(session, 'target')
    host = MNN.Tensor(tensor.getShape(), MNN.Halide_Type_Float, np.zeros(tensor.getShape(), np.float32), MNN.Tensor_DimensionType_Caffe)
    if not tensor.copyToHostTensor(host):
        raise RuntimeError('SEG output copy failed')
    return np.asarray(host.getData(), np.float32).reshape(tensor.getShape())


def validate(graph, reference, threads):
    model = MNN.Interpreter(str(graph))
    session = model.createSession(dict(backend='CPU', numThread=threads, precision='high'))
    cases = json.loads((reference / 'cases.json').read_text())['cases']
    rows = []
    try:
        for row in cases + [cases[0]]:
            name = row['case']
            audio = np.fromfile(reference / (name + '-input.f32'), np.float32).reshape(1, row['samples'])
            gestures = np.fromfile(reference / (name + '-gesture.f32'), np.float32).reshape(1, row['frames'], 30)
            expected = np.fromfile(reference / (name + '-target.f32'), np.float32).reshape(1, -1)
            for repeat in range(2):
                actual = run(model, session, audio, gestures)
                if actual.shape != expected.shape or not np.isfinite(actual).all():
                    raise ValueError('Invalid SEG waveform: ' + name)
                error = float(np.linalg.norm(actual - expected) / (np.linalg.norm(expected) or 1.))
                if not np.isfinite(error) or error >= .001:
                    raise ValueError('SEG original source error: ' + name + ' ' + str(error))
                rows.append(dict(case=name, threads=threads, repeat=repeat, waveform_nrmse=error))
    finally:
        session = None
        model = None
        gc.collect()
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['graph', 'oracle', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    rows = validate(args.graph, args.oracle, 2) + validate(args.graph, args.oracle, 4)
    args.output.write_text(json.dumps(rows, indent=2) + '\n')
    print('SEG source parity: ' + str(len(rows)) + ' runs passed')


if __name__ == '__main__':
    main()
