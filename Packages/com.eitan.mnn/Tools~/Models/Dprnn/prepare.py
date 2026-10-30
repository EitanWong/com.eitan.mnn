#!/usr/bin/env python3
"""Reproduce pinned DPRNN WHAM with original local sources/checkpoint and official MNNConvert. No downloads or native code additions."""
import argparse,hashlib,importlib.util,json,subprocess
import onnxruntime as ort
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
    recurrent=selected(ASTEROID/'masknn/recurrent.py',['SingleRNN','DPRNNBlock','DPRNN'],dict(COMMON,norms=norms,activations=activations,has_arg=lambda fn,arg:arg in inspect.signature(fn).parameters,fold=F.fold,unfold=F.unfold),'recurrent')
    base=selected(ASTEROID/'models/base_models.py',['BaseModel','_unsqueeze_to_3d','_shape_reconstructed','BaseEncoderMaskerDecoder'],dict(COMMON,script_if_tracing=u.script_if_tracing,pad_x_to_y=u.pad_x_to_y,jitable_shape=u.jitable_shape,activations=activations),'base')
    model=selected(ASTEROID/'models/dprnn_tasnet.py',['DPRNNTasNet'],dict(COMMON,make_enc_dec=factory.make_enc_dec,DPRNN=recurrent.DPRNN,BaseEncoderMaskerDecoder=base.BaseEncoderMaskerDecoder),'dprnntasnet')
    value=torch.load(checkpoint,map_location='cpu',weights_only=False)
    assert value['model_name']=='DPRNNTasNet'
    net=model.DPRNNTasNet(**value['model_args']).eval();print(net.load_state_dict(value['state_dict'],strict=True),flush=True)
    return net,value

SOURCE_HASHES={'asteroid/utils/torch_utils.py': 'd31c58dacf8a768b6de7428e8634a0a87a330c7c7d4833f1bd37e43957674d0c', 'asteroid/masknn/activations.py': '586710e02a9b7990f38cdaee3fa3d6c5f8cbaba733a294d706c8208a3a950b91', 'asteroid/masknn/norms.py': '80bc9d54d9bbae5b3a110516cb927b5fc9a8ff72e10a257acb6ed4569c1e605b', 'asteroid/models/base_models.py': 'a88d38b4acb3b5ee5cbbd3122f5db2639109890f53670ac2ae36dc8b6b50bd5b', 'filterbanks/scripting.py': '04752287c9b6ef73c621e0d8fdf28d81cf8a28726432ab378fefbe8eb170bb8c', 'filterbanks/enc_dec.py': '4b385912861a60c6ebcadc170a3ef747637952e03ab6eab303c523788cb3beee', 'filterbanks/free_fb.py': '90bfab19257287781bef6c45aef3a6379d4369f13d8fa848b461716e98d4fc16', 'filterbanks/__init__.py': '84589b2b47ca3cd3cca538a5507b108670d29ae456956c589d9c1f457f7f0244', 'asteroid/masknn/recurrent.py': 'a606833c7116f9a5a19f788c3c1c923e838048d52f5682456e2027dbe6d5769c', 'asteroid/models/dprnn_tasnet.py': '814fe18c31327795631c6f116be417261c20d427dd27421bf6a48628a8b0a98f'}
CHECKPOINT_SHA='cc1f1fecf24ea3e486521029dc0e1444686bd4b6fdf9715e7757936cbd9ffdf6'
GRAPH_UUID=b'f20bfcc2-afb6-4fb9-802c-93d2c3fc17a8'

def normalize_uuid(path):
    spec=importlib.util.spec_from_file_location('schema',Path(__file__).resolve().parents[1]/'deduplicate_mnn_external_weights.py')
    schema=importlib.util.module_from_spec(spec);spec.loader.exec_module(schema)
    value=schema.Model(path.read_bytes());field=value.field(value.integer(0),11)
    if not field:raise ValueError('Missing public serialized Net.mnn_uuid')
    position=field+value.integer(field)
    if value.integer(position)!=len(GRAPH_UUID):raise ValueError('Unexpected public UUID length')
    value.data[position+4:position+4+len(GRAPH_UUID)]=GRAPH_UUID;path.write_bytes(value.data)

def nrmse(expected,actual):
    errors=[]
    for speaker in range(2):
        e=expected[:,:,speaker];energy=np.linalg.norm(e)
        errors.append(float(np.linalg.norm(actual[:,:,speaker]-e)/(energy if energy else 1)))
    if not np.isfinite(errors).all() or max(errors)>=.001:
        raise ValueError('Original waveform differs: '+str(errors))
    return errors

def chunked_forward(self,mixture_w):
    frames=mixture_w.shape[2];output=self.bottleneck(mixture_w);chunks=(frames+250)//125+1
    starts=torch.arange(chunks,device=mixture_w.device)*125
    indices=starts.unsqueeze(1)+torch.arange(250,device=mixture_w.device).unsqueeze(0)
    output=F.pad(output,(250,250))[:,:,indices.reshape(-1)].reshape(1,128,chunks,250).permute(0,1,3,2)
    output=self.first_out(self.net(output)).reshape(2,128,250,chunks).reshape(2,128,250*chunks)
    positions=torch.arange(frames,device=mixture_w.device);j=(positions+250)//125;r=positions%125
    output=output[:,:,r*chunks+j]+output[:,:,(r+125)*chunks+j-1]
    output=self.net_out(output)*self.net_gate(output)
    return self.output_act(self.mask_net(output)).reshape(1,2,64,frames)

