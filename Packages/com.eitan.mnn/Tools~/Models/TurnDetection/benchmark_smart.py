from workspace import workspace
from pathlib import Path
import os,sys,time,json,hashlib,subprocess,importlib.util
import numpy as np
import onnxruntime as ort
from scipy.io import wavfile
from scipy.signal import resample_poly
from transformers import WhisperFeatureExtractor
from urllib.request import urlopen
A,R=workspace();
from native_graph import Graph
source=R/'Assets/StreamingAssets/MNN/Models/SmartTurn-v3-Source';source.mkdir(parents=True,exist_ok=True)
onnx=source/'smart-turn-v3.2-gpu.onnx'
if not onnx.exists():
 with urlopen('https://huggingface.co/pipecat-ai/smart-turn-v3/resolve/f766f81d3cfdf7737ac64aad813d91bbfd56bf93/smart-turn-v3.2-gpu.onnx',timeout=60) as response,onnx.open('wb') as f:
  while b:=response.read(1024*1024):f.write(b)
assert hashlib.sha256(onnx.read_bytes()).hexdigest()=='ab8dc64b88713f90b571c15b714bd1330e6c883cad8763dacf65c9376dc539be'
work=A/'SmartTurnQuantization';work.mkdir(exist_ok=True)
variants={'FP32':[],'FP16':['--fp16'],'W8':['--weightQuantBits','8','--weightQuantBlock','64','--weightQuantAsymmetric'],'W4-HQQ':['--weightQuantBits','4','--weightQuantBlock','64','--weightQuantAsymmetric','--hqq']}
for name,flags in variants.items():
 out=work/(name+'.mnn')
 if not out.exists():
  cmd=[sys.executable,'-m','MNN.tools.mnnconvert','-f','ONNX','--modelFile',str(onnx),'--MNNModel',str(out),'--bizCode','SmartTurn','--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']+flags
  with (work/(name+'-convert.log')).open('w') as f:subprocess.run(cmd,check=True,stdout=f,stderr=subprocess.STDOUT)
  print('CONVERTED',name,out.stat().st_size,flush=True)
extractor=WhisperFeatureExtractor(feature_size=80,sampling_rate=16000,hop_length=160,chunk_length=8,n_fft=400)
session=ort.InferenceSession(str(onnx),providers=['CPUExecutionProvider'],sess_options=ort.SessionOptions())
feeds=[]
for path in sorted((A/'dependencies/Easy-Turn/samples').glob('*.wav')):
 rate,pcm=wavfile.read(path);pcm=pcm.astype(np.float32)/(32768 if pcm.dtype==np.int16 else 1);pcm=pcm.mean(axis=1) if pcm.ndim==2 else pcm
 if rate!=16000:
  import math
  g=math.gcd(rate,16000);pcm=resample_poly(pcm,16000//g,rate//g).astype(np.float32)
 pcm=np.clip(pcm,-1,1)
 for tail in [0,800]:
  audio=np.concatenate([pcm,np.zeros(tail*16,np.float32)])[-128000:];audio=np.pad(audio,(128000-len(audio),0));audio=(audio-audio.mean())/np.sqrt(audio.var()+1e-7)
  x=extractor(audio,sampling_rate=16000,return_tensors='np',do_normalize=False)['input_features'].astype(np.float32)
  expected=float(session.run(None,{'input_features':x})[0].reshape(-1)[0]);feeds.append((path.stem+'-tail'+str(tail),x,expected))
print('CASES',len(feeds),flush=True)
rows=[]
for name in variants:
 for threads in [1,2,4]:
  graph=Graph(work/(name+'.mnn'),threads=threads);values=[];timings=[]
  for label,x,expected in feeds:
   graph.run(['logits'],{'input_features':x});start=time.perf_counter();actual=float(graph.run(['logits'],{'input_features':x})[0].reshape(-1)[0]);timings.append((time.perf_counter()-start)*1000);values.append(dict(case=label,source=expected,actual=actual,error=abs(expected-actual),flip=(expected>.5)!=(actual>.5)))
  row=dict(variant=name,threads=threads,bytes=(work/(name+'.mnn')).stat().st_size,max_error=max(v['error'] for v in values),flips=sum(v['flip'] for v in values),median_ms=float(np.median(timings)),p95_ms=float(np.percentile(timings,95)),cases=values)
  rows.append(row);print('MEASURED',json.dumps({k:v for k,v in row.items() if k!='cases'}),flush=True);(work/'report.json').write_text(json.dumps(rows,indent=2)+'\n')
  del graph
accepted=[v for v in rows if v['max_error']<=.01 and v['flips']==0];best=min(accepted,key=lambda v:v['median_ms']);(work/'selected.json').write_text(json.dumps(best,indent=2)+'\n');print('SELECTED',best['variant'],best['threads'],best['median_ms'],flush=True)
