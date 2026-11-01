"""Strict source/checkpoint inspection; original inference math stays unchanged."""
from pathlib import Path
import hashlib
import importlib
import inspect
import json
import sys
import types

import torch

CONTRACT = json.loads(Path(__file__).with_name('contracts.json').read_text())
from collections import defaultdict
from typing import Any


class MetadataOnlyBase(torch.nn.Module):
    """Only the training/logging base is omitted; the original DCCRN ctor/forward run."""
    def __init__(self, **kwargs):
        super().__init__()
        self.hparams = types.SimpleNamespace(**kwargs)

    def save_hyperparameters(self, *names):
        values = inspect.currentframe().f_back.f_locals
        for name in names:
            setattr(self.hparams, name, values[name])


def original(source, checkpoint, metadata_dependency_path):
    if torch.__version__ != CONTRACT['torch_version'] or torch.version.git_version != CONTRACT['torch_revision']:
        raise ValueError('Use the pinned original Torch build for reproducible source comparison')
    sys.path.append(str(metadata_dependency_path))
    from omegaconf.base import ContainerMetadata, Metadata
    from omegaconf.listconfig import ListConfig
    from omegaconf.nodes import AnyNode
    for name, expected in CONTRACT['source_sha256'].items():
        assert hashlib.sha256((source / name).read_bytes()).hexdigest() == expected, name
    assert checkpoint.stat().st_size == CONTRACT['checkpoint_bytes']
    assert hashlib.sha256(checkpoint.read_bytes()).hexdigest() == CONTRACT['checkpoint_sha256']
    # Explicit safe data types for old Lightning checkpoint metadata; no arbitrary pickle execution.
    with torch.serialization.safe_globals([dict, list, int, defaultdict, ContainerMetadata, Metadata, ListConfig, AnyNode, torch.torch_version.TorchVersion, Any]):
        data = torch.load(checkpoint, map_location='cpu', weights_only=True)
    for name, folder in [('mayavoz', source / 'mayavoz'), ('mayavoz.models', source / 'mayavoz/models'), ('mayavoz.utils', source / 'mayavoz/utils')]:
        module = types.ModuleType(name)
        module.__path__ = [str(folder)]
        sys.modules[name] = module
    sys.modules['mayavoz.models'].Mayamodel = MetadataOnlyBase
    dataset = types.ModuleType('mayavoz.data')
    dataset.MayaDataset = type('MayaDataset', (), {})
    sys.modules[dataset.__name__] = dataset
    network_type = importlib.import_module('mayavoz.models.dccrn').DCCRN
    configuration = dict(num_channels=1, sampling_rate=16000, complex_lstm=True, complex_norm=True,
                         complex_relu=True, masking_mode='E',
                         encoder_decoder=dict(initial_output_channels=32, depth=6, kernel_size=5, growth_factor=2,
                                              stride=2, padding=2, output_padding=1),
                         lstm=dict(num_layers=2, hidden_size=256), stft=dict(window_len=400, hop_size=100, nfft=512))
    model = network_type(**configuration).eval().requires_grad_(False)
    tensors = data['state_dict']
    model.load_state_dict(data['state_dict'], strict=True, assign=True)
    return model, data.get('hyper_parameters', {}), configuration
