# Hojo V2 conversion

`prepare.py` verifies `source-lock.json`, exactly promotes BF16 source tensors, rewrites the LM cache protocol, and invokes the official MNN converter. It preserves source assets. Install `numpy`, `onnx`, `onnxruntime`, `ml_dtypes` and official `MNN` in a dedicated Python environment; keep that environment and intermediate graphs under project `TestArtifacts~/`.

From the project root:

```sh
python Packages/com.eitan.mnn/Tools~/Models/HojoTts/prepare.py \
  --source Assets/StreamingAssets/MNN/Models/Hojo-TTS-Light-40M-V2-Source-MNN \
  --work TestArtifacts~/HojoTtsV2/onnx \
  --output Assets/StreamingAssets/MNN/Models/Hojo-TTS-Light-40M-V2-MNN \
  --precision fp16
```

The source lock supplies exact upstream file hashes and revision. The final deployment additionally requires the source license, model card, source/optimization metadata and checksums, all covered by the registered pinned file plan.

The graph tests use actual original ONNX tensors and inputs captured as `llm-fp32.onnx` and `fixtures/llm-1.npz`. Set `HOJO_TEST_WORK` to that fixture directory, then run `Tools~/Models/Tests/test_hojo_graph.py`. The padded-cache case deliberately poisons unused KV slots and compares real ONNX Runtime outputs.

Unity integration tests require `HOJO_TEST_ARTIFACTS`, the complete deployment under StreamingAssets, official reference PCM files and tokenizer/speech case tables. They are explicit and do not download by default. Run Metal separately; `PublishedInstallerDownloadsAndLoadsThePinnedExport` is the explicit network verification after publication.

`publish.py --model MODEL_DIRECTORY --results UNITY_RESULTS.xml` validates native/catalog evidence and every deployment hash before using the existing ModelScope SDK login. Credentials stay in the SDK login or environment. Use `--verify-only` for local verification. After publication, obtain the immutable commit from the file listing, verify the remote file sizes/hashes, update the one model registration revision, then run the package install/download test against that commit.
