"""Export the released Thinker+Talker to ONNX without altering upstream code.

Scope: token inputs and audio-code outputs only. Frozen encoders and Mimi are
not part of these graphs; these files are explicitly not a complete Omni app.
"""
#!/usr/bin/env python3
import importlib
import json
from pathlib import Path
import sys
import types
import numpy as np
import onnx
import onnxruntime as ort
import torch
from torch import nn
from transformers import AutoTokenizer

import argparse
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--reference', type=Path, required=True)
parser.add_argument('--work', type=Path, required=True)
args = parser.parse_args()
REFERENCE = args.reference.resolve()
OUT = args.work.resolve()
OUT.mkdir(parents=True, exist_ok=True)
torch.set_num_threads(4)
torch.manual_seed(42)
package = types.ModuleType("released_minimind")
package.__path__ = [str(REFERENCE)]
sys.modules[package.__name__] = package
native = importlib.import_module("released_minimind.model_omni")
config = native.OmniConfig(**json.loads((REFERENCE / "config.json").read_text()))
config.flash_attn = False
model = native.MiniMindOmni(config)
state = torch.load(REFERENCE / "pytorch_model.bin", map_location="cpu", weights_only=True)
model.load_state_dict(state, strict=True)
model.eval()
for block in list(model.thinker.layers) + list(model.talker.layers):
    block.self_attn.flash = False
print("Loaded released core parameters:", sum(p.numel() for p in model.parameters()), flush=True)
tokenizer = AutoTokenizer.from_pretrained(REFERENCE)

class Core(nn.Module):
    def __init__(self, net, decode):
        super().__init__()
        self.net = net
        self.decode = decode
    def forward(self, tokens, audio_codes, past=None):
        kv = [(past[i, 0], past[i, 1]) for i in range(12)] if self.decode else None
        out = self.net(torch.cat((audio_codes, tokens.unsqueeze(1)), dim=1),
                       past_key_values=kv, use_cache=True, logits_to_keep=1)
        cache = torch.stack([torch.stack(pair) for pair in out.past_key_values])
        return out.logits, torch.stack(out.audio_logits), cache

prompt = "请用一句话介绍你自己。"
text = tokenizer.apply_chat_template([{"role": "user", "content": prompt}], tokenize=False,
                                     add_generation_prompt=True, open_thinking=False)
tokens = tokenizer(text, return_tensors="pt").input_ids
audio = torch.full((1, 8, tokens.shape[1]), config.audio_pad_token, dtype=torch.long)
prefill = Core(model, False)
decode = Core(model, True)
with torch.inference_mode():
    expected = prefill(tokens, audio)
    next_token = expected[0][:, -1].argmax(-1).unsqueeze(1)
    next_audio = torch.full((1, 8, 1), config.audio_pad_token, dtype=torch.long)
    decode_args = (next_token, next_audio, expected[2])
    decode_expected = decode(*decode_args)
    for name, module, args in (("prefill", prefill, (tokens, audio)), ("decode", decode, decode_args)):
        axes = {"tokens": {1: "sequence"}, "audio_codes": {2: "sequence"},
                "present": {3: "total_sequence"}}
        inputs = ["tokens", "audio_codes"]
        if name == "decode":
            axes["past"] = {3: "past_sequence"}
            inputs.append("past")
        torch.onnx.export(module, args, str(OUT / (name + ".onnx")),
                          input_names=inputs, output_names=["text_logits", "audio_logits", "present"],
                          dynamic_axes=axes, opset_version=17, dynamo=False)
        onnx.checker.check_model(str(OUT / (name + ".onnx")))
        print("Exported", name, flush=True)
    opts = ort.SessionOptions()
    opts.intra_op_num_threads = 4
    checks = {}
    for name, args, reference in (("prefill", (tokens, audio), expected),
                                  ("decode", decode_args, decode_expected)):
        session = ort.InferenceSession(str(OUT / (name + ".onnx")), opts, providers=["CPUExecutionProvider"])
        feed = {key: value.numpy() for key, value in zip(["tokens", "audio_codes", "past"], args)}
        got = session.run(None, feed)
        checks[name] = {}
        for key, x, ref in zip(["text_logits", "audio_logits", "present"], got, reference):
            err = float(np.max(np.abs(x - ref.numpy())))
            np.testing.assert_allclose(x, ref.numpy(), rtol=2e-3, atol=2e-3)
            checks[name][key + "_max_abs_error"] = err
        np.savez(OUT / (name + "-fixture.npz"), **feed,
                 reference_text=reference[0].numpy(), reference_audio=reference[1].numpy())
        print("ORT verification", name, checks[name], flush=True)
(OUT / 'core-export-results.json').write_text(json.dumps({'onnx_checks': checks}, indent=2) + '\n')
