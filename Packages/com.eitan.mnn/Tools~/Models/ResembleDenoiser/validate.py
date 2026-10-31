#!/usr/bin/env python3
"""Independent NumPy/MNN process: source-waveform gate across dynamic shapes."""
from pathlib import Path
import argparse
import gc
import json
import numpy as np
import MNN


def reconstruct(spectrum, window, samples, scale):
    value = spectrum[0, 0].astype(np.float64) + 1j * spectrum[0, 1].astype(np.float64)
    value = np.concatenate([value, value[:, -1:]], axis=1)
    length = 1680 + 420 * (value.shape[1] - 1)
    signal, envelope = np.zeros(length, np.float64), np.zeros(length, np.float64)
    for frame in range(value.shape[1]):
        start = frame * 420
        signal[start:start + 1680] += np.fft.irfft(value[:, frame], n=1680) * window
        envelope[start:start + 1680] += window * window
    return (signal[840:840 + samples] / envelope[840:840 + samples] * scale).astype(np.float32)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['graph', 'oracle', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    cases = json.loads((args.oracle / 'cases.json').read_text())['cases']
    window = np.fromfile(args.oracle / 'window.f32', np.float32).astype(np.float64)
    rows = []
    for threads in [2, 4]:
        model = MNN.Interpreter(str(args.graph))
        session = model.createSession(dict(backend='CPU', numThread=threads, precision='high'))
        try:
            for item in cases + cases[:1]:
                name = item['case']
                value = np.fromfile(args.oracle / (name + '-features.f32'), np.float32).reshape(item['shape'])
                expected = np.fromfile(args.oracle / (name + '-target.f32'), np.float32)
                samples = np.fromfile(args.oracle / (name + '-input.f32'), np.float32)
                scale = np.maximum(np.abs(samples).max(), np.float32(1e-7))
                model.resizeTensor(model.getSessionInput(session, 'features'), value.shape)
                model.resizeSession(session)
                for repeat in range(2):
                    model.getSessionInput(session, 'features').copyFrom(MNN.Tensor(value.shape, MNN.Halide_Type_Float, value.copy(), MNN.Tensor_DimensionType_Caffe))
                    if model.runSession(session) != 0:
                        raise RuntimeError('MNN runSession failed')
                    output = model.getSessionOutput(session, 'spectrum')
                    host = MNN.Tensor(output.getShape(), MNN.Halide_Type_Float, np.zeros(output.getShape(), np.float32), MNN.Tensor_DimensionType_Caffe)
                    if not output.copyToHostTensor(host):
                        raise RuntimeError('MNN output readback failed')
                    actual = np.asarray(host.getData(), np.float32).reshape(output.getShape())
                    if actual.shape != (1, 2, 841, item['frames']) or not np.isfinite(actual).all():
                        raise ValueError('Invalid output spectrum')
                    wave = reconstruct(actual, window, len(samples), scale)
                    error = float(np.linalg.norm(wave.astype(np.float64) - expected) / (np.linalg.norm(expected.astype(np.float64)) or 1.))
                    row = dict(case=name, threads=threads, repeat=repeat, waveform_nrmse=error)
                    rows.append(row)
                    print(row, flush=True)
                    args.output.write_text(json.dumps(rows, indent=2) + '\n')
                    if not np.isfinite(error) or error >= .001:
                        raise ValueError(row)
        finally:
            output = None
            host = None
            session = None
            model = None
            gc.collect()


if __name__ == '__main__':
    main()
