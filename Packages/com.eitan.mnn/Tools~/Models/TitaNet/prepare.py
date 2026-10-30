#!/usr/bin/env python3
"""Reproduce original-checkpoint TitaNet FP32 MNN and NeMo frontend metadata.

Requires local pinned unmodified NeMo sources and .nemo archive; no downloads,
native code, upstream changes or automatic deployment installation.
"""
from pathlib import Path
import argparse,ast,collections,hashlib,importlib.util,json,logging,math,random,subprocess,sys,tarfile,typing
import numpy as np,torch,librosa
from torch import nn,Tensor
from torch.nn import functional as F
from torch.nn.init import _calculate_correct_fan
from torch.nn.modules.utils import _single
from omegaconf import DictConfig,ListConfig,OmegaConf
sys.dont_write_bytecode=True
CHECKPOINT_SHA256='e838520693f269e7984f55bc8eb3c2d60ccf246bf4b896d4be9bcabe3e4b0fe3'
SOURCE_REVISION='2b03b748bb7d28e4e382df3d085078c6146d174e'
SOURCE_HASHES={'conv_asr.py': 'f044c82dcfd9fe33301dd9e11d1a2d3d58b6f7b3bb802b663a4387146c156c2a', 'jasper.py': '062680d444150cc64b35985f347cd702d9d600efbb719eff3d431a443c6c514a', 'tdnn_attention.py': 'd9be78ce635122002017d635ecda4f364c58a2f541db745da2b3cd32074e2431', 'label_models.py': '562fbb0eaea4b15675d394bddfc062804248baee1a15a030fd2129786929d10d', 'features.py': 'bd333719eec5b65053a97e29f12618c9e82121fb0ab0c1c8854bb47a1114b101'}

def verify_sources(checkpoint, source):
 if hashlib.sha256(checkpoint.read_bytes()).hexdigest()!=CHECKPOINT_SHA256:raise ValueError('expected pinned original TitaNet .nemo archive')
 for name,digest in SOURCE_HASHES.items():
  if hashlib.sha256((source/name).read_bytes()).hexdigest()!=digest:raise ValueError('original NeMo source differs: '+name)

GRAPH_UUID=b'26d71078-06cd-4c82-b276-99a4a213668b'
class Exportable:pass
class AccessMixin:
 def is_access_enabled(self,*args):return False
class AdapterModuleMixin:
 def is_adapter_available(self):return False

def source_classes(source,filename,namespace,names=None):
 p=source/filename;tree=ast.parse(p.read_text());nodes=[n for n in tree.body if isinstance(n,(ast.ClassDef,ast.FunctionDef)) and (names is None or n.name in names)]
 # Type checking/export metadata and TorchScript compilation decorators only.
 for node in ast.walk(ast.Module(body=nodes,type_ignores=[])):
  if isinstance(node,(ast.ClassDef,ast.FunctionDef)):
   kept=[]
   for d in node.decorator_list:
    if isinstance(d,ast.Name) and d.id in ['property','staticmethod','classmethod']:kept.append(d)
    elif ast.unparse(d) not in ['torch.jit.script','torch.jit.script_if_tracing','typecheck()']:raise ValueError(ast.unparse(d))
   node.decorator_list=kept
 exec(compile(ast.Module(body=nodes,type_ignores=[]),str(p),'exec'),namespace)

def load(checkpoint, source):
 verify_sources(checkpoint, source)
 with tarfile.open(checkpoint) as archive:
  config=OmegaConf.create(archive.extractfile('model_config.yaml').read().decode())
 # EncDecSpeakerLabelModel injects angular=True for its AngularSoftmaxLoss.
 if 'angular' in config.loss.get('_target_',{}):config.decoder.angular=True
 ns=dict(torch=torch,nn=nn,F=F,np=np,math=math,Tensor=Tensor,Callable=typing.Callable,Iterable=typing.Iterable,List=typing.List,Optional=typing.Optional,Tuple=typing.Tuple,Union=typing.Union,Set=typing.Set,
  _calculate_correct_fan=_calculate_correct_fan,_single=_single,PYTORCH_QUANTIZATION_AVAILABLE=False,logging=logging,jasper_activations={'relu':nn.ReLU},AccessMixin=AccessMixin,AdapterModuleMixin=AdapterModuleMixin,
  OrderedDict=collections.OrderedDict,DictConfig=DictConfig,ListConfig=ListConfig,OmegaConf=OmegaConf,NeuralModule=nn.Module,Exportable=Exportable,inf=np.inf)
 source_classes(source,'jasper.py',ns)
 source_classes(source,'tdnn_attention.py',ns)
 source_classes(source,'conv_asr.py',ns,['ConvASREncoder','SpeakerDecoder'])
 encoder=ns['ConvASREncoder'](**{k:v for k,v in config.encoder.items() if k!='_target_'}).eval()
 decoder=ns['SpeakerDecoder'](**{k:v for k,v in config.decoder.items() if k!='_target_'}).eval()
 archive=checkpoint;assert hashlib.sha256(archive.read_bytes()).hexdigest()=='e838520693f269e7984f55bc8eb3c2d60ccf246bf4b896d4be9bcabe3e4b0fe3'
 with tarfile.open(archive) as tar:state=torch.load(tar.extractfile('model_weights.ckpt'),map_location='cpu',weights_only=True)
 for prefix,model in [('encoder.',encoder),('decoder.',decoder)]:
  print(prefix,model.load_state_dict({k.removeprefix(prefix):v for k,v in state.items() if k.startswith(prefix)},strict=True),flush=True)
 print('other state',{k:tuple(v.shape) for k,v in state.items() if not k.startswith(('encoder.','decoder.'))},flush=True)
 return encoder,decoder,state,config

