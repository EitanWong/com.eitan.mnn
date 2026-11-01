"""Export-only lowering; original first-eval arithmetic and output amplitude are retained."""
import torch


class FrozenComplexNorm(torch.nn.Module):
    def __init__(self, original):
        super().__init__()
        # Fold exactly the original FIRST eval call from independent saved checkpoint buffers.
        rr, ri, ii = original.Var_rr.clone(), original.Var_ri.clone(), original.Var_ii.clone()
        rr += original.eps
        ii += original.eps
        tau, s = rr + ii, rr * ii - ri * ri
        t = (tau + 2 * s).sqrt()
        rst = (s * t).reciprocal()
        urr, uri, uii = (ii + s) * rst, -ri * rst, (rr + s) * rst
        values = dict(mean_real=original.Mean_real, mean_imag=original.Mean_imag,
                      zrr=original.Wrr * urr + original.Wri * uri,
                      zri=original.Wrr * uri + original.Wri * uii,
                      zir=original.Wii * uri + original.Wri * urr,
                      zii=original.Wri * uri + original.Wii * uii,
                      br=original.Br, bi=original.Bi)
        for name, value in values.items():
            self.register_buffer(name, value.clone().reshape(1, -1, 1, 1))

    def forward(self, value):
        real, imag = torch.chunk(value, 2, 1)
        real, imag = real - self.mean_real, imag - self.mean_imag
        return torch.cat([self.zrr * real + self.zri * imag + self.br,
                          self.zir * real + self.zii * imag + self.bi], 1)


def freeze(module):
    for name, child in list(module.named_children()):
        if type(child).__name__ == 'ComplexBatchNorm2D':
            setattr(module, name, FrozenComplexNorm(child))
        else:
            freeze(child)



class Analysis(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.stft = model.stft

    def forward(self, pcm):
        spectrum = self.stft(pcm)
        real, imag = spectrum[:, :257], spectrum[:, 257:]
        magnitude, phase = torch.sqrt(real ** 2 + imag ** 2 + 1e-9), torch.atan2(imag, real)
        return torch.stack([magnitude, phase], 1)[:, :, 1:], magnitude, phase


class Encoder(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.layers = model.encoder

    def forward(self, value):
        outputs = []
        for layer in self.layers:
            value = layer(value)
            outputs.append(value)
        return tuple(outputs)



class Recurrent(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.lstm = model.lstm

    def forward(self, value):
        batch, channels, bins, frames = value.shape
        value = value.permute(3, 0, 1, 2)
        real = value[:, :, :channels // 2].reshape(frames, batch, channels // 2 * bins)
        imag = value[:, :, channels // 2:].reshape(frames, batch, channels // 2 * bins)
        real, imag = self.lstm([real, imag])
        value = torch.cat([real.reshape(frames, batch, channels // 2, bins), imag.reshape(frames, batch, channels // 2, bins)], 2)
        return value.permute(1, 2, 3, 0)


class Back(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.decoder, self.istft = model.decoder, model.istft

    def forward(self, recurrent, skip0, skip1, skip2, skip3, skip4, skip5, magnitude, phase):
        from mayavoz.models.complexnn.utils import complex_cat
        value = recurrent
        for layer, skip in zip(self.decoder, [skip5, skip4, skip3, skip2, skip1, skip0]):
            value = layer(complex_cat([skip, value]))[..., 1:]
        real = torch.nn.functional.pad(value[:, 0], [0, 0, 1, 0])
        imag = torch.nn.functional.pad(value[:, 1], [0, 0, 1, 0])
        mask = torch.sqrt(real ** 2 + imag ** 2)
        mask_phase = torch.atan2(imag / (mask + 1e-8), real / (mask + 1e-8))
        estimate, angle = torch.tanh(mask) * magnitude, mask_phase * phase
        # Preserve the published E-mask followed by the original non-C else branch.
        spectrum = torch.cat([(estimate + torch.cos(angle)) * real, (estimate + torch.sin(angle)) * imag], 1)
        return self.istft(spectrum).clamp_(-1, 1)
