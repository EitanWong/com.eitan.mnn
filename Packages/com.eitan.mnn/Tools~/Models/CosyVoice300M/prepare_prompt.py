#!/usr/bin/env python3
"""Prepare the official example reference voice using official ONNX frontends.

ONNX Runtime is used only to establish an independent reference for the MNN
frontend conversion. This is not included in steady-state synthesis timing.
"""
import argparse
from pathlib import Path

import numpy as np
import onnxruntime as ort
import torch
import torchaudio
from torchaudio.compliance import kaldi
from scipy.io import wavfile

from runtime import tokenizer


def slaney_mel(rate, fft, mels, fmin=0., fmax=None):
    fmax = fmax or rate / 2
    def to_mel(f):
        f = np.asarray(f, dtype=float)
        return np.where(f >= 1000, 15 + np.log(np.maximum(f, 1e-9) / 1000) / (np.log(6.4) / 27), f / (200 / 3))
    def to_hz(m):
        return np.where(m >= 15, 1000 * np.exp((m - 15) * (np.log(6.4) / 27)), (200 / 3) * m)
    edges = to_hz(np.linspace(to_mel(fmin), to_mel(fmax), mels + 2))
    frequency = np.linspace(0, rate / 2, fft // 2 + 1)
    ramps = edges[:, None] - frequency[None]
    weights = np.maximum(0, np.minimum(-ramps[:-2] / np.diff(edges)[:-1, None], ramps[2:] / np.diff(edges)[1:, None]))
    return (weights * (2 / (edges[2:] - edges[:-2]))[:, None]).astype(np.float32)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--models', type=Path, required=True)
    p.add_argument('--work', type=Path, required=True)
    p.add_argument('--reference-audio', type=Path, help='Alternate reference WAV, already present under model assets')
    p.add_argument('--reference-text', default='希望你以后能够做的比我还好呦。')
    p.add_argument('--output', type=Path, help='Alternate prompt cache path; default common/prompt.npz')
    p.add_argument('--fixture-name', default='frontend.npz')
    p.add_argument('--speaker-cache', type=Path,
                   help='Use speaker embedding from an existing prompt NPZ while extracting tokens/mel from the reference WAV')
    a = p.parse_args()
    torch.set_num_threads(4)
    rate, signal = wavfile.read(a.reference_audio or a.models / 'common/prompt.wav')
    if signal.dtype == np.int16:
        signal = signal.astype(np.float32) / 32768
    signal = torch.from_numpy(signal.astype(np.float32))[None]
    signal = torchaudio.functional.resample(signal, rate, 16000)
    spectrum = torch.stft(signal, 400, 160, window=torch.hann_window(400), return_complex=True)
    power = spectrum[..., :-1].abs() ** 2
    mel = torch.from_numpy(slaney_mel(16000, 400, 128)) @ power
    logmel = torch.clamp(mel, min=1e-10).log10()
    logmel = (torch.maximum(logmel, logmel.max() - 8) + 4) / 4
    options = ort.SessionOptions(); options.intra_op_num_threads = 4
    token_graph = ort.InferenceSession(str(a.models / 'source/speech_tokenizer_v1.onnx'), options)
    tokens = token_graph.run(None, {token_graph.get_inputs()[0].name: logmel.numpy(),
                            token_graph.get_inputs()[1].name: np.array([logmel.shape[-1]], np.int32)})[0].reshape(1, -1)
    feats = kaldi.fbank(signal, num_mel_bins=80, dither=0, sample_frequency=16000)
    feats = feats - feats.mean(0, keepdim=True)
    campplus = ort.InferenceSession(str(a.models / 'source/campplus.onnx'), options)
    speaker = campplus.run(None, {campplus.get_inputs()[0].name: feats[None].numpy()})[0]
    frontend_speaker = speaker
    if a.speaker_cache:
        with np.load(a.speaker_cache) as cached:
            speaker = cached['speaker'].copy()
        if speaker.shape != (1, 192) or not np.isfinite(speaker).all() or np.linalg.norm(speaker) < 1e-6:
            raise ValueError('Cached speaker must be a finite, nonzero float embedding [1,192]')
    signal = torchaudio.functional.resample(signal, 16000, 22050)
    signal = torch.nn.functional.pad(signal[:, None], (384, 384), mode='reflect')[:, 0]
    magnitude = torch.stft(signal, 1024, 256, win_length=1024, window=torch.hann_window(1024), center=False, return_complex=True).abs()
    # Matcha uses sqrt(real^2 + imag^2 + 1e-9), then ln(max(mel, 1e-5)).
    magnitude = (magnitude ** 2 + 1e-9).sqrt()
    mel = torch.log(torch.clamp(torch.from_numpy(slaney_mel(22050, 1024, 80, 0, 8000)) @ magnitude, min=1e-5))
    text_tokens = np.array([tokenizer(a.work / 'Dependencies').encode(a.reference_text, allowed_special='all')], np.int32)
    output = a.output or a.models / 'common/prompt.npz'
    output.parent.mkdir(parents=True, exist_ok=True)
    np.savez(output, text_tokens=text_tokens, speech_tokens=tokens.astype(np.int32),
             speaker=speaker.astype(np.float32), mel=mel.transpose(1, 2).numpy())
    a.work.joinpath('Fixtures').mkdir(exist_ok=True)
    np.savez(a.work / 'Fixtures' / a.fixture_name, logmel=logmel.numpy(), feats=feats[None].numpy(),
             tokens=tokens.astype(np.int32), speaker=frontend_speaker)
    print('prompt tokens', tokens.shape, 'mel', mel.shape, 'speaker', speaker.shape)
    print('campplus contract', [(v.name,v.shape,v.type) for v in campplus.get_inputs()], [v.name for v in campplus.get_outputs()])
    print('tokenizer contract', [(v.name,v.shape,v.type) for v in token_graph.get_inputs()], [v.name for v in token_graph.get_outputs()])


if __name__ == '__main__':
    main()
