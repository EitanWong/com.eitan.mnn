#!/usr/bin/env python3
"""Reproduce pinned SpeechBrain ECAPA-TDNN FP32 MNN and frontend coefficients.

Use a local unmodified SpeechBrain1.0.3 source package and original checkpoint.
No downloads or native code changes are performed. Artifacts belong under TestArtifacts~/.
"""
from pathlib import Path
import argparse,ast,hashlib,importlib.util,json,logging,math,subprocess,sys,typing
import numpy as np
import torch
import torch.nn.functional as F
sys.dont_write_bytecode=True
CHECKPOINT_SHA256='0575cb64845e6b9a10db9bcb74d5ac32b326b8dc90352671d345e2ee3d0126a2'
REVISION='0f99f2d0ebe89ac095bcc5903c4dd8f72b367286'
GRAPH_UUID=b'42ae447f-2e84-4c6d-b60f-6dbe17b6b17a'
SOURCE_HASHES={'dataio/dataio.py': '11c75f1e91776905fd0d6ce33b49172eda6b0cb6acca1a2396a5cb4a556369f4', 'nnet/CNN.py': '3015222f2a305ea9cb0462c2e2bfadebff94eacf52dda32f29a061662734abe4', 'nnet/normalization.py': '60892f40f9298354d71aae491e7ecbb70061399a7ab01054355fec34b5db0846', 'nnet/linear.py': '86dd93dac701a8e79ff54e012531ba8b6330679eed3b16b63cf45e9354177f51', 'lobes/models/ECAPA_TDNN.py': 'd8fcb3fadbfecbb5f032fa8f6a5b16ae78c2a88dfcbfc92406d9f4daa9badff7', 'processing/features.py': 'af93cd8ca38d188e4d749426f3923a89b364253f78080a44326ba0f8c3f555cb', 'lobes/features.py': '6e2633d0a6f2c94f22b78b53a91b3ac1476bff8c3d6d99a5529eb4bec4d733f5'}

def verify_sources(checkpoint, source):
    if hashlib.sha256(checkpoint.read_bytes()).hexdigest()!=CHECKPOINT_SHA256:
        raise ValueError("expected pinned official ECAPA checkpoint")
    for name,digest in SOURCE_HASHES.items():
        if hashlib.sha256((source/name).read_bytes()).hexdigest()!=digest:
            raise ValueError("SpeechBrain1.0.3 source differs: "+name)

def selected(source,relative,names,namespace):
    path=source/relative
    tree=ast.parse(path.read_text())
    nodes=[n for n in tree.body if isinstance(n,(ast.ClassDef,ast.FunctionDef)) and n.name in names]
    assert set(n.name for n in nodes)==set(names),(relative,names)
    # Serialization hooks and the disabled CPU autocast decorator do not change forward arithmetic.
    for n in ast.walk(ast.Module(body=nodes,type_ignores=[])):
        if isinstance(n,(ast.FunctionDef,ast.ClassDef)):
            kept=[]
            for decorator in n.decorator_list:
                name=decorator.id if isinstance(decorator,ast.Name) else decorator.func.id if isinstance(decorator,ast.Call) and isinstance(decorator.func,ast.Name) else None
                if name in ['staticmethod','classmethod','property']:kept.append(decorator)
                elif name not in ['mark_as_loader','mark_as_saver','mark_as_transfer','register_checkpoint_hooks','fwd_default_precision']:raise ValueError('unhandled decorator '+ast.dump(decorator))
            n.decorator_list=kept
    exec(compile(ast.Module(body=nodes,type_ignores=[]),str(path),'exec'),namespace)
    return namespace

