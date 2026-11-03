# Package tools

Unity ignores `Tools~`; these scripts do not compile into Runtime, Editor or test assemblies.
Run commands from the development repository root. No tool automatically downloads models or dependencies.

| Tool | Purpose | Requirements |
| --- | --- | --- |
| `Validation/validate_package.py` | Layout, assembly ownership, meta/GUID and documentation checks | Python 3.9+ standard library |
| `Validation/check_assembly_compilation.py` | Compile all eight asmdefs separately with declared package dependencies, including samples and test fixtures | Installed macOS Unity .NET/Roslyn, cached test framework references from the development project |
| `Models/prepare_image_profile.py` | Prepare SHA-checked Candy/DeepLab profiles with `--profile`; explicit optional source download | Python 3.9+, existing unmodified official MNN 3.6.1 converter |
| `Testing/generate_image_reference.py` | Generate independent RGB input/logit/PNG references from existing pinned Candy/DeepLab assets | Python with Pillow, numpy and onnxruntime or ai-edge-litert |
| `Testing/generate_sensevoice_reference.py` | Generate centered FunASR LFR/CMVN references from existing SenseVoice assets and WAVs | Python with numpy and kaldi-native-fbank; no downloads |
| `Validation/validate_repository.py` | Tracked assets, manifest exclusions and Markdown link membership | Python 3.11+ and Git |
| `Validation/test_*.py` | Repository-policy and CI-preparation regression tests | Python 3.11+ and Git |
| `Testing/prepare_ci_project.py` | Isolated Unity test project with the committed package | Python 3.11+; output under `TestArtifacts~/` |
| `Testing/check_platform_compilation.py` | 13 conditional C# compilation/import configurations | Installed macOS Unity .NET/Roslyn and `monodis` |
| `Testing/prepare_multimodal_fixtures.py` | Small local color images and WAV input | Python 3.9+, macOS `say`/`afconvert` and installed voice |
| `Testing/run_model_tests.py` | Six representative models, strict offline EditMode run | Python 3.9+, existing isolated Unity project, downloaded models and local fixtures |
| `Testing/discover_model_catalog.py` | Public ModelScope/Hugging Face MNN metadata audit; no weight downloads or inference | Python 3.9+ standard library, network access |
| `Testing/run_platform_tests.py` | PlayMode/Player inference or platform rejection policy | Python 3.9+, existing isolated Unity project and target build support |
| `Testing/prepare_mobile_llm.py` | Stage four existing LLM/VLM/audio model folders, image/WAV inputs and a 9-case mobile measurement plan; SHA-256 manifest, no downloads | Python 3.9+, existing weights and fixtures |
| `Testing/PlatformValidationSetup.cs` | Player launch/build configuration helper copied by the runner | Isolated Unity Editor only |

Use `python3 <tool-path> --help` for arguments. The repository-relative output root is `TestArtifacts~/`.
Sync the current package into an existing isolated project before running Unity tests; do not launch a second
Unity instance against an open project. A structural check or conditional compile is not a native-link,
device or model-inference test. A contract-only run verifies the supported-platform guard.

The representative model runner filters the six named fixtures as well as their category, so later
generation/Studio fixtures sharing `RepresentativeModel` do not silently expand this validation scope.
The helper class and assembly names remain stable when source files move.

See [project structure](../Documentation~/ProjectStructure.md) and [test evidence and commands](../Documentation~/Testing.md).

平台测试的 `--vision-root TestArtifacts~/ModelDiscovery/Vision` 使用本地已准备的
`modelscope/rap20171201/YOLOv8-N-MNN/` 和 `bus.jpg`，不会下载文件。
提供该选项时，两项 CPU/Auto YOLO Player 检测必须实际通过；未提供时明确跳过。
可与 `--bert-root`、`--native-bert-root` 一起回归。产物名称增加 `-vision` 后缀。

当前 `--vision-root` 同时要求已准备的十四组 PP-OCRv5/v4/fp16 profile、配套 DB 检测器及
`OCR/`、`OCR/Multilingual/`、`OCR/Pages/`、`OCR/AdditionalScripts/` 已知文字图片；
CPU/Auto 二十八项行识别与二十八项整页识别必须实际通过。
可用 `--label-suffix multilingual-page-ocr` 保留不同版本记录；整页 Auto 当前使用通过质量门槛的 CPU 配置。
`VisionPerformanceTests` 为显式本地基准，输出实际后端、加载/首轮/热运行中位数及 P95；
不会替用户下载模型，也不会改写 Studio 的手动线程设置。


