# NAMO v1 multilingual preparation

Use the original author-published FP32 ONNX and tokenizer at revision
`59245aac1f0a0f170277ebecc32ddaea418083b4` of
`videosdk-live/Namo-Turn-Detector-v1-Multilingual`. This is an ONNX source
comparison; it does not establish parity with an unavailable original PyTorch
checkpoint. The author's default dynamically quantized ONNX is a separate
reference and is not silently substituted for the FP32 source.

```sh
python prepare.py --source <frozen-local-source-folder> \
  --converter <official-MNNConvert> --output <TestArtifacts-work-folder>
```

Requires NumPy, ONNX, ONNX Runtime and the official MNN Python package. The
tool does not install dependencies or download weights. It verifies four source
SHA-256 values, groups each of the 22 attention layers into three independent
groups of four heads, compares source and lowered ONNX at 11 conditions through
8192 tokens, converts FP16 weights and checks MNN probabilities against the
unchanged FP32 source with an absolute error budget of 0.001.

The compact tokenizer retains all 256,000 vocabulary entries, 580,604 ranked
merges and 249 added tokens. Its runtime contract is ASCII-space replacement,
Metaspace splitting, exact added-token matching, Unicode scalar BPE, byte
fallback and BOS/EOS-preserving right truncation. Unity tokenizer parity is an
independent Hugging Face tokenizer test, including full-context truncation.

Only the selected `.mnn`, `tokenizer.txt` and runtime JSON are deployment
outputs. Keep ONNX, reports and logs in the work folder. The public
`Net.mnn_uuid` is fixed to the validated initial export UUID for reproducibility;
no official native source or private runtime memory layouts are modified.
