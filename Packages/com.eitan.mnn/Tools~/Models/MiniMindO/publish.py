#!/usr/bin/env python3
"""Publish only a verified MiniMind-o export; use ModelScope's existing SDK login."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def verify(model, results):
    run = ET.parse(results).getroot()
    assert run.get('result') == 'Passed' and int(run.get('failed', '0')) == 0
    assert int(run.get('skipped', '0')) == 0
    required = {'BothCatalogsFindTheInstalledOmniExport', 'ConversationCatalogHasAMiniMindExportProtocolAndResolvesItsInstallation',
                'DiscoveredRuntimeAdapterActuallyGeneratesSpeech'}
    passed = {case.get('name').split('(')[0] for case in run.iter('test-case') if case.get('result') == 'Passed'}
    assert required <= passed, 'Missing Editor/runtime discovery and native loading evidence'
    manifest = json.loads((model / 'files.sha256.json').read_text())
    graphs = ['prefill', 'decode', 'multimodal_prefill', 'audio_encoder', 'vision_encoder', 'mimi_decoder']
    expected = {name + suffix for name in graphs for suffix in ['.mnn', '.mnn.weight']} | {'tokenizer.json', 'mnn_minimind_o.json'}
    assert expected == set(manifest), 'Incomplete export file manifest'
    for name, row in manifest.items():
        path = model / name
        assert path.stat().st_size == row['bytes'], name
        with path.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        assert digest == row['sha256'], name
    print('LOCAL_HASHES=PASS; SHARED_DISCOVERY_AND_NATIVE_RUNTIME=PASS', flush=True)
    return sorted(expected | {'files.sha256.json', 'README.md', 'configuration.json'})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--results', type=Path, required=True)
    parser.add_argument('--repository', default='EitanWang/MiniMindO-113M-MNN')
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args()
    files = verify(args.model, args.results)
    if args.verify_only:
        return
    from modelscope.hub.api import HubApi, ModelScopeConfig
    api = HubApi()
    assert args.repository.split('/')[0] == ModelScopeConfig.get_user_info()[0], 'Repository owner differs from SDK login'
    if not api.repo_exists(args.repository):
        api.create_model(args.repository, visibility=5, license='Apache License 2.0',
                         chinese_name='MiniMind-o 113M MNN 多模态语音模型')
    result = api.upload_folder(repo_id=args.repository, folder_path=str(args.model.resolve()),
                              allow_patterns=files, ignore_patterns=['*.meta'],
                              commit_message='Publish verified mixed W8 FP16 MiniMind-o export', max_workers=4)
    print('PUBLISHED_REPOSITORY=' + args.repository, flush=True)
    print('UPLOAD_RESULT=' + str(result), flush=True)


if __name__ == '__main__':
    main()
