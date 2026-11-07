#!/usr/bin/env python3
"""Generate wake regression WAVs using existing macOS speech voices.

No models/dependencies are downloaded. Set MNN_TEST_WAKE_FIX_AUDIO to the printed
output directory and MNN_ENGLISH_KWS_MODELS to the prepared GigaSpeech directory.
--sensitivity adds bilingual voices and negatives for MNN_TEST_WAKE_SENSITIVITY_AUDIO.
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
SENSITIVITY_VOICES = {
    'Samantha': ('Samantha', 'en'), 'Daniel': ('Daniel', 'en'),
    'EddyEnglish': ('Eddy (English (US))', 'en'),
    'FloEnglish': ('Flo (English (US))', 'en'),
    'Tingting': ('Tingting', 'zh'), 'Eddy': ('Eddy (Chinese (China mainland))', 'zh'),
}
CHINESE_PHRASES = {
    'xiaoai': '小爱同学。', 'nihao': '你好助手。', 'siri': '嘿，西瑞。',
    'command': '小爱同学，今天天气怎么样？', 'negative': '今天的天气真不错。',
}
NEAR_MISSES = {
    'en': {
        'city': 'The city is very busy.', 'series': 'Start a new series.',
        'seriously': 'Seriously, are you ready?', 'see': 'I see what you mean.',
        'greeting': 'Hi, how are you?', 'acknowledge': 'Right, right, okay, I understand.',
    },
    'zh': {
        'tongxue': '同学们，开始上课了。', 'zhushou': '这个助手很好用。',
        'xiao': '小李同学，今天怎么样？', 'nihao-only': '你好，今天天气怎么样？',
        'greeting': '嘿，大家好。', 'acknowledge': '好的，好的，我知道了。',
    },
}


def main():
    root = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=root / 'TestArtifacts~/EnglishWakeFix/audio')
    parser.add_argument('--sensitivity', action='store_true', help='Generate bilingual multi-voice positives and near-miss negatives.')
    args = parser.parse_args()
    if sys.platform != 'darwin':
        parser.error('This fixture generator requires macOS say and afconvert with existing English voices.')
    output = args.output.resolve()
    if root / 'TestArtifacts~' not in output.parents:
        parser.error('Keep generated audio under this repository TestArtifacts~ directory.')
    output.mkdir(parents=True, exist_ok=True)
    voices = SENSITIVITY_VOICES if args.sensitivity else {v: (v, 'en') for v in VOICES}
    manifest = []
    for voice, (system_voice, language) in voices.items():
        phrases = dict(PHRASES if language == 'en' else CHINESE_PHRASES)
        if args.sensitivity:
            phrases.update({'negative-' + k: v for k, v in NEAR_MISSES[language].items()})
        for name, phrase in phrases.items():
            aiff = output / (voice + '-' + name + '.aiff')
            wave = aiff.with_suffix('.wav')
            subprocess.run(['say', '-v', system_voice, '-o', str(aiff), phrase], check=True)
            subprocess.run(['afconvert', '-f', 'WAVE', '-d', 'LEI16@16000', str(aiff), str(wave)], check=True)
            manifest.append({'voice': voice, 'system_voice': system_voice, 'language': language, 'phrase': name, 'text': phrase})
    (output / 'fixtures.json').write_text(json.dumps({
        'fixtures': manifest, 'sample_rate': 16000,
        'source': 'macOS system speech synthesis; synthetic regression examples, not human accuracy measurements',
    }, indent=2) + '\n')
    print(output)


if __name__ == '__main__':
    main()
