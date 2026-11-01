from pathlib import Path
import hashlib
import json
import sys
import types
import torch

CHECKPOINT_SHA = "082a3f30c3f3a32f3c64ab7e6954b26fc5dd4ffa986bdd657049a9f3a93da7b6"


def load(checkpoint, source, filterbanks):
    hashes = json.loads(Path(__file__).with_name("source-hashes.json").read_text())
    for name, expected in hashes.items():
        kind, relative = name.split("/", 1)
        parent = source if kind == "asteroid" else filterbanks
        if hashlib.sha256((parent / relative).read_bytes()).hexdigest() != expected:
            raise ValueError("Pinned original source differs: " + name)
    if hashlib.sha256(checkpoint.read_bytes()).hexdigest() != CHECKPOINT_SHA:
        raise ValueError("Pinned checkpoint differs")
    sys.path.insert(0, str(filterbanks.parent))
    # Avoid the package root importing unrelated training frameworks. All actual
    # model/filterbank implementations are the original source modules.
    for name, path in [("asteroid", source), ("asteroid.masknn", source / "masknn"), ("asteroid.models", source / "models")]:
        module = types.ModuleType(name)
        module.__path__ = [str(path)]
        sys.modules[name] = module
    from asteroid.models.dcunet import DCUNet
    value = torch.load(checkpoint, map_location="cpu", weights_only=False)
    if value["model_name"] != "DCUNet" or value["model_args"] != dict(architecture="Large-DCUNet-20", stft_n_filters=1024, stft_kernel_size=1024, stft_stride=256, sample_rate=16000.0, fix_length_mode="pad", n_src=1):
        raise ValueError("Unexpected original model protocol")
    model = DCUNet(**value["model_args"]).eval()
    model.load_state_dict(value["state_dict"], strict=True)
    return model, value
