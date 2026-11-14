#!/usr/bin/env python3
"""Compare actual MNN FP16 components with their checked ONNX reference."""
import argparse,json
from pathlib import Path
import numpy as np
import onnxruntime as ort
import MNN
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--onnx-directory',type=Path,required=True);p.add_argument('--model',type=Path,required=True);p.add_argument('--stage',required=True);p.add_argument('--report',type=Path,required=True);a=p.parse_args()
rng=np.random.default_rng(42);stage=a.stage
if stage=='mimi_decoder':cases=[{'codes':rng.integers(0,2048,(1,8,n),dtype=np.int64)} for n in [1,4,5,6,8]]
elif stage=='audio_encoder':cases=[{'fbank':rng.standard_normal((1,n,560)).astype('f')} for n in [7,24]]
elif stage=='vision_encoder':cases=[{'pixels':rng.uniform(-1,1,(1,3,256,256)).astype('f')}]
else:
 cases=[]
 for n in [5,77]:cases.append({'tokens':rng.integers(32,6400,(1,n),dtype=np.int64),'audio_codes':np.full((1,8,n),2049,np.int64),'features':rng.standard_normal((1,n,768)).astype('f'),'feature_mask':rng.integers(0,2,(1,n,1)).astype('f')})
options=ort.SessionOptions();options.intra_op_num_threads=4
ref=ort.InferenceSession(str(a.onnx_directory/(stage+'.onnx')),options,providers=['CPUExecutionProvider'])
interpreter=MNN.Interpreter(str(a.model/(stage+'.mnn')));session=interpreter.createSession({'backend':'CPU','numThread':4,'precision':'low','memory':2})
rows=[]
for feed in cases:
 feed={d.name:feed[d.name].astype(np.int32 if d.type=="tensor(int32)" else np.int64 if d.type=="tensor(int64)" else np.float32) for d in ref.get_inputs()}
 expected=ref.run(None,feed);inputs=interpreter.getSessionInputAll(session)
 for key,tensor in inputs.items():interpreter.resizeTensor(tensor,tuple(feed[key].shape))
 interpreter.resizeSession(session)
 for key,tensor in inputs.items():
  value=feed[key];value=value.astype(np.int32) if value.dtype.kind in 'iu' else value
  tensor.copyFrom(MNN.Tensor(tuple(value.shape),MNN.Halide_Type_Int if value.dtype.kind in 'iu' else MNN.Halide_Type_Float,value,MNN.Tensor_DimensionType_Caffe))
 assert interpreter.runSession(session)==0
 row={'inputs':{k:list(v.shape) for k,v in feed.items()},'outputs':{}}
 for descriptor,y in zip(ref.get_outputs(),expected):
  tensor=interpreter.getSessionOutput(session,descriptor.name);shape=tuple(tensor.getShape());host=MNN.Tensor(shape,MNN.Halide_Type_Float,np.zeros(shape,np.float32),MNN.Tensor_DimensionType_Caffe);tensor.copyToHostTensor(host);x=np.asarray(host.getData(),np.float32).reshape(shape)
  assert x.shape==y.shape and np.isfinite(x).all()
  xf=x.ravel().astype('d');yf=y.ravel().astype('d');mse=float(np.mean((xf-yf)**2));energy=float(np.mean(yf*yf));cos=float(xf@yf/(np.linalg.norm(xf)*np.linalg.norm(yf)));snr=float(10*np.log10(energy/max(mse,1e-30)))
  row['outputs'][descriptor.name]={'shape':list(x.shape),'max_abs_error':float(np.max(np.abs(x-y))),'cosine':cos,'snr_db':snr}
  assert cos>=0.99 and snr>=20,(descriptor.name,row)
 rows.append(row);print(json.dumps(row),flush=True)
a.report.write_text(json.dumps({'stage':stage,'passed':True,'runtime':'MNN 3.6.1 Python CPU low precision/memory low','cases':rows},indent=2)+'\n')
