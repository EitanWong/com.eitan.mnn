#!/usr/bin/env python3
"""Run unmodified official MNN tools on Android; report component checks only."""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess

import numpy as np
import MNN
from validate import metrics


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--models', type=Path, required=True)
    p.add_argument('--work', type=Path, required=True)
    p.add_argument('--serial', required=True)
    p.add_argument('--backends', nargs='+', type=int, default=[0])
    p.add_argument('--skip-push-models', action='store_true')
    p.add_argument('--compute', choices=['high', 'low'], default='high')
    p.add_argument('--graphs', nargs='+', help='Restrict the component names to benchmark')
    p.add_argument('--output', type=Path)
    a = p.parse_args()
    output = a.output or a.work / ('Android-' + a.compute)
    output.mkdir(parents=True, exist_ok=True)
    remote = '/data/local/tmp/mnn-cosyvoice300m'
    adb = ['adb', '-s', a.serial]

    def run(args, timeout=90):
        return subprocess.run(adb + args, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                              text=True, timeout=timeout)

    run(['shell', 'mkdir', '-p', remote + '/output', remote + '/fixtures'])
    if not a.skip_push_models:
        run(['push', str(a.models / 'fp16'), remote + '/'])
    for binary in ['ModuleBasic.out', 'testModel_expr.out']:
        run(['push', str(a.work / 'Dependencies/AndroidBuild' / binary), remote + '/'])
        run(['shell', 'chmod', '+x', remote + '/' + binary])
    fixtures = output / 'fixtures'
    report = {'serial': a.serial, 'scope': 'fixed-shape component tests; no end-to-end Android RTF',
              'device': run(['shell', 'getprop', 'ro.product.model']).stdout.strip(), 'runs': []}
    for path in sorted((a.work / 'Fixtures/MNN').glob('*-input.mnn')):
        name = path.name.removesuffix('-input.mnn')
        if a.graphs and name not in a.graphs:
            continue
        directory = fixtures / name
        directory.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(path, directory / 'input.mnn')
        reference = path.with_name(name + '-output.mnn')
        shutil.copyfile(reference, directory / 'output.mnn')
        for key, value in MNN.expr.load_as_dict(str(reference)).items():
            np.savetxt(directory / (key + '.txt'), value.read().reshape(-1).astype(np.float32), fmt='%.9g')
        run(['push', str(directory), remote + '/fixtures/'])
        model = 'llm_cached_projected' if name == 'llm_cached' else name
        for backend in a.backends:
            command = ('cd ' + remote + ' && timeout 70 ./ModuleBasic.out fp16/' + model + '.mnn fixtures/' + name
                       + ' 160 ' + str(backend) + ' 5 4 ' + ('1' if a.compute == 'high' else '2')
                       + ' ' + name + '-b' + str(backend) + '.cache')
            try:
                result = run(['shell', command])
                log = result.stdout
                (output / (name + '-b' + str(backend) + '.log')).write_text(log)
                timing = re.search(r'Avg\s*=\s*([\d.]+)', log)
                row = {'graph': name, 'requested_backend': backend, 'exit_code': result.returncode,
                       'precision': a.compute, 'threads': 4,
                       'avg_ms': float(timing[1]) if timing else None,
                       'official_comparison_passed': 'TESTERROR' not in log and 'Skip check' not in log and 'before compare' in log,
                       'comparison_rule': 'official ModuleBasic maximum absolute error / maximum reference value <= 1%; integer token IDs need separate exact checks'}
                if timing:
                    actual_path = output / (name + '-b' + str(backend) + '-output.mnn')
                    run(['pull', remote + '/output/output.mnn', str(actual_path)])
                    actual = MNN.expr.load_as_dict(str(actual_path))
                    expected = MNN.expr.load_as_dict(str(reference))
                    row['outputs'] = {}
                    for key, value in expected.items():
                        target = value.read()
                        measured = actual[key].read().reshape(target.shape).astype(target.dtype)
                        if not np.isfinite(measured).all():
                            row['outputs'][key] = {'finite': False}
                        else:
                            row['outputs'][key] = metrics(measured, target)
            except subprocess.TimeoutExpired:
                row = {'graph': name, 'requested_backend': backend, 'timeout': True}
            report['runs'].append(row)
            (output / 'results.json').write_text(json.dumps(report, indent=2))
            print(json.dumps(row), flush=True)


if __name__ == '__main__':
    main()
