#!/usr/bin/env python3
"""Create INT8 and FFN-only INT8 candidates from the existing compact ONNX.

Uses the unmodified official converter. No downloads or source-model changes.
Candidate models are explicit experiments until complete Unity/quality validation.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['models', 'work', 'onnx', 'converter']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    destination = args.models.resolve() / 'experimental-int8'
    destination.mkdir(exist_ok=True)
    args.work.mkdir(parents=True, exist_ok=True)
    guide = args.work.resolve() / 'int8-guide.json'
    common = [str(args.converter.resolve()), '-f', 'ONNX', '--modelFile', str(args.onnx.resolve()),
              '--targetVersion', '3.6.1', '--optimizePrefer', '2',
              '--weightQuantBits', '8', '--weightQuantBlock', '64', '--weightQuantAsymmetric=1']
    report = []
    for profile in ['int8', 'ffn_int8']:
        graph = destination / ('llm_decode_' + profile + '.mnn')
        if graph.exists() or Path(str(graph) + '.weight').exists():
            parser.error('Refusing to overwrite candidate: ' + str(graph))
        strategy = guide
        if profile == 'ffn_int8':
            data = json.loads(guide.read_text())
            data['forGuide'] = True
            layers = next(item['quantParams']['layer'] for item in data['algo'] if 'quantParams' in item)
            for layer in layers:
                for weight in layer['weight']:
                    weight.update(bits=8 if 'feed_forward' in layer['opName'] else 0, blockSize=64, asymmetric=True)
            strategy = args.work.resolve() / 'ffn-int8-strategy.json'
            strategy.write_text(json.dumps(data, indent=2) + '\n')
        intermediate = args.work.resolve() / ('llm_decode_' + profile + '_before_half.mnn')
        command = common + ['--MNNModel', str(intermediate), '--compressionParamsFile', str(strategy)]
        (args.work / (profile + '-command.json')).write_text(json.dumps(command, indent=2) + '\n')
        log = args.work / (profile + '-conversion.log')
        with log.open('w') as stream:
            subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT, check=True)
        if 'Converted Success' not in log.read_text():
            raise RuntimeError('Converter did not report success: ' + profile)
        # --fp16 runs before quantization in this converter and would prevent it.
        # A separate official MNN->MNN conversion retains existing INT8 encodings,
        # while compressing only the remaining floating constants/projections.
        half_command = [str(args.converter.resolve()), '-f', 'MNN', '--modelFile', str(intermediate),
                        '--MNNModel', str(graph), '--saveExternalData', '--fp16']
        (args.work / (profile + '-half-command.json')).write_text(json.dumps(half_command, indent=2) + '\n')
        half_log = args.work / (profile + '-half-conversion.log')
        with half_log.open('w') as stream:
            subprocess.run(half_command, stdout=stream, stderr=subprocess.STDOUT, check=True)
        if 'Converted Success' not in half_log.read_text():
            raise RuntimeError('Floating compression failed: ' + profile)
        if Path(str(graph) + '.weight').stat().st_size < 100_000_000:
            raise RuntimeError('Missing external decoder weights: ' + profile)
        files = []
        for path in [graph, Path(str(graph) + '.weight')]:
            with path.open('rb') as stream:
                sha = hashlib.file_digest(stream, 'sha256').hexdigest()
            files.append(dict(path=str(path.relative_to(args.models.resolve())), bytes=path.stat().st_size, sha256=sha))
        report.append(dict(profile=profile, files=files, status='experimental; no full quality acceptance'))
    (destination / 'candidates.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
