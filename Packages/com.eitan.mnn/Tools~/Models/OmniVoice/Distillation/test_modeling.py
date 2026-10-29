"""Tiny actual Qwen3 checks, requiring a local upstream checkout, no weights."""
import os
import sys
import unittest
from pathlib import Path
import torch
from modeling import target_logits
from objectives import trajectory_ce

sys.path.insert(0,str(Path(os.environ['OMNIVOICE_UPSTREAM']).resolve()))
from omnivoice.models.omnivoice import OmniVoice, OmniVoiceConfig
from transformers import Qwen3Config


class ModelingTests(unittest.TestCase):
    def test_future_audio_affects_earlier_logit_and_gradients_work(self):
        torch.manual_seed(7)
        llm=Qwen3Config(vocab_size=2048,hidden_size=32,intermediate_size=64,
            num_hidden_layers=2,num_attention_heads=4,num_key_value_heads=2,head_dim=8)
        llm._attn_implementation='sdpa'
        model=OmniVoice(OmniVoiceConfig(llm_config=llm.to_dict())).eval()
        ids=torch.full((1,8,3),1024,dtype=torch.long)
        mask=torch.ones((1,3),dtype=torch.bool)
        original=target_logits(model,ids,mask,3)
        changed=ids.clone(); changed[:,:,-1]=2
        actual=target_logits(model,changed,mask,3)
        self.assertGreater((actual[:,:,0]-original[:,:,0]).abs().max().item(),1e-6)
        endpoint=torch.full((1,8,3),3,dtype=torch.long)
        supervision=torch.zeros_like(endpoint,dtype=torch.bool); supervision[:,:,-1]=True
        loss=trajectory_ce(actual,endpoint,supervision)
        loss.backward()
        self.assertGreater(model.audio_heads.weight.grad.abs().sum().item(),0)
        self.assertTrue(torch.isfinite(model.audio_heads.weight.grad).all())


if __name__=='__main__': unittest.main()
