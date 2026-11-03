#!/usr/bin/env python3
"""Generate training-frontend SenseVoice references from existing local WAV/model assets. No downloads."""
import argparse
import json
from pathlib import Path
import wave

import kaldi_native_fbank as knf
import numpy as np


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, required=True, help='SenseVoice root containing wo105090--SenseVoiceSmall-MNN; sibling Zipformer/Audio holds WAVs')
    args = parser.parse_args()
    root = args.root.resolve()
    metadata = json.loads((root / 'wo105090--SenseVoiceSmall-MNN/protocol.json').read_text())['metadata']
    mean = np.fromstring(metadata['neg_mean'], sep=',', dtype=np.float32)
    scale = np.fromstring(metadata['inv_stddev'], sep=',', dtype=np.float32)
    for name in ['en', 'zh-en']:
        with wave.open(str(root.parent / 'Zipformer/Audio' / (name + '-0.wav'))) as wav:
            if (wav.getframerate(), wav.getnchannels(), wav.getsampwidth()) != (16000, 1, 2):
                raise ValueError('Expected mono 16 kHz PCM16 WAV')
            pcm = np.frombuffer(wav.readframes(wav.getnframes()), dtype='<i2').astype(np.float32)
        options = knf.FbankOptions()
        options.frame_opts.dither = 0
        options.frame_opts.snip_edges = True
        options.frame_opts.window_type = 'hamming'
        options.mel_opts.num_bins = 80
        options.mel_opts.high_freq = 0
        fbank = knf.OnlineFbank(options)
        fbank.accept_waveform(16000, pcm.tolist())
        fbank.input_finished()
        features = np.array([fbank.get_frame(i) for i in range(fbank.num_frames_ready)])
        # FunASR WavFrontend.apply_lfr: prepend m//2 replicas, stride n,
        # then replicate the last frame to finish ceil(T/n) windows.
        windows = (len(features) + 5) // 6
        padded = np.pad(features, ((3, 6), (0, 0)), mode='edge')
        stacked = np.stack([padded[i * 6:i * 6 + 7].reshape(-1) for i in range(windows)])
        reference = (stacked + mean) * scale
        path = root / (name + '-sensevoice-training.f32')
        reference.astype('<f4').tofile(path)
        print(path, reference.shape)


if __name__ == '__main__':
    main()
