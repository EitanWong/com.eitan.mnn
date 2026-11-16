from workspace import workspace
from pathlib import Path
import json,time,sys,subprocess
import numpy as np
A,R=workspace();from native_graph import Graph
work=A/'LiveKit';rows=[];refs=json.loads((work/'reference.json').read_text())
variants={'FP32':[],'FP16':['--fp16'],'W8-HQQ':['--weightQuantBits','8','--weightQuantBlock','64','--weightQuantAsymmetric','--hqq'],'W4-HQQ':['--weightQuantBits','4','--weightQuantBlock','64','--weightQuantAsymmetric','--hqq']}
for variant,flags in variants.items():
 target=work/(variant+'.mnn')
 if not target.exists():
  with (work/(variant+'-convert.log')).open('w') as log:subprocess.run([sys.executable,'-m','MNN.tools.mnnconvert','-f','ONNX','--modelFile',str(work/'source.onnx'),'--MNNModel',str(target),'--bizCode','LiveKitMultilingual','--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']+flags,check=True,stdout=log,stderr=subprocess.STDOUT)
 for threads in [1,2,4]:
  graph=Graph(target,threads=threads);times=[];cases=[]
  for r in refs:
   inputs={'input_ids':np.array([r['ids']+[0]*(128-len(r['ids']))],np.int64),'last_token_index':np.array([len(r['ids'])-1],np.int64)}
   graph.run(['prob'],inputs);t=time.perf_counter();actual=float(graph.run(['prob'],inputs)[0].ravel()[0]);times.append((time.perf_counter()-t)*1000);cases.append(dict(text=r['text'],source=r['probability'],actual=actual,error=abs(actual-r['probability']),flip=(actual>=.5)!=(r['probability']>=.5)))
  row=dict(variant=variant,threads=threads,bytes=target.stat().st_size,median_ms=float(np.median(times)),p95_ms=float(np.percentile(times,95)),max_error=max(x['error'] for x in cases),flips=sum(x['flip'] for x in cases),cases=cases);rows.append(row);(work/'report.json').write_text(json.dumps(rows,indent=2)+'\n');print(json.dumps({k:v for k,v in row.items() if k!='cases'}),flush=True);del graph
valid=[r for r in rows if r['max_error']<=.01 and r['flips']==0];fast=min(r['median_ms'] for r in valid);selected=min((r for r in valid if r['median_ms']<=fast*1.1),key=lambda r:(r['bytes'],r['median_ms']));(work/'selected.json').write_text(json.dumps(selected,indent=2)+'\n')
