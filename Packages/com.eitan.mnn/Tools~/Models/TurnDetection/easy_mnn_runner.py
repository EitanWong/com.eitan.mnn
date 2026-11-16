from workspace import workspace
from pathlib import Path
import sys,json,re,time
import numpy as np
_,R=workspace();sys.path.insert(0,str(R/'Packages/com.eitan.mnn/Tools~/Models/TurnDetection'));from native_graph import Graph
class Easy:
 def __init__(self,w,audio='FP16',decoder='FP16',threads=2):
  self.w=w;self.audio=Graph(w/('audio-'+audio+'.mnn'),threads=threads);self.decoder=Graph(w/('decoder-'+decoder+'.mnn'),threads=threads)
  u=np.fromfile(w/'embeddings_bf16.bin',np.uint16).astype(np.uint32)<<16;self.emb=u.view(np.float32).reshape(-1,896);self.boundary=np.fromfile(w/'speech-boundary.f32',np.float32).reshape(2,896);self.prompt=json.loads((w/'prompt.json').read_text())['ids']
 def predict(self,fixture):
  z=np.load(fixture);audio,lens=self.audio.run(['embeddings','speech_length'],{k:z[k] for k in ['features','lengths']});n=int(lens.ravel()[0]);emb=np.concatenate([self.emb[self.prompt],self.boundary[:1],audio[0,:n],self.boundary[1:]],axis=0)[None];return self.generate(emb)
 def generate(self,emb):
  key=np.zeros((24,1,2,512,64),np.float32);value=np.zeros_like(key);tokens=[];pos=0
  for step in range(100):
   p=emb.shape[1];freq=np.arange(pos,pos+p,dtype=np.float32)[:,None]/(1000000**(np.arange(0,64,2,dtype=np.float32)/64))[None];freq=np.concatenate([freq,freq],-1);mask=np.full((1,1,p,512+p),-10000,np.float32);mask[:,:,:,:pos]=0;mask[:,:,:,512:]=np.triu(np.full((p,p),-10000,np.float32),1)
   logits,k,v=self.decoder.run(['logits','new_key','new_value'],dict(embeddings=emb,past_key=key,past_value=value,mask=mask,cos=np.cos(freq)[None,None],sin=np.sin(freq)[None,None],last_token_index=np.array([p-1],np.int64)))
   if pos+p>512:raise ValueError('context overflow')
   key[:,:,:,pos:pos+p]=k;value[:,:,:,pos:pos+p]=v;pos+=p;scores=logits.ravel()
   for t in set(tokens):scores[t]=scores[t]*1.05 if scores[t]<0 else scores[t]/1.05
   token=int(scores.argmax());tokens.append(token)
   if token==151643:return tokens
   emb=self.emb[token][None,None]
  raise ValueError('generation reached budget without EOS')
