# Windows Unity LLM benchmark

Use these C# sources in a disposable Unity project under the repository's
`TestArtifacts~/`. They are tooling, excluded from the UPM runtime assemblies.
The project needs the current package, a licensed Unity Editor and Windows build
modules; IL2CPP also needs the Visual Studio C++ toolchain and Windows SDK.
Reuse complete existing MNN exports. Nothing downloads weights.

Copy `MNNWindowsLlmBenchmark.cs` to the isolated project's `Assets/` and
`MNNWindowsLlmBenchmarkSetup.cs` to its `Assets/Editor/`. From PowerShell:

```powershell
$env:MNN_WINDOWS_BENCHMARK_BACKEND = 'IL2CPP' # or Mono
$env:MNN_WINDOWS_BENCHMARK_PLAYER = 'E:/PROJECT/TestArtifacts~/Benchmark/Benchmark.exe'
$unity = 'C:/Program Files/Unity/Hub/Editor/2021.3.45f2/Editor/Unity.exe'
$arguments = @('-batchmode', '-nographics', '-quit',
    '-projectPath', 'E:/PROJECT/TestArtifacts~/BenchmarkProject',
    '-buildTarget', 'Win64', '-executeMethod', 'MNNWindowsLlmBenchmarkSetup.Build',
    '-logFile', 'E:/PROJECT/TestArtifacts~/Benchmark/build.log')
$build = Start-Process $unity -ArgumentList $arguments -Wait -PassThru
if ($build.ExitCode -ne 0) { throw 'Build failed; inspect build.log' }
```

Replace `PROJECT` with the actual repository. Set `MNN_WINDOWS_BENCHMARK_REQUEST`
to an absolute UTF-8 JSON file with all fields present, for example:

```json
{
  "model": "E:/PROJECT/Assets/StreamingAssets/MNN/Models/Qwen3.5-0.8B-MNN",
  "output": "E:/PROJECT/TestArtifacts~/Benchmark/case-01/result.json",
  "backend": "CPU",
  "precision": "Normal",
  "power": "Normal",
  "threads": 4,
  "repeats": 3,
  "tokens": 64,
  "automatic": false,
  "reorder": false
}
```

Run `Benchmark.exe -batchmode -nographics -logFile ABSOLUTE_LOG` and wait for its
exit. Give every run a fresh output and `MNN_INFERENCE_CACHE_DIRECTORY` under
`TestArtifacts~/`. Use a new process per configuration and keep other inference
and build jobs stopped. Preserve the actual executable/DLL hashes and request
alongside the report. An exit status of zero and `passed: true` are both required.
An explicit backend silently falling back fails the benchmark.

One warmup precedes the measured repetitions. The harness checks Unicode tokens,
Paris, arithmetic exactly `4`, exact prompt counts, stable greedy token IDs and
streaming reconstruction before accepting timing samples. A model failing these
gates has no accepted speed result; retain its error. The small SmolLM2 model
failed the arithmetic prompt in the original CPU baseline, while Qwen3.5-0.8B
passed. This benchmark does not certify every answer or all models.

The report separates load time, warm prefill, first text, decode, wall time and
peak host working set (Windows `GetProcessMemoryInfo`; excludes GPU VRAM).
For OpenCL, use a matching audited official backend build and verify the report's
actual backend plus the device identity in the log. See
[Windows validation](../../../Documentation~/WindowsPlayer.md).
