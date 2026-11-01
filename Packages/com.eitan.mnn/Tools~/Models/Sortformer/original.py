"""Load pinned NeMo mathematical classes without Lightning/training infrastructure.

Metadata typechecking, absent adapters/access instrumentation and FP32 autocast guards
are stubbed explicitly; original mathematical class/function bodies remain unchanged.
"""
from pathlib import Path
import ast,collections,contextlib,copy,dataclasses,functools,io,logging,math,random,sys,tarfile,types,typing
import numpy as np
import torch
from torch import nn
import torch.nn.functional as F
sys.dont_write_bytecode=True
from omegaconf import OmegaConf,DictConfig,ListConfig,open_dict
class NeuralModule(nn.Module):
 @property
 def device(self):return next(self.parameters()).device
class StreamingEncoder:pass
class Exportable:pass
class AccessMixin:
 @staticmethod
 def is_access_enabled(*args,**kwargs):return False
class AttentionAdapterModuleMixin:
 def is_adapter_available(self):return False
COMMON=dict(torch=torch,nn=nn,F=F,np=np,math=math,random=random,copy=copy,Optional=typing.Optional,Tuple=typing.Tuple,List=typing.List,Set=typing.Set,Dict=typing.Dict,Any=typing.Any,Union=typing.Union,dataclass=dataclasses.dataclass,OrderedDict=collections.OrderedDict,lru_cache=functools.lru_cache,logging=logging,DictConfig=DictConfig,ListConfig=ListConfig,open_dict=open_dict,NeuralModule=NeuralModule,StreamingEncoder=StreamingEncoder,Exportable=Exportable,AccessMixin=AccessMixin,AttentionAdapterModuleMixin=AttentionAdapterModuleMixin,typecheck=lambda **kwargs:lambda f:f,avoid_float16_autocast_context=contextlib.nullcontext,adapter_mixins=types.SimpleNamespace(AdapterModuleMixin=AttentionAdapterModuleMixin),INF_VAL=10000.0)
def selected(source,relative,names,extra=None):
 path=source/relative;tree=ast.parse(path.read_text());nodes=[]
 for node in tree.body:
  if isinstance(node,(ast.ClassDef,ast.FunctionDef)) and node.name in names:nodes.append(node)
 assert {n.name for n in nodes}==set(names),(relative,names)
 module=types.ModuleType('original_sortformer_'+Path(relative).stem);module.__file__=str(path);sys.modules[module.__name__]=module
 module.__dict__.update(COMMON);module.__dict__.update(extra or {})
 future=ast.ImportFrom(module='__future__',names=[ast.alias(name='annotations')],level=0)
 exec(compile(ast.fix_missing_locations(ast.Module(body=[future]+nodes,type_ignores=[])),str(path),'exec'),module.__dict__)
 return module

