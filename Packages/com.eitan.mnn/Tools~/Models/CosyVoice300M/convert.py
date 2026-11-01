#!/usr/bin/env python3
"""Convert exported CosyVoice-300M graphs with the unmodified MNN converter."""
import argparse
import json
from pathlib import Path
import subprocess


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--converter', type=Path, required=True)
    p.add_argument('--models', type=Path, required=True)
    p.add_argument('--work', type=Path, required=True)
    p.add_argument('--precision', choices=['fp16', 'int8', 'int4'], default='fp16')
    p.add_argument('--project-positions', action='store_true')
    p.add_argument('--force', action='store_true', help='Reconvert after changing exporter/source graphs')
    a = p.parse_args()
    destination = a.models / a.precision
    destination.mkdir(parents=True, exist_ok=True)
    sources = {name: a.work / 'ONNX' / (name + '.onnx') for name in
               ['text_encoder', 'llm_cached', 'flow_encoder', 'flow_regulator', 'hift_f0', 'hift_core']}
    if a.project_positions:
        sources.pop('llm_cached')
        sources['llm_cached_projected'] = a.work / 'ONNX/llm_cached_projected.onnx'
    if a.precision == 'fp16':
        sources.update({name: a.models / 'source' / file for name, file in
                        [('flow_estimator', 'flow_estimator.onnx'), ('campplus', 'campplus.onnx'),
                         ('speech_tokenizer', 'speech_tokenizer_v1.onnx')]})
    else:
        sources = {k: v for k, v in sources.items() if k in ['text_encoder', 'llm_cached', 'llm_cached_projected']}
    for name, source in sources.items():
        output = destination / (name + '.mnn')
        log = a.work / ('convert-' + a.precision + '-' + name + '.log')
        if not a.force and output.exists() and log.exists() and 'Converted Success' in log.read_text():
            continue
        cmd = [str(a.converter.resolve()), '-f', 'ONNX', '--modelFile', str(source.resolve()),
               '--MNNModel', str(output.resolve()), '--bizCode', 'CosyVoice-300M-50Hz',
               '--targetVersion', '3.6.1', '--optimizePrefer', '2', '--saveExternalData']
        if a.precision == 'fp16':
            cmd += ['--fp16']
        else:
            cmd += ['--weightQuantBits', a.precision[3:], '--weightQuantBlock', '64', '--weightQuantAsymmetric']
        (a.work / ('command-' + a.precision + '-' + name + '.json')).write_text(json.dumps(cmd, indent=2))
        print('convert', a.precision, name, flush=True)
        with log.open('w') as f:
            subprocess.run(cmd, stdout=f, stderr=subprocess.STDOUT, check=True)
        content = log.read_text()
        if 'Converted Success' not in content:
            raise RuntimeError('Conversion failed: ' + str(log))


if __name__ == '__main__':
    main()
