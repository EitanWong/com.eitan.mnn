#!/usr/bin/env python3
"""Reproduce the pinned original SpeechBrain WSJ02Mix SepFormer dynamic graph.

Uses existing local source and checkpoints. No network, compiled torchaudio,
native additions or upstream source modifications are required.
"""
from pathlib import Path
import argparse, ast, copy, hashlib, importlib.util, json, math, subprocess, sys, types, typing
import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F
import onnxruntime as ort
sys.dont_write_bytecode=True
REVISION='3a2826343a10e2d2e8a75f79aeab5ff3a2473531'
GRAPH_UUID=b'3fbd1737-8b40-42c2-9f2a-ff0e46ea3351'
CHECKPOINTS={
    'encoder':'3139bb880b29ea77ae8a168b8f2ad6e8eb5c2c0904289676c223d0e93cd2a35d',
    'decoder':'abea1a2d41151331b4c36071d1b3205aed940a189721f008b12a703e9c63e7e4',
    'masknet':'57dd5f49bf21c5a2101bb4e46648d05d34d517a59e26f0b06646d0bebe8214c7'}
SOURCE_HASHES={'lobes/models/dual_path.py': 'd17e7d77fc0f2ed9bca1a0e8c793dff386e406c76a293ac3b92b69eb3c3ba63d', 'lobes/models/transformer/Transformer.py': '77e4b1405ce8e6f4addde0fb6b2fe41f7c092d8f2aa520471c70ac4a78d1783e', 'nnet/attention.py': '2a0a6d1b3839287e3d0820858c73a770c1f6e373d10afca4c47b621d8e5dc40f', 'nnet/normalization.py': '60892f40f9298354d71aae491e7ecbb70061399a7ab01054355fec34b5db0846', 'inference/separation.py': 'c1e8d5b7dce625c21d1e70a77a3044a0a2202c89ca53dceaaff0588811490d95'}

def selected(source,relative,names,namespace):
    path=source/relative;tree=ast.parse(path.read_text())
    nodes=[n for n in tree.body if isinstance(n,(ast.ClassDef,ast.FunctionDef)) and n.name in names]
    if {n.name for n in nodes}!=set(names):raise ValueError('Missing original definitions: '+relative)
    exec(compile(ast.Module(body=nodes,type_ignores=[]),str(path),'exec'),namespace)
    return namespace

def load(args):
    for name,digest in SOURCE_HASHES.items():
        if hashlib.sha256((args.source/name).read_bytes()).hexdigest()!=digest:
            raise ValueError('SpeechBrain1.0.3 source differs: '+name)
    for name,digest in CHECKPOINTS.items():
        if hashlib.sha256((args.checkpoints/(name+'.ckpt')).read_bytes()).hexdigest()!=digest:
            raise ValueError('Expected pinned original checkpoint: '+name)
    ns=dict(torch=torch,nn=nn,F=F,np=np,math=math,copy=copy,Optional=typing.Optional,
            Tuple=typing.Tuple,Callable=typing.Callable,Any=typing.Any,Dict=typing.Dict)
    attention=selected(args.source,'nnet/attention.py',['MultiheadAttention','PositionalwiseFeedForward'],dict(ns))
    norm=selected(args.source,'nnet/normalization.py',['LayerNorm'],dict(ns))
    ns['sb']=types.SimpleNamespace(nnet=types.SimpleNamespace(attention=types.SimpleNamespace(
        MultiheadAttention=attention['MultiheadAttention'],PositionalwiseFeedForward=attention['PositionalwiseFeedForward']),
        normalization=types.SimpleNamespace(LayerNorm=norm['LayerNorm'])))
    selected(args.source,'lobes/models/transformer/Transformer.py',['PositionalEncoding','TransformerEncoderLayer','TransformerEncoder'],ns)
    selected(args.source,'lobes/models/dual_path.py',['GlobalLayerNorm','CumulativeLayerNorm','select_norm','Encoder','Decoder',
        'SBTransformerBlock','Dual_Computation_Block','Dual_Path_Model'],ns)
    block=lambda:ns['SBTransformerBlock'](8,256,8,1024,dropout=0,use_positional_encoding=True,norm_before=True)
    encoder=ns['Encoder'](kernel_size=16,out_channels=256)
    masknet=ns['Dual_Path_Model'](256,256,block(),block(),num_layers=2,K=250,num_spks=2,norm='ln',
        linear_layer_after_inter_intra=False,skip_around_intra=True)
    decoder=ns['Decoder'](in_channels=256,out_channels=1,kernel_size=16,stride=8,bias=False)
    for name,model in [('encoder',encoder),('masknet',masknet),('decoder',decoder)]:
        print(name,model.load_state_dict(torch.load(args.checkpoints/(name+'.ckpt'),map_location='cpu',weights_only=True),strict=True),flush=True)
        model.eval()
    ns['Pretrained']=object;selected(args.source,'inference/separation.py',['SepformerSeparation'],ns)
    original=object.__new__(ns['SepformerSeparation']);original.mods=types.SimpleNamespace(encoder=encoder,masknet=masknet,decoder=decoder)
    original.hparams=types.SimpleNamespace(num_spks=2);original.device='cpu'
    class Separate(nn.Module):
        def __init__(self):super().__init__();self.encoder=encoder;self.masknet=masknet;self.decoder=decoder
        def forward(self,mixture):
            encoded=self.encoder(mixture);masks=self.masknet(encoded)
            waves=torch.cat([self.decoder(encoded*masks[i]).unsqueeze(-1) for i in range(2)],dim=-1)
            return F.pad(waves,(0,0,0,mixture.shape[1]-waves.shape[1]))
    return original,Separate().eval()

