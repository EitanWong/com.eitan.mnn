#!/usr/bin/env python3
"""Reproduce original MossFormer2 SE 48 kHz dynamic FP32 graph and frontend buffers.

Requires local frozen ClearerVoice source, checkpoint and pure-Torch Kaldi source.
Does not download files, compile native code or modify upstream sources.
"""
from pathlib import Path
import argparse, hashlib, importlib, importlib.util, json, subprocess, sys, types
import numpy as np
import torch
import onnx
import onnxruntime as ort
from rotary_embedding_torch import RotaryEmbedding
sys.dont_write_bytecode = True
CHECKPOINT_SHA256 = '03692b9f773bbd6bb43b9c5a41f96b1e28affd66e13796b7bec66ad3d8b227c6'
SOURCE_REVISION = '6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61'
KALDI_SHA256 = '5cbea1a584ddea748f6f68a621d794e13334e88d9faa1d40986f7af32f196d29'
GRAPH_UUID = b'922f309e-69e4-44de-89b1-e5a0f89bd07c'
SOURCE_HASHES = {'models/mossformer2_se/conv_module.py': 'cd16d84a55ed22a234d91871d685a7ae97fa3fbca3f0d94bd065ce0b4c9a1180', 'models/mossformer2_se/fsmn.py': '62c376b2708ac1c59853c13cbe9acb7ea011082c39f1a42e1af0f8213a00bb76', 'models/mossformer2_se/layer_norm.py': 'd28fea41b3c5a2dd8bb7a1a8c1c97492fcc7eb88cc7b46283fc21e4dbf19f062', 'models/mossformer2_se/mossformer2.py': '528b7d6078c8ac99806a2baefe2dc707a2d9ddc6595abe95e4084b92b397b94b', 'models/mossformer2_se/mossformer2_block.py': '8b4f1714156365d9f1e5d96f750793f47d92f51c14d0afd00d43055cd78bdee7', 'models/mossformer2_se/mossformer2_se_wrapper.py': '85e71167a9025a137f9ca8d314c3da4a34cd3285dc856d51352050ecc56e4dda'}

def load(args):
    if hashlib.sha256(args.checkpoint.read_bytes()).hexdigest() != CHECKPOINT_SHA256:
        raise ValueError('Expected original pinned MossFormer2 SE checkpoint')
    for name, digest in SOURCE_HASHES.items():
        if hashlib.sha256((args.source/name).read_bytes()).hexdigest() != digest:
            raise ValueError('ClearerVoice source differs: '+name)
    if hashlib.sha256(args.kaldi_source.read_bytes()).hexdigest() != KALDI_SHA256:
        raise ValueError('Expected pinned torchaudio2.8 pure-PyTorch Kaldi source')
    for name,path in [('clearvoice',args.source),('clearvoice.models',args.source/'models'),
                      ('clearvoice.models.mossformer2_se',args.source/'models/mossformer2_se')]:
        package=types.ModuleType(name);package.__path__=[str(path)];sys.modules[name]=package
    original=importlib.import_module('clearvoice.models.mossformer2_se.mossformer2_se_wrapper')
    model=original.MossFormer2_SE_48K(types.SimpleNamespace()).model.eval()
    state=torch.load(args.checkpoint,map_location='cpu',weights_only=True);state=state.get('model',state)
    print(model.load_state_dict({k.removeprefix('module.'):v for k,v in state.items()},strict=True),flush=True)
    class Core(torch.nn.Module):
        def __init__(self):super().__init__();self.net=model
        def forward(self,features):return self.net(features)[-1]
    return Core().eval()

def normalize_uuid(path):
    spec=importlib.util.spec_from_file_location('mnn_schema',Path(__file__).resolve().parents[1]/'deduplicate_mnn_external_weights.py')
    schema=importlib.util.module_from_spec(spec);spec.loader.exec_module(schema)
    model=schema.Model(path.read_bytes());field=model.field(model.integer(0),11)
    if not field:raise ValueError('Missing public Net.mnn_uuid')
    position=field+model.integer(field)
    if model.integer(position)!=len(GRAPH_UUID):raise ValueError('Unexpected public UUID length')
    model.data[position+4:position+4+len(GRAPH_UUID)]=GRAPH_UUID;path.write_bytes(model.data)

