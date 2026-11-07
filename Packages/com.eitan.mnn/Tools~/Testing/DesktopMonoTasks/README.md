# Desktop Mono native task checks

`run_desktop_mono_tasks.py` compiles the package's production C# code against
installed Mono framework assemblies using an installed Unity Roslyn compiler.
Only Unity host services are adapted; all MNN calls use the production interop
partials and bundled official C++ symbols. Docker images and local model files
must already exist. The runner does not download or install dependencies.

Run from the Unity project root, substituting the installed Unity executable:

```sh
python3 Packages/com.eitan.mnn/Tools~/Testing/run_desktop_mono_tasks.py \
  --project "$PWD" --unity UNITY_EXECUTABLE --platform Linux --task express \
  --output "$PWD/TestArtifacts~/DesktopMonoTasks/LinuxExpress"
```

For `--task llm`, `embedding`, `reranker`, or `omni`, also supply `--model` with
an existing compatible model directory. The checked fixtures use SmolLM2 135M,
bge-small-zh-v1.5, Qwen3 Reranker 0.6B, and SmolVLM 256M respectively. The Omni
check creates a deterministic red PNG inside its output directory. Express uses
the package's embedded affine model and checks constants, Modules, Variables,
numerical outputs, shape changes, GC and disposal. LLM checks config/tokenizer,
greedy text/chat, template counting, streaming snapshots/deltas, cancellation,
recovery and disposal. x64 conversation caching keeps the verified full-prefill
fallback; speech waveform callbacks remain unsupported.

Windows requires `--platform Windows --wine-prefix EXISTING_PREFIX`; the default
image is `codex-mnn-wine-mono:ubuntu24.04-wine9`. Linux defaults to
`codex-mnn-linux-gcc13` and runs Mono's interpreter to avoid the emulated x64 JIT
failure. Override these with `--wine-image` and `--linux-image`. Use
`--mono-references` if the runner cannot locate installed Mono 4.5 assemblies.
`--package` permits a candidate, pristine baseline or restored package copy.

Use separate output directories for concurrent runs. Serialize all runs sharing
one Wine prefix. Every compile/run records exact commands, stdout, stderr, exit
status, and source/native hashes; reruns preserve prior logs under `History/`.
Outputs must stay inside the project's `TestArtifacts~/`. These checks establish
native task behavior under host Mono/Wine. They are not Unity Editor/Player or
Windows hardware validation. The Runtime `ExpressPlayerTests` cover the same
numerical graph and Module path when run in actual Unity Players.