def staged_forward(self,x):
    mean=x
    for dim in range(x.dim()-1,0,-1):mean=mean.mean(dim=dim,keepdim=True)
    centered=x-mean;variance=centered*centered
    for dim in range(x.dim()-1,0,-1):variance=variance.mean(dim=dim,keepdim=True)
    return self.apply_gain_and_bias(centered/torch.sqrt(variance+1e-8))

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','filterbanks','checkpoint','converter','output']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args()
    for name,expected in SOURCE_HASHES.items():
        kind,relative=name.split('/',1);parent=args.source if kind=='asteroid' else args.filterbanks
        if hashlib.sha256((parent/relative).read_bytes()).hexdigest()!=expected:raise ValueError('Original source differs: '+name)
    if hashlib.sha256(args.checkpoint.read_bytes()).hexdigest()!=CHECKPOINT_SHA:raise ValueError('Original checkpoint differs')
    torch.set_num_threads(2);model,checkpoint=load(args.checkpoint,args.source,args.filterbanks)
    protocol={'sample_rate':8000,'kernel_size':2,'stride':1,'n_filters':64,'chunk_size':250,'hop_size':125,'n_src':2,'bn_chan':128,'hid_size':128,'n_repeats':6,'bidirectional':True}
    if any(checkpoint['model_args'][key]!=value for key,value in protocol.items()):raise ValueError('Unexpected checkpoint protocol')
    args.output.mkdir(parents=True,exist_ok=True)
    class Export(torch.nn.Module):
        def __init__(self):super().__init__();self.model=model
        def forward(self,mixture):
            encoded=F.conv1d(mixture.unsqueeze(1),self.model.encoder.filterbank.filters(),stride=1)
            masked=self.model.apply_masks(encoded,self.model.forward_masker(encoded))
            output=F.conv_transpose1d(masked.reshape(2,64,-1),self.model.decoder.filterbank.filters(),stride=1).reshape(1,2,-1)
            return F.pad(output,(0,mixture.shape[1]-output.shape[2])).transpose(1,2)
    wrapper=Export().eval();rng=np.random.default_rng(20261030);cases=[]
    with torch.no_grad():
        for size in [2,64,125,126,127,8007,16013,32000]:
            x=rng.normal(0,.03,(1,size)).astype(np.float32)
            expected=model(torch.from_numpy(x.copy())).transpose(1,2).numpy();cases.append((x,expected))
        model.masker.forward=types.MethodType(chunked_forward,model.masker)
        for x,expected in cases:
            if not np.array_equal(wrapper(torch.from_numpy(x.copy())).numpy(),expected):raise ValueError('Chunk/rank lowering changed source waveform')
        count=0
        for layer in model.modules():
            if type(layer).__name__=='GlobLN':layer.forward=types.MethodType(staged_forward,layer);count+=1
        if count!=13:raise ValueError('Unexpected original GlobLN count: '+str(count))
        lowering=[dict(samples=x.shape[1],source_nrmse=nrmse(expected,wrapper(torch.from_numpy(x.copy())).numpy())) for x,expected in cases]
        onnx=args.output/'dprnn_dynamic.onnx'
        torch.onnx.export(wrapper,torch.zeros(1,8007),onnx,opset_version=17,dynamo=False,input_names=['mixture'],output_names=['separated'],dynamic_axes={'mixture':{1:'samples'},'separated':{1:'samples'}})
    options=ort.SessionOptions();options.intra_op_num_threads=2
    session=ort.InferenceSession(str(onnx),options,providers=['CPUExecutionProvider'])
    parity=[dict(samples=x.shape[1],source_nrmse=nrmse(expected,session.run(['separated'],{'mixture':x})[0])) for x,expected in cases]
    graph=args.output/'dprnn_native_fp32.mnn'
    command=[str(args.converter),'-f','ONNX','--modelFile',str(onnx),'--MNNModel',str(graph),'--bizCode','DPRNNWHAM8K','--optimizeLevel','1','--optimizePrefer','2','--useOriginRNNImpl']
    with (args.output/'conversion.log').open('w') as log:subprocess.run(command,check=True,stdout=log,stderr=subprocess.STDOUT)
    normalize_uuid(graph)
    profile=dict(repository='mpariente/DPRNNTasNet-ks2_WHAM_sepclean',revision='548f4f1c9476ed0af6b4bea3403247c71d5d7d46',asteroid_version='0.7.0',filterbanks_version='0.4.0',graph=graph.name,sample_rate=8000,speakers=2,input='mixture',output='separated',normalization='none; original forward_wav',minimum_samples=2,maximum_samples=32000)
    (args.output/'mnn_dprnn.json').write_text(json.dumps(profile,indent=2)+'\n')
    report=dict(source_hashes=SOURCE_HASHES,checkpoint_sha256=CHECKPOINT_SHA,model_args=checkpoint['model_args'],rank_chunk_fold_lowering='bit-identical to original forward',staged_normalization_lowering=lowering,onnx_parity=parity,graph_bytes=graph.stat().st_size,graph_sha256=hashlib.sha256(graph.read_bytes()).hexdigest(),converter_arguments=command,canonical_public_uuid=GRAPH_UUID.decode())
    (args.output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
