from workspace import workspace
from pathlib import Path
import sys,subprocess,hashlib,json,time,importlib.util
import numpy as np
A,R=workspace();source=R/'Assets/StreamingAssets/MNN/Models/DualTurn-Source/stream_tick.onnx';w=A/'DualTurn';w.mkdir(exist_ok=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()=='6700ea2f919b4c66355b3dafb4f91f2db9dae9e18a302b812730c04340bf819f'
sys.path.insert(0,str(R/'Packages/com.eitan.mnn/Tools~/Models/TurnDetection'));from native_graph import Graph
spec=importlib.util.spec_from_file_location('official_streamer',source.parent/'onnx_streaming.py');mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod)
variants={'FP32':[]}
# Exercise startup empty states and sustained causal history through the KV ring limit.
clips=[]
from scipy.io import wavfile
from scipy.signal import resample_poly
import math
for p in sorted((A/'dependencies/Easy-Turn/samples').glob('*.wav')):
 rate,pcm=wavfile.read(p);pcm=pcm.astype(np.float32)/32768
 if pcm.ndim>1:pcm=pcm.mean(axis=1)
 if rate!=24000:g=math.gcd(rate,24000);pcm=resample_poly(pcm,24000//g,rate//g).astype(np.float32)
 clips.append((p.stem,np.stack([pcm,np.zeros_like(pcm)])))
clips.insert(0,('silence',np.zeros((2,1920*128),np.float32)))
refs=[];oracle=mod.DualTurnONNXStreamer(str(source),threads=2);lowered_oracle=mod.DualTurnONNXStreamer(str(w/'source-positive-slices.onnx'),threads=2)
for label,pcm in clips:
 oracle.reset();lowered_oracle.reset();ticks=[]
 for start in range(0,pcm.shape[1],1920):
  chunk=np.pad(pcm[:,start:start+1920],((0,0),(0,max(0,start+1920-pcm.shape[1]))));out=oracle.push(chunk);lowered_out=lowered_oracle.push(chunk);assert abs(out['eot']-lowered_out['eot'])<.00001;assert np.max(np.abs(np.array(out['vad'])-np.array(lowered_out['vad'])))<.00001;ticks.append([out['eot'],*out['vad'],*np.array(out['fvad']).ravel()])
 pcm.tofile(w/(label+'.f32'));refs.append(dict(name=label,samples=pcm.shape[1],probabilities=np.array(ticks).tolist()))
(w/'reference.json').write_text(json.dumps(refs,indent=2)+'\n');del oracle
rows=[];names=['eot','vad','fvad','audio_ctx2','pk2','pv2','tf_hist2','h2','c2','pos2'];state_names=['audio_ctx','pk','pv','tf_hist','h','c','pos']
for variant,flags in variants.items():
 target=w/(variant+'-positive.mnn')
 if not target.exists():
  with (w/(variant+'-convert.log')).open('w') as log:subprocess.run([sys.executable,'-m','MNN.tools.mnnconvert','-f','ONNX','--modelFile',str(w/'source-positive-slices.onnx'),'--MNNModel',str(target),'--bizCode','DualTurn','--optimizeLevel','1','--optimizePrefer','2','--saveExternalData']+flags,stdout=log,stderr=subprocess.STDOUT,check=True)
 for threads in [1]:
  graph=Graph(target,threads=threads);error=0;flips=0;times=[]
  for ref,(_,pcm) in zip(refs,clips):
   state={'audio_ctx':np.zeros((2,0),np.float32),'pk':np.zeros((8,2,8,0,64),np.float32),'pv':np.zeros((8,2,8,0,64),np.float32),'tf_hist':np.zeros((2,260,512),np.float32),'h':np.zeros((2,1,256),np.float32),'c':np.zeros((2,1,256),np.float32),'pos':np.zeros(1,np.int32)}
   for tick,start in enumerate(range(0,pcm.shape[1],1920)):
    chunk=np.pad(pcm[:,start:start+1920],((0,0),(0,max(0,start+1920-pcm.shape[1]))));t=time.perf_counter();out=graph.run(names,dict(chunk=chunk,**state));times.append((time.perf_counter()-t)*1000);state=dict(zip(state_names,out[3:]));actual=np.concatenate([x.ravel() for x in out[:3]]);expected=np.array(ref['probabilities'][tick]);error=max(error,float(np.max(np.abs(actual-expected))));flips+=int(np.sum((actual>=.5)!=(expected>=.5)))
  row=dict(variant=variant,threads=threads,max_error=error,flips=flips,median_ms=float(np.median(times)),p95_ms=float(np.percentile(times,95)),bytes=sum(p.stat().st_size for p in w.glob(variant+'-positive.mnn*')));rows.append(row);(w/'selected-full-report.json').write_text(json.dumps(rows,indent=2)+'\n');print(json.dumps(row),flush=True);del graph
