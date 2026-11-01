"""Real-valued original TIGER spectrum and source-ordered dynamic pooling."""
import torch


class Spectrum(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, spec):
        batch = spec.shape[0]
        features, bands = [], []
        start = 0
        for i, width in enumerate(self.model.band_width):
            band = spec[:, :, start:start + width]
            bands.append(band)
            features.append(self.model.BN[i](band.reshape(batch, width * 2, -1)))
            start += width
        output = self.model.separator(torch.stack(features, dim=1))
        separated = []
        for i, width in enumerate(self.model.band_width):
            mask = self.model.mask[i](output[:, i]).reshape(batch, 2, 2, 2, width, -1)
            mask = mask[:, 0] * torch.sigmoid(mask[:, 1])
            real, imag = mask[:, 0], mask[:, 1]
            real = real - (real.sum(1, keepdim=True) - 1) / 2
            imag = imag - imag.sum(1, keepdim=True) / 2
            source_real, source_imag = bands[i][:, 0, None], bands[i][:, 1, None]
            separated.append(torch.stack([source_real * real - source_imag * imag,
                                          source_real * imag + source_imag * real], dim=2))
        return torch.cat(separated, dim=3).reshape(batch, 4, 321, -1)


def adaptive_pool(x, output_size):
    # All author call sites pool a multiscale level to the fifth level.
    # Exact adaptive intervals have at most 17 elements (ceil(input/output)+1).
    length = x.shape[-1]
    positions = torch.arange(output_size, device=x.device)
    starts = torch.div(positions * length, output_size, rounding_mode='floor')
    ends = torch.div((positions + 1) * length + output_size - 1, output_size, rounding_mode='floor')
    offsets = torch.arange(17, device=x.device)
    indices = starts[:, None] + offsets[None, :]
    valid = indices < ends[:, None]
    gathered = x[:, :, torch.clamp(indices, max=length - 1)]
    gathered = gathered * valid.to(x.dtype)[None, None]
    # Native adaptive pooling accumulates intervals in source order. Output=1
    # takes a different fast path in PyTorch: the native mean reduction.
    total = gathered[..., 0] * 0
    for offset in range(17):
        total = total + gathered[..., offset]
    pooled = total / (ends - starts).to(x.dtype)[None, None]
    condition = torch.tensor(output_size, device=x.device) if isinstance(output_size, int) else output_size
    return torch.where(condition == 1, x.mean(-1, keepdim=True), pooled)
