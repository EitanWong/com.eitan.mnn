from workspace import workspace
from pathlib import Path
import sys,types,typing,torch,json
A,R=workspace();deps=A/'dependencies'
import torch.nn.modules.conv as conv
conv.Union=typing.Union;conv.Optional=typing.Optional;conv._size_2_t=tuple
for name,path in [('wenet',deps/'Easy-Turn/Easy_Turn/wenet'),('whisper',deps/'whisper')]:
 module=types.ModuleType(name);module.__path__=[str(path)];sys.modules[name]=module
from wenet.transformer.encoder import TransformerEncoder
from wenet.llm_asr.downsampler import get_downsampler
import yaml
cfg=yaml.safe_load((deps/'Easy-Turn/Easy_Turn/examples/wenetspeech/whisper/conf/train.yaml').read_text())
state=torch.load(R/'Assets/StreamingAssets/MNN/Models/Easy-Turn-Source/checkpoint.pt',mmap=True,weights_only=True,map_location='cpu')
torch.set_num_threads(4)
class Audio(torch.nn.Module):
 def __init__(self):
  super().__init__();self.encoder=TransformerEncoder(cfg['input_dim'],**cfg['encoder_conf']);self.down_sample_2=get_downsampler(4,1024);self.speech_transformer=TransformerEncoder(input_size=1024,output_size=1024,attention_heads=4,linear_units=2560,num_blocks=4,dropout_rate=.1,positional_dropout_rate=.1,attention_dropout_rate=0,input_layer='linear',pos_enc_layer_type='abs_pos',normalize_before=True);self.speech_llama_proj=torch.nn.Linear(1024,896)
 def forward(self,features,lengths):
  x,mask=self.encoder(features,lengths);x,mask=self.down_sample_2(x,mask);length=mask.squeeze(1).sum(-1);x,mask=self.speech_transformer(x,length);return self.speech_llama_proj(x),length
m=Audio().eval()
for name,mod in [('encoder',m.encoder),('speech_transformer',m.speech_transformer)]:mod.embed.pos_enc.pe=state[name+'.embed.pos_enc.pe']
keys={k:v for k,v in state.items() if k.startswith(('encoder.','down_sample_2.','speech_transformer.','speech_llama_proj.'))};m.load_state_dict(keys,strict=True,assign=True)
w=A/'Easy';w.mkdir(exist_ok=True)
with torch.inference_mode():
 x=torch.zeros((1,800,80));length=torch.tensor([640]);out=m(x,length);print('AUDIO',[(tuple(v.shape),v.dtype) for v in out],flush=True)
 if not (w/'audio.onnx').exists():torch.onnx.export(m,(x,length),str(w/'audio.onnx'),input_names=['features','lengths'],output_names=['embeddings','speech_length'],opset_version=17,dynamo=False,external_data=False)
 print('AUDIO_EXPORTED',flush=True)
import numpy as np
from transformers import WhisperFeatureExtractor
extractor=WhisperFeatureExtractor(feature_size=80,sampling_rate=16000,hop_length=160,chunk_length=8,n_fft=400)
for p in sorted((A/'Bairong/fixtures-1.0').glob('*-input.f32')):
 pcm=np.fromfile(p,np.float32);frames=len(pcm)//160
 # Independent Whisper extractor without normalization, trim true feature frames; pad features rather than waveform.
 stft=torch.stft(torch.from_numpy(pcm),400,160,window=torch.hann_window(400),return_complex=True);power=stft[...,:-1].abs().square();mel=torch.from_numpy(extractor.mel_filters.T.astype(np.float32))@power;log=mel.clamp(min=1e-10).log10();feats=((torch.maximum(log,log.max()-8)+4)/4).T.numpy().copy();assert len(feats)==frames;padded=np.pad(feats,((0,800-frames),(0,0)))
 with torch.inference_mode():emb,lens=m(torch.from_numpy(padded[None]),torch.tensor([frames]))
 label=p.name.removesuffix('-input.f32');np.savez(w/(label+'.npz'),features=padded[None],lengths=np.array([frames],np.int64),embeddings=emb.numpy(),speech_length=lens.numpy());print('AUDIO_REFERENCE',label,int(lens[0]),flush=True)
