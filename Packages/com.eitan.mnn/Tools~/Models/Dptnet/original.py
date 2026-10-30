"""Execute pinned original Asteroid DPTNet math without training-package imports."""
from pathlib import Path
import ast,functools,inspect,sys,types,typing,warnings
import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
sys.dont_write_bytecode=True
def selected(path,names,namespace,label):
    module=types.ModuleType('original_asteroid_'+label);module.__file__=str(path);sys.modules[module.__name__]=module
    module.__dict__.update(namespace)
    tree=ast.parse(path.read_text());nodes=[n for n in tree.body if isinstance(n,(ast.ClassDef,ast.FunctionDef)) and n.name in names]
    assert {n.name for n in nodes}==set(names),(path,names)
    exec(compile(ast.Module(body=nodes,type_ignores=[]),str(path),'exec'),module.__dict__)
    return module
COMMON=dict(torch=torch,nn=nn,F=F,np=np,warnings=warnings,Optional=typing.Optional,List=typing.List,Tuple=typing.Tuple,functools=functools,partial=functools.partial)
def load(checkpoint,source,filterbanks):
    ASTEROID=source;FB=filterbanks
    u=selected(ASTEROID/'utils/torch_utils.py',['is_tracing','script_if_tracing','pad_x_to_y','jitable_shape'],dict(COMMON),'utils')
    s=selected(FB/'scripting.py',['is_tracing','script_if_tracing'],dict(COMMON),'scripting')
    e=selected(FB/'enc_dec.py',['Filterbank','_EncDec','Encoder','Decoder','multishape_conv1d','batch_packed_1d_conv','multishape_conv_transpose1d'],dict(COMMON,script_if_tracing=s.script_if_tracing),'encdec')
    free=selected(FB/'free_fb.py',['FreeFB'],dict(COMMON,Filterbank=e.Filterbank),'free')
    # Execute the original factory with only the FreeFB lookup used by this checkpoint.
    factory=selected(FB/'__init__.py',['make_enc_dec'],dict(Encoder=e.Encoder,Decoder=e.Decoder,get=lambda name:free.FreeFB if name.lower() in ('free','freefb') else (_ for _ in ()).throw(ValueError(name))),'factory')
    activations=selected(ASTEROID/'masknn/activations.py',['linear','relu','prelu','sigmoid','softmax','tanh','get'],dict(COMMON),'activations')
    norms=selected(ASTEROID/'masknn/norms.py',['z_norm','_glob_norm','_LayerNorm','GlobLN','get'],dict(COMMON,EPS=1e-8,script_if_tracing=u.script_if_tracing),'norms');norms.gLN=norms.GlobLN
    ola=selected(ASTEROID/'dsp/overlap_add.py',['DualPathProcessing'],dict(COMMON),'ola')
    attention=selected(ASTEROID/'masknn/attention.py',['ImprovedTransformedLayer','DPTransformer'],dict(COMMON,norms=norms,activations=activations,has_arg=lambda fn,arg:arg in inspect.signature(fn).parameters,DualPathProcessing=ola.DualPathProcessing,MultiheadAttention=nn.MultiheadAttention,ceil=__import__('math').ceil),'attention')
    base=selected(ASTEROID/'models/base_models.py',['BaseModel','_unsqueeze_to_3d','_shape_reconstructed','BaseEncoderMaskerDecoder'],dict(COMMON,script_if_tracing=u.script_if_tracing,pad_x_to_y=u.pad_x_to_y,jitable_shape=u.jitable_shape,activations=activations),'base')
    model=selected(ASTEROID/'models/dptnet.py',['DPTNet'],dict(COMMON,make_enc_dec=factory.make_enc_dec,DPTransformer=attention.DPTransformer,BaseEncoderMaskerDecoder=base.BaseEncoderMaskerDecoder),'dptnet')
    value=torch.load(checkpoint,map_location='cpu',weights_only=False)
    assert value['model_name']=='DPTNet'
    net=model.DPTNet(**value['model_args']).eval();print(net.load_state_dict(value['state_dict'],strict=True),flush=True)
    return net,value
