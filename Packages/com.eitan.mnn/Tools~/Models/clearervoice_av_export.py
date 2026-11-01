"""Shared independent export lowering for ClearerVoice audiovisual models.

The source models remain unchanged; callers apply these functions to a deep copy.
"""
import torch
import torch.nn.functional as F

def decoder(self,mixture,mask):
    return torch.nn.functional.conv_transpose1d(mixture*mask,self.basis_signals.weight.T.unsqueeze(1),stride=self.L//2)[:,0]

def frontend(self,batch):
    # B=1, grayscale112px contract; depth5 temporal convolution equals five
    # adjacent frames as the input channels of one spatial Conv2d.
    count=batch.shape[2]
    flat=batch[0].transpose(0,1)
    zero=flat.new_zeros((2,1,112,112))
    padded=torch.cat([zero,flat,zero],dim=0)
    channels=torch.cat([padded[i:i+count] for i in range(5)],dim=1)
    convolution=self.frontend3D[0]
    output=F.conv2d(channels,convolution.weight.reshape(64,5,7,7),stride=2,padding=3)
    norm=self.frontend3D[1]
    output=F.batch_norm(output,norm.running_mean,norm.running_var,norm.weight,norm.bias,False,norm.momentum,norm.eps)
    output=F.max_pool2d(F.relu(output),kernel_size=3,stride=2,padding=1)
    output=self.resnet(output).reshape(1,count,512)
    return output.transpose(1,2)

class Visual(torch.nn.Module):
    def __init__(self,model):super().__init__();self.ref=model.ref_encoder
    def forward(self,frames):return self.ref(frames)

class Audio(torch.nn.Module):
    def __init__(self,model):super().__init__();self.net=model.sep_network
    def forward(self,mixture,visual_features):return self.net(mixture,visual_features)
