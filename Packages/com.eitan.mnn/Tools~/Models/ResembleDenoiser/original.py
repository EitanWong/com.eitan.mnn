"""Load the pinned original inference methods, excluding the unused training mel frontend."""
from pathlib import Path
import ast
import hashlib
import json
import logging
import types
import torch
import torch.nn.functional as F

REVISION = '4e3510ce4a8391159f665903544c5150bee7b2cb'
SOURCE_REVISION = '8e978149bfe8abab3eb77d965d579a111afdb0ff'
CHECKPOINT_SHA = 'f9d035f318de3e6d919bc70cf7ad7d32b4fe92ec5cbe0b30029a27f5db07d9d6'


def load(source, checkpoint):
    hashes = json.loads(Path(__file__).with_name('source-hashes.json').read_text())
    for name, expected in hashes.items():
        if hashlib.sha256((source / name).read_bytes()).hexdigest() != expected:
            raise ValueError('Pinned original source hash mismatch: ' + name)
    with checkpoint.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != CHECKPOINT_SHA:
            raise ValueError('Pinned checkpoint SHA256 mismatch')
    env = dict(torch=torch, nn=torch.nn, F=F, Tensor=torch.Tensor, logging=logging,
               HParams=types.SimpleNamespace, MelSpectrogram=lambda hp: torch.nn.Identity())
    original = source / 'resemble_enhance'
    path = original / 'denoiser/unet.py'
    exec(compile(path.read_text(), str(path), 'exec'), env)
    path = original / 'denoiser/denoiser.py'
    parsed = ast.parse(path.read_text())
    parsed.body = [node for node in parsed.body if not isinstance(node, (ast.Import, ast.ImportFrom))]
    exec(compile(parsed, str(path), 'exec'), env)
    model = env['Denoiser'](types.SimpleNamespace(wav_rate=44100, hop_size=420))
    state = torch.load(checkpoint, map_location='cpu', weights_only=True)['module']
    state = {key.removeprefix('denoiser.'): value for key, value in state.items() if key.startswith('denoiser.')}
    omitted = sorted(key for key in state if key.startswith('mel_fn.'))
    model.load_state_dict({key: value for key, value in state.items() if key not in omitted}, strict=True)
    path = original / 'inference.py'
    parsed = ast.parse(path.read_text())
    parsed.body = [node for node in parsed.body if isinstance(node, ast.FunctionDef) and node.name == 'inference_chunk']
    exec(compile(parsed, str(path), 'exec'), env)
    return model.eval().requires_grad_(False), env['inference_chunk'], omitted


class Spectrum(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, features):
        magnitude, cosine, sine = features[:, 0], features[:, 1], features[:, 2]
        mask, sine_residual, cosine_residual = self.model._predict(magnitude, cosine, sine)
        magnitude, cosine, sine = self.model._separate(magnitude, cosine, sine, mask, cosine_residual, sine_residual)
        return torch.stack([magnitude * cosine, magnitude * sine], dim=1)
