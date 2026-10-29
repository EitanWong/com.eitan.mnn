"""Local-only upstream model loading and explicit bidirectional inference."""
import hashlib
import sys
from pathlib import Path
import torch


def sha256(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def load_model(upstream, checkpoint, device):
    sys.path.insert(0, str(Path(upstream).resolve()))
    from omnivoice.models.omnivoice import OmniVoice
    model = OmniVoice.from_pretrained(str(Path(checkpoint).resolve()), train=True,
        local_files_only=True, attn_implementation='sdpa', dtype=torch.float32)
    model.config.llm_config.use_cache = False
    model.llm.config.use_cache = False
    return model.eval().to(device)


def target_logits(model, ids, audio_mask, frames):
    # Passing a raw bool mask to HF can invoke causal-mask construction. Use the
    # same explicit full_attention mapping as the validated ONNX export.
    embeddings = model._prepare_embed_inputs(ids, audio_mask)
    sequence = ids.shape[-1]
    bias = embeddings.new_zeros((ids.shape[0], 1, sequence, sequence))
    positions = torch.arange(sequence, device=ids.device)[None].expand(ids.shape[0], -1)
    hidden = model.llm(inputs_embeds=embeddings, attention_mask={'full_attention': bias},
        position_ids=positions, use_cache=False, return_dict=True)[0][:, -frames:]
    flat = model.audio_heads(hidden)
    return flat.view(ids.shape[0], frames, 8, 1025).permute(0,2,1,3).contiguous()


def set_target(record, tokens):
    ids = record['input_ids'].clone()
    ids[:, :, -tokens.shape[-1]:] = tokens
    return ids


def apply_patch(model, path, expected_base_hash):
    patch = torch.load(path, map_location='cpu', weights_only=True)
    if patch['kind'] != 'omnivoice-distillation-patch-v1' or patch['base_sha256'] != expected_base_hash:
        raise ValueError('Patch does not match this base checkpoint.')
    state = model.state_dict()
    for name, value in patch['state_dict'].items():
        if name not in state or state[name].shape != value.shape or not torch.isfinite(value).all():
            raise ValueError('Invalid patch tensor: ' + name)
    model.load_state_dict(patch['state_dict'], strict=False)
    return patch
