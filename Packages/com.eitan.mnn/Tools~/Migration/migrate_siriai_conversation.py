#!/usr/bin/env python3
"""Apply one checked, idempotent shared-capability migration to a local SiriAI import."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[4])
    parser.add_argument('--stage', choices=['conversation', 'reuse', 'full', 'acceptance', 'recording', 'glass', 'reflection', 'optical', 'caustics', 'soft-ends', 'refraction', 'pocket', 'product', 'tasks', 'comfort'], default='conversation',
                        help='Apply conversation, reuse, full capability, acceptance-contract, recording-gate, glass-continuity, source-reflection, optical-detail, fold-caustics, soft-endpoint, glass-refraction, Pocket-model, product-interaction, task-settings, or desktop-comfort migration')
    args = parser.parse_args()
    root = args.root.resolve()
    patch = Path(__file__).with_name({'conversation': 'SiriAISharedConversation.patch',
                                    'reuse': 'SiriAIReusableCapabilities.patch',
                                    'full': 'SiriAIFullCapabilities.patch',
                                    'acceptance': 'SiriAIAcceptanceContracts.patch',
                                    'recording': 'SiriAIRecordingGate.patch',
                                    'glass': 'SiriAIGlassContinuity.patch',
                                    'reflection': 'SiriAISourceReflections.patch',
                                    'optical': 'SiriAIOpticalDetail.patch',
                                    'caustics': 'SiriAIFoldCaustics.patch',
                                    'soft-ends': 'SiriAIRibbonSoftEdges.patch',
                                    'refraction': 'SiriAIGlassRefraction.patch',
                                    'pocket': 'SiriAIPocketModels.patch',
                                    'product': 'SiriAIProductPolish.patch',
                                    'tasks': 'SiriAITaskSettings.patch',
                                    'comfort': 'SiriAIDesktopComfort.patch'}[args.stage]).resolve()
    command = ['git', '-C', str(root), 'apply', '--whitespace=nowarn']
    if args.stage in ('full', 'acceptance', 'recording', 'glass', 'reflection', 'optical', 'caustics', 'soft-ends', 'refraction', 'pocket', 'product', 'tasks', 'comfort'):
        # git apply checks surrounding lines, not every byte in changed files.
        # Reject a partial or independently edited import before any mutation.
        records = json.loads(patch.with_suffix('.files.json').read_text())['files']
        actual = {}
        for record in records:
            target = root / record['path']
            actual[record['path']] = hashlib.sha256(target.read_bytes()).hexdigest() if target.exists() else None
        if all(actual[item['path']] == item['after'] for item in records):
            print('SiriAI already uses the checked ' + args.stage + ' migration.')
            return
        mismatched = [item['path'] for item in records if actual[item['path']] != item['before']]
        if mismatched:
            raise SystemExit('The local SiriAI version differs from the checked ' + args.stage + ' migration baseline. No files changed.\n' +
                             '\n'.join(mismatched))
    if subprocess.run(command + ['--reverse', '--check', str(patch)], capture_output=True).returncode == 0:
        print('SiriAI already uses the shared ' + ('conversation engine.' if args.stage == 'conversation' else 'reusable capabilities.'))
        return
    checked = subprocess.run(command + ['--check', str(patch)], capture_output=True, text=True)
    if checked.returncode:
        raise SystemExit('The local SiriAI version differs from the migration baseline. No files changed.\n' + checked.stderr)
    subprocess.run(command + [str(patch)], check=True)
    print('SiriAI migration applied (' + args.stage + '). Existing asset GUIDs are preserved.')


if __name__ == '__main__':
    main()
