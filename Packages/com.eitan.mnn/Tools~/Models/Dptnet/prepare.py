#!/usr/bin/env python3
"""Reproduce pinned DPTNet Libri1Mix16k with local original sources/checkpoint and official MNNConvert."""
from pathlib import Path
import argparse,hashlib,importlib.util,json,subprocess,sys,types,uuid
import numpy as np
import torch
import torch.nn.functional as F
import onnxruntime as ort
from original import load
sys.dont_write_bytecode=True
SOURCE_HASHES={'asteroid/utils/torch_utils.py': 'd31c58dacf8a768b6de7428e8634a0a87a330c7c7d4833f1bd37e43957674d0c', 'asteroid/masknn/activations.py': '586710e02a9b7990f38cdaee3fa3d6c5f8cbaba733a294d706c8208a3a950b91', 'asteroid/masknn/norms.py': '80bc9d54d9bbae5b3a110516cb927b5fc9a8ff72e10a257acb6ed4569c1e605b', 'asteroid/models/base_models.py': 'a88d38b4acb3b5ee5cbbd3122f5db2639109890f53670ac2ae36dc8b6b50bd5b', 'asteroid/masknn/attention.py': '887f38af89deb6945c9ab5c480053e022f9c470676ac1c6b44fc8a0903aca746', 'asteroid/dsp/overlap_add.py': 'dd1e8cb2ce83b252ce7d86999937be7d3ecce57e5370b98f54258c8130a7a81a', 'asteroid/models/dptnet.py': '0551f16e1aa86d6f0430dcb836d3273aedc80aa9de9b856b36aee97852ffd057', 'filterbanks/scripting.py': '04752287c9b6ef73c621e0d8fdf28d81cf8a28726432ab378fefbe8eb170bb8c', 'filterbanks/enc_dec.py': '4b385912861a60c6ebcadc170a3ef747637952e03ab6eab303c523788cb3beee', 'filterbanks/free_fb.py': '90bfab19257287781bef6c45aef3a6379d4369f13d8fa848b461716e98d4fc16', 'filterbanks/__init__.py': '84589b2b47ca3cd3cca538a5507b108670d29ae456956c589d9c1f457f7f0244'}
CHECKPOINT_SHA="0f4f38dc0be2bcb479364b4b49fdc0c92d77fc3f1aa6049090cd3ea0db95019f"

def chunked(self,mixture_w):
 assert self.input_layer is None
 frames=mixture_w.shape[2];x=self.in_norm(mixture_w);chunks=(frames+100)//50+1
 indices=(torch.arange(chunks,device=x.device)*50).unsqueeze(1)+torch.arange(100,device=x.device).unsqueeze(0)
 x=F.pad(x,(100,100))[:,:,indices.reshape(-1)].reshape(1,64,chunks,100).permute(0,1,3,2)
 for intra,inter in self.layers:
  x=self.ola.intra_process(x,intra);x=self.ola.inter_process(x,inter)
 x=self.first_out(x).reshape(1,64,100,chunks).reshape(1,64,100*chunks)
 positions=torch.arange(frames,device=x.device);j=(positions+100)//50;r=positions%50
 x=(x[:,:,r*chunks+j]+x[:,:,(r+50)*chunks+j-1])/2.0
 x=self.net_out(x)*self.net_gate(x)
 return self.output_act(x.reshape(1,1,64,-1))

def dynamic_attention(self,query,key,value,**kwargs):
 seq,batch,channels=query.shape
 q,k,v=F.linear(query,self.in_proj_weight,self.in_proj_bias).chunk(3,dim=-1)
 def heads(x):return x.reshape(seq,batch,4,16).permute(1,2,0,3)
 q,k,v=heads(q),heads(k),heads(v)
 weights=torch.softmax(torch.matmul(q*.25,k.transpose(-2,-1)),dim=-1)
 out=torch.matmul(weights,v).permute(2,0,1,3).reshape(seq,batch,64)
 return F.linear(out,self.out_proj.weight,self.out_proj.bias),None

def staged(self,x):
 mean=x
 for dim in range(x.dim()-1,0,-1):mean=mean.mean(dim=dim,keepdim=True)
 centered=x-mean;var=centered*centered
 for dim in range(x.dim()-1,0,-1):var=var.mean(dim=dim,keepdim=True)
 return self.apply_gain_and_bias(centered/torch.sqrt(var+1e-8))

def error(expected,actual):
    energy=np.linalg.norm(expected);value=float(np.linalg.norm(actual-expected)/(energy if energy else 1))
    if not np.isfinite(value) or value>=.001:raise ValueError('Original waveform differs: '+str(value))
    return value

