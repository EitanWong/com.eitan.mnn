"""Independent AV-DPRNN visual convolution and synthesis lowering."""
from pathlib import Path
import copy
import sys
import types
import torch.nn.functional as F

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from clearervoice_av_export import frontend, decoder, Visual, Audio

def padding(self,value,size):
    hop=size//2
    gap=size-(hop+value.shape[-1]%size)%size
    return F.pad(value,(hop,gap+hop)),gap

def lower(model):
    clone=copy.deepcopy(model).eval()
    clone.ref_encoder.v_frontend.forward=types.MethodType(frontend,clone.ref_encoder.v_frontend)
    clone.sep_network.decoder.forward=types.MethodType(decoder,clone.sep_network.decoder)
    clone.sep_network.separator._padding=types.MethodType(padding,clone.sep_network.separator)
    return clone
