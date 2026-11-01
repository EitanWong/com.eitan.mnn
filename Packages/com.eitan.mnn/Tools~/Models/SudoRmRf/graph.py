"""Equivalent SudoRMRF graph with managed boundary padding and staged reductions."""
import types
import torch


def staged_norm(self, value):
    mean = value.mean(dim=2, keepdim=True).mean(dim=1, keepdim=True)
    centered = value - mean
    variance = (centered * centered).mean(dim=2, keepdim=True).mean(dim=1, keepdim=True)
    return self.apply_gain_and_bias(centered / torch.sqrt(variance + 1e-8))


class PaddedGraph(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, mixture):
        model = self.model
        encoded = model.encoder(mixture.unsqueeze(1))
        features = model.sm(model.bottleneck(model.ln(encoded)))
        masks = model.mask_nl_class(model.mask_net(features).reshape(1, 2, 512, -1))
        decoded = model.decoder((masks * encoded.unsqueeze(1)).reshape(1, 1024, -1))
        return decoded[:, :, :mixture.shape[1]].transpose(1, 2)


def lower(model):
    count = 0
    for layer in model.modules():
        if type(layer).__name__ == "GlobLN":
            layer.forward = types.MethodType(staged_norm, layer)
            count += 1
    if count != 113:
        raise ValueError("Unexpected original global-normalization count")
    return PaddedGraph(model).eval()
