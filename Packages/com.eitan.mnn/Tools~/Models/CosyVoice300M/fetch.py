#!/usr/bin/env python3
"""Fetch pinned official CosyVoice-300M source assets, verifying SHA256."""
import argparse
import hashlib
from pathlib import Path
import urllib.request

REVISION = '46b5c06a21fe6dfc3deedf7fc8fea4abe9c71e9b'
SOURCES = {
    'llm.pt': (REVISION, '59f7eb172d2f33f6c8c02709d829c83960f97876fa9886c7ea232597b51af976'),
    'flow.pt': (REVISION, 'fd80b089444a95e52956c57cdf177d7f6017a5af13b8a697717628a1d2be6b55'),
    'hift.pt': (REVISION, '91e679b6ca1eff71187ffb4f3ab0444935594cdcc20a9bd12afad111ef8d6012'),
    'campplus.onnx': (REVISION, 'a6ac6a63997761ae2997373e2ee1c47040854b4b759ea41ec48e4e42df0f4d73'),
    'speech_tokenizer_v1.onnx': (REVISION, '23b5a723ed9143aebfd9ffda14ac4c21231f31c35ef837b6a13bb9e5488abb1e'),
    'cosyvoice.yaml': ('121c883104db335799d20e8c1fe7f4963c5a8058', '5475b9b2f788f3ddd3798898041197e076d20cb2ea783ef9638e6eed36ba16db'),
}


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def download(url, path, sha):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and digest(path) == sha:
        print('verified', path, flush=True)
        return
    if path.exists():
        raise ValueError('Existing file has a different hash; inspect it before replacing: ' + str(path))
    partial = path.with_suffix(path.suffix + '.part')
    with urllib.request.urlopen(url, timeout=120) as response, partial.open('wb') as output:
        while block := response.read(4 * 1024 * 1024):
            output.write(block)
    if digest(partial) != sha:
        raise ValueError('Downloaded SHA256 mismatch: ' + str(partial))
    partial.replace(path)
    print('downloaded', path, flush=True)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--models', type=Path, required=True)
    a = p.parse_args()
    for name, (revision, sha) in SOURCES.items():
        url = 'https://modelscope.cn/api/v1/models/iic/CosyVoice-300M/repo?Revision=' + revision + '&FilePath=' + name
        download(url, a.models / 'source' / name, sha)
    download('https://huggingface.co/FunAudioLLM/CosyVoice-300M/resolve/24c40509c3c5ea6fe06b5f8790ff99e3714a6bee/flow.decoder.estimator.fp32.onnx',
             a.models / 'source/flow_estimator.onnx', '8c02bc651f599d66e5786b9dc66d7841153a2f1ae682306a5bac84d067f7c947')
    download('https://raw.githubusercontent.com/FunAudioLLM/CosyVoice/074ca6dc9e80a2f424f1f74b48bdd7d3fea531cc/asset/zero_shot_prompt.wav',
             a.models / 'common/prompt.wav', 'c7b31d6dbe7cc6a716dded00550db5b50940bf209e424e4ad207b12e657c8ff6')


if __name__ == '__main__':
    main()