def load(source,checkpoint,config_path):
 p='nemo/collections/asr/parts/submodules/'
 causal=selected(source,p+'causal_convs.py',['CausalConv1D','CausalConv2D'])
 subsampling=selected(source,p+'subsampling.py',['StackingSubsampling','ConvSubsampling','SubsamplingReductionModule','calc_length','MaskedConvSequential','apply_channel_mask','calculate_conv_output_size'],dict(CausalConv1D=causal.CausalConv1D,CausalConv2D=causal.CausalConv2D,LayerNorm=nn.LayerNorm))
 mha=selected(source,p+'multi_head_attention.py',['MultiHeadAttention','RelPositionMultiHeadAttention','PositionalEncoding','RelPositionalEncoding'])
 activation=selected(source,'nemo/collections/asr/parts/utils/activations.py',['Swish'])
 conformer=selected(source,p+'conformer_modules.py',['ConformerConvolution','ConformerFeedForward','ConformerLayer'],dict(MultiHeadAttention=mha.MultiHeadAttention,RelPositionMultiHeadAttention=mha.RelPositionMultiHeadAttention,LayerNorm=nn.LayerNorm,CausalConv1D=causal.CausalConv1D,Swish=activation.Swish,activation_registry={'identity':nn.Identity,'hardtanh':nn.Hardtanh,'relu':nn.ReLU,'selu':nn.SELU,'swish':nn.SiLU,'silu':nn.SiLU,'gelu':nn.GELU}))
 cfg=selected(source,'nemo/collections/asr/models/configs/asr_models_config.py',['CacheAwareStreamingConfig'])
 regularization=selected(source,'nemo/collections/asr/parts/utils/regularization_utils.py',['compute_stochastic_depth_drop_probs'])
 encoder=selected(source,'nemo/collections/asr/modules/conformer_encoder.py',['ConformerEncoder'],dict(CausalConv1D=causal.CausalConv1D,ConformerLayer=conformer.ConformerLayer,MultiHeadAttention=mha.MultiHeadAttention,PositionalEncoding=mha.PositionalEncoding,RelPositionalEncoding=mha.RelPositionalEncoding,RelPositionMultiHeadAttention=mha.RelPositionMultiHeadAttention,ConvSubsampling=subsampling.ConvSubsampling,StackingSubsampling=subsampling.StackingSubsampling,SubsamplingReductionModule=subsampling.SubsamplingReductionModule,CacheAwareStreamingConfig=cfg.CacheAwareStreamingConfig,compute_stochastic_depth_drop_probs=regularization.compute_stochastic_depth_drop_probs))
 mask=selected(source,'nemo/collections/common/parts/transformer_utils.py',['form_attention_mask'],dict(NEG_INF=-10000.0))
 transformer=selected(source,'nemo/collections/asr/modules/transformer/transformer_modules.py',['MultiHeadAttention','PositionWiseFF'],dict(gelu=F.gelu))
 tenc=selected(source,'nemo/collections/asr/modules/transformer/transformer_encoders.py',['TransformerEncoderBlock','TransformerEncoder'],dict(MultiHeadAttention=transformer.MultiHeadAttention,PositionWiseFF=transformer.PositionWiseFF,form_attention_mask=mask.form_attention_mask))
 sort=selected(source,'nemo/collections/asr/modules/sortformer_modules.py',['StreamingSortformerState','SortformerModules'])
 config=OmegaConf.load(config_path)
 class Model(NeuralModule):
  def __init__(self):
   super().__init__()
   self.encoder=encoder.ConformerEncoder(**{k:v for k,v in config.encoder.items() if k!='_target_'})
   self.transformer_encoder=tenc.TransformerEncoder(**{k:v for k,v in config.transformer_encoder.items() if k!='_target_'})
   self.sortformer_modules=sort.SortformerModules(**{k:v for k,v in config.sortformer_modules.items() if k!='_target_'})
   self.spec_augmentation=None
   self.async_streaming=False;self.streaming_mode=True;self.max_batch_dur=0;self.eps=1e-5;self.negative_init_val=-80.0
   self.concat_and_pad_script=self.sortformer_modules.concat_and_pad
 model=Model().eval()
 # Preserve original executable forward methods; omit only Lightning/training infrastructure.
 path=source/'nemo/collections/asr/models/sortformer_diar_models.py';tree=ast.parse(path.read_text());cl=next(n for n in tree.body if isinstance(n,ast.ClassDef) and n.name=='SortformerEncLabelModel')
 for name in ['frontend_encoder','forward_infer','forward_for_export','forward_streaming_step','forward_streaming','process_signal']:
  node=next(n for n in cl.body if isinstance(n,ast.FunctionDef) and n.name==name);ns=dict(COMMON,dist=torch.distributed,tqdm=lambda sequence,**kwargs:sequence);exec(compile(ast.fix_missing_locations(ast.Module(body=[node],type_ignores=[])),str(path),'exec'),ns);setattr(Model,name,ns[name])
 with tarfile.open(checkpoint) as archive:
  checkpoint=torch.load(io.BytesIO(archive.extractfile('model_weights.ckpt').read()),map_location='cpu',weights_only=False)
 print('checkpoint keys',len(checkpoint),flush=True)
 frontend={k:v for k,v in checkpoint.items() if k.startswith('preprocessor.')}
 print('frontend buffers',[(k,list(v.shape)) for k,v in frontend.items()],flush=True)
 learned={k:v for k,v in checkpoint.items() if not k.startswith('preprocessor.')}
 print(model.load_state_dict(learned,strict=True),flush=True)
 return model,frontend,config