def load(checkpoint, source):
    verify_sources(checkpoint, source)
    ns={'torch':torch,'nn':torch.nn,'F':F,'math':math,'np':np,'Tuple':typing.Tuple,'Optional':typing.Optional,'Union':typing.Union,'logger':logging.getLogger('ecapa_reference')}
    selected(source,'dataio/dataio.py',['length_to_mask'],ns)
    cn=selected(source,'nnet/CNN.py',['Conv1d','get_padding_elem'],dict(ns))
    bn=selected(source,'nnet/normalization.py',['BatchNorm1d'],dict(ns))
    linear=selected(source,'nnet/linear.py',['Linear'],dict(ns))
    ns.update(_Conv1d=cn['Conv1d'],_BatchNorm1d=bn['BatchNorm1d'],Linear=linear['Linear'])
    tree=ast.parse((source/'lobes/models/ECAPA_TDNN.py').read_text())
    exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,(ast.ClassDef,ast.FunctionDef))],type_ignores=[]),str(source/'lobes/models/ECAPA_TDNN.py'),'exec'),ns)
    model=ns['ECAPA_TDNN'](input_size=80,channels=[1024,1024,1024,1024,3072],kernel_sizes=[5,3,3,3,1],dilations=[1,2,3,4,1],attention_channels=128,lin_neurons=192).eval()
    ckpt=checkpoint
    assert hashlib.sha256(ckpt.read_bytes()).hexdigest()=='0575cb64845e6b9a10db9bcb74d5ac32b326b8dc90352671d345e2ee3d0126a2'
    print(model.load_state_dict(torch.load(ckpt,map_location='cpu',weights_only=True),strict=True),flush=True)
    fs=dict(ns);fs['FilterProperties']=object
    selected(source,'processing/features.py',['STFT','spectral_magnitude','Filterbank','Deltas','ContextWindow','InputNormalization'],fs)
    selected(source,'lobes/features.py',['Fbank'],fs)
    frontend=fs['Fbank'](n_mels=80).eval();normalizer=fs['InputNormalization'](norm_type='sentence',std_norm=False).eval()
    return model,frontend,normalizer

def verify_onnx(model, path):
    import onnxruntime as ort
    options=ort.SessionOptions();options.intra_op_num_threads=2
    session=ort.InferenceSession(str(path),options,providers=['CPUExecutionProvider'])
    rng=np.random.default_rng(20261030);rows=[]
    with torch.no_grad():
        for frames in [5,26,101,372,801]:
            x=rng.normal(0,1,(1,frames,80)).astype(np.float32)
            expected=model(torch.from_numpy(x.copy())).numpy().reshape(-1)
            actual=session.run(['embedding'],{'x':x})[0].reshape(-1)
            expected/=np.linalg.norm(expected);actual/=np.linalg.norm(actual)
            error=float(np.linalg.norm(actual-expected))
            if not np.isfinite(error) or error>.0001:raise ValueError('ONNX unit-vector mismatch: '+str(error))
            rows.append({'frames':frames,'unit_embedding_l2':error})
    return rows

def normalize_uuid(path):
    # Public serialized Net.mnn_uuid only; operators/weights and native source are untouched.
    source=Path(__file__).resolve().parents[1]/'deduplicate_mnn_external_weights.py'
    spec=importlib.util.spec_from_file_location('mnn_schema',source)
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    model=module.Model(path.read_bytes());field=model.field(model.integer(0),11)
    if not field:raise ValueError('missing public Net.mnn_uuid')
    position=field+model.integer(field)
    if model.integer(position)!=len(GRAPH_UUID):raise ValueError('unexpected UUID length')
    model.data[position+4:position+4+len(GRAPH_UUID)]=GRAPH_UUID;path.write_bytes(model.data)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','checkpoint','converter','output']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args();torch.set_num_threads(2)
    model,frontend,normalizer=load(args.checkpoint,args.source)
    args.output.mkdir(parents=True,exist_ok=True);onnx=args.output/'ecapa_dynamic.onnx'
    torch.onnx.export(model,torch.zeros(1,327,80),onnx,opset_version=17,dynamo=False,
        input_names=['x'],output_names=['embedding'],dynamic_axes={'x':{1:'frames'}})
    parity=verify_onnx(model,onnx);graph=args.output/'ecapa_fp32.mnn'
    command=[str(args.converter),'-f','ONNX','--modelFile',str(onnx),'--MNNModel',str(graph),'--bizCode','ECAPA-TDNN',
        '--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0']
    with (args.output/'conversion.log').open('w') as log:subprocess.run(command,check=True,stdout=log,stderr=subprocess.STDOUT)
    normalize_uuid(graph)
    filters=frontend.compute_fbanks
    config={'repository':'speechbrain/spkrec-ecapa-voxceleb','source_revision':REVISION,'graph':graph.name,'graph_fp16':'',
        'frontend':'speechbrain-1.0.3-fbank80-sentence-cmn','analysis_window':frontend.compute_STFT.window.tolist(),
        'mel_centers':filters.f_central.tolist(),'mel_bands':filters.band.tolist()}
    (args.output/'mnn_speaker.json').write_text(json.dumps(config,indent=2)+'\n')
    report={'checkpoint_sha256':CHECKPOINT_SHA256,'speechbrain_version':'1.0.3','source_hashes':SOURCE_HASHES,
        'onnx_parity':parity,'graph_sha256':hashlib.sha256(graph.read_bytes()).hexdigest(),'graph_bytes':graph.stat().st_size,
        'converter_arguments':command,'canonical_uuid':GRAPH_UUID.decode()}
    (args.output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report,indent=2))

if __name__=='__main__':main()