Zipformer Player verification accepts `--asr-root` (upstream WAV fixture root)
and `--asr-models` (existing English/bilingual model directories) together with
`run_platform_tests.py`. Both are local inputs; the runner does not download them.
Four CPU/Auto ASR cases verify exact upstream English transcription, bilingual
smoke coverage, request-state reset and cancellation. `zipformer_asr_verified`
in the summary requires all four to pass; it does not certify other ASR models.


Whisper Player verification accepts `--whisper-root` pointing to existing pinned
weights for all five profiles under their `owner--repository/` folders, with
`en-0.wav`, `en-trans.txt` and `zh-en-0.wav` in `../../Zipformer/Audio/`. It sets `MNN_PLAYER_WHISPER_ROOT`; the runner
requires all six real Whisper Player cases (five English profiles and small Chinese
with language switching) to pass before setting `whisper_asr_verified`.
No assets are downloaded. Other exports, languages and recording limits remain
outside this check.

Qwen3-ASR Player verification accepts `--qwen3-asr-root` pointing to the existing
prepared `hb-dev--Qwen3-ASR-0.6B-INT8-MNN/` model, with English and Chinese/English
WAVs and `en-trans.txt` in `../../Zipformer/Audio/`. It sets
`MNN_PLAYER_QWEN3_ASR_ROOT`. All four real Qwen3-ASR cases (two language fixtures,
CPU and Auto, reset and token cancellation) must pass before the runner sets
`qwen3_asr_verified`. No model downloads are performed.

`run_platform_tests.py --paraformer-root <existing-root>` enables the actual Player
Chinese Paraformer reference/reset/cancellation test. The root contains the prepared
`Models/KindBrave-paraformer-online` profile and `Inspection/Reference/example--asr_example.wav`.
This option never downloads models and does not validate other Paraformer exports.

`run_platform_tests.py --silero-root <existing-root>` enables actual Player VAD
reference/reset/cancellation checks. The root contains the prepared `Models/`
profile and independent `Reference/two-utterances-{pcm,probs}.f32` fixtures.
No weights or ONNX runtime are downloaded by this runner.


`run_platform_tests.py --smart-turn-root <existing-root>` enables the actual
Player endpoint probability/decision, input ownership and cancellation checks.
The prepared `Models/` folder and `Reference/manifest.json` / `*-pcm.f32` must
already exist; this option never downloads model weights or test recordings.


`Testing/generate_pocket_reference.py` uses existing Pocket assets under
`Assets/StreamingAssets/MNN/Models/Pocket-TTS-MNN` to create 161 independent
SentencePiece cases, ONNX priming cache references and voice-encoder features.
Run with Python containing numpy, onnxruntime and sentencepiece. Output stays
under `TestArtifacts~/ModelDiscovery/Tts`; nothing is downloaded. ONNX is used
only by this reference tool, not the package's runtime or installation plan.

Mobile LLM verification stages `mobile-llm/` under the device persistent data path.
Use `prepare_mobile_llm.py --help` for CPU/Metal plans and `--plan-only` to reuse
weights. `run_platform_tests.py --require-mobile-llm` requires all four real model
tests to pass; it does not copy these large assets automatically. Android runs
stop the old Player before building, and device XML must match the generated
`mnn-build-id`, preventing a previous installation from supplying stale results.
The Player heartbeat timeout defaults to 1800 seconds, matching the whole-run
deadline. Synchronous native tests can block Unity frames for several minutes;
`--player-heartbeat-timeout` controls this separately from inference. A shorter
timeout can disconnect Unity Test Framework 1.1.33 while its result queue is
nonempty, causing its sender to spin before the device XML can be written.
iOS `--export-only` still requires signing, installation, launch and result copy;
export success alone is not device inference evidence.

`Testing/benchmark_llm.py` runs an existing official `llm_bench` and matching
library with a fixed shared-KV prefill/decode workload. It sweeps thread, backend,
precision and power settings in separate processes, verifies each reported
configuration, and writes binary/config identities and per-case JSON/logs into a
fresh project `TestArtifacts~/` directory. With `--adb-serial`, model assets must
already be on that device; only the runner/library are temporarily staged.
Synthetic throughput does not certify answer correctness or Unity overhead.
See [LLM performance](../Documentation~/LlmPerformance.md) for commands, real
managed correctness gates and the current device recommendations.

CI, runner setup and the distinction between static and native checks are documented in
[CI and validation](../Documentation~/development/ContinuousIntegration.md).
