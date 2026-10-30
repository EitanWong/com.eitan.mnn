"""Independent batch-one SpEx+ split with staged normalization reductions."""
import copy,types,torch

class Encoder(torch.nn.Module):
    def __init__(self,model):
        super().__init__();self.short=model.encoder_1d_short;self.middle=model.encoder_1d_middle;self.long=model.encoder_1d_long
    def forward(self,x):
        a=torch.relu(self.short(x));t=a.shape[-1];n=x.shape[-1]
        b=torch.relu(self.middle(torch.nn.functional.pad(x,(0,(t-1)*10+80-n))))
        c=torch.relu(self.long(torch.nn.functional.pad(x,(0,(t-1)*10+160-n))))
        return a,b,c

class Reference(torch.nn.Module):
    def __init__(self,model):
        super().__init__();self.encoder=Encoder(model);self.speaker=model.aux_enc3
    def forward(self,reference):
        # Batch one, no edge padding: the author's aux_len denominator is
        # exactly the pooled feature length. Preserve embedding magnitude.
        return self.speaker(torch.cat(self.encoder(reference),1)).mean(-1)

class Extraction(torch.nn.Module):
    def __init__(self,model):
        super().__init__();self.encoder=Encoder(model);self.ln=model.ln;self.proj=model.proj
        self.blocks=torch.nn.ModuleList([getattr(model,'conv_block_'+str(i)) for i in range(1,5)])
        self.other=torch.nn.ModuleList([getattr(model,'conv_block_'+str(i)+'_other') for i in range(1,5)])
        self.mask=model.mask1;self.decoder=model.decoder_1d_1
    def forward(self,mixture,embedding):
        w=self.encoder(mixture);y=self.proj(self.ln(torch.cat(w,1)))
        for block,other in zip(self.blocks,self.other):y=other(block(y,embedding))
        decoded=torch.nn.functional.conv_transpose1d(w[0]*torch.relu(self.mask(y)),self.decoder.weight,self.decoder.bias,stride=10)
        # At most nine source zero-tail samples. Fixed padding removes ONNX's
        # dynamic Pad/Squeeze control-flow without changing decoder arithmetic.
        return torch.nn.functional.pad(decoded,(0,9)).reshape(1,-1)[:,:mixture.shape[-1]]

def centered_norm(self,x):
    mean=x.mean(1,keepdim=True)
    centered=x-mean
    return centered/torch.sqrt((centered*centered).mean(1,keepdim=True)+self.eps)*self.weight.reshape(1,-1,1)+self.bias.reshape(1,-1,1)

def staged_global_norm(self,x):
    mean=x.mean(1,keepdim=True).mean(2,keepdim=True)
    centered=x-mean
    var=(centered*centered).mean(1,keepdim=True).mean(2,keepdim=True)
    return self.gamma*centered/torch.sqrt(var+self.eps)+self.beta

def lower(model):
    result=copy.deepcopy(model)
    for module in result.modules():
        if type(module).__name__=='ChannelWiseLayerNorm':module.forward=types.MethodType(centered_norm,module)
        if type(module).__name__=='GlobalChannelLayerNorm':module.forward=types.MethodType(staged_global_norm,module)
    return result