def normalize_uuid(path):
    spec=importlib.util.spec_from_file_location('schema',Path(__file__).resolve().parents[1]/'deduplicate_mnn_external_weights.py')
    schema=importlib.util.module_from_spec(spec);spec.loader.exec_module(schema)
    model=schema.Model(path.read_bytes());field=model.field(model.integer(0),11)
    if not field:raise ValueError('Missing public serialized Net.mnn_uuid')
    position=field+model.integer(field)
    if model.integer(position)!=len(GRAPH_UUID):raise ValueError('Unexpected public UUID length')
    model.data[position+4:position+4+len(GRAPH_UUID)]=GRAPH_UUID;path.write_bytes(model.data)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','checkpoints','converter','output']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args();torch.set_num_threads(2);original,model=load(args)
    args.output.mkdir(parents=True,exist_ok=True);cases=[];rng=np.random.default_rng(20261030)
    with torch.no_grad():
        for size in [16,512,1992,2000,2008,8007,16013,80000]:
            x=rng.normal(0,.03,(1,size)).astype(np.float32);expected=original.separate_batch(torch.from_numpy(x.copy())).numpy()
            if not np.array_equal(model(torch.from_numpy(x.copy())).numpy(),expected):raise ValueError('Wrapper changed original separation')
            cases.append((x,expected))
        torch.backends.mha.set_fastpath_enabled(False)
        for x,expected in cases:
            if not np.array_equal(model(torch.from_numpy(x.copy())).numpy(),expected):raise ValueError('MHA export lowering changed original output')
        onnx=args.output/'sepformer_dynamic.onnx'
        torch.onnx.export(model,torch.zeros(1,8007),onnx,opset_version=17,dynamo=False,
            input_names=['mixture'],output_names=['separated'],dynamic_axes={'mixture':{1:'samples'},'separated':{1:'samples'}})
    options=ort.SessionOptions();options.intra_op_num_threads=2
    session=ort.InferenceSession(str(onnx),options,providers=['CPUExecutionProvider']);rows=[]
    for x,expected in cases:
        actual=session.run(['separated'],{'mixture':x})[0];errors=[]
        for speaker in range(2):
            energy=np.linalg.norm(expected[:,:,speaker]);error=float(np.linalg.norm(actual[:,:,speaker]-expected[:,:,speaker])/(energy if energy else 1))
            if not np.isfinite(error) or error>.001:raise ValueError('ONNX source mismatch: '+str((x.shape[1],speaker,error)))
            errors.append(error)
        rows.append(dict(samples=x.shape[1],source_nrmse=errors));print(rows[-1],flush=True)
    graph=args.output/'sepformer_fp32.mnn'
    command=[str(args.converter),'-f','ONNX','--modelFile',str(onnx),'--MNNModel',str(graph),'--bizCode','SepFormerWSJ02Mix',
        '--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0','--transformerFuseQkvProj','0',
        '--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']
    with (args.output/'conversion.log').open('w') as log:subprocess.run(command,check=True,stdout=log,stderr=subprocess.STDOUT)
    normalize_uuid(graph)
    profile=dict(repository='speechbrain/sepformer-wsj02mix',revision=REVISION,speechbrain_version='1.0.3',graph=graph.name,
        sample_rate=8000,speakers=2,input='mixture',output='separated',normalization='none',minimum_samples=16,maximum_samples=80000)
    (args.output/'mnn_sepformer.json').write_text(json.dumps(profile,indent=2)+'\n')
    report=dict(source_hashes=SOURCE_HASHES,checkpoint_hashes=CHECKPOINTS,onnx_parity=rows,graph_bytes=graph.stat().st_size,
        graph_sha256=hashlib.sha256(graph.read_bytes()).hexdigest(),converter_arguments=command,canonical_public_uuid=GRAPH_UUID.decode())
    (args.output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
