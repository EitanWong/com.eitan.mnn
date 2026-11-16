from workspace import workspace
from pathlib import Path
import sys,json,time,unicodedata,subprocess
import numpy as np,onnxruntime as ort
from tokenizers import Tokenizer
A,R=workspace();w=A/'LiveKitEnglish';w.mkdir(exist_ok=True);s=R/'Assets/StreamingAssets/MNN/Models/LiveKit-English-Source';d=s.parent/'LiveKit-Turn-Detector-MNN'
sys.path.insert(0,str(R/'Packages/com.eitan.mnn/Tools~/Models/TurnDetection'));from native_graph import Graph
texts=['That is all, thank you.','I would like to','Can you help me?','The answer is forty two.','I think that','Yes.','Please wait a moment.','Today the weather is nice.','我的问题说完了。','我想要一个','İstanbul and ＡＢＣ １２３！','Tell me about the history of the world. '*30]
tok=Tokenizer.from_file(str(d/'tokenizer.json'));so=ort.SessionOptions();so.intra_op_num_threads=2;oracle=ort.InferenceSession(str(s/'onnx/model.onnx'),so,providers=['CPUExecutionProvider']);refs=[]
for text in texts:
 n=unicodedata.normalize('NFKC',text.lower());n=''.join(c for c in n if not unicodedata.category(c).startswith('P') or c in "'-");n=' '.join(n.split());prompt='<|im_start|><|user|>'+n;ids=tok.encode(prompt,add_special_tokens=False).ids[-128:];p=float(oracle.run(None,{'input_ids':np.array([ids],np.int64)})[0].ravel()[0]);refs.append(dict(text=text,ids=ids,probability=p))
(w/'reference.json').write_text(json.dumps(refs,ensure_ascii=False,indent=2)+'\n');del oracle
# Existing verified floating exports are reused; weight-only alternatives are generated from the same source.
variants={'FP32':(d/'turn_detector_fp32.mnn',False),'FP16':(d/'turn_detector_fp16.mnn',True)}
for bits in [8,4]:
 p=w/f'W{bits}.mnn';variants[f'W{bits}']=(p,False)
 if not p.exists():
  with (w/f'W{bits}-convert.log').open('w') as log:subprocess.run([sys.executable,'-m','MNN.tools.mnnconvert','-f','ONNX','--modelFile',str(s/'onnx/model.onnx'),'--MNNModel',str(p),'--bizCode','LiveKitEnglish','--weightQuantBits',str(bits),'--weightQuantBlock','64','--weightQuantAsymmetric','--hqq','--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0'],stdout=log,stderr=subprocess.STDOUT,check=True)
rows=[]
for variant,(p,fixed) in variants.items():
 for threads in [1,2,4]:
  graph=Graph(p,threads=threads);times=[];cases=[]
  for r in refs:
   ids=r['ids'];feed={'input_ids':np.array([ids+([0]*(128-len(ids)) if fixed else [])],np.int64)}
   if fixed:feed['last_token_index']=np.array([len(ids)-1],np.int64)
   graph.run(['prob'],feed);start=time.perf_counter();v=float(graph.run(['prob'],feed)[0].ravel()[0]);times.append((time.perf_counter()-start)*1000);cases.append(dict(text=r['text'],source=r['probability'],actual=v,error=abs(v-r['probability']),flip=(v>=.5)!=(r['probability']>=.5)))
  row=dict(variant=variant,threads=threads,median_ms=float(np.median(times)),p95_ms=float(np.percentile(times,95)),max_error=max(c['error'] for c in cases),flips=sum(c['flip'] for c in cases),bytes=p.stat().st_size,cases=cases);rows.append(row);(w/'report.json').write_text(json.dumps(rows,indent=2)+'\n');print(json.dumps({k:v for k,v in row.items() if k!='cases'}),flush=True);del graph
