from workspace import workspace
from pathlib import Path
import torch,json,sys
from transformers import AutoConfig,AutoModelForCausalLM,AutoTokenizer
A,R=workspace();d=R/'Assets/StreamingAssets/MNN/Models/LiveKit-Multilingual-Source';w=A/'LiveKit';w.mkdir(exist_ok=True)
torch.set_num_threads(4)
model=AutoModelForCausalLM.from_pretrained(d,local_files_only=True,dtype=torch.float32,attn_implementation='eager').eval()
class Export(torch.nn.Module):
 def __init__(self,m):
  super().__init__();self.m=m;self.register_buffer('mask',torch.full((128,128),torch.finfo(torch.float32).min).triu(1)[None,None]);self.register_buffer('position',torch.arange(128)[None])
 def forward(self,input_ids,last_token_index):
  h=self.m.model(input_ids,attention_mask=self.mask,position_ids=self.position,use_cache=False,return_dict=False)[0]
  h=torch.index_select(h,1,last_token_index).squeeze(1)
  return torch.softmax(self.m.lm_head(h),dim=-1)[:,151645]
x=torch.ones((1,128),dtype=torch.long);index=torch.tensor([19]);e=Export(model).eval()
import warnings
with torch.inference_mode():
 print('EXPORT_START',flush=True)
 torch.onnx.export(e,(x,index),str(w/'source.onnx'),input_names=['input_ids','last_token_index'],output_names=['prob'],opset_version=17,dynamo=False,external_data=False)
 print('EXPORTED',flush=True)
 tok=AutoTokenizer.from_pretrained(d,local_files_only=True)
 texts=['我已经说完了谢谢','我想要一个','等一下我还没说完','请帮我查一下今天的天气','如果明天下雨的话','让我想一想','that is all thank you','i would like to','please wait a second','can you tell me the time','because i was thinking about','hold on let me finish']
 rows=[]
 for text in texts:
  prompt=tok.apply_chat_template([{'role':'user','content':text}],tokenize=False,add_generation_prompt=False);prompt=prompt[:prompt.rfind('<|im_end|>')];ids=tok.encode(prompt,add_special_tokens=False)[-128:]
  raw=model(torch.tensor([ids]),use_cache=False).logits[:,-1].softmax(-1)[0,151645].item();padded=ids+[0]*(128-len(ids));actual=e(torch.tensor([padded]),torch.tensor([len(ids)-1])).item();assert abs(raw-actual)<1e-5
  rows.append(dict(text=text,ids=ids,probability=raw));print(text,raw,flush=True)
 (w/'reference.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
