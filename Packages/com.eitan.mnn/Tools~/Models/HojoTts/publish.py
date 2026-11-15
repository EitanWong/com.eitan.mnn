#!/usr/bin/env python3
"""Verify a deployable Hojo export and publish it using the existing ModelScope login."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def verify(model, results):
    run = ET.parse(results).getroot()
    if run.get('result') != 'Passed' or int(run.get('failed', '0')) or int(run.get('skipped', '0')):
        raise ValueError('Require a successful native and catalog validation run')
    passed = {case.get('methodname') for case in run.iter('test-case') if case.get('result') == 'Passed'}
    required = {'BothCatalogsExposeTheSameDedicatedExport', 'InstallerAndCatalogUseTheSamePinnedRelease',
                'CpuPipelineMatchesOfficialGreedySpeech', 'ManagedTokenizerMatchesTheOfficialTokenizer',
                'BilingualVoicesMatchOfficialGreedySpeech', 'SharedAdapterLoadsTheDedicatedModel',
                'CompleteInstallationRejectsMissingStagesAndMalformedMetadata',
                'LongerSampledSpeechCompletesAndTokenBudgetNeverReturnsTruncatedAudio'}
    if not required <= passed:
        raise ValueError('Missing required validation evidence: ' + str(required - passed))
    manifest = json.loads((model / 'files.sha256.json').read_text())
    required_files = {'prefill.mnn', 'decode.mnn', 'fine_local.mnn', 'decoder.mnn',
                      'token_embedding.bin', 'speaker_embeds.bin', 'speaker_vecs.bin',
                      'tokenizer.json', 'tokenizer_config.json', 'mnn_hojo_tts.json',
                      'configuration.json', 'README.md', 'LICENSE', 'source.json', 'optimization.json'}
    if set(manifest) != required_files:
        raise ValueError('Deployment contains an incomplete or unexpected file set')
    for name, row in manifest.items():
        path = model / name
        with path.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        if path.stat().st_size != row['bytes'] or digest != row['sha256']:
            raise ValueError('Deployment hash mismatch: ' + name)
    print('LOCAL_HASHES=PASS; CATALOG_AND_NATIVE_CPU=PASS', flush=True)
    return sorted(required_files | {'files.sha256.json'})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--results', type=Path, required=True)
    parser.add_argument('--repository', default='EitanWang/Hojo-TTS-Light-40M-V2-MNN')
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args()
    files = verify(args.model, args.results)
    if args.verify_only:
        return
    from modelscope.hub.api import HubApi, ModelScopeConfig
    if args.repository.split('/')[0] != ModelScopeConfig.get_user_info()[0]:
        raise ValueError('Repository owner differs from the SDK login')
    api = HubApi()
    if not api.repo_exists(args.repository):
        api.create_model(args.repository, visibility=5, license='Apache License 2.0',
                         chinese_name='Hojo TTS Light V2 MNN 中英语音合成')
    result = api.upload_folder(repo_id=args.repository, folder_path=str(args.model.resolve()),
                              allow_patterns=files, ignore_patterns=['*.meta'],
                              commit_message='Publish verified FP16 Hojo V2 with bounded KV cache', max_workers=4)
    print('PUBLISHED_REPOSITORY=' + args.repository, flush=True)
    print('UPLOAD_RESULT=' + str(result), flush=True)


if __name__ == '__main__':
    main()
