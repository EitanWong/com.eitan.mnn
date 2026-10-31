"""TIGER independent waveform validation with explicit session lifetime."""
from pathlib import Path
import argparse
import gc
import json
import numpy as np
import MNN


def error(actual, expected):
    if actual.shape != expected.shape or not np.isfinite(actual).all():
        return float('inf')
    return float(np.linalg.norm(actual.astype(np.float64) - expected) /
                 (np.linalg.norm(expected.astype(np.float64)) or 1.))


def reconstruct(spectrum, window, count):
    spectrum = spectrum.reshape(2, 2, 321, -1)
    frames = spectrum.shape[-1]
    outputs = []
    for source in range(2):
        value = spectrum[source, 0].astype(np.float64) + 1j * spectrum[source, 1].astype(np.float64)
        length = 640 + 160 * (frames - 1)
        signal, envelope = np.zeros(length), np.zeros(length)
        for frame in range(frames):
            start = frame * 160
            signal[start:start + 640] += np.fft.irfft(value[:, frame], n=640) * window
            envelope[start:start + 640] += window * window
        outputs.append((signal[320:320 + count] / envelope[320:320 + count]).astype(np.float32))
    return np.stack(outputs)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--graph', type=Path)
    parser.add_argument('--oracle', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--threads', type=int, nargs='+', default=[1, 4])
    parser.add_argument('--precision', choices=['high', 'normal', 'low'], default='high')
    args = parser.parse_args()
    cases = json.loads((args.oracle / 'cases.json').read_text())['cases']
    window = np.fromfile(args.oracle / 'window.f32', np.float32).astype(np.float64)
    source_rows = []
    for item in cases:
        name = item['case']
        expected = np.fromfile(args.oracle / (name + '-target.f32'), np.float32).reshape(2, -1)
        spectrum = np.fromfile(args.oracle / (name + '-spectrum.f32'), np.float32)
        waveform = reconstruct(spectrum, window, item['samples'])
        errors = [error(waveform[i], expected[i]) for i in range(2)]
        source_rows.append(dict(case=name, waveform_nrmse=errors))
        if max(errors) >= 1e-5:
            raise ValueError(source_rows[-1])
    (args.output.parent / (args.output.stem + '-source-inverse.json')).write_text(json.dumps(source_rows, indent=2) + '\n')
    if args.graph is None:
        print('Independent inverse STFT passes all original sources', flush=True)
        return
    rows = []
    for threads in args.threads:
        interpreter = MNN.Interpreter(str(args.graph))
        session = interpreter.createSession(dict(backend='CPU', numThread=threads, precision=args.precision))
        output, host = None, None
        try:
            for item in cases + cases[:1]:
                name = item['case']
                inputs = np.fromfile(args.oracle / (name + '-features.f32'), np.float32).reshape(item['shape'])
                expected = np.fromfile(args.oracle / (name + '-target.f32'), np.float32).reshape(2, -1)
                interpreter.resizeTensor(interpreter.getSessionInput(session, 'spectrum'), inputs.shape)
                interpreter.resizeSession(session)
                for repeat in range(2):
                    interpreter.getSessionInput(session, 'spectrum').copyFrom(MNN.Tensor(inputs.shape, MNN.Halide_Type_Float, inputs.copy(), MNN.Tensor_DimensionType_Caffe))
                    if interpreter.runSession(session) != 0:
                        raise RuntimeError('MNN runSession failed')
                    output = interpreter.getSessionOutput(session, 'separated')
                    host = MNN.Tensor(output.getShape(), MNN.Halide_Type_Float, np.zeros(output.getShape(), np.float32), MNN.Tensor_DimensionType_Caffe)
                    if not output.copyToHostTensor(host):
                        raise RuntimeError('MNN output readback failed')
                    spectrum = np.asarray(host.getData(), np.float32).reshape(output.getShape())
                    if spectrum.shape != (1, 4, 321, item['shape'][-1]):
                        raise ValueError('Invalid output shape: ' + str(spectrum.shape))
                    waveform = reconstruct(spectrum, window, item['samples'])
                    errors = [error(waveform[i], expected[i]) for i in range(2)]
                    row = dict(case=name, threads=threads, precision=args.precision, repeat=repeat, waveform_nrmse=errors)
                    rows.append(row)
                    print(row, flush=True)
                    args.output.write_text(json.dumps(rows, indent=2) + '\n')
                    if max(errors) >= .001:
                        raise ValueError(row)
        finally:
            output = None
            host = None
            session = None
            interpreter = None
            gc.collect()


if __name__ == '__main__':
    main()
