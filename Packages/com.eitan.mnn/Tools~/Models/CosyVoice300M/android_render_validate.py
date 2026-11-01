#!/usr/bin/env python3
"""Check captured render-stage fixtures with unmodified official Android tools.

Reports fixed-input component numerics and synchronized forward timings only.
It does not execute a complete Android synthesis or measure its RTF.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shlex
import subprocess


def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for name in ['models', 'work', 'fixtures', 'output']:
        p.add_argument('--' + name, type=Path, required=True)
    p.add_argument('--serial', required=True)
    p.add_argument('--cases', nargs='+')
    p.add_argument('--backends', nargs='+', type=int, choices=[0, 3, 7], default=[0, 3])
    p.add_argument('--compute', choices=['high', 'low'], default='high')
    p.add_argument('--threads', type=int, default=4, help='CPU thread count')
    p.add_argument('--gpu-mode', type=int, default=1,
                   help='Official MNN GPU bitmask; default1=no tuning; OpenCL16=fast,4=wide,65=buffer/no tuning')
    p.add_argument('--loops', type=int, default=3)
    p.add_argument('--rounds', type=int, default=1)
    p.add_argument('--device-timeout', type=int, default=60)
    p.add_argument('--cache-policy', choices=['shared', 'run'], default='shared',
                   help='Reuse official tuning cache per tool/model/weight/backend/mode/precision, or per run directory')
    args = p.parse_args()
    if min(args.threads, args.loops, args.rounds, args.device_timeout) < 1:
        p.error('Thread count, loops, rounds and timeout must be positive')
    if len(set(args.backends)) != len(args.backends):
        p.error('Backend IDs must be unique')
    tuning = args.gpu_mode & 31
    if args.gpu_mode <= 0 or args.gpu_mode & ~(31 | 64 | 128 | 256 | 512):
        p.error('Unknown GPU mode bits')
    if tuning == 0 or tuning & (tuning - 1) or args.gpu_mode & 192 == 192:
        p.error('Choose exactly one GPU tuning level and at most one OpenCL memory mode')
    if args.gpu_mode & 768 == 768:
        p.error('Choose at most one GPU recording mode')
    if 7 in args.backends and (tuning not in [1, 2, 4] or args.gpu_mode & (64 | 128 | 256)):
        p.error('Vulkan supports tuning none/heavy/wide and optional record-batch only')
    for path in [args.output, args.fixtures]:
        if not path.resolve().is_relative_to(args.work.resolve()):
            p.error('--fixtures and --output must be inside --work')
    if (args.output / 'results.json').exists():
        p.error('Output already has results; choose a new directory to preserve evidence')
    records = json.loads((args.fixtures / 'reference.json').read_text())['fixtures']
    known_cases = {r['case'] for r in records}
    if args.cases and not set(args.cases).issubset(known_cases):
        p.error('Unknown fixture case')
    records = [r for r in records if not args.cases or r['case'] in args.cases]
    allowed_graphs = {'flow_encoder', 'flow_regulator', 'flow_estimator', 'hift_f0', 'hift_core'}
    for record in records:
        if record['graph'] not in allowed_graphs or not re.fullmatch(r'[A-Za-z0-9_-]+', record['case']):
            p.error('Unsupported graph or fixture case name')
        if any(not re.fullmatch(r'[A-Za-z0-9_]+', name) for name in record['inputs'] + record['outputs']):
            p.error('Tensor names must be simple filenames')
    args.output.mkdir(parents=True, exist_ok=True)
    adb = ['adb', '-s', args.serial]
    remote = '/data/local/tmp/mnn-cosyvoice300m'
    run_root = remote + '/render-runs/' + hashlib.sha256(str(args.output.resolve()).encode()).hexdigest()[:16]

    def run(arguments, check=True, timeout=90):
        result = subprocess.run(adb + arguments, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, timeout=timeout)
        if check and result.returncode:
            raise RuntimeError(result.stdout)
        return result

    def shell(arguments, check=True):
        return run(['shell', shlex.join([str(v) for v in arguments])], check=check)

    def ensure_file(local, target):
        expected = digest(local)
        before = shell(['sha256sum', target], check=False)
        if before.returncode or before.stdout.split()[0] != expected:
            run(['push', str(local), target])
        if shell(['sha256sum', target]).stdout.split()[0] != expected:
            raise RuntimeError('Device file hash mismatch: ' + target)
        return expected

    import MNN
    import numpy as np
    from validate import metrics

    shell(['mkdir', '-p', remote + '/render-fixtures', remote + '/render-runs', remote + '/render-cache', remote + '/fp16'])
    binary = args.work / 'Dependencies/AndroidBuild/ModuleBasic.out'
    binary_sha = ensure_file(binary, remote + '/ModuleBasic.out')
    shell(['chmod', '+x', remote + '/ModuleBasic.out'])
    model_hashes = {}
    for name in sorted({r['graph'] for r in records}):
        for suffix in ['.mnn', '.mnn.weight']:
            filename = name + suffix
            model_hashes[filename] = ensure_file(args.models / 'fp16' / filename, remote + '/fp16/' + filename)
    report = {'scope': 'fixed captured real render inputs; no complete Android synthesis, audio acceptance or RTF',
              'serial': args.serial, 'device': shell(['getprop', 'ro.product.model']).stdout.strip(),
              'module_basic_sha256': binary_sha, 'model_sha256': model_hashes,
              'compute': args.compute, 'cpu_threads': args.threads, 'gpu_mode': args.gpu_mode, 'loops': args.loops,
              'cache_policy': args.cache_policy,
              'device_run_root': run_root,
              'reference': 'M5 CPU-high execution of same FP16 converted graphs on captured M5 candidate inputs',
              'fixtures': [], 'runs': []}
    for record in records:
        case = record['case']
        fixture = args.fixtures / 'fixtures' / case
        reference = MNN.expr.load_as_dict(str(fixture / 'output.mnn'))
        expected = {key: reference[key].read().copy() for key in record['outputs']}
        assert all(np.isfinite(v).all() for v in expected.values())
        run(['push', str(fixture), remote + '/render-fixtures/'])
        report['fixtures'].append(dict(record, input_sha256=digest(fixture / 'input.mnn'),
                                       reference_sha256=digest(fixture / 'output.mnn')))
        for repeat in range(args.rounds):
            order = args.backends[repeat % len(args.backends):] + args.backends[:repeat % len(args.backends)]
            for backend in order:
                mode = args.threads if backend == 0 else args.gpu_mode
                label = f'{case}-{args.compute}-b{backend}-t{mode}-r{repeat}'
                directory = run_root + '/' + label
                shell(['mkdir', '-p', directory + '/output'])
                shell(['rm', '-f', directory + '/output/output.mnn'])
                cache_identity = [binary_sha, model_hashes[record['graph'] + '.mnn'],
                                  model_hashes[record['graph'] + '.mnn.weight'], backend, mode, args.compute]
                cache_key = hashlib.sha256(json.dumps(cache_identity).encode()).hexdigest()
                cache = remote + '/render-cache/' + cache_key + '.cache' if args.cache_policy == 'shared' else 'runtime.cache'
                cache_before = shell(['sha256sum', cache if cache.startswith('/') else directory + '/' + cache], check=False)
                command = [remote + '/ModuleBasic.out', remote + '/fp16/' + record['graph'] + '.mnn',
                           remote + '/render-fixtures/' + case, '160', str(backend), str(args.loops),
                           str(mode), str(1 if args.compute == 'high' else 2), cache]
                command_text = 'cd ' + shlex.quote(directory) + ' && ' + shlex.join(['timeout', str(args.device_timeout)] + command)
                row = {'case': case, 'graph': record['graph'], 'backend': backend, 'round': repeat,
                       'threads_or_gpu_mode': mode,
                       'device_run_directory': directory,
                       'cache_sha256_before': cache_before.stdout.split()[0] if cache_before.returncode == 0 else None,
                       'command': command, 'finite': None, 'official_comparison_passed': False}
                try:
                    # Force a device PTY so unmodified printf diagnostics are
                    # line-buffered and survive a startup/compile timeout.
                    result = run(['shell', '-tt', command_text], check=False, timeout=args.device_timeout + 15)
                    (args.output / (label + '.log')).write_text(result.stdout)
                    timing = re.search(r'Avg=\s*([\d.]+) ms, min=\s*([\d.]+) ms, max=\s*([\d.]+) ms', result.stdout)
                    compared = ('TESTERROR' not in result.stdout and 'Skip check' not in result.stdout
                                and result.stdout.count('before compare ') == 2 * len(expected))
                    row.update(exit_code=result.returncode, avg_ms=float(timing[1]) if timing else None,
                               min_ms=float(timing[2]) if timing else None, max_ms=float(timing[3]) if timing else None,
                               official_output_checks_passed=compared,
                               official_comparison_passed=(result.returncode == 0 and compared))
                    # The tool writes the first actual output before its timed
                    # loop. Preserve it even when later benchmarking times out;
                    # output/output.mnn was removed immediately before this run.
                    row['actual_output_saved'] = shell(['test', '-f', directory + '/output/output.mnn'], check=False).returncode == 0
                    if row['actual_output_saved']:
                        target = args.output / (label + '-output.mnn')
                        run(['pull', directory + '/output/output.mnn', str(target)])
                        actual = MNN.expr.load_as_dict(str(target))
                        row['outputs'] = {}
                        for key, target_values in expected.items():
                            values = actual[key].read().copy()
                            if values.shape != target_values.shape:
                                raise RuntimeError('Output shape mismatch: ' + key)
                            finite = bool(np.isfinite(values).all())
                            item = {'finite': finite, 'shape': list(values.shape),
                                    'sha256': hashlib.sha256(values.tobytes()).hexdigest()}
                            if finite:
                                item.update(metrics(values, target_values))
                            row['outputs'][key] = item
                        row['finite'] = all(v['finite'] for v in row['outputs'].values())
                except subprocess.TimeoutExpired as error:
                    log = error.stdout or ''
                    if isinstance(log, bytes):
                        log = log.decode(errors='replace')
                    (args.output / (label + '.log')).write_text(log)
                    row['timeout'] = True
                report['runs'].append(row)
                (args.output / 'results.json').write_text(json.dumps(report, indent=2) + '\n')
                print(json.dumps({k: v for k, v in row.items() if k not in ['command', 'outputs']}), flush=True)
    if any(row.get('avg_ms') is None or not row['finite'] or not row['official_comparison_passed']
           for row in report['runs']):
        raise SystemExit('A timing, finite-output or official comparison check failed; inspect results.json and raw logs')


if __name__ == '__main__':
    main()
