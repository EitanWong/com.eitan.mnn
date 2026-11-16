from workspace import workspace
"""Memory-bounded source oracle using unmodified transformers Qwen2 layers, FP32.
Only orchestration is streamed; attention/rotary/norm/MLP are official modules.
"""
from pathlib import Path
import torch,json,time,gc
from safetensors import safe_open
from transformers import AutoConfig,AutoTokenizer
from transformers.models.qwen2.modeling_qwen2 import Qwen2DecoderLayer,Qwen2RotaryEmbedding,Qwen2RMSNorm
A,R=workspace();d=R/'Assets/StreamingAssets/MNN/Models/TEN-Turn-Detection-Source'
torch.set_num_threads(4)
texts=['我已经说完了，谢谢。','我想要一个','等一下，我还没说完。','请帮我查一下今天的天气。','如果明天下雨的话','让我想一想。','That is all, thank you.','I would like to','Please wait a second.','Can you tell me the time?','Because I was thinking about','Hold on, let me finish.']
tok=AutoTokenizer.from_pretrained(d,local_files_only=True);cfg=AutoConfig.from_pretrained(d,local_files_only=True);cfg._attn_implementation='eager'
seqs=[tok.encode(tok.apply_chat_template([{'role':'system','content':''},{'role':'user','content':t}],tokenize=False,add_generation_prompt=True),add_special_tokens=False) for t in texts]
index=json.loads((d/'model.safetensors.index.json').read_text())['weight_map'];handles={n:safe_open(d/n,framework='pt',device='cpu') for n in set(index.values())}
def tensor(k):return handles[index[k]].get_tensor(k).float()
length=max(map(len,seqs));ids=torch.zeros((len(texts),length),dtype=torch.long)
for i,s in enumerate(seqs):ids[i,:len(s)]=torch.tensor(s)
with torch.inference_mode():
 w=tensor('model.embed_tokens.weight');hidden=torch.nn.functional.embedding(ids,w);del w
 pos=torch.arange(length)[None,:];rotary=Qwen2RotaryEmbedding(cfg);pe=rotary(hidden,pos)
 mask=torch.full((length,length),torch.finfo(torch.float32).min).triu(1)[None,None]
 for n in range(cfg.num_hidden_layers):
  with torch.device('meta'):layer=Qwen2DecoderLayer(cfg,n)
  prefix=f'model.layers.{n}.';state={k[len(prefix):]:tensor(k) for k in index if k.startswith(prefix)}
  layer.load_state_dict(state,assign=True,strict=True);layer.eval();hidden=layer(hidden,attention_mask=mask,position_ids=pos,position_embeddings=pe,use_cache=False)
  del layer,state;gc.collect();print('SOURCE_LAYER',n,flush=True)
 norm=Qwen2RMSNorm(cfg.hidden_size,eps=cfg.rms_norm_eps);norm.load_state_dict({'weight':tensor('model.norm.weight')});hidden=norm(hidden)
 final=torch.stack([hidden[i,len(s)-1] for i,s in enumerate(seqs)])
 weight=handles[index['lm_head.weight']].get_slice('lm_head.weight');scores=[]
 for start in range(0,cfg.vocab_size,8192):scores.append(torch.nn.functional.linear(final,weight[start:start+8192].float()))
 logits=torch.cat(scores,dim=-1);prob=torch.softmax(logits,dim=-1)
 rows=[]
 for i,text in enumerate(texts):
  token=int(logits[i].argmax());top=torch.topk(prob[i],3);row=dict(text=text,input_ids=seqs[i],token=token,label=tok.decode([token],skip_special_tokens=True),top=[dict(token=int(k),probability=float(p)) for k,p in zip(top.indices,top.values)])
  rows.append(row);print(json.dumps(row,ensure_ascii=False),flush=True)
 (A/'TEN/source-reference.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
