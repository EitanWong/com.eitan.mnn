from workspace import workspace
from pathlib import Path
import json,time,statistics,sys
import MNN.llm as llm
A,R=workspace();d=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else A/'TEN/template'
from transformers import AutoTokenizer
tok=AutoTokenizer.from_pretrained(R/'Assets/StreamingAssets/MNN/Models/TEN-Turn-Detection-Source',local_files_only=True)
rows=json.loads((A/'TEN/source-reference.json').read_text());results=[]
for threads in [1,2,4]:
 cfg=json.loads((d/'config.json').read_text());cfg.update(thread_num=threads,max_new_tokens=1,use_template=False,sampler_type='greedy');p=d/f'benchmark-{threads}.json';p.write_text(json.dumps(cfg))
 m=llm.create(str(p));m.load();times=[];cases=[]
 for row in rows:
  m.reset();prompt=tok.apply_chat_template([{'role':'system','content':''},{'role':'user','content':row['text']}],tokenize=False,add_generation_prompt=True);assert m.tokenizer_encode(prompt)==row['input_ids']
  t=time.perf_counter();out=m.response(prompt,False);elapsed=(time.perf_counter()-t)*1000
  case=dict(text=row['text'],source=row['label'],actual=out,match=out==row['label'],ms=elapsed);cases.append(case);times.append(elapsed);print(json.dumps(case,ensure_ascii=False),flush=True)
 result=dict(variant=('W8' if d.name=='int8' else 'W4')+'-block64-headW8-RTN-symmetric-scaleFP16',threads=threads,median_ms=statistics.median(times),max_ms=max(times),flips=sum(not r['match'] for r in cases),bytes=sum((d/n).stat().st_size for n in ['llm.mnn','llm.mnn.weight','embeddings_bf16.bin']),cases=cases);results.append(result);(A/('TEN/report-int8.json' if d.name=='int8' else 'TEN/report.json')).write_text(json.dumps(results,ensure_ascii=False,indent=2)+'\n');del m
