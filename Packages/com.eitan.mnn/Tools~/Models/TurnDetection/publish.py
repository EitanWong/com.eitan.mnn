#!/usr/bin/env python3
"""Publish a hash-verified turn export after its native acceptance tests pass."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


def verify(directory, results, tests):
    root = ET.parse(results).getroot()
    passed = {c.get('methodname') for c in root.iter('test-case') if c.get('result') == 'Passed'}
    if root.get('result') != 'Passed' or int(root.get('skipped', '0')) or not set(tests) <= passed:
        raise ValueError('Require successful acceptance results containing: ' + ', '.join(tests))
    rows = json.loads((directory / 'files.sha256.json').read_text())
    required = {'README.md', 'LICENSE', 'source.json', 'optimization.json', 'configuration.json'}
    if not required <= rows.keys() or not any(n.endswith('.mnn') for n in rows):
        raise ValueError('Missing deployable graph, provenance, license or measurements')
    for name, row in rows.items():
        path = directory / name
        if path.parent != directory or path.is_symlink():
            raise ValueError('Only sibling deployment files are allowed')
        with path.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'sha256').hexdigest()
        if path.stat().st_size != row['bytes'] or digest != row['sha256']:
            raise ValueError('Hash mismatch: ' + name)
    return sorted(set(rows) | {'files.sha256.json'})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model', type=Path, required=True)
    parser.add_argument('--results', type=Path, required=True)
    parser.add_argument('--tests', nargs='+', required=True)
    parser.add_argument('--repository', required=True)
    parser.add_argument('--license', required=True)
    parser.add_argument('--verify-only', action='store_true')
    args = parser.parse_args()
    directory = args.model.resolve()
    files = verify(directory, args.results, args.tests)
    print('DEPLOYMENT_HASHES=PASS; ACCEPTANCE=PASS', flush=True)
    if args.verify_only:
        return
    from modelscope.hub.api import HubApi, ModelScopeConfig
    if args.repository.split('/')[0] != ModelScopeConfig.get_user_info()[0]:
        raise ValueError('Repository owner differs from SDK login')
    api = HubApi()
    if not api.repo_exists(args.repository):
        api.create_model(args.repository, visibility=5, license=args.license)
    result = api.upload_folder(repo_id=args.repository, folder_path=str(directory),
                               allow_patterns=files, ignore_patterns=['*.meta'], max_workers=3,
                               commit_message='Publish source-verified MNN turn deployment and quantization evidence')
    print('PUBLISHED_REPOSITORY=' + args.repository, flush=True)
    print('UPLOAD_RESULT=' + str(result), flush=True)


if __name__ == '__main__':
    main()
