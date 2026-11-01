#!/usr/bin/env python3
"""Reproduce pinned ClearerVoice AV-DPRNN with safe dynamic visual convolution."""
from pathlib import Path
import argparse,hashlib,importlib.util,json,subprocess,uuid
import numpy as np
import onnxruntime as ort
import torch
from graph import lower,Visual,Audio
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
    identity=str(uuid.uuid5(uuid.NAMESPACE_URL,'alibabasglab/AV-DPRNN/'+REVISION+'/'+path.name))
    value.data[position+4:position+40]=identity.encode();path.write_bytes(value.data);return identity

def convert(converter,onnx,graph):
    command=[str(converter),'-f','ONNX','--modelFile',str(onnx),'--MNNModel',str(graph),'--bizCode','AVDPRNN','--optimizeLevel','1','--optimizePrefer','2','--useOriginRNNImpl','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']
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
    return dict(repository='alibabasglab/log_VoxCeleb2_lip_dprnn_2spk',revision=REVISION,source_revision=SOURCE_REVISION,
                visual_graph='av_dprnn_visual_fp32.mnn',audio_graph='av_dprnn_audio_fp32.mnn',sample_rate=16000,
                frames_per_second=25,face_size=112,visual_features=256,minimum_samples=640,maximum_samples=96000,
                visual_input='frames',visual_output='visual_features',audio_input='mixture',audio_output='target',
                normalization='(gray/255-0.4161)/0.1688; no audio normalization',causal=False)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','checkpoint','converter','output']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True);torch.set_num_threads(2)
    model,config=load(args.source,args.checkpoint,args.output);clone=lower(model);rng=np.random.default_rng(20261031);cases=[];lowering=[]
    with torch.inference_mode():
        for size in [640,659,660,1020,1040,16000,48000,96000]:
            frames=max(1,size//640);pcm=rng.normal(0,.03,(1,size)).astype(np.float32);visual=(rng.uniform(0,1,(1,frames,112,112)).astype(np.float32)-.4161)/.1688
            x=torch.from_numpy(pcm.copy());v=torch.from_numpy(visual.copy());expected=source_forward(model,x,v).numpy();expected_features=model.ref_encoder(v).numpy()
            lowering.append(dict(samples=size,visual_nrmse=nrmse(clone.ref_encoder(v).numpy(),expected_features),waveform_nrmse=nrmse(clone(x,v).numpy(),expected)))
            cases.append((size,pcm,visual,expected,expected_features))
        torch.onnx.export(Visual(clone),torch.zeros(1,25,112,112),args.output/'visual_dynamic.onnx',input_names=['frames'],output_names=['visual_features'],opset_version=17,dynamo=False,dynamic_axes={'frames':{1:'frames'},'visual_features':{2:'frames'}})
        torch.onnx.export(Audio(clone),(torch.zeros(1,16000),torch.zeros(1,256,25)),args.output/'audio_dynamic.onnx',input_names=['mixture','visual_features'],output_names=['target'],opset_version=17,dynamo=False,dynamic_axes={'mixture':{1:'samples'},'visual_features':{2:'frames'},'target':{1:'samples'}})
    options=ort.SessionOptions();options.intra_op_num_threads=2
    visual=ort.InferenceSession(str(args.output/'visual_dynamic.onnx'),options,providers=['CPUExecutionProvider']);audio=ort.InferenceSession(str(args.output/'audio_dynamic.onnx'),options,providers=['CPUExecutionProvider']);onnx=[]
    for size,x,v,y,f in cases:
        actual_f=visual.run(None,dict(frames=v))[0];actual=audio.run(None,dict(mixture=x,visual_features=actual_f))[0]
        onnx.append(dict(samples=size,visual_nrmse=nrmse(actual_f,f),waveform_nrmse=nrmse(actual,y)))
    graphs={name:convert(args.converter,args.output/(name+'_dynamic.onnx'),args.output/('av_dprnn_'+name+'_fp32.mnn')) for name in ['visual','audio']}
    visual=Graph(args.output/'av_dprnn_visual_fp32.mnn');audio=Graph(args.output/'av_dprnn_audio_fp32.mnn');mnn=[]
    for size,x,v,y,f in cases+[cases[0]]:
        for repeat in range(2):
            actual_f=visual.run(dict(frames=v),'visual_features');actual=audio.run(dict(mixture=x,visual_features=actual_f),'target')
            mnn.append(dict(samples=size,repeat=repeat,visual_nrmse=nrmse(actual_f,f),waveform_nrmse=nrmse(actual,y)))
    (args.output/'mnn_av_dprnn.json').write_text(json.dumps(profile(),indent=2)+'\n')
    report=dict(source_revision=SOURCE_REVISION,checkpoint_revision=REVISION,checkpoint_sha256=CHECKPOINT_SHA,source_hashes=json.loads(Path(__file__).with_name('source-hashes.json').read_text()),torch_version=torch.__version__,original_config=config,lowering=lowering,onnx_parity=onnx,mnn_parity=mnn,graphs=graphs)
    (args.output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
