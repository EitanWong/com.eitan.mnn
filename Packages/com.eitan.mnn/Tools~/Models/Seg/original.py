"""Pinned original SEG network; only the author's CUDA index device is ported."""
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import hashlib
import json
import sys
import torch
import yaml

REVISION = '9d5b3e567d04abc7190d49325e79101e52a28930'
SOURCE_REVISION = '6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61'
CHECKPOINT_SHA = '78b2d7d6bc6b97496b85c6db9247055538c9ed3fe33bf0a9818af0f340c8aae3'


def namespace(value):
    return SimpleNamespace(**{key: namespace(item) if isinstance(item, dict) else item for key, item in value.items()})


def load(source, checkpoint, config_path):
    if hashlib.sha256(checkpoint.read_bytes()).hexdigest() != CHECKPOINT_SHA:
        raise ValueError('SEG original checkpoint digest mismatch')
    hashes = json.loads(Path(__file__).with_name('source-hashes.json').read_text())
    for name, expected in hashes.items():
        if hashlib.sha256((source / name).read_bytes()).hexdigest() != expected:
            raise ValueError('SEG original source digest mismatch: ' + name)
    config = yaml.safe_load(config_path.read_text())
    if (config['audio_sr'], config['ref_sr'], config['max_length'], config['causal']) != (16000, 15, 10, 0):
        raise ValueError('Unsupported SEG source contract')
    args = namespace(config)
    args.device = 'cpu'
    sys.path.insert(0, str(source))
    from networks import network_wrapper
    model = network_wrapper(args).eval()
    state = torch.load(checkpoint, map_location='cpu', weights_only=False)['model']
    model.load_state_dict({key.removeprefix('module.'): value for key, value in state.items()}, strict=True)
    return model, config


def source_forward(model, audio, gestures):
    # The sole port keeps the original integer index_add operations and weights.
    with patch.object(torch.Tensor, 'cuda', lambda self, *args, **kwargs: self):
        return model(audio, gestures)
