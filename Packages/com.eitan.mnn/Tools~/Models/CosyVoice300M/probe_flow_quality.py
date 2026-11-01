#!/usr/bin/env python3
"""Compare Flow steps with identical speech tokens, conditioning and initial noise.

This is a quality diagnostic, not an end-to-end RTF benchmark. It replays the
acoustic stages and checks the original two-step waveform before comparisons.
"""
import argparse
import json
from pathlib import Path
import wave

import numpy as np

from runtime import Pipeline, source_spectrum, waveform


def render(pipeline, trace, steps, guidance):
    z = trace['initial_noise'].copy()
    mu, cond, speaker = trace['mu'], trace['cond'], trace['speaker']
    batch = 2 if guidance else 1
    cfg_mu = np.concatenate([mu, np.zeros_like(mu)]) if guidance else mu
    cfg_cond = np.concatenate([cond, np.zeros_like(cond)]) if guidance else cond
    cfg_speaker = np.concatenate([speaker, np.zeros_like(speaker)]) if guidance else speaker
    mask = np.ones((batch, 1, mu.shape[-1]), np.float32)
    span = (1 - np.cos(np.linspace(0, 1, steps + 1) * .5 * np.pi)).astype(np.float32)
    for index in range(steps):
        velocity = pipeline.graphs['flow_estimator'](
            np.repeat(z, batch, axis=0), mask, cfg_mu, np.full(batch, span[index], np.float32),
            cfg_speaker, cfg_cond)[0]
        guided = (1 + guidance) * velocity[:1] - guidance * velocity[1:] if guidance else velocity
        z += (span[index + 1] - span[index]) * guided
    mel = z[:, :, trace['prompt_mel_frames']:].copy()
    rng = np.random.default_rng()
    rng.bit_generator.state = trace['rng_state_after_noise']
    f0 = pipeline.graphs['hift_f0'](mel)[0]
    spectrum = pipeline.graphs['hift_core'](mel, source_spectrum(f0, pipeline.source, rng))[0]
    return waveform(spectrum)


def save_wav(path, pcm):
    with wave.open(str(path), 'wb') as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(22050)
        file.writeframes((pcm * 32767).astype('<i2').tobytes())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['models', 'work', 'output']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--text', required=True)
    parser.add_argument('--seed', type=int, default=47)
    parser.add_argument('--decode-profile', default='splitbody4out8b64')
    parser.add_argument('--steps', type=int, nargs='+', default=[2, 3, 5, 10])
    parser.add_argument('--guidance', type=float, default=.7)
    parser.add_argument('--flow-backend', type=int, default=1)
    parser.add_argument('--hift-backend', type=int, default=1)
    args = parser.parse_args()
    if not args.output.resolve().is_relative_to(args.work.resolve()):
        parser.error('output must be inside work')
    if any(step < 1 for step in args.steps) or not 0 <= args.guidance <= 2:
        parser.error('steps must be positive and guidance must be in [0,2]')
    args.output.mkdir(parents=True, exist_ok=True)
    pipeline = Pipeline(args.models, args.work, compute='low', llm_backend=0,
                        flow_backend=args.flow_backend, hift_backend=args.hift_backend,
                        decode_profile=args.decode_profile, decode_memory=2,
                        prompt_path=args.models / 'common/prompt-1.0-fullspeaker.npz')
    trace = {}
    original, _ = pipeline.synthesize(args.text, steps=2, seed=args.seed, flow_guidance=args.guidance,
                                     max_token_text_ratio=40, flow_trace=trace)
    replay = render(pipeline, trace, 2, args.guidance)
    error = float(np.max(np.abs(original - replay)))
    if not np.array_equal(original, replay):
        raise RuntimeError(f'Two-step replay differs from original waveform: max_abs={error}')
    report = {'scope': 'quality only; fixed speech tokens/conditioning/noise; no end-to-end RTF',
              'text': args.text, 'seed': args.seed, 'decode_profile': args.decode_profile,
              'flow_guidance': args.guidance, 'flow_backend': args.flow_backend,
              'hift_backend': args.hift_backend, 'speech_token_count': int(trace['speech_tokens'].size),
              'two_step_replay_max_abs': error, 'runs': []}
    arrays = {key: value for key, value in trace.items() if isinstance(value, np.ndarray)}
    np.savez(args.output / 'flow-trace.npz', **arrays,
             prompt_mel_frames=np.array(trace['prompt_mel_frames']),
             rng_state_json=np.array(json.dumps(trace['rng_state_after_noise'])))
    save_wav(args.output / 'original-2.wav', original)
    for steps in dict.fromkeys(args.steps):
        pcm = replay if steps == 2 else render(pipeline, trace, steps, args.guidance)
        if not np.isfinite(pcm).all() or np.sqrt(np.mean(pcm * pcm)) < 1e-4:
            raise RuntimeError('Invalid waveform at steps=' + str(steps))
        save_wav(args.output / ('steps-' + str(steps) + '.wav'), pcm)
        row = {'steps': steps, 'audio_s': len(pcm) / 22050, 'rms': float(np.sqrt(np.mean(pcm * pcm)))}
        report['runs'].append(row)
        (args.output / 'results.json').write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n')
        print(json.dumps(row), flush=True)


if __name__ == '__main__':
    main()
