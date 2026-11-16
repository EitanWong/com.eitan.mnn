from workspace import workspace
from pathlib import Path
import sys,time,json,hashlib,subprocess,math
import numpy as np,onnx,onnxruntime as ort
from scipy.io import wavfile
from scipy.signal import resample_poly
A,R=workspace()
sys.path.insert(0,str(A/'dependencies/Bairong-TurnSense'));from frontend import AudioFrontend
from native_graph import Graph
work=A/'Bairong';work.mkdir(exist_ok=True)
rows=[]
def probs(y):
 y=np.asarray(y,dtype=np.float64).reshape(-1)
 if not(np.all(y>=0) and np.all(y<=1) and abs(y.sum()-1)<1e-4):y=np.exp(y-y.max());y=y/y.sum()
 return y
for version in ['1.0','1.1']:
 source=R/'Assets/StreamingAssets/MNN/Models/Bairong-TurnSense-Source'/('TurnSense'+version)
 frontend=AudioFrontend(cmvn_file=str(source/'am.mvn'),window='hamming',n_mels=80,lfr_m=7,lfr_n=6,dither=0)
 options=ort.SessionOptions();options.intra_op_num_threads=2;session=ort.InferenceSession(str(source/'model_fp32.onnx'),options,providers=['CPUExecutionProvider'])
 feeds=[];pad_error=0
 fixtures=work/('fixtures-'+version);fixtures.mkdir(exist_ok=True)
 for path in sorted((A/'dependencies/Easy-Turn/samples').glob('*.wav')):
  rate,pcm=wavfile.read(path);pcm=pcm.astype(np.float32)/(32768 if pcm.dtype==np.int16 else 1)
  if pcm.ndim==2:pcm=pcm.mean(axis=1)
  if rate!=16000:g=math.gcd(rate,16000);pcm=resample_poly(pcm,16000//g,rate//g).astype(np.float32)
  pcm=np.clip(pcm[-128000:],-1,1);f,l=frontend.extract_features(pcm);f=f[None];lens=np.array([l],np.int64)
  raw=session.run(None,{'feats':f,'feat_lengths':lens})[0];expected=probs(raw)
  fixed=np.pad(f,((0,0),(0,133-f.shape[1]),(0,0)));padded=session.run(None,{'feats':fixed,'feat_lengths':lens})[0];pad_error=max(pad_error,float(np.max(np.abs(probs(padded)-expected))))
  feeds.append((path.stem,fixed,lens,expected));pcm.tofile(fixtures/(path.stem+'-input.f32'));f.tofile(fixtures/(path.stem+'-features.f32'))
  (fixtures/(path.stem+'.json')).write_text(json.dumps(dict(frames=int(l),probabilities=expected.tolist()))+'\n')
 print('SOURCE',version,'cases',len(feeds),'pad_max_error',pad_error,flush=True)
 if pad_error>.0001:raise ValueError('fixed padding changes source decisions')
 model=onnx.load(source/'model_fp32.onnx');inputs={v.name:v for v in model.graph.input}
 for v,dims in [(inputs['feats'],[1,133,560]),(inputs['feat_lengths'],[1])]:
  for d,n in zip(v.type.tensor_type.shape.dim,dims):d.ClearField('dim_param');d.dim_value=n
 fixed_source=work/(version+'-fixed.onnx');onnx.save(model,fixed_source);del model
 variants={'FP32':[],'FP16':['--fp16'],'W8-HQQ':['--weightQuantBits','8','--weightQuantBlock','64','--weightQuantAsymmetric','--hqq'],'W4-HQQ':['--weightQuantBits','4','--weightQuantBlock','64','--weightQuantAsymmetric','--hqq']}
 for name,flags in variants.items():
  target=work/(version+'-'+name+'.mnn')
  if not target.exists():
   cmd=[sys.executable,'-m','MNN.tools.mnnconvert','-f','ONNX','--modelFile',str(fixed_source),'--MNNModel',str(target),'--bizCode','BairongTurnSense','--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']+flags
   with (work/(version+'-'+name+'-convert.log')).open('w') as log:subprocess.run(cmd,check=True,stdout=log,stderr=subprocess.STDOUT)
  for threads in [1,2,4]:
   graph=Graph(target,threads=threads);values=[];times=[]
   for label,features,lengths,expected in feeds:
    feed={'feats':features,'feat_lengths':lengths};graph.run(['logits'],feed);start=time.perf_counter();actual=probs(graph.run(['logits'],feed)[0]);times.append((time.perf_counter()-start)*1000)
    values.append(dict(case=label,source=expected.tolist(),actual=actual.tolist(),error=float(np.max(np.abs(actual-expected))),flip=bool(np.argmax(actual)!=np.argmax(expected))))
   row=dict(version=version,variant=name,threads=threads,bytes=target.stat().st_size,max_error=max(v['error'] for v in values),flips=sum(v['flip'] for v in values),median_ms=float(np.median(times)),p95_ms=float(np.percentile(times,95)),cases=values)
   rows.append(row);(work/'report.json').write_text(json.dumps(rows,indent=2)+'\n');print('MEASURED',json.dumps({k:v for k,v in row.items() if k!='cases'}),flush=True);del graph
 selected=min((r for r in rows if r['version']==version and r['max_error']<=.02 and r['flips']==0),key=lambda r:r['median_ms'])
 (work/('selected-'+version+'.json')).write_text(json.dumps(selected,indent=2)+'\n');print('SELECTED',version,selected['variant'],selected['threads'],flush=True)
