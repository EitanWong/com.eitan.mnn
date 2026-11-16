from workspace import workspace
from pathlib import Path
import sys,subprocess,time,json
import numpy as np
A,R=workspace();sys.path.insert(0,str(R/'Packages/com.eitan.mnn/Tools~/Models/TurnDetection'));from native_graph import Graph
w=A/'Easy';rows=[]
for variant,flags in {'FP32':[],'FP16':['--fp16'],'W8-HQQ':['--weightQuantBits','8','--weightQuantBlock','64','--weightQuantAsymmetric','--hqq'],'W4-HQQ':['--weightQuantBits','4','--weightQuantBlock','64','--weightQuantAsymmetric','--hqq']}.items():
 p=w/('audio-'+variant+'.mnn')
 if not p.exists():
  with (w/('audio-'+variant+'-convert.log')).open('w') as log:subprocess.run([sys.executable,'-m','MNN.tools.mnnconvert','-f','ONNX','--modelFile',str(w/'audio.onnx'),'--MNNModel',str(p),'--bizCode','EasyTurnAudio','--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']+flags,stdout=log,stderr=subprocess.STDOUT,check=True)
 for threads in [1,2,4]:
  graph=Graph(p,threads=threads);cases=[];times=[]
  for fixture in sorted(w.glob('*.npz')):
   z=np.load(fixture);inputs={k:z[k] for k in ['features','lengths']};graph.run(['embeddings','speech_length'],inputs);t=time.perf_counter();emb,length=graph.run(['embeddings','speech_length'],inputs);times.append((time.perf_counter()-t)*1000);n=int(z['speech_length'][0]);assert int(length.ravel()[0])==n
   cases.append(dict(case=fixture.stem,max_error=float(abs(emb[:,:n]-z['embeddings'][:,:n]).max()),mean_error=float(abs(emb[:,:n]-z['embeddings'][:,:n]).mean())))
  row=dict(variant=variant,threads=threads,bytes=p.stat().st_size,median_ms=float(np.median(times)),p95_ms=float(np.percentile(times,95)),max_error=max(c['max_error'] for c in cases),cases=cases);rows.append(row);(w/'audio-report.json').write_text(json.dumps(rows,indent=2)+'\n');print(json.dumps({k:v for k,v in row.items() if k!='cases'}),flush=True);del graph
