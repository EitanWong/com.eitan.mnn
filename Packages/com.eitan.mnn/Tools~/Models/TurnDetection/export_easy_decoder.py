from workspace import workspace
from pathlib import Path
import torch,json,sys,math
from transformers import Qwen2Config,Qwen2ForCausalLM
A,R=workspace();w=A/'Easy';torch.set_num_threads(4)
s=torch.load(R/'Assets/StreamingAssets/MNN/Models/Easy-Turn-Source/checkpoint.pt',mmap=True,weights_only=True,map_location='cpu')
print('GATE',s['llama_model.model.layers.0.mlp.gate_proj.weight'].shape,flush=True)
c=Qwen2Config(vocab_size=151936,hidden_size=896,intermediate_size=int(s['llama_model.model.layers.0.mlp.gate_proj.weight'].shape[0]),num_hidden_layers=24,num_attention_heads=14,num_key_value_heads=2,max_position_embeddings=32768,rope_theta=1000000,rms_norm_eps=1e-6,tie_word_embeddings=True);c._attn_implementation='eager'
with torch.device('meta'):m=Qwen2ForCausalLM(c)
m.load_state_dict({k.removeprefix('llama_model.'):v for k,v in s.items() if k.startswith('llama_model.')},assign=True,strict=True);m.eval()
from transformers.models.qwen2.modeling_qwen2 import Qwen2RotaryEmbedding
m.model.rotary_emb=Qwen2RotaryEmbedding(c)
class Decode(torch.nn.Module):
 def __init__(self,m):super().__init__();self.m=m
 def forward(self,embeddings,past_key,past_value,mask,cos,sin,last_token_index):
  x=embeddings;keys=[];values=[];p=x.shape[1]
  def rotary(x):a,b=x.chunk(2,dim=-1);return x*cos+torch.cat([-b,a],dim=-1)*sin
  for i,layer in enumerate(self.m.model.layers):
   norm=layer.input_layernorm(x);attn=layer.self_attn
   q=rotary(attn.q_proj(norm).view(1,p,14,64).transpose(1,2));k=rotary(attn.k_proj(norm).view(1,p,2,64).transpose(1,2));v=attn.v_proj(norm).view(1,p,2,64).transpose(1,2)
   keys.append(k);values.append(v)
   k=torch.cat([past_key[i],k],dim=2).repeat_interleave(7,dim=1);v=torch.cat([past_value[i],v],dim=2).repeat_interleave(7,dim=1)
   scores=(q@k.transpose(-1,-2))*.125+mask
   out=(scores.softmax(-1)@v).transpose(1,2).reshape(1,p,896)
   x=x+attn.o_proj(out);x=x+layer.mlp(layer.post_attention_layernorm(x))
  last=torch.index_select(self.m.model.norm(x),1,last_token_index).squeeze(1)
  return self.m.lm_head(last),torch.stack(keys),torch.stack(values)
dec=Decode(m).eval();p=4;emb=torch.randn(1,p,896);past=torch.zeros(24,1,2,512,64);pos=torch.arange(p);inv=1/(1000000**(torch.arange(0,64,2).float()/64));freq=pos[:,None]*inv[None];freq=torch.cat([freq,freq],-1);cos=freq.cos()[None,None];sin=freq.sin()[None,None];mask=torch.full((1,1,p,512+p),-10000.);mask[:,:,:,512:]=torch.full((p,p),-10000.).triu(1);idx=torch.tensor([p-1])
with torch.inference_mode():
 expected=m(inputs_embeds=emb,use_cache=False).logits[:,-1];actual=dec(emb,past,past,mask,cos,sin,idx)[0];print('LOGITS_MAX_ERROR',float(abs(expected-actual).max()),flush=True);assert float(abs(expected-actual).max())<.0002
 torch.onnx.export(dec,(emb,past,past,mask,cos,sin,idx),str(w/'decoder.onnx'),input_names=['embeddings','past_key','past_value','mask','cos','sin','last_token_index'],output_names=['logits','new_key','new_value'],dynamic_axes={'embeddings':{1:'sequence'},'cos':{2:'sequence'},'sin':{2:'sequence'},'mask':{2:'sequence',3:'context'},'new_key':{3:'sequence'},'new_value':{3:'sequence'}},opset_version=17,dynamo=False,external_data=False)
 print('DECODER_EXPORTED',flush=True)
# Export reusable text embedding data, exact checkpoint fp32 source -> bf16 storage, verified later end to end.
m.model.embed_tokens.weight.detach().to(torch.bfloat16).view(torch.uint16).numpy().tofile(w/'embeddings_bf16.bin')
(w/'decoder_config.json').write_text(c.to_json_string())
