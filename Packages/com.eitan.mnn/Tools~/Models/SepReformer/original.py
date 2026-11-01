from pathlib import Path
import hashlib
import json
import sys
import torch
import yaml

REVISION = "6dd3d163cc7c1f3a5534f22daf68382a8ea135e9"
CHECKPOINT_SHA = "336d6f40035e00496aacbe42c9ddf263d21b25dcb7941b5b90e245e9145043d2"


def load(source, checkpoint):
    hashes = json.loads(Path(__file__).with_name("source-hashes.json").read_text())
    for name, expected in hashes.items():
        if hashlib.sha256((source / name).read_bytes()).hexdigest() != expected:
            raise ValueError("Pinned source differs: " + name)
    if hashlib.sha256(checkpoint.read_bytes()).hexdigest() != CHECKPOINT_SHA:
        raise ValueError("Pinned checkpoint differs")
    sys.path.insert(0, str(source))
    from models.SepReformer_Base_WSJ0.model import Model
    from loguru import logger
    logger.remove()
    config = yaml.safe_load((source / "models/SepReformer_Base_WSJ0/configs.yaml").read_text())["config"]
    if config["dataset"]["sampling_rate"] != 8000 or config["model"]["num_spks"] != 2:
        raise ValueError("Unexpected original model protocol")
    value = torch.load(checkpoint, map_location="cpu", weights_only=False)
    model = Model(**config["model"]).eval()
    model.load_state_dict(value["model_state_dict"], strict=True)
    return model, config["model"]
