#!/usr/bin/env python3
"""Generate isolated English wake regression WAVs using existing macOS speech voices.

No models/dependencies are downloaded. Set MNN_TEST_WAKE_FIX_AUDIO to the printed
output directory and MNN_ENGLISH_KWS_MODELS to the prepared GigaSpeech directory.
"""
import argparse
import json
from pathlib import Path
import subprocess
import sys

PHRASES = {
    'siri': 'Siri.',
    'hi-siri': 'Hi Siri.',
    'hey-siri': 'Hey Siri.',
    'command': 'Hey Siri, what time is it?',
    'negative': 'The weather is nice today.',
}
VOICES = ('Samantha', 'Daniel')


def main():
    root = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=root / 'TestArtifacts~/EnglishWakeFix/audio')
    args = parser.parse_args()
    if sys.platform != 'darwin':
        parser.error('This fixture generator requires macOS say and afconvert with existing English voices.')
    output = args.output.resolve()
    if root / 'TestArtifacts~' not in output.parents:
        parser.error('Keep generated audio under this repository TestArtifacts~ directory.')
    output.mkdir(parents=True, exist_ok=True)
    for voice in VOICES:
        for name, phrase in PHRASES.items():
            aiff = output / (voice + '-' + name + '.aiff')
            wave = aiff.with_suffix('.wav')
            subprocess.run(['say', '-v', voice, '-o', str(aiff), phrase], check=True)
            subprocess.run(['afconvert', '-f', 'WAVE', '-d', 'LEI16@16000', str(aiff), str(wave)], check=True)
    (output / 'fixtures.json').write_text(json.dumps({
        'voices': VOICES, 'phrases': PHRASES, 'sample_rate': 16000,
        'source': 'macOS system speech synthesis; synthetic regression examples, not human accuracy measurements',
    }, indent=2) + '\n')
    print(output)


if __name__ == '__main__':
    main()
