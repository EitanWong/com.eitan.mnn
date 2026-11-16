from workspace import workspace
import os
"""Use official converter with its final-op mapping corrected at the Python call boundary.
The installed load_mnn returns the last transformer block, whereas convert expects
an output block. Official sources are untouched; assert graph structure first.
"""
from pathlib import Path
from types import SimpleNamespace
import sys
A,root=workspace()
export=Path(os.environ['MNN_OFFICIAL_EXPORT_ROOT'])
sys.path.insert(0,str(export))
import safetensors2mnn as official
original=official.load_mnn
quantize=official.torch_utils.quant
official.torch_utils.quant=lambda weight,bits,block,symmetric,awq: quantize(weight,bits,block,symmetric,awq,False)
def mapped(path):
    graph,ops,convs,blocks,last=original(path)
    assert len(blocks)==28 and len(convs)==197
    norm=ops['output_norm'];head=convs[-1]
    assert head['main']['common']['outputCount']==152064
    return graph,ops,convs,blocks,SimpleNamespace(attn_norm=norm,conv=[head])
official.load_mnn=mapped
import torch
torch.set_num_threads(4)
official.convert(SimpleNamespace(path=str(root/'Assets/StreamingAssets/MNN/Models/TEN-Turn-Detection-Source'),mnn_dir=str(Path(sys.argv[2]).resolve()) if len(sys.argv)>2 else str(A/'TEN/template'),quant_bit=int(sys.argv[1]) if len(sys.argv)>1 else 4,quant_block=64,lm_quant_bit=8,sym=True,scale_bit=16))
print('REAL_SOURCE_WEIGHTS_WRITTEN',flush=True)
