#!/usr/bin/env python3
"""Compare identical forced decode inputs; this measures token latency, not TTS RTF."""
import argparse
import json
from pathlib import Path
import time

import numpy as np

from runtime import Graph


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--models', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--profiles', nargs='+', default=['baseline', 'compact', 'compact8-low'])
    p.add_argument('--past', type=int, default=200)
    p.add_argument('--steps', type=int, default=100)
    p.add_argument('--rounds', type=int, default=3)
    p.add_argument('--threads', type=int, default=4)
    a = p.parse_args()
    import MNN
    f = MNN.expr
    config = json.loads((a.models / 'experimental-decode/config.json').read_text())
    if a.past < 2 or a.steps < 1 or a.rounds < 1:
        p.error('past >= 2, steps >= 1 and rounds >= 1 are required')
    rng = np.random.default_rng(20261028)
    embeddings = np.load(a.models / 'common/llm_embeddings.npz')['speech_embedding_weight']
    sequence = [embeddings[int(x)][None, None].copy() for x in rng.integers(0, 4096, a.steps)]
    table = np.load(a.models / 'common/projected_positions.npy')
    middle = table.shape[2] // 2
    if a.past + a.steps > middle + 1:
        p.error('Forced sequence exceeds the exported position table')
    a.output.parent.mkdir(parents=True, exist_ok=True)
    base = Graph(a.models / 'fp16/llm_cached_projected.mnn', 'llm_cached', a.threads, precision='low')
    table_var = base.var(table)
    prefill = rng.standard_normal((1, a.past - 1, 1024), dtype=np.float32)
    mask = np.concatenate([np.zeros((1, a.past - 1, 1), np.int32), np.tri(a.past - 1, dtype=np.int32)[None]], axis=2)
    initial = base(prefill, np.zeros((14, 16, 1, 128), np.float32),
                   table[:, :, middle - a.past + 1:middle + a.past].copy(), mask)[1]
    graphs = {}
    for label in a.profiles:
        name = label.removesuffix('-low')
        if name != 'baseline' and name not in config:
            p.error('Unknown profile: ' + label)
        contract = config.get('decode_contracts', {}).get(name)
        capacity = (contract or {}).get('capacity') or (config['capacity'] if name.startswith('static') else None)
        if capacity and a.past + a.steps > capacity:
            p.error(label + ': forced sequence exceeds static capacity')
        graphs[label] = base if name == 'baseline' else Graph(
            a.models / 'experimental-decode' / config[name], 'llm_cached', a.threads,
            precision='low', memory=2 if label.endswith('-low') else 0,
            contract=(contract['inputs'], contract['outputs']) if contract else None)
    report = {'scope': '100% identical forced embeddings and initial KV; excludes prefill and model load; no end-to-end RTF',
              'past': a.past, 'steps': a.steps, 'threads': a.threads, 'backend': 'CPU', 'compute': 'low', 'runs': []}
    for repeat in range(a.rounds):
        order = a.profiles[repeat % len(a.profiles):] + a.profiles[:repeat % len(a.profiles)]
        for label in order:
            graph = graphs[label]
            name = label.removesuffix('-low')
            contract = config.get('decode_contracts', {}).get(name)
            split = contract is not None and contract['state_layout'] == 'per_layer_kv'
            capacity = (contract or {}).get('capacity') or (config['capacity'] if name.startswith('static') else None)
            static = capacity is not None
            excludes_sentinel = bool(contract and contract.get('excludes_sentinel'))
            if split:
                first_key = 1 if excludes_sentinel else 0
                cache = []
                for j in range(14):
                    for k in (0, 64):
                        state = np.pad(initial[j:j + 1, :, first_key:, k:k + 64],
                                       ((0, 0), (0, 0), (capacity - a.past if static else 0, 0), (0, 0)))
                        if k == 0 and contract.get('key_layout') == 'B,H,D,P':
                            state = state.transpose(0, 1, 3, 2).copy()
                        cache.append(graph.var(state))
            else:
                cache = graph.var(np.pad(initial, ((0, 0), (0, 0), (capacity - a.past, 0), (0, 0)))) if static else graph.var(initial)
            static_positions = graph.var(table[:, :, middle - capacity:middle + 1].copy()) if static else None
            start = time.perf_counter()
            for step, value in enumerate(sequence):
                past = a.past + step
                if static:
                    mask = np.zeros((1, 1, capacity + 1), np.int32)
                    mask[:, :, -past:] = 1
                    positions = static_positions
                else:
                    mask = np.ones((1, 1, past + 1), np.int32)
                    mask[:, :, 0] = 0
                    count = 2 * (past + 1) - 1 if name == 'baseline' else past + 1 - int(excludes_sentinel)
                    positions = (np.array([middle - past], np.int32) if contract and contract.get('internal_positions') else
                                 f.slice(table_var, [0, 0, middle - past + int(excludes_sentinel), 0], [14, 16, count, 64]))
                if split:
                    result = (graph(value, positions, *cache, native=True) if excludes_sentinel else
                              graph(value, positions, mask, *cache, native=True))
                    logits, cache = result[0], result[1:]
                else:
                    logits, cache = graph(value, cache, positions, mask, native=True)
                if not np.isfinite(graph.array(logits)).all():
                    raise RuntimeError(label + ': non-finite logits')
            seconds = time.perf_counter() - start
            row = {'profile': label, 'round': repeat, 'first_run_for_graph': repeat == 0,
                   'seconds': seconds, 'mean_ms_per_token': seconds * 1000 / a.steps}
            report['runs'].append(row)
            print(json.dumps(row), flush=True)
            a.output.write_text(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
