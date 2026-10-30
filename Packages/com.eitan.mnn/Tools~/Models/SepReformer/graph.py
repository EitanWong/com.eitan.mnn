"""Equivalent integer-ratio pooling and unconditional time padding on an independent export clone."""
import copy
import types
import torch

def pooled_forward(self, x, pos_k):
    down_len = pos_k.shape[0]
    x_down = x.reshape(x.shape[0], x.shape[1], down_len, -1).mean(-1)
    x = x.permute(0, 2, 1)
    x_down = x_down.permute(0, 2, 1)
    x_down = self.block['self_attn'](x_down, pos_k, None).permute(0, 2, 1)
    x_downup = torch.nn.functional.interpolate(x_down, size=x.shape[1], mode='nearest').permute(0, 2, 1)
    return x + self.block['linear'](x) * x_downup

def padded_signal(self, x):
    rest = -x.shape[-1] % 16
    return (torch.nn.functional.pad(x, (0, rest)), rest)

class Export(torch.nn.Module):

    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, mixture):
        features = self.model.audio_encoder(mixture)
        projected = self.model.feature_projector(features)
        last, _ = self.model.separator(projected)
        outputs = self.model.out_layer(last, features)
        return torch.stack([self.model.audio_decoder(outputs[i]).reshape(1, -1) for i in range(2)], dim=-1)


def polynomial_gelu(self, x):
    u = torch.abs(x) * 0.7071067811865476
    t = 1.0 / (1.0 + 0.3275911 * u)
    erfc = ((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * torch.exp(-u * u)
    return torch.where(x >= 0.0, x * (1.0 - 0.5 * erfc), x * (0.5 * erfc))


def lower(model):
    lowered = copy.deepcopy(model).eval()
    lowered.separator.pad_signal = types.MethodType(padded_signal, lowered.separator)
    for module in lowered.modules():
        if isinstance(module, torch.nn.GELU):
            module.forward = types.MethodType(polynomial_gelu, module)
        elif type(module).__name__ == "EGA":
            module.forward = types.MethodType(pooled_forward, module)
    return Export(lowered).eval()
