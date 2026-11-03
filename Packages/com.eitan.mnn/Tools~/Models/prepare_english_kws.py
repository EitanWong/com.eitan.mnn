#!/usr/bin/env python3
"""Prepare the pinned official English KWS release with an existing official MNN 3.6.1 converter.

Runtime needs neither ONNX Runtime nor SentencePiece/Python. --vocabulary-output is a
maintainer-only rebuild option requiring the Python sentencepiece package.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tarfile
import urllib.request

NAME = 'sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01'
URL = 'https://github.com/k2-fsa/sherpa-onnx/releases/download/kws-models/' + NAME + '.tar.bz2'
ARCHIVE_HASH = 'f170013b4716e41b62b9bfd809687c207cef798ef9bc6534d524e17af9b6561a'
STEMS = [stage + '-epoch-12-avg-2-chunk-16-left-64' for stage in ('encoder', 'decoder', 'joiner')]
ONNX_HASHES = [
    '063fbc1aeae8a9b574607a331a00e60371846ef9eaa3c1d9ea48176665dfc693',
    'f61ebd3eed3773a44d088d53dfae92dbb6aec4839f4dcaee2d402414741663a3',
    '0d7a37e749d8055223029318d6ffae82db1dae2d315d0892a68ba5dad17c1d2d']
MNN_HASHES = [
    '2cff4ced4134e90a287ec6ae2fc6ff959e8b218a4d6bd5f9b0a43473c06a1ac0',
    'f8232799a4b89121a34a76f1212fd291d558e536ab8cb94690786be337f2c13c',
    'ea4a4984c292609835864a9a7197856dd618acfd2968381cd26ad2e4d1bd4dcd']
TEXT_HASHES = {
    'tokens.txt': 'fd2ded4050a55d2b1578870ba8697d02371980217806b7558bd0a5cc60f3ba53',
    'bpe.model': 'c8a2a0129c4ab8e463164c142f82d25649661b122c8cd0b7aab5c9e80b90ad24'}


def verify(path, expected):
    if hashlib.sha256(path.read_bytes()).hexdigest() != expected:
        raise ValueError('Unexpected model data: ' + str(path))


def extract(directory):
    archive = directory / (NAME + '.tar.bz2')
    urllib.request.urlretrieve(URL, archive)
    verify(archive, ARCHIVE_HASH)
    allowed = {stem + '.onnx' for stem in STEMS} | set(TEXT_HASHES) | {
        'README.md', 'keywords.txt', 'keywords_raw.txt', 'test_wavs/0.wav', 'test_wavs/1.wav',
        'test_wavs/test_keywords.txt', 'test_wavs/trans.txt'}
    with tarfile.open(archive) as bundle:
        for relative in sorted(allowed):
            item = bundle.getmember(NAME + '/' + relative)
            if not item.isfile():
                raise ValueError('Expected a regular model file: ' + relative)
            target = directory / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            with bundle.extractfile(item) as source, target.open('wb') as destination:
                shutil.copyfileobj(source, destination)
    archive.unlink()


def vocabulary(model, target):
    import sentencepiece as sp
    from sentencepiece import sentencepiece_model_pb2
    processor = sp.SentencePieceProcessor(model_file=str(model))
    protocol = sentencepiece_model_pb2.ModelProto()
    protocol.ParseFromString(model.read_bytes())
    if protocol.trainer_spec.model_type != 1 or processor.vocab_size() != 500:
        raise ValueError('Expected the pinned 500-piece SentencePiece unigram model.')
    target.write_text('// Generated from the pinned official GigaSpeech SentencePiece unigram model (Apache-2.0).\n'
        'namespace MNN.Unity\n{\n    public static partial class MNNKeywordText\n    {\n'
        '        private static readonly string[] EnglishTokens =\n        {\n'
        + ''.join('            ' + json.dumps(processor.id_to_piece(i), ensure_ascii=False) + ',\n' for i in range(500))
        + '        };\n        private static readonly float[] EnglishPieceScores =\n        {\n'
        + ''.join('            ' + str(processor.get_score(i)) + 'f,\n' for i in range(500))
        + '        };\n    }\n}\n')


def main():
    root = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--models-root', type=Path, default=root / 'Assets/StreamingAssets/MNN/Models')
    parser.add_argument('--converter', type=Path, help='Existing official MNN 3.6.1 MNNConvert executable')
    parser.add_argument('--verify-only', action='store_true', help='Check existing prepared assets without downloading or converting')
    parser.add_argument('--vocabulary-output', type=Path, help='Explicit generated C# vocabulary destination for maintainers')
    args = parser.parse_args()
    directory = args.models_root.resolve() / 'English-KWS-GigaSpeech'
    reports = root / 'TestArtifacts~/EnglishKwsPreparation'
    reports.mkdir(parents=True, exist_ok=True)
    if not args.verify_only:
        directory.mkdir(parents=True, exist_ok=True)
        if not all((directory / (stem + '.onnx')).exists() for stem in STEMS):
            extract(directory)
    if not args.verify_only:
        notice = root / 'Packages/com.eitan.mnn/Documentation~/EnglishKeywordModelNotice.md'
        license_text = notice.read_text().split('```text\n', 1)[1].split('\n```', 1)[0]
        (directory / 'LICENSE').write_text(license_text)
    for name, expected in TEXT_HASHES.items():
        verify(directory / name, expected)
    for stem, onnx_hash, mnn_hash in zip(STEMS, ONNX_HASHES, MNN_HASHES):
        target = directory / (stem + '.mnn')
        if not args.verify_only:
            source = directory / (stem + '.onnx')
            verify(source, onnx_hash)
            if not target.exists():
                if args.converter is None:
                    parser.error('--converter is required when MNN graphs have not been prepared.')
                result = subprocess.run([str(args.converter.resolve()), '-f', 'ONNX', '--modelFile', str(source),
                    '--MNNModel', str(target), '--bizCode', 'MNN', '--keepInputFormat'], capture_output=True, text=True)
                (reports / (stem + '.log')).write_text(result.stdout + result.stderr)
                result.check_returncode()
        verify(target, mnn_hash)
    if not args.verify_only:
        (directory / 'mnn_kws.json').write_text(json.dumps({'repository': 'k2-fsa/' + NAME}, indent=2) + '\n')
    if json.loads((directory / 'mnn_kws.json').read_text()).get('repository') != 'k2-fsa/' + NAME:
        raise ValueError('The English KWS profile does not match the verified release.')
    if args.vocabulary_output:
        vocabulary(directory / 'bpe.model', args.vocabulary_output)
    print('Verified official English KWS profile: ' + str(directory))


if __name__ == '__main__':
    main()
