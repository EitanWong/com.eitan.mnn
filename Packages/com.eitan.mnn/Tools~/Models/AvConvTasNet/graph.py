"""Independent AV-ConvTasNet visual convolution and synthesis lowering."""
from pathlib import Path
import copy
import sys
import types

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from clearervoice_av_export import frontend, decoder, Visual, Audio

def lower(model):
    clone = copy.deepcopy(model).eval()
    clone.ref_encoder.v_frontend.forward = types.MethodType(frontend, clone.ref_encoder.v_frontend)
    clone.sep_network.decoder.forward = types.MethodType(decoder, clone.sep_network.decoder)
    return clone
