from workspace import workspace
from pathlib import Path
import json,time,re,sys,gc
A,R=workspace();w=A/'Easy'
from easy_mnn_runner import Easy
from transformers import AutoTokenizer
tok=AutoTokenizer.from_pretrained(R/'Assets/StreamingAssets/MNN/Models/LiveKit-Multilingual-Source',local_files_only=True);refs=json.loads((w/'reference.json').read_text());reports=[]
for audio,decoder in [('FP16','FP16'),('FP16','W8-HQQ'),('W8-HQQ','W8-HQQ'),('FP16','W4-HQQ')]:
 model=Easy(w,audio,decoder,2);cases=[];times=[]
 for r in refs:
  start=time.perf_counter();tokens=model.predict(w/(r['case']+'.npz'));elapsed=(time.perf_counter()-start)*1000;text=tok.decode(tokens,skip_special_tokens=True);labels=re.findall(r'<(complete|incomplete|backchannel|wait)>',text);label=labels[-1] if labels else None;row=dict(case=r['case'],source=r['label'],actual=label,text=text,token_match=tokens==r['tokens'],match=r['label']==label,ms=elapsed);cases.append(row);times.append(elapsed);print(json.dumps(row,ensure_ascii=False),flush=True)
 report=dict(audio=audio,decoder=decoder,threads=2,median_ms=sorted(times)[len(times)//2],flips=sum(not c['match'] for c in cases),bytes=(w/('audio-'+audio+'.mnn')).stat().st_size+(w/('decoder-'+decoder+'.mnn')).stat().st_size,cases=cases);reports.append(report);(w/'report.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2)+'\n');del model;gc.collect()
