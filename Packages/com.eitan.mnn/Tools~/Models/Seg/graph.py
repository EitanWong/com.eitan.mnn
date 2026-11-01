"""SEG export using bounded initial moments and explicit overlapping windows."""
import copy
import sys
import types
from pathlib import Path
import torch
import torch.nn.functional as F
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from clearervoice_av_export import decoder


def bounded_mean(value):
    count = value.shape[1]
    for _ in range(4):
        length = value.shape[-1]
        value = F.pad(value, (0, (64 - length % 64) % 64)).reshape(1, -1, 64).sum(-1)
    return value.sum(-1, keepdim=True) / count


def initial_norm(self, value):
    flat = value.reshape(1, -1)
    mean = bounded_mean(flat)
    centered = flat - mean
    variance = bounded_mean(centered * centered)
    normalized = (centered / torch.sqrt(variance + self.eps)).reshape(value.shape)
    return normalized * self.weight.reshape(1, -1, 1) + self.bias.reshape(1, -1, 1)


def padding(self, value, size):
    hop = size // 2
    gap = size - (hop + value.shape[-1] % size) % size
    return F.pad(value, (hop, gap + hop)), gap


def segmentation(self, value, size):
    padded, gap = self._padding(value, size)
    steps = (padded.shape[-1] - size) // (size // 2) + 1
    indices = torch.arange(steps, device=value.device)[:, None] * (size // 2) + torch.arange(size, device=value.device)[None, :]
    windows = torch.index_select(padded, 2, indices.reshape(-1))
    return windows.reshape(value.shape[0], value.shape[1], steps, size).transpose(2, 3).contiguous(), gap


def lower(model):
    result = copy.deepcopy(model).eval()
    network = result.sep_network
    network.decoder.forward = types.MethodType(decoder, network.decoder)
    network.separator._padding = types.MethodType(padding, network.separator)
    network.separator._Segmentation = types.MethodType(segmentation, network.separator)
    network.separator.layer_norm.forward = types.MethodType(initial_norm, network.separator.layer_norm)
    return result


class Gesture(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.encoder = model.sep_network.separator.visual_net

    def forward(self, gestures):
        return self.encoder(gestures)[0]


class EncodedGesture(torch.nn.Module):
    def forward(self, features):
        return features, None


def audio_only(model):
    result = copy.deepcopy(model)
    result.sep_network.separator.visual_net = EncodedGesture()
    return result