def frontend(source,state):
 tree=ast.parse((source/'features.py').read_text())
 nodes=[n for n in tree.body if isinstance(n,(ast.ClassDef,ast.FunctionDef)) and n.name in ['FilterbankFeatures','normalize_batch']]
 ns={'torch':torch,'nn':nn,'librosa':librosa,'math':math,'random':random,'logging':logging,'CONSTANT':1e-5}
 exec(compile(ast.Module(body=nodes,type_ignores=[]),str(source/'features.py'),'exec'),ns)
 net=ns['FilterbankFeatures'](sample_rate=16000,n_window_size=400,n_window_stride=160,window='hann',normalize='per_feature',n_fft=512,nfilt=80,dither=1e-5,pad_to=16).eval()
 net.load_state_dict({'window':state['preprocessor.featurizer.window'],'fb':state['preprocessor.featurizer.fb']},strict=True)
 return net

def verify_onnx(model,path):
 import onnxruntime as ort
 options=ort.SessionOptions();options.intra_op_num_threads=2
 session=ort.InferenceSession(str(path),options,providers=['CPUExecutionProvider']);rng=np.random.default_rng(20261030);rows=[]
 with torch.no_grad():
  for frames,valid in [(16,5),(112,101),(272,261),(384,372),(816,801),(3008,3001)]:
   x=rng.normal(0,1,(1,80,frames)).astype(np.float32);x[:,:,valid:]=0
   length=np.array([valid],np.int64)
   expected=model(torch.from_numpy(x.copy()),torch.from_numpy(length.copy())).numpy().reshape(-1)
   actual=session.run(None,{'audio_signal':x,'length':length})[0].reshape(-1)
   expected/=np.linalg.norm(expected);actual/=np.linalg.norm(actual);error=float(np.linalg.norm(actual-expected))
   if not np.isfinite(error) or error>.0001:raise ValueError('ONNX unit-vector mismatch: '+str((frames,valid,error)))
   rows.append({'frames':frames,'valid':valid,'unit_embedding_l2':error})
 return rows

def normalize_uuid(path,identity):
 spec=importlib.util.spec_from_file_location('mnn_schema',Path(__file__).resolve().parents[1]/'deduplicate_mnn_external_weights.py')
 schema=importlib.util.module_from_spec(spec);spec.loader.exec_module(schema)
 model=schema.Model(path.read_bytes());field=model.field(model.integer(0),11)
 if not field:raise ValueError('missing public serialized Net.mnn_uuid')
 position=field+model.integer(field)
 if model.integer(position)!=len(identity):raise ValueError('unexpected public UUID length')
 model.data[position+4:position+4+len(identity)]=identity;path.write_bytes(model.data)

def main():
 parser=argparse.ArgumentParser(description=__doc__)
 for name in ['source','checkpoint','converter','output']:parser.add_argument('--'+name,type=Path,required=True)
 args=parser.parse_args();torch.set_num_threads(2)
 encoder,decoder,state,config=load(args.checkpoint,args.source);features=frontend(args.source,state)
 class Embedding(torch.nn.Module):
  def __init__(self):super().__init__();self.encoder=encoder;self.decoder=decoder
  def forward(self,audio_signal,length):
   encoded,new_length=self.encoder(audio_signal,length)
   return self.decoder(encoded,new_length)[1]
 model=Embedding().eval();encoder.update_max_sequence_length(4096,torch.device('cpu'))
 args.output.mkdir(parents=True,exist_ok=True);onnx=args.output/'titanet_dynamic.onnx'
 torch.onnx.export(model,(torch.zeros(1,80,272),torch.tensor([261])),onnx,opset_version=17,dynamo=False,
  input_names=['audio_signal','length'],output_names=['embedding'],dynamic_axes={'audio_signal':{2:'frames'}})
 parity=verify_onnx(model,onnx);graph=args.output/'titanet_fp32.mnn'
 command=[str(args.converter),'-f','ONNX','--modelFile',str(onnx),'--MNNModel',str(graph),'--bizCode','TitaNet','--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0']
 with (args.output/'conversion.log').open('w') as log:subprocess.run(command,check=True,stdout=log,stderr=subprocess.STDOUT)
 normalize_uuid(graph,GRAPH_UUID)
 profile={'repository':'nvidia/speakerverification_en_titanet_large','source_revision':'0dc382f40121a5fbd34db10a2bb04d826c2be6a8','graph':graph.name,'graph_fp16':'','frontend':'nemo-2.3.0-titanet-fbank80-per-feature','analysis_window':features.window.tolist(),'mel_filters':features.fb.flatten().tolist()}
 (args.output/'mnn_speaker.json').write_text(json.dumps(profile,indent=2)+'\n')
 report={'checkpoint_sha256':CHECKPOINT_SHA256,'nemo_revision':SOURCE_REVISION,'source_hashes':SOURCE_HASHES,'onnx_parity':parity,
  'graph_bytes':graph.stat().st_size,'graph_sha256':hashlib.sha256(graph.read_bytes()).hexdigest(),'converter_arguments':command,'canonical_public_uuid':GRAPH_UUID.decode()}
 (args.output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
