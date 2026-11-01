#!/usr/bin/env python3
"""Reproduce pinned SpEx+ split graphs against the original author model."""
from pathlib import Path
import argparse,hashlib,importlib.util,json,subprocess,uuid
import numpy as np
import onnxruntime as ort
import torch
from graph import Reference,Extraction,lower
from original import load,source_forward,REVISION,SOURCE_REVISION,CHECKPOINT_SHA

def nrmse(actual,expected):
    if actual.shape!=expected.shape:raise ValueError('Source shape differs')
    error=float(np.linalg.norm(actual-expected)/(np.linalg.norm(expected) or 1.))
    if not np.isfinite(error) or error>=.001:raise ValueError('Original source error: '+str(error))
    return error

def normalize_uuid(path):
    spec=importlib.util.spec_from_file_location('schema',Path(__file__).resolve().parents[1]/'deduplicate_mnn_external_weights.py')
    schema=importlib.util.module_from_spec(spec);spec.loader.exec_module(schema)
    value=schema.Model(path.read_bytes());field=value.field(value.integer(0),11)
    if not field:raise ValueError('Missing public Net.mnn_uuid')
    position=field+value.integer(field)
    if value.integer(position)!=36:raise ValueError('Invalid UUID')
    identity=str(uuid.uuid5(uuid.NAMESPACE_URL,'alibabasglab/SpExPlus-WSJ02Mix-8K/'+REVISION+'/'+path.name))
    value.data[position+4:position+40]=identity.encode();path.write_bytes(value.data);return identity

def convert(converter,onnx,graph):
    command=[str(converter),'-f','ONNX','--modelFile',str(onnx),'--MNNModel',str(graph),'--bizCode','SPEXPLUS','--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']
    with graph.with_suffix('.log').open('w') as log:subprocess.run(command,check=True,stdout=log,stderr=subprocess.STDOUT)
    identity=normalize_uuid(graph)
    return dict(arguments=command,uuid=identity,bytes=graph.stat().st_size,sha256=hashlib.sha256(graph.read_bytes()).hexdigest())

class Graph:
    def __init__(self,path):
        import MNN
        self.model=MNN.Interpreter(str(path));self.session=self.model.createSession(dict(backend='CPU',numThread=2,precision='high'))
    def run(self,inputs,output):
        import MNN
        for name,value in inputs.items():self.model.resizeTensor(self.model.getSessionInput(self.session,name),value.shape)
        self.model.resizeSession(self.session)
        for name,value in inputs.items():self.model.getSessionInput(self.session,name).copyFrom(MNN.Tensor(value.shape,MNN.Halide_Type_Float,value.copy(),MNN.Tensor_DimensionType_Caffe))
        if self.model.runSession(self.session)!=0:raise RuntimeError('MNN run failed')
        tensor=self.model.getSessionOutput(self.session,output);host=MNN.Tensor(tensor.getShape(),MNN.Halide_Type_Float,np.zeros(tensor.getShape(),np.float32),MNN.Tensor_DimensionType_Caffe);tensor.copyToHostTensor(host)
        return np.asarray(host.getData(),np.float32).reshape(tensor.getShape())

def profile():
    return dict(repository='alibabasglab/log_wsj0-2mix_speech_SpEx-plus_2spk',revision=REVISION,source_revision=SOURCE_REVISION,
                reference_graph='spexplus_reference_fp32.mnn',audio_graph='spexplus_audio_fp32.mnn',sample_rate=8000,embedding_size=256,
                minimum_samples=20,maximum_samples=32000,minimum_reference_samples=280,maximum_reference_samples=64000,
                reference_input='reference',reference_output='embedding',audio_input='mixture',audio_output='target',
                normalization='none; unpadded batch one; unnormalized reference embedding',causal=False)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','checkpoint','converter','output']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True);torch.set_num_threads(2)
    model,config=load(args.source,args.checkpoint);clone=lower(model.sep_network);reference=Reference(clone).eval();audio=Extraction(clone).eval()
    original_reference=Reference(model.sep_network).eval();rng=np.random.default_rng(20261031);cases=[];lowering=[]
    with torch.inference_mode():
        for n,r in [(20,280),(29,549),(30,550),(31,551),(8000,64000),(16007,8071),(32000,32000),(12801,280)]:
            x=rng.normal(0,.03,(1,n)).astype(np.float32);v=rng.normal(0,.03,(1,r)).astype(np.float32);tx=torch.from_numpy(x);tr=torch.from_numpy(v)
            y=source_forward(model,tx,tr).numpy();e=original_reference(tr).numpy();ae=reference(tr).numpy()
            lowering.append(dict(samples=n,reference_samples=r,embedding_nrmse=nrmse(ae,e),waveform_nrmse=nrmse(audio(tx,torch.from_numpy(ae)).numpy(),y)))
            cases.append((n,r,x,v,y,e))
        torch.onnx.export(reference,torch.zeros(1,8000),args.output/'reference_dynamic.onnx',input_names=['reference'],output_names=['embedding'],opset_version=17,dynamo=False,dynamic_axes={'reference':{1:'reference_samples'}})
        torch.onnx.export(audio,(torch.zeros(1,8000),torch.zeros(1,256)),args.output/'audio_dynamic.onnx',input_names=['mixture','embedding'],output_names=['target'],opset_version=17,dynamo=False,dynamic_axes={'mixture':{1:'samples'},'target':{1:'samples'}})
    options=ort.SessionOptions();options.intra_op_num_threads=2
    rs=ort.InferenceSession(str(args.output/'reference_dynamic.onnx'),options,providers=['CPUExecutionProvider']);ex=ort.InferenceSession(str(args.output/'audio_dynamic.onnx'),options,providers=['CPUExecutionProvider']);onnx=[]
    for n,r,x,v,y,e in cases:
        ae=rs.run(None,dict(reference=v))[0];actual=ex.run(None,dict(mixture=x,embedding=ae))[0]
        onnx.append(dict(samples=n,reference_samples=r,embedding_nrmse=nrmse(ae,e),waveform_nrmse=nrmse(actual,y)))
    graphs={stage:convert(args.converter,args.output/(stage+'_dynamic.onnx'),args.output/('spexplus_'+stage+'_fp32.mnn')) for stage in ['reference','audio']}
    rs=Graph(args.output/'spexplus_reference_fp32.mnn');ex=Graph(args.output/'spexplus_audio_fp32.mnn');mnn=[]
    for n,r,x,v,y,e in cases+[cases[0]]:
        for repeat in range(2):
            ae=rs.run(dict(reference=v),'embedding');actual=ex.run(dict(mixture=x,embedding=ae),'target')
            mnn.append(dict(samples=n,reference_samples=r,repeat=repeat,embedding_nrmse=nrmse(ae,e),waveform_nrmse=nrmse(actual,y)))
    (args.output/'mnn_spexplus.json').write_text(json.dumps(profile(),indent=2)+'\n')
    report=dict(source_revision=SOURCE_REVISION,checkpoint_revision=REVISION,checkpoint_sha256=CHECKPOINT_SHA,source_hashes=json.loads(Path(__file__).with_name('source-hashes.json').read_text()),torch_version=torch.__version__,original_config=config,lowering=lowering,onnx_parity=onnx,mnn_parity=mnn,graphs=graphs)
    (args.output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
