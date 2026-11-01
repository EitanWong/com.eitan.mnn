from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import hashlib,json,os,sys
import torch,yaml

REVISION='0b05921c9bf896a0cf772ac75521b7a35a671c4f'
SOURCE_REVISION='6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61'
CHECKPOINT_SHA='0025f15957716bf3f46297cf55b3c40930c00a1843821d8f0c642a18e7874d12'

def namespace(value):
    return SimpleNamespace(**{k:namespace(v) if isinstance(v,dict) else v for k,v in value.items()})

def load(source,checkpoint,output):
    for name,expected in json.loads(Path(__file__).with_name('source-hashes.json').read_text()).items():
        if hashlib.sha256((source/name).read_bytes()).hexdigest()!=expected:raise ValueError('Pinned source differs: '+name)
    if hashlib.sha256(checkpoint.read_bytes()).hexdigest()!=CHECKPOINT_SHA:raise ValueError('Pinned checkpoint differs')
    config=yaml.safe_load((source/'config/config_LRS2_lip_convtasnet_2spk.yaml').read_text())
    if config['audio_sr']!=16000 or config['ref_sr']!=25 or config['causal']!=0:raise ValueError('Unexpected author protocol')
    args=namespace(config);args.device='cpu'
    saved=torch.load(checkpoint,map_location='cpu',weights_only=False)
    state={k.removeprefix('module.'):v for k,v in saved['model'].items()}
    initial={k.removeprefix('ref_encoder.v_frontend.'):v for k,v in state.items() if k.startswith('ref_encoder.v_frontend.')}
    temporary=output.resolve()/'constructor';target=temporary/'models/visual_frontend/resnet18.pth';target.parent.mkdir(parents=True,exist_ok=True)
    torch.save(initial,target)
    previous=Path.cwd();sys.path.insert(0,str(source.resolve()))
    try:
        os.chdir(temporary)
        from networks import network_wrapper
        model=network_wrapper(args).eval()
        model.load_state_dict(state,strict=True)
    finally:
        os.chdir(previous)
        target.unlink(missing_ok=True)
        for directory in [target.parent,target.parent.parent,temporary]:
            if directory.is_dir() and not any(directory.iterdir()):directory.rmdir()
    return model,config

def source_forward(model,audio,visual):
    # Device compatibility only: preserve the author's integer index_add oracle.
    with patch.object(torch.Tensor,'cuda',lambda self,*args,**kwargs:self):return model(audio,visual)
