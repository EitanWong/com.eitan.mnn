from pathlib import Path
from types import SimpleNamespace
import hashlib,json,sys
import torch,yaml

REVISION='2b99a144297ac4d074bb9dcc4ce9734a7e8924fd'
SOURCE_REVISION='6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61'
CHECKPOINT_SHA='5ffcf87a45f46ece3fa43db5b4d7f9779a73392933fed0b563f0ead9bd9b492f'

def namespace(value):
    return SimpleNamespace(**{k:namespace(v) if isinstance(v,dict) else v for k,v in value.items()})

def load(source,checkpoint):
    for name,expected in json.loads(Path(__file__).with_name('source-hashes.json').read_text()).items():
        if hashlib.sha256((source/name).read_bytes()).hexdigest()!=expected:raise ValueError('Pinned source differs: '+name)
    if hashlib.sha256(checkpoint.read_bytes()).hexdigest()!=CHECKPOINT_SHA:raise ValueError('Pinned checkpoint differs')
    config=yaml.safe_load((source/'config/config_wsj0-2mix_speech_SpEx-plus_2spk.yaml').read_text())
    if (config['audio_sr'],config['ref_sr'],config['causal'])!=(8000,8000,0):raise ValueError('Unexpected source contract')
    args=namespace(config);args.device='cpu';sys.path.insert(0,str(source.resolve()))
    from networks import network_wrapper
    model=network_wrapper(args).eval()
    saved=torch.load(checkpoint,map_location='cpu',weights_only=False)
    state={k.removeprefix('module.'):v for k,v in saved['model'].items()}
    model.load_state_dict(state,strict=True)
    return model,config

def source_forward(model,mixture,reference):
    return model(mixture,(reference,torch.tensor([reference.shape[-1]]),torch.tensor([-1])))
