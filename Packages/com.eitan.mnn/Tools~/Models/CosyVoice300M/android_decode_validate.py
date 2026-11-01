#!/usr/bin/env python3
"""Validate split-Q=1 decoders with unmodified official Android ModuleBasic.

These are fixed-input component checks. They do not measure complete Android
TTS RTF, dynamic KV growth, sampling, Flow, HiFT or Unity integration.
"""
import argparse
import hashlib
import json
from pathlib import Path
import platform
import re
import shlex
import subprocess


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            value.update(block)
    return value.hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['models', 'work']:
        parser.add_argument('--' + name, type=Path, required=True)
    parser.add_argument('--serial', required=True)
    parser.add_argument('--profiles', nargs='+', default=['splitbody4out8b64nopost', 'splitbody4out8b64nopostfixedshape'])
    parser.add_argument('--past', nargs='+', type=int, default=[31])
    parser.add_argument('--backends', nargs='+', type=int, choices=[0, 3, 7], default=[0])
    parser.add_argument('--compute', choices=['high', 'low'], default='high')
    parser.add_argument('--memory', choices=['normal', 'low'], default='low')
    parser.add_argument('--threads', type=int, default=4, help='ModuleBasic numThread: CPU threads or GPU mode')
    parser.add_argument('--cpu-ids', nargs='+', type=int,
                        help='Optional official CPU_CORE_IDS hint, e.g. 6 7; identify cores on the target device first')
    parser.add_argument('--loops', type=int, default=20)
    parser.add_argument('--rounds', type=int, default=1)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    if min(args.threads, args.loops, args.rounds) < 1:
        parser.error('Threads, loops and rounds must be positive')
    if args.cpu_ids is not None and (min(args.cpu_ids) < 0 or len(set(args.cpu_ids)) != len(args.cpu_ids)):
        parser.error('CPU IDs must be unique nonnegative integers')
    if args.cpu_ids is not None and args.backends != [0]:
        parser.error('Explicit CPU affinity is implemented only for CPU backend0')
    output = args.output or args.work / ('Android-decode-' + args.compute)
    if not output.resolve().is_relative_to(args.work.resolve()):
        parser.error('--output must be inside --work')
    output.mkdir(parents=True, exist_ok=True)
    config = json.loads((args.models / 'experimental-decode/config.json').read_text())
    contracts = [config.get('decode_contracts', {}).get(name, {}) for name in args.profiles]
    for contract in contracts:
        if (contract.get('state_layout') != 'per_layer_kv' or contract.get('capacity') or
                contract.get('internal_positions') or contract.get('excludes_sentinel') or
                contract.get('key_layout', 'B,H,P,D') != 'B,H,P,D'):
            parser.error('Only dynamic split-KV decoders with original external positions/sentinel and B,H,P,D Keys are implemented')
    if not all((c['inputs'], c['outputs']) == (contracts[0]['inputs'], contracts[0]['outputs']) for c in contracts):
        parser.error('Profiles must share identical input/output contracts')
    contract = contracts[0]
    if len(contract['inputs']) != 31 or len(contract['outputs']) != 29:
        parser.error('Expected the original31-input/29-output split decoder')
    if any(not re.fullmatch(r'[A-Za-z0-9_]+', name) for name in contract['inputs'] + contract['outputs']):
        parser.error('Fixture tensor names must be simple filenames')
    if any(not re.fullmatch(r'[A-Za-z0-9_]+', name) for name in args.profiles):
        parser.error('Profile names must be simple filenames')
    adb = ['adb', '-s', args.serial]
    remote = '/data/local/tmp/mnn-cosyvoice300m'

    def run(arguments, check=True, timeout=100):
        result = subprocess.run(adb + arguments, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, timeout=timeout)
        if check and result.returncode:
            raise RuntimeError(result.stdout)
        return result

    def shell(arguments, check=True):
        return run(['shell', shlex.join([str(item) for item in arguments])], check=check)

    def ensure_file(local, target):
        expected = digest(local)
        before = shell(['sha256sum', target], check=False)
        if before.returncode or before.stdout.split()[0] != expected:
            run(['push', str(local), target])
        actual = shell(['sha256sum', target]).stdout.split()[0]
        if actual != expected:
            raise RuntimeError('Device file hash differs: ' + target)
        return expected

    shell(['mkdir', '-p', remote + '/decode-models', remote + '/decode-fixtures', remote + '/decode-runs'])
    binary = args.work / 'Dependencies/AndroidBuild/ModuleBasic.out'
    binary_sha = ensure_file(binary, remote + '/ModuleBasic.out')
    shell(['chmod', '+x', remote + '/ModuleBasic.out'])
    model_files = {}
    for name in args.profiles:
        local = args.models / 'experimental-decode' / config[name]
        target = remote + '/decode-models/' + local.name
        graph_sha = ensure_file(local, target)
        weight = Path(str(local) + '.weight')
        weight_sha = digest(weight)
        shared = remote + '/decode-models/weights-' + weight_sha + '.bin'
        ensure_file(weight, shared)
        linked = shell(['ln', '-f', shared, target + '.weight'], check=False)
        materialization = 'hardlink'
        if linked.returncode:
            # Android SELinux policies can deny hard links in shell storage.
            # Copy on the device to avoid uploading identical weights again.
            shell(['cp', '-f', shared, target + '.weight'])
            materialization = 'device copy'
        if shell(['sha256sum', target + '.weight']).stdout.split()[0] != weight_sha:
            raise RuntimeError('Device weight link differs')
        model_files[name] = {'path': target, 'graph_sha256': graph_sha, 'weight_sha256': weight_sha,
                             'weight_materialization': materialization}

    import MNN
    import numpy as np
    from runtime import Graph
    from validate import metrics
    memory = 2 if args.memory == 'low' else 0
    precision = 1 if args.compute == 'high' else 2
    reference = Graph(args.models / 'experimental-decode' / config[args.profiles[0]], 'llm_cached',
                      threads=args.threads, precision=args.compute, memory=memory,
                      contract=(contract['inputs'], contract['outputs']))
    table = np.load(args.models / 'common/projected_positions.npy', mmap_mode='r')
    middle = table.shape[2] // 2
    if min(args.past) < 2 or max(args.past) > middle:
        parser.error('Past length must fit the exported position table')
    report = {'serial': args.serial, 'device': shell(['getprop', 'ro.product.model']).stdout.strip(),
              'board': shell(['getprop', 'ro.board.platform']).stdout.strip(),
              'scope': 'fixed-input Q=1 component checks only; no end-to-end Android RTF',
              'reference_host': platform.platform(), 'reference_profile': args.profiles[0],
              'compute': args.compute, 'memory': args.memory, 'numThread': args.threads,
              'cpu_ids_hint': args.cpu_ids,
              'loops': args.loops, 'rounds': args.rounds, 'official_binary_sha256': binary_sha,
              'official_sme_ratio_argument': 41, 'model_files': model_files, 'fixtures': [], 'runs': []}
    result_path = output / 'results.json'
    for past in args.past:
        rng = np.random.default_rng(20261028 + past)
        x = rng.standard_normal((1, 1, 1024), dtype=np.float32) * .1
        positions = table[:, :, middle - past:middle + 1].copy()
        mask = np.ones((1, 1, past + 1), np.int32)
        mask[:, :, 0] = 0
        packed = rng.standard_normal((14, 16, past, 128), dtype=np.float32) * .1
        values = [x, positions, mask] + [packed[j:j + 1, :, :, k:k + 64].copy()
                                       for j in range(14) for k in [0, 64]]
        expected = reference(*values)
        if not all(np.isfinite(value).all() for value in expected):
            raise RuntimeError('Reference contains nonfinite outputs')
        fixture_name = f'{args.compute}-m{memory}-p{past}'
        fixture = output / 'fixtures' / fixture_name
        fixture.mkdir(parents=True, exist_ok=True)

        def save_constants(filename, names, arrays):
            variables = []
            for name, array in zip(names, arrays):
                value = MNN.expr.const(array, list(array.shape), MNN.expr.NCHW,
                                       MNN.expr.int if array.dtype.kind in 'iu' else MNN.expr.float)
                value.name = name
                variables.append(value)
            MNN.expr.save(variables, str(fixture / filename))

        save_constants('input.mnn', contract['inputs'], values)
        save_constants('output.mnn', contract['outputs'], expected)
        for name, array in zip(contract['outputs'], expected):
            np.savetxt(fixture / (name + '.txt'), array.reshape(-1), fmt='%.9g')
        run(['push', str(fixture), remote + '/decode-fixtures/'])
        remote_fixture = remote + '/decode-fixtures/' + fixture_name
        report['fixtures'].append({'name': fixture_name, 'past': past,
                                    'input_sha256': digest(fixture / 'input.mnn'),
                                    'reference_output_sha256': digest(fixture / 'output.mnn')})
        peers = {}
        for repeat in range(args.rounds):
            order = args.profiles if repeat % 2 == 0 else list(reversed(args.profiles))
            for backend in args.backends:
                for name in order:
                    label = f'{fixture_name}-b{backend}-{name}-r{repeat}'
                    if args.cpu_ids is not None:
                        label += '-t' + str(args.threads) + '-c' + '_'.join(map(str, args.cpu_ids))
                    directory = remote + '/decode-runs/' + label
                    shell(['mkdir', '-p', directory + '/output'])
                    shell(['rm', '-f', directory + '/output/output.mnn'])
                    command = [remote + '/ModuleBasic.out', model_files[name]['path'], remote_fixture,
                               '160', str(backend), str(args.loops), str(args.threads), str(precision + memory * 4),
                               'runtime.cache', ','.join(map(str, args.cpu_ids or [])), '0', '41']
                    command_text = 'cd ' + shlex.quote(directory) + ' && ' + shlex.join(['timeout', '80'] + command)
                    row = {'profile': name, 'past': past, 'backend': backend, 'round': repeat, 'command': command}
                    try:
                        result = run(['shell', command_text], check=False)
                        (output / (label + '.log')).write_text(result.stdout)
                        timing = re.search(r'Avg=\s*([\d.]+) ms, min=\s*([\d.]+) ms, max=\s*([\d.]+) ms', result.stdout)
                        row.update(exit_code=result.returncode, avg_ms=float(timing[1]) if timing else None,
                                   official_comparison_passed=(result.returncode == 0 and 'TESTERROR' not in result.stdout and
                                       'Skip check' not in result.stdout and result.stdout.count('before compare ') == 58))
                        if timing and result.returncode == 0:
                            target = output / (label + '-output.mnn')
                            run(['pull', directory + '/output/output.mnn', str(target)])
                            actual_vars = MNN.expr.load_as_dict(str(target))
                            actual = [actual_vars[key].read().copy().reshape(value.shape)
                                      for key, value in zip(contract['outputs'], expected)]
                            row['finite'] = all(bool(np.isfinite(value).all()) for value in actual)
                            if row['finite']:
                                row['outputs'] = {key: dict(metrics(a, b), sha256=hashlib.sha256(a.tobytes()).hexdigest())
                                                  for key, a, b in zip(contract['outputs'], actual, expected)}
                                if backend not in peers:
                                    peers[backend] = actual
                                row['same_device_first_output_exact_count'] = sum(np.array_equal(a, b)
                                    for a, b in zip(actual, peers[backend]))
                    except subprocess.TimeoutExpired as error:
                        captured = error.stdout or ''
                        if isinstance(captured, bytes):
                            captured = captured.decode(errors='replace')
                        (output / (label + '.log')).write_text(captured)
                        row['timeout'] = True
                    report['runs'].append(row)
                    result_path.write_text(json.dumps(report, indent=2) + '\n')
                    print(json.dumps({key: value for key, value in row.items() if key not in ['outputs', 'command']}), flush=True)
    if any(row.get('avg_ms') is None or not row.get('finite', False) for row in report['runs']):
        raise SystemExit('At least one component failed; inspect results.json and raw logs')


if __name__ == '__main__':
    main()