def canonical_uuid(path):
    spec=importlib.util.spec_from_file_location('mnn_schema',Path(__file__).resolve().parents[1]/'deduplicate_mnn_external_weights.py');schema=importlib.util.module_from_spec(spec);spec.loader.exec_module(schema)
    model=schema.Model(path.read_bytes());field=model.field(model.integer(0),11)
    if not field:raise ValueError('Missing public Net.mnn_uuid')
    position=field+model.integer(field)
    if model.integer(position)!=36:raise ValueError('Invalid public UUID')
    identity=str(uuid.uuid5(uuid.NAMESPACE_URL,'JorisCos/DPTNet_Libri1Mix_enhsingle_16k/935f441c53e44d40ca0cf138f71e850defc8bea5')).encode();model.data[position+4:position+40]=identity;path.write_bytes(model.data)
    return identity.decode()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','filterbanks','checkpoint','converter','output']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args()
    for name,expected in SOURCE_HASHES.items():
        kind,relative=name.split('/',1);parent=args.source if kind=='asteroid' else args.filterbanks
        if hashlib.sha256((parent/relative).read_bytes()).hexdigest()!=expected:raise ValueError('Original source differs: '+name)
    if hashlib.sha256(args.checkpoint.read_bytes()).hexdigest()!=CHECKPOINT_SHA:raise ValueError('Original checkpoint differs')
    args.output.mkdir(parents=True,exist_ok=True);torch.set_num_threads(2);model,checkpoint=load(args.checkpoint,args.source,args.filterbanks)
    protocol=dict(sample_rate=16000,kernel_size=16,stride=8,n_filters=64,n_src=1,n_repeats=2,chunk_size=100,hop_size=50,n_heads=4,encoder_activation='relu',mask_act='sigmoid',bidirectional=True)
    if any(checkpoint['model_args'][key]!=value for key,value in protocol.items()):raise ValueError('Unexpected original checkpoint protocol')
    class Export(torch.nn.Module):
        def __init__(self):super().__init__();self.model=model
        def forward(self,mixture):
            encoded=self.model.enc_activation(F.conv1d(mixture.unsqueeze(1),self.model.encoder.filterbank.filters(),stride=8))
            masked=self.model.apply_masks(encoded,self.model.forward_masker(encoded))
            output=F.conv_transpose1d(masked.reshape(1,64,-1),self.model.decoder.filterbank.filters(),stride=8).reshape(1,1,-1)
            return F.pad(output,(0,mixture.shape[1]-output.shape[2])).transpose(1,2)
    wrapper=Export().eval();rng=np.random.default_rng(20261031);cases=[]
    with torch.no_grad():
        for length in [16,64,807,808,809,8007,40013,48000]:
            x=rng.normal(0,.03,(1,length)).astype(np.float32);expected=model(torch.from_numpy(x.copy())).transpose(1,2).numpy();cases.append((x,expected))
        model.masker.forward=types.MethodType(chunked,model.masker)
        for x,expected in cases:
            if not np.array_equal(wrapper(torch.from_numpy(x.copy())).numpy(),expected):raise ValueError('Rank/chunk/fold lowering changes original waveform')
        attention_count=0;norm_count=0
        for layer in model.modules():
            if isinstance(layer,torch.nn.MultiheadAttention):layer.forward=types.MethodType(dynamic_attention,layer);attention_count+=1
            if type(layer).__name__=='GlobLN':layer.forward=types.MethodType(staged,layer);norm_count+=1
        if (attention_count,norm_count)!=(4,9):raise ValueError('Unexpected original layer counts')
        lowering=[dict(samples=x.shape[1],source_nrmse=error(expected,wrapper(torch.from_numpy(x.copy())).numpy())) for x,expected in cases]
        onnx=args.output/'dptnet_dynamic.onnx';torch.onnx.export(wrapper,torch.zeros(1,8007),onnx,opset_version=17,dynamo=False,input_names=['mixture'],output_names=['enhanced'],dynamic_axes={'mixture':{1:'samples'},'enhanced':{1:'samples'}})
    options=ort.SessionOptions();options.intra_op_num_threads=2;session=ort.InferenceSession(str(onnx),options,providers=['CPUExecutionProvider'])
    parity=[dict(samples=x.shape[1],source_nrmse=error(expected,session.run(['enhanced'],dict(mixture=x))[0])) for x,expected in cases]
    graph=args.output/'dptnet_native_fp32.mnn';command=[str(args.converter),'-f','ONNX','--modelFile',str(onnx),'--MNNModel',str(graph),'--bizCode','DPTNetLibri1Mix16K','--optimizeLevel','1','--optimizePrefer','2','--useOriginRNNImpl','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']
    with (args.output/'conversion.log').open('w') as log:subprocess.run(command,check=True,stdout=log,stderr=subprocess.STDOUT)
    identity=canonical_uuid(graph)
    profile=dict(repository='JorisCos/DPTNet_Libri1Mix_enhsingle_16k',revision='935f441c53e44d40ca0cf138f71e850defc8bea5',asteroid_version='0.7.0',checkpoint_asteroid_version='0.4.1rc0',filterbanks_version='0.4.0',graph=graph.name,sample_rate=16000,speakers=1,input='mixture',output='enhanced',normalization='none; original forward_wav',minimum_samples=16,maximum_samples=48000)
    (args.output/'mnn_dptnet.json').write_text(json.dumps(profile,indent=2)+'\n')
    report=dict(source_hashes=SOURCE_HASHES,checkpoint_sha256=CHECKPOINT_SHA,model_args=checkpoint['model_args'],framework=checkpoint['infos'],rank_chunk_fold_lowering='bit-identical across8lengths',dynamic_attention_layers=attention_count,staged_global_norm_layers=norm_count,lowering=lowering,onnx_parity=parity,graph_bytes=graph.stat().st_size,graph_sha256=hashlib.sha256(graph.read_bytes()).hexdigest(),canonical_public_uuid=identity,converter_arguments=command)
    (args.output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
