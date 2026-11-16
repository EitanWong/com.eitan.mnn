from workspace import workspace
from pathlib import Path
import torch,json,re,numpy as np
from transformers import Qwen2Config,Qwen2ForCausalLM,AutoTokenizer
from transformers.models.qwen2.modeling_qwen2 import Qwen2RotaryEmbedding
A,R=workspace();w=A/'Easy';torch.set_num_threads(4)
s=torch.load(R/'Assets/StreamingAssets/MNN/Models/Easy-Turn-Source/checkpoint.pt',mmap=True,map_location='cpu',weights_only=True);c=Qwen2Config.from_json_file(w/'decoder_config.json');c._attn_implementation='eager'
with torch.device('meta'):m=Qwen2ForCausalLM(c)
m.load_state_dict({k.removeprefix('llama_model.'):v for k,v in s.items() if k.startswith('llama_model.')},assign=True,strict=True);m.model.rotary_emb=Qwen2RotaryEmbedding(c);m.eval()
tok=AutoTokenizer.from_pretrained(R/'Assets/StreamingAssets/MNN/Models/LiveKit-Multilingual-Source',local_files_only=True)
prompt='请将音频转写为文本，并在结尾标注打断类型：<complete>（语义完整）、<incomplete>（语义不完整）、<backchannel>（附和语句）、<wait>（请求暂停或结束对话）。'
ids=tok.encode(prompt,add_special_tokens=False);(w/'prompt.json').write_text(json.dumps(dict(text=prompt,ids=ids),ensure_ascii=False,indent=2)+'\n');rows=[]
s['speech_token_emded.weight'][4097:4099].detach().numpy().tofile(w/'speech-boundary.f32')
for p in sorted(w.glob('*.npz')):
 z=np.load(p);n=int(z['speech_length'][0]);audio=torch.from_numpy(z['embeddings'][:,:n])
 with torch.inference_mode():
  emb=torch.cat([m.model.embed_tokens(torch.tensor([ids])),s['speech_token_emded.weight'][4097][None,None],audio,s['speech_token_emded.weight'][4098][None,None]],dim=1)
  out=m.generate(inputs_embeds=emb,max_new_tokens=100,do_sample=False,repetition_penalty=1.05,eos_token_id=151643,pad_token_id=151643,use_cache=True)
 tokens=out[0].tolist();text=tok.decode(tokens,skip_special_tokens=True);label=re.findall(r'<(complete|incomplete|backchannel|wait)>',text);row=dict(case=p.stem,tokens=tokens,text=text,label=label[-1] if label else None,frames=int(z['lengths'][0]),speech_length=n,prompt_length=emb.shape[1]);rows.append(row);print(json.dumps(row,ensure_ascii=False),flush=True);(w/'reference.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n')
