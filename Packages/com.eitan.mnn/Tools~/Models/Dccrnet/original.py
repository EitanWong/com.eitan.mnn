from pathlib import Path
import hashlib
import json
import sys
import types
import torch

CHECKPOINT_SHA = "c1f519207604649cd90586e85adf2e93c4f4b26b216f0a71635013a90e64d939"


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
    from asteroid.models.dccrnet import DCCRNet
    from asteroid.masknn.recurrent import DCCRMaskNetRNN
    original_init = DCCRMaskNetRNN.__init__
    def integer_constructor(self, in_size, *args, **kwargs):
        return original_init(self, int(in_size), *args, **kwargs)
    DCCRMaskNetRNN.__init__ = integer_constructor
    value = torch.load(checkpoint, map_location="cpu", weights_only=False)
    if value["model_name"] != "DCCRNet" or value["model_args"] != dict(architecture="DCCRN-CL", stft_kernel_size=400, stft_stride=100, sample_rate=16000.0, n_src=1, n_freqs=200):
        raise ValueError("Unexpected original model protocol")
    model = DCCRNet(**value["model_args"]).eval()
    model.load_state_dict(value["state_dict"], strict=True)
    return model, value
