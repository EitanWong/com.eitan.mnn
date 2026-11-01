#!/usr/bin/env python3
"""Synthesize WAV and measure complete MNN pipeline RTF on this device."""
import argparse
import json
from pathlib import Path
import platform
import time
import wave

import numpy as np

from runtime import Pipeline, RUNTIME_REVISION


def main():
    p = argparse.ArgumentParser(description=__doc__, epilog=(
        'MNN runtime hints can affect other managers in the current executor. '
        'Compare different hint policies in separate processes.'))
    p.add_argument('--models', type=Path, required=True)
    p.add_argument('--work', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--precision', choices=['fp16', 'int8', 'int4', 'mixed8'], default='fp16')
    p.add_argument('--backend', type=int, default=0, help='MNN backend: CPU=0, Metal=1, OpenCL=3, Vulkan=7')
    p.add_argument('--threads', type=int, default=4)
    p.add_argument('--compute', choices=['high', 'normal', 'low'], default='high')
    p.add_argument('--steps', type=int, default=10)
    p.add_argument('--repeats', type=int, default=2)
    p.add_argument('--text', default='你好，欢迎使用语音合成。')
    p.add_argument('--engine', choices=['mnn', 'onnx'], default='mnn')
    p.add_argument('--greedy', action='store_true')
    p.add_argument('--hift-backend', type=int, help='Override only HiFT graphs, e.g. 1 for Metal')
    p.add_argument('--flow-backend', type=int, help='Override Flow graphs only')
    p.add_argument('--llm-backend', type=int, help='Override the autoregressive LLM only')
    p.add_argument('--gpu-record-batch', action='store_true', help='Allow official Metal deferred command encoding')
    p.add_argument('--host-positions', action='store_true', help='Use host slicing for an A/B comparison')
    p.add_argument('--decode-profile', default='baseline', help='baseline or a profile registered in experimental-decode/config.json')
    p.add_argument('--decode-memory', choices=['normal', 'low'], default='normal')
    p.add_argument('--expr-backend', type=int, help='Explicit backend for auxiliary official MNN expression operations')
    p.add_argument('--expr-threads', type=int, default=4)
    p.add_argument('--flow-prompt-seconds', type=float, help='Experiment: shorten only Flow reference conditioning; AR reference unchanged')
    p.add_argument('--decode-kleidiai', action='store_true', help='Set official CPU_ENABLE_KLEIDIAI through the decode manager; shared executor hint')
    p.add_argument('--flow-native', action='store_true', help='Use fused CFG/Euler MNN graph and keep state native between steps')
    p.add_argument('--decode-threads', type=int, help='Override only the independent decode module thread count')
    p.add_argument('--prefill-backend', type=int, help='Override prefill graph backend while keeping independent decode backend')
    p.add_argument('--flow-guidance', type=float, default=.7, help='Experiment: CFG coefficient; 0 removes the unconditioned batch')
    p.add_argument('--reference-mode', choices=['zero-shot', 'cross-lingual'], default='zero-shot',
                   help='Official conditioning mode; cross-lingual removes AR prompt text/speech, retains speaker/Flow reference')
    p.add_argument('--decode-commit-ops', type=int, help='Set official Metal commit interval through the decode manager; shared executor hint')
    p.add_argument('--flow-commit-ops', type=int, help='Set official Metal commit interval through Flow managers; shared executor hint')
    p.add_argument('--prompt', type=Path, help='Alternate aligned zero-shot prompt NPZ')
    p.add_argument('--seed', type=int, default=42)
    p.add_argument('--sampling-policy', choices=['official-ras', 'legacy'], default='official-ras',
                   help='Official RAS fallback retains the repeated candidate; legacy reproduces older reports')
    p.add_argument('--decode-quant-option', type=int, help='Set official DYNAMIC_QUANT_OPTIONS through the decode manager; shared executor hint')
    p.add_argument('--sampling-temperature', type=float, default=1., help='Experiment: rescale logits before RAS; default unchanged distribution')
    p.add_argument('--max-token-text-ratio', type=float, default=20.,
                   help='Generation safety cap, as in upstream inference; increasing it permits longer speech without forcing EOS')
    p.add_argument('--cache-dir', type=Path,
                   help='Official GPU tuning cache directory inside --work; use a different directory for each runtime/device')
    a = p.parse_args()
    if a.cache_dir and not a.cache_dir.resolve().is_relative_to(a.work.resolve()):
        p.error('--cache-dir must be inside --work to keep device caches with test artifacts')
    a.output.mkdir(parents=True, exist_ok=True)
    if a.expr_backend is not None:
        import MNN
        MNN.expr.set_global_executor_config(a.expr_backend, {'high': 1, 'normal': 0, 'low': 2}[a.compute], a.expr_threads)
    start = time.perf_counter()
    model = Pipeline(a.models, a.work, a.precision, a.backend, a.threads, a.compute, a.engine,
                     a.hift_backend, not a.host_positions, a.flow_backend, a.llm_backend, a.gpu_record_batch,
                     a.decode_profile, 2 if a.decode_memory == 'low' else 0, a.decode_kleidiai, a.flow_native, a.decode_threads,
                     a.prefill_backend, a.decode_commit_ops, a.flow_commit_ops, a.prompt, a.decode_quant_option, a.cache_dir)
    report = {'device': platform.platform(), 'engine': a.engine, 'weights': a.precision,
              'runtime_revision': RUNTIME_REVISION,
              'requested_backend': a.backend, 'compute_precision': a.compute, 'threads': a.threads,
              'memory_mode': 0,
              'hift_backend': a.hift_backend if a.hift_backend is not None else a.backend,
              'flow_backend': a.flow_backend if a.flow_backend is not None else a.backend,
              'llm_backend': a.llm_backend if a.llm_backend is not None else a.backend,
              'gpu_record_batch': a.gpu_record_batch,
              'decode_profile': a.decode_profile,
              'decode_memory': a.decode_memory,
              'expr_backend': a.expr_backend, 'expr_threads': a.expr_threads,
              'flow_prompt_seconds': a.flow_prompt_seconds,
              'decode_kleidiai': a.decode_kleidiai,
              'flow_native': a.flow_native,
              'decode_threads': a.decode_threads if a.decode_threads is not None else a.threads,
              'prefill_backend': a.prefill_backend if a.prefill_backend is not None else a.llm_backend,
              'flow_guidance': a.flow_guidance,
              'reference_mode': a.reference_mode,
              'decode_commit_ops': a.decode_commit_ops, 'flow_commit_ops': a.flow_commit_ops,
              'prompt': str(a.prompt) if a.prompt else 'common/prompt.npz',
              'seed': a.seed,
              'sampling_policy': a.sampling_policy,
              'decode_quant_option': a.decode_quant_option,
              'sampling_temperature': a.sampling_temperature,
              'max_token_text_ratio': a.max_token_text_ratio,
              'cache_dir': str(a.cache_dir) if a.cache_dir else None,
              'native_position_slice': not a.host_positions,
              'load_s': time.perf_counter() - start, 'text': a.text,
              'rtf_definition': 'text tokenization + text encoder + cached AR + flow + neural HiFT + audio DSP / output duration; excludes model load and cached reference voice preprocessing',
              'runs': []}
    for i in range(a.repeats):
        print('synthesize', i, flush=True)
        pcm, metrics = model.synthesize(a.text, a.steps, seed=a.seed, greedy=a.greedy, flow_prompt_seconds=a.flow_prompt_seconds,
                                        flow_guidance=a.flow_guidance, reference_mode=a.reference_mode,
                                        sampling_policy=a.sampling_policy, sampling_temperature=a.sampling_temperature,
                                        max_token_text_ratio=a.max_token_text_ratio)
        metrics['cold_graph_run'] = i == 0
        metrics['rms'] = float(np.sqrt(np.mean(pcm ** 2)))
        with wave.open(str(a.output / ('sample-' + str(i) + '.wav')), 'wb') as f:
            f.setnchannels(1); f.setsampwidth(2); f.setframerate(22050)
            f.writeframes((pcm * 32767).astype('<i2').tobytes())
        report['runs'].append(metrics)
        cache_start = time.perf_counter()
        model.update_caches()
        metrics['cache_update_s_outside_synthesis'] = time.perf_counter() - cache_start
        (a.output / 'results.json').write_text(json.dumps(report, indent=2, ensure_ascii=False))
        print(json.dumps(metrics), flush=True)


if __name__ == '__main__':
    main()
