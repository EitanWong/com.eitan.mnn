"""Original DCUNet real/imaginary lowering, including pad-to-stride and output crop."""
import torch
import torch.nn.functional as F


def cmul(layer, pair):
    real, imag = pair
    return layer.re_module(real) - layer.im_module(imag), layer.re_module(imag) + layer.im_module(real)


def reim(layer, pair):
    return layer.re_module(pair[0]), layer.im_module(pair[1])


def block(layer, pair):
    convolution = layer.conv if hasattr(layer, 'conv') else layer.deconv
    return reim(layer.activation, reim(layer.norm, cmul(convolution, pair)))


class Export(torch.nn.Module):
    def __init__(self, model):
        super().__init__()
        self.model = model
        assert model.stft_n_filters == 1024 and model.stft_kernel_size == 1024 and model.stft_stride == 256
        assert model.masker.fix_length_mode == 'pad' and list(model.masker.encoders_stride_product) == [256, 16]
        assert model.masker.mask_bound == 'tanh'

    def forward(self, mixture):
        spectrum = F.conv1d(mixture.unsqueeze(1), self.model.encoder.filterbank.filters(), stride=256)
        real, imag = spectrum[:, :513], spectrum[:, 513:]
        frames = real.shape[2]
        time_pad = (1 - frames) % 16
        pair = F.pad(real.unsqueeze(1), (0, time_pad)), F.pad(imag.unsqueeze(1), (0, time_pad))
        saved = []
        for layer in self.model.masker.encoders:
            pair = block(layer, pair)
            saved.append(pair)
        for enc, dec in zip(reversed(saved[:-1]), self.model.masker.decoders):
            pair = block(dec, pair)
            pair = torch.cat([pair[0], enc[0]], dim=1), torch.cat([pair[1], enc[1]], dim=1)
        r, i = cmul(self.model.masker.output_layer[0], pair)
        mag = torch.sqrt(r*r + i*i + 1e-8)
        phase = torch.atan2(i, r)
        bounded = torch.tanh(mag)
        mr, mi = (bounded * torch.cos(phase))[:, 0, :, :frames], (bounded * torch.sin(phase))[:, 0, :, :frames]
        spec = torch.cat([mr*real-mi*imag, mr*imag+mi*real], dim=1)
        waveform = F.conv_transpose1d(spec, self.model.decoder.filterbank.filters(), stride=256)
        return F.pad(waveform, (0, mixture.shape[1]-waveform.shape[2])).transpose(1, 2)
