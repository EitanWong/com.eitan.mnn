"""Bounded centered GroupNorm reductions preserve the pinned source waveform budget."""
import copy
import types
import torch
import torch.nn.functional as F

def bounded_mean(value):
    count=value.shape[-1]
    for _ in range(4):
        length=value.shape[-1]
        value=F.pad(value,(0,(64-length%64)%64)).reshape(value.shape[0],value.shape[1],-1,64).sum(-1)
    return value.sum(-1,keepdim=True)/count

def norm(self,x):
    flat=x.reshape(x.shape[0],self.num_groups,-1)
    mean=bounded_mean(flat);centered=flat-mean;variance=bounded_mean(centered*centered)
    value=(centered/torch.sqrt(variance+self.eps)).reshape(x.shape)
    return value*self.weight.reshape(1,-1,1,1)+self.bias.reshape(1,-1,1,1)

def lower(model):
    model=copy.deepcopy(model).eval()
    for m in model.modules():
        if isinstance(m,torch.nn.GroupNorm):m.forward=types.MethodType(norm,m)
    return model
