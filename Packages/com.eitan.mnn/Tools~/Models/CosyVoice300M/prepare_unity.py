#!/usr/bin/env python3
"""Prepare the existing CosyVoice-300M MNN export for the managed Unity runner.

Uses local assets only. Does not download, re-quantize, change graph weights,
select experimental speculative models, or require a Python process in Unity.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import sys


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def save(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n')


def main():
    root = Path(__file__).resolve().parents[5]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--models', type=Path, default=root / 'Assets/StreamingAssets/MNN/Models/CosyVoice-300M-MNN')
    parser.add_argument('--dependencies', type=Path, default=root / 'TestArtifacts~/CosyVoice300M/Dependencies')
    parser.add_argument('--artifacts', type=Path, default=root / 'TestArtifacts~/CosyVoice300M/PackageIntegration')
    args = parser.parse_args()
    args.artifacts.mkdir(parents=True, exist_ok=True)
    manifest_path = args.models / 'manifest.json'
    manifest = json.loads(manifest_path.read_text())
    assert manifest['model'] == 'iic/CosyVoice-300M' and manifest['generation'] == 1
    before = args.artifacts / 'manifest-before.json'
    if not before.exists():
        save(before, manifest)
    source = args.dependencies / 'whisper/assets/multilingual.tiktoken'
    destination = args.models / 'common/whisper.tiktoken'
    if destination.exists():
        assert digest(source) == digest(destination)
    else:
        shutil.copyfile(source, destination)
    spec = importlib.util.spec_from_file_location('unity_cosy_whisper', args.dependencies / 'whisper/tokenizer.py')
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    tokenizer = module.get_tokenizer(multilingual=True, num_languages=100, language='en', task='transcribe')
    specials = tokenizer.special_tokens
    save(args.models / 'common/whisper-special-tokens.json', specials)
    marker = dict(schema_version=1, model='iic/CosyVoice-300M', sample_rate=22050,
        runtime='MNN.Unity.MNNCosyVoice300M', default_flow_steps=10, default_full_prompt=True,
        default_quantized_decoder=False, speculative_decoder_enabled=False,
        managed_random='System.Random and Box-Muller; not NumPy PCG64 seed equivalence')
    optimized = manifest.get('distribution') == 'unity-optimized'
    if optimized:
        for suffix in ['', '.weight']:
            assert (args.models / ('fp16/llm_decode_compact.mnn' + suffix)).is_file()
        marker['decoder_graph'] = 'fp16/llm_decode_compact.mnn'
    save(args.models / 'mnn_cosyvoice300m.json', marker)
    paths = ['common/whisper.tiktoken', 'common/whisper-special-tokens.json', 'mnn_cosyvoice300m.json']
    files = []
    for name in paths:
        path = args.models / name
        entry = dict(path=name, bytes=path.stat().st_size, sha256=digest(path), status='managed Unity frontend asset')
        existing = next((row for row in manifest['files'] if row['path'] == name), None)
        if existing is None:
            manifest['files'].append(entry)
        else:
            assert all(existing[key] == entry[key] for key in ['path', 'bytes', 'sha256'])
        files.append(entry)
    if not optimized:
        manifest['unity_integration'] = 'MNNCosyVoice300M managed runner and Studio; platform inference evidence in PackageIntegration report; no portable MNN C# ABI claim'
        manifest['unity_package'] = dict(config='mnn_cosyvoice300m.json', api='MNN.Unity.MNNCosyVoice300M',
            pipeline='first generation 22050 Hz; separate from MNNCosyVoice (CosyVoice3 24000 Hz)',
            default='full prompt, FP16 graphs, High compute, 10 Flow steps',
            optional_exact_decoder='fp16/llm_decode_compact.mnn and .weight; original prefill; High compute; legacy experimental-decode path also supported',
            experimental='explicit quantized CPU decoder only; no speculative decoder default',
            cpp_abi='existing package platform/compiler/binary contract only', native_code_added=False,
            python_dependency_at_runtime=False)
    save(manifest_path, manifest)
    cases = ['你好，欢迎使用语音合成。', 'The nearest station is across the street, next to the post office.',
             '图书馆下午五点关门，请提前归还借阅的杂志。', "I'm ready! 你好，world 123.\nNext line.", '<|endoftext|>']
    save(args.artifacts / 'tokenizer-reference.json', [dict(text=text, tokens=tokenizer.encode(text, allowed_special='all')) for text in cases])
    save(args.artifacts / 'preparation.json', dict(files=files, tokenizer_source=str(source), tokenizer_source_sha256=digest(source),
        graph_weights_changed=False, downloads=0, manifest_files=len(manifest['files'])))
    print('Prepared Unity frontend:', len(files), 'files; original graph weights unchanged.')


if __name__ == '__main__':
    main()
