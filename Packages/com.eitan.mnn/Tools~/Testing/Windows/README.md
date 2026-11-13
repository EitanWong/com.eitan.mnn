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
  "gpuTuning": "Wide",
  "attention": "Default",
  "chunk": -1,
  "promptCopies": 24,
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

For an NVIDIA GPU, run the Player through the PowerShell telemetry wrapper:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./Measure-MNNWindowsGpuBenchmark.ps1 `
    -Player 'E:/PROJECT/Benchmark/Benchmark.exe' `
    -Request 'E:/PROJECT/TestArtifacts~/request.json' `
    -EvidenceDirectory 'E:/PROJECT/TestArtifacts~/case-01-evidence'
```

The wrapper requires one NVIDIA GPU, records executable/DLL hashes and samples
whole-device utilization, memory, power, clocks and temperature with a requested
100 ms interval. Driver/query overhead can make the actual interval longer;
use the CSV timestamps. These are sampled counters, not kernel profiling or an
exact peak-memory measurement. Stdout redirection retains short traces; NVIDIA's
`-f` file output left the short forced-stop probe empty.
It refuses to launch when another Unity workload is active, GPU utilization is
above 5%, or idle GPU memory exceeds 2048 MiB. It retains the idle reading and
does not stop other applications. These checks do not guarantee exclusivity
throughout a run; inspect the trace and retain all failures. The counters include
display and other applications, and do not measure per-process VRAM. Each warm
sample and model load has UTC boundaries in the result for trace correlation.

`gpuTuning` selects exactly one of `None`, `Heavy`, `Wide`, `Normal`, `Fast`;
`threads` remains the CPU count, including CPU fallback. MNN supplies OpenCL LLM
buffer mode internally. `attention` accepts `Default`, `Enabled` or `Disabled`;
`chunk: -1` preserves the export, zero disables chunking, and a positive value
sets the prefill chunk size (exports with `chunk_limits` reject overrides).
`promptCopies` controls the repeated system text; 24 preserves the original
298-token Qwen workload. Compare identical workloads and generated token IDs.
An optional absolute `cacheDirectory` retains native shader caches across
processes; isolate it by model, native build, GPU/driver and tuning settings.
Report cache reuse and first-load compilation separately from warm inference.
Automatic loading rejects explicit GPU/attention/chunk/reordering overrides.

Sweep GPU tuning first, then attention/chunks on both short and long prompts.
Confirm a candidate in separate Mono and IL2CPP processes with at least five
measured repetitions after warmup. Keep `Wide` until a matched, correct benchmark
proves an improvement. `Low` only requests FP16: the official backend uses FP32
when the driver lacks `cl_khr_fp16`/half-float support. An OpenCL result is not
evidence of CUDA or Tensor Core use.
