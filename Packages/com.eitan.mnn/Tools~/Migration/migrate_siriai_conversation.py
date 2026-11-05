#!/usr/bin/env python3
"""Apply one checked, idempotent shared-capability migration to a local SiriAI import."""
import argparse
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[4])
    parser.add_argument('--stage', choices=['conversation', 'reuse'], default='conversation',
                        help='Apply the original conversation extraction or the subsequent reusable-capability extraction')
    args = parser.parse_args()
    root = args.root.resolve()
    patch = Path(__file__).with_name('SiriAISharedConversation.patch' if args.stage == 'conversation'
                                    else 'SiriAIReusableCapabilities.patch').resolve()
    command = ['git', '-C', str(root), 'apply', '--whitespace=nowarn']
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
