#!/usr/bin/env python3
"""Convert the pinned official Smart Turn v3.2 floating ONNX with MNNConvert.

This tool performs no download. Keep its work directory under TestArtifacts~/.
Source ONNX and conversion intermediates must not enter a deployment directory.
"""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

REVISION = 'f766f81d3cfdf7737ac64aad813d91bbfd56bf93'
SOURCE_SHA256 = 'ab8dc64b88713f90b571c15b714bd1330e6c883cad8763dacf65c9376dc539be'


def prepare(source: Path, converter: Path, work: Path) -> dict:
    if hashlib.sha256(source.read_bytes()).hexdigest() != SOURCE_SHA256:
        raise ValueError('expected official pipecat-ai/smart-turn-v3 v3.2 floating ONNX')
    work.mkdir(parents=True, exist_ok=True)
    target = work / 'smart_turn_v32_fp32.mnn'
    command = [str(converter), '-f', 'ONNX', '--modelFile', str(source), '--MNNModel', str(target),
               '--bizCode', 'SmartTurn', '--optimizeLevel', '1', '--optimizePrefer', '2',
               '--transformerFuseC4', '0', '--transformerFuseQkvProj', '0',
               '--transformerFuseGateUpProj', '0', '--transformerFuseLnProj', '0']
    with (work / 'conversion.log').open('w') as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    report = {'source_revision': REVISION, 'source_sha256': SOURCE_SHA256,
              'graph_bytes': target.stat().st_size,
              'graph_sha256': hashlib.sha256(target.read_bytes()).hexdigest()}
    (work / 'preparation.json').write_text(json.dumps(report, indent=2) + '\n')
    return report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-onnx', required=True, type=Path)
    parser.add_argument('--converter', required=True, type=Path)
    parser.add_argument('--work-directory', required=True, type=Path)
    args = parser.parse_args()
    print(json.dumps(prepare(args.source_onnx.resolve(), args.converter.resolve(), args.work_directory.resolve()), indent=2))


if __name__ == '__main__':
    main()