def profile(args,graph):
    # Only pure-Torch mel-bank/window functions are used; compiled torchaudio is unnecessary.
    sys.modules['torchaudio']=types.ModuleType('torchaudio')
    spec=importlib.util.spec_from_file_location('official_kaldi',args.kaldi_source)
    kaldi=importlib.util.module_from_spec(spec);spec.loader.exec_module(kaldi)
    window=torch.hamming_window(1920,periodic=False).numpy()
    filters,_=kaldi.get_mel_banks(60,2048,48000,20,0,100,-500,1)
    filters=filters.numpy().reshape(-1)
    coefficients=np.concatenate([window,filters]).astype('<f4')
    return dict(analysis_window=window.tolist(),mel_filters=filters.tolist(),
        coefficient_sha256=hashlib.sha256(coefficients.tobytes()).hexdigest(),
        repository='alibabasglab/MossFormer2_SE_48K',revision='eff8c97925c8bec812af707814b3e5d777fd4503',
        source_revision=SOURCE_REVISION,graph=graph.name,sample_rate=48000,fft_size=1920,hop_size=384,
        mel_fft_size=2048,mel_bins=60,dither=1.0,dither_seed=20261030)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','checkpoint','kaldi-source','converter','output']:
        parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args();torch.set_num_threads(2);model=load(args)
    args.output.mkdir(parents=True,exist_ok=True)
    rng=np.random.default_rng(20261030);cases=[]
    with torch.no_grad():
        for frames in [1,7,40,125,250,500,1246]:
            x=rng.normal(0,3,(1,frames,180)).astype(np.float32);x[:,:,60:]*=.1
            expected=model(torch.from_numpy(x.copy())).numpy();cases.append((x,expected))
        for layer in model.modules():
            if isinstance(layer,RotaryEmbedding):layer.cache_if_possible=False
        for x,expected in cases:
            if not np.array_equal(model(torch.from_numpy(x.copy())).numpy(),expected):
                raise ValueError('Disabling rotary cache changed original math')
        path=args.output/'mossformer2_se_core.onnx'
        torch.onnx.export(model,torch.zeros(1,125,180),path,opset_version=17,dynamo=False,
            input_names=['features'],output_names=['mask'],dynamic_axes={'features':{1:'frames'},'mask':{1:'frames'}})
    spec=importlib.util.spec_from_file_location('padding',Path(__file__).resolve().parents[1]/'canonicalize_onnx_padding.py')
    padding=importlib.util.module_from_spec(spec);spec.loader.exec_module(padding)
    graph=onnx.load(path);count=padding.canonicalize(graph)
    if count!=24:raise ValueError('Expected exactly24 negative Pad nodes')
    canonical=args.output/'mossformer2_se_core_canonical.onnx';onnx.save(graph,canonical)
    options=ort.SessionOptions();options.intra_op_num_threads=2
    session=ort.InferenceSession(str(canonical),options,providers=['CPUExecutionProvider']);parity=[]
    for x,expected in cases:
        actual=session.run(['mask'],{'features':x})[0]
        error=float(np.linalg.norm(actual-expected)/np.linalg.norm(expected))
        if not np.isfinite(error) or error>.001:raise ValueError('ONNX mismatch: '+str(error))
        parity.append(dict(frames=x.shape[1],nrmse=error));print(parity[-1],flush=True)
    graph=args.output/'mossformer2_se_fp32.mnn'
    command=[str(args.converter),'-f','ONNX','--modelFile',str(canonical),'--MNNModel',str(graph),
        '--bizCode','MossFormer2SE48K','--optimizeLevel','1','--optimizePrefer','2',
        '--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']
    with (args.output/'conversion.log').open('w') as log:subprocess.run(command,check=True,stdout=log,stderr=subprocess.STDOUT)
    normalize_uuid(graph)
    (args.output/'mnn_mossformer2_se.json').write_text(json.dumps(profile(args,graph),indent=2)+'\n')
    report=dict(checkpoint_sha256=CHECKPOINT_SHA256,source_revision=SOURCE_REVISION,source_hashes=SOURCE_HASHES,
        kaldi_sha256=KALDI_SHA256,onnx_parity=parity,negative_pads_canonicalized=count,
        graph_bytes=graph.stat().st_size,graph_sha256=hashlib.sha256(graph.read_bytes()).hexdigest(),
        converter_arguments=command,canonical_public_uuid=GRAPH_UUID.decode())
    (args.output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
