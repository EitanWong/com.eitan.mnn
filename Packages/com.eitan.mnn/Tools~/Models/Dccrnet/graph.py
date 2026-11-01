"""Real/imaginary lowering of the original complex DCCRNet forward."""
import torch
import torch.nn.functional as F

def cmul(layer, pair):
    r, i = pair
    return (layer.re_module(r) - layer.im_module(i), layer.re_module(i) + layer.im_module(r))

def reim(layer, pair):
    return (layer.re_module(pair[0]), layer.im_module(pair[1]))

def rnn_layer(layer, pair):
    r, i = pair
    r = r.permute(0, 3, 1, 2)
    i = i.permute(0, 3, 1, 2)
    shape = r.shape
    r = r.reshape(1, shape[1], 512)
    i = i.reshape(1, shape[1], 512)
    for block in layer.rnn.rnns:
        r, i = cmul(block, (r, i))
    r, i = cmul(layer.linear, (r, i))
    r = r.reshape(shape).permute(0, 2, 3, 1)
    i = i.reshape(shape).permute(0, 2, 3, 1)
    assert layer.norm is None
    return (r, i)

def block(layer, pair):
    name = type(layer).__name__
    if name == 'DCUNetComplexEncoderBlock':
        return reim(layer.activation, reim(layer.norm, cmul(layer.conv, pair)))
    if name == 'DCUNetComplexDecoderBlock':
        return reim(layer.activation, reim(layer.norm, cmul(layer.deconv, pair)))
    if name == 'DCCRMaskNetRNN':
        return rnn_layer(layer, pair)
    if isinstance(layer, torch.nn.Identity):
        return pair
    raise ValueError(name)

class Export(torch.nn.Module):

    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, mixture):
        tf = F.conv1d(mixture.unsqueeze(1), self.model.encoder.filterbank.filters(), stride=100)
        real = tf[:, :256]
        imag = tf[:, 257:513]
        pair = (real.unsqueeze(1), imag.unsqueeze(1))
        saved = []
        for layer in self.model.masker.encoders:
            pair = block(layer, pair)
            saved.append(pair)
        for enc, dec in zip(reversed(saved[:-1]), self.model.masker.decoders):
            pair = block(dec, pair)
            pair = (torch.cat([pair[0], enc[0]], dim=1), torch.cat([pair[1], enc[1]], dim=1))
        r, i = cmul(self.model.masker.output_layer[0], pair)
        mag = torch.sqrt(r * r + i * i + 1e-08)
        phase = torch.atan2(i, r)
        bounded = torch.tanh(mag)
        mr = (bounded * torch.cos(phase))[:, 0]
        mi = (bounded * torch.sin(phase))[:, 0]
        sr = mr * real - mi * imag
        si = mr * imag + mi * real
        spec = torch.cat([F.pad(sr, (0, 0, 0, 1)), F.pad(si, (0, 0, 0, 1))], dim=1)
        y = F.conv_transpose1d(spec, self.model.decoder.filterbank.filters(), stride=100)
        return F.pad(y, (0, mixture.shape[1] - y.shape[2])).transpose(1, 2)
