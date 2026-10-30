# SpEx+ WSJ0-2mix reproduction

Pinned public checkpoint `alibabasglab/log_wsj0-2mix_speech_SpEx-plus_2spk`,
revision `2b99a144297ac4d074bb9dcc4ce9734a7e8924fd`, SHA256
`5ffcf87a45f46ece3fa43db5b4d7f9779a73392933fed0b563f0ead9bd9b492f`.
ClearerVoice-Studio source revision `6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61`;
the original wrapper loads every checkpoint key strictly and remains unchanged.

```sh
PYTHONDONTWRITEBYTECODE=1 python3 prepare.py \
  --source /absolute/path/to/ClearerVoice-Studio/train/target_speaker_extraction \
  --checkpoint /absolute/path/to/last_best_checkpoint.pt \
  --converter /absolute/path/to/MNNConvert \
  --output /absolute/path/to/TestArtifacts~/SpExPlusReproduction
```

Requires PyTorch, PyYAML, NumPy, ONNX, ONNX Runtime and matching MNN Python.
Batch one, unpadded 8 kHz mono input; mixture 20–32000 samples, reference
280–64000 samples. The eight-second reference bound is this validated deployment
bound; the author's four-second data-loader truncation applies only to mixtures.
No audio normalization or unit-length embedding normalization is added.

The independent clone separates reference encoding (256 floats) from extraction,
prunes the training speaker classifier and secondary decoders, replaces the
primary dynamic tail pad by fixed nine-zero padding plus trimming, and computes
global means/centered variances in separate channel/time reductions. This removes
large CPU accumulation drift without changing normalization epsilon or gamma/beta
ordering. Channel normalization uses an explicit centered variance. Source
speaker embeddings and complete primary waveforms are compared independently.

This command checks eight seeded random/boundary pairs against the original
wrapper, split PyTorch, ONNX, and repeated MNN dynamic execution, then returns to
the first shape. Every embedding and waveform NRMSE must be below 0.001. Public
Net UUIDs are deterministic. Broader physical, accelerator, Unity, Studio,
performance and built-Player tests run separately; conversion success alone
proves none of them. The full physical gate also covers real two-speaker speech,
quiet/gain/DC, reference changes, silent mixture/reference and pooling boundaries.

The source weights produce large raw amplitudes under scale-invariant training.
The runtime API preserves these values; Studio peak-normalizes playback. Silence
is not replaced by zero. This is one target waveform, without persistent speaker
IDs, causal streaming, long-recording overlap-add or speech transcription.
Retain the author model card, root Apache2 and source MIT notices including
Meng Ge, Jee-weon Jung and the referenced WaveRNN attribution. WSJ0 corpus rights
are separate. Raw checkpoints, ONNX, diagnostics and dependencies remain under
TestArtifacts; deploy only selected graphs, configuration and attribution files.
No C++, native bridge, custom export or official MNN source change is needed.
