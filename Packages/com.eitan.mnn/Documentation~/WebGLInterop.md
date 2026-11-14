# Experimental WebGL text inference

The shipped experimental contract is **Unity 2021.3.45f2 / Emscripten 2.0.19 /
IL2CPP wasm32 / little endian / libc++ `std::__2` / Chrome 157**. The library uses
unmodified official MNN 3.6.2 revision `7e3c6fb5225d3ccf22807ee641332669df34650d`.
This is a project-maintained compiler ABI, not an official portable MNN C# API.
Other Unity/compiler versions, browsers and deployment settings need validation.

## Verified scope

- CPU, one thread, generic Wasm SIMD; core Interpreter/Session/Tensor inference.
- Text loading, tokenization, generation, repeated output, streaming, cancellation
  and recovery for Pocket 0.3B, Qwen2.5-0.5B, Qwen3-0.6B and the dedicated
  MiniMind-O-113M adapter with `GenerateSpeech=false`.
- Actual Chrome keyboard/mouse input, intermediate text screenshots, callback and
  rAF timing, ownership rejection, callback-error recovery and disposal checks.

Media/speech, Express, other model adapters, WebGPU/GPU, Worker inference,
managed/native threads, persistent browser cache and mobile-browser memory limits
are not certified. Qwen3.5 visual/Omni exports are outside this WebGL contract.
A supported ABI flag does not broaden this measured task scope.

## Unity build settings

Use the bundled `Runtime/Plugins/WebGL/libMNN.a` with the matching managed code.
It enables generic `-msimd128` kernels with SSE emulation disabled; the rejected
SSE-emulation candidate produced corrupt model output and is not shipped.
Preserve Unity's export sets and set `PlayerSettings.WebGL.emscriptenArgs` to:

```text
-msimd128 -s ALLOW_TABLE_GROWTH=1 -s EXTRA_EXPORTED_RUNTIME_METHODS=addFunction -Wl,--export=_ZNSt3__212basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEEC2EPKcm -Wl,--export=_ZNSt3__212basic_stringIcNS_11char_traitsIcEENS_9allocatorIcEEED2Ev
```

`EXTRA_EXPORTED_RUNTIME_METHODS` appends in this older Emscripten. Replacing
`EXPORTED_RUNTIME_METHODS` or `EXPORTED_FUNCTIONS` removes Unity startup exports.
The browser must support WebAssembly SIMD. Static linking is required; the
`MAIN_MODULE`/PIC experiment did not link Unity's prebuilt runtime.
The archive checksum and compiler scope are recorded in
[the native manifest](../Native~/build-manifest.json).

Compiler record layouts establish: pointer4, string12/align4, vector<int>12/align4;
LlmContext fields `end_with`16, `load_us`32, `prefill_us`56, `decode_us`64,
`history_tokens`100, `output_tokens`112, `generate_str`124 and `status`136.
Strings use official constructor/data/size/destructor functions; their contents
are never decoded by reading private STL tags. Compiler LLVM IR establishes that
wasm constructors and destructors return `this`, represented as IntPtr.

IL2CPP direct C declarations of string construction conflict with libc++ headers.
The two original official string functions are exported and registered into the
Wasm function table through Emscripten addFunction using Unity 2021's built-in
synchronous Application.ExternalEval. Managed delegates call those actual
functions. The invoked script is formed exclusively from internal constants and
a caller-owned result slot, never model or user text. This requires JavaScript
execution compatible with Unity 2021 ExternalEval and a growing function table.
There are no custom C ABI exports or JS plugin exports.

## Model staging and configuration

Fetch the complete existing export with UnityWebRequest and stage it into the
browser memory filesystem before loading. Keep configuration/tokenizer files and
external `.weight` files together. Use CPU/thread1, `async=false`, mmap=false,
cached mmap=false and KV-cache mmap=false for the LLM recipe. LLM checks use High
precision; MiniMind retains CPU1/Low precision/Low memory. No model, quantization,
input history or output budget was changed for these measurements.

## Asynchronous execution and cancellation

Invoke async APIs on Unity's main-thread synchronization context.
`MNNModelLoading.LoadAsync` yields one frame, then calls the synchronous factory;
loading can still block for roughly0.4–1.1 seconds and is not background loading.

`MNNLlm.GenerateAsync`, `GenerateStreamingAsync` and conversation async text APIs
use the existing official `generate_init`, `generate(vector<int>,0)` and
`generate(1)` functions. The chat template is rendered once; all prompt tokens
enter retained KV state before output decoding. Unity continuations yield between
calls. `GenerateMultimodalAsync` with no media shares this path; async image/audio
or speech requests are rejected for this WebGL entry.

For MiniMind-O text-only async requests, the existing prefill graph consumes the
first prompt token; the existing decode graph consumes every remaining token
through explicit past/present state before sampling. The original 512-token
prompt-plus-budget boundary is retained. Synchronous Generate keeps full prefill;
media/speech paths are not newly validated. The original graphs still contain
thinker/talker computation: not reading audio logits would not eliminate the
talker state required by the current combined present tensor.

Only one request owns a model. Await or cancel it before reuse/disposal; busy
Generate/Dispose calls are rejected. Cancellation is checked between synchronous
calls and clears partial recurrent state. It **cannot interrupt** a running native
Run, resize or load. Text/MiniMind async requests throw OperationCanceledException;
conversation async retains its partial-result `Cancelled=true` contract.
Callback exceptions propagate, and the next independent request can recover.

## Measured MiniMind tradeoff

The full input is 48 repetitions of `你好。` followed by `请简单介绍你自己。`:
119 prompt tokens, limit16, seed42, temperature0, GenerateSpeech=false.
All rows produce `你好！你好！你问到什么问题？`, 11 tokens, exit0.
Times are milliseconds; these are recorded single-run observations.

| Version | Load | First callback | rAF text observation | Generation wall | Max generation frame gap | Whole lifecycle frame gap | Wasm capacity MiB | Sampled Chrome RSS MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Before prompt splitting | 679.5 | 2200.0 | 2207.0 | 4396.6 | 2175.1 | 2175.1 | 1711.0 | 3562.8 |
| Response-priority prompt replay | 696.7 | 14241.6 | 14253.0 | 16442.5 | 227.5 | 705.6 | 1310.1 | 3015.1 |
| Retained finite-scan optimization | 679.3 | 3071.6 | 3081.0 | 3378.3 | 162.5 | 684.0 | 1310.1 | 3524.8 |
| Independent rollback to response-priority replay | 674.6 | 13355.6 | 13365.0 | 15430.2 | 212.5 | 24809.6 | 1315.8 | 3468.5 |

Profiling129 executions identified full-output finite-value validation as the main
regression:12.62s versus2.16s in native Run,0.92s in resize and0.14s in yield waits.
`MNNGenerationGraph.Validate` now pins managed storage, caches its endpoint and
checks every IEEE-754 exponent. Validation falls to about22ms without skipping KV,
changing values, or accepting NaN/infinity. Empty output and non-finite values at
array boundaries remain rejected. One intermediate pointer loop still fetched
array length on every iteration and was replaced after its measured remaining4s
scan cost. Production timing hooks and temporary diagnostics are not shipped.

First response remains about872ms slower than the pre-split reference, while wall
and generation frame gaps improve. No50ms guarantee or stable RSS reduction is
established. A separate synchronous Generate took2368.5ms with a2374.9ms lifecycle
frame gap: faster completion still blocks the main thread. Initial native resize
remains about130ms; steady diagnostic decode Run calls were about16–19ms.

The rollback's later repeat phase had a24809.6ms Unity Update gap and52581.2ms
JavaScript rAF gap, while CDP observations continued with at most814ms between
responses. Its cause is unresolved; it is not evidence of one52s native call and
is not used to compute optimization gains. The first timed rollback generation
and all lifecycle results remain recorded rather than discarded.

rAF observes the event loop, **not hardware-presented TTFT**. Wasm heap capacity is
not live memory. Chrome process-tree RSS is sampled every200ms, can double-count
shared pages and miss peaks. Download/staging, loading and all lifecycle phases
must be considered; short-input success does not qualify long contexts as smooth.

## Integrated package confirmation

The 2026-11-15 package integration preserves the current dev Windows IL2CPP/OpenCL
changes and DLL checksum. A fresh Unity WebGL build and Chrome run of that
integrated source passed the four text-model lifecycles, original Pocket/MiniMind
long inputs, and MiniMind synchronous entry. The same 119-token MiniMind input
returned the same 11 tokens: load672.3ms, callback2922.7ms, rAF2932.0ms,
wall3224.1ms, generation frame gap160.5ms and lifecycle gap676.4ms. Wasm capacity
was1313.3MiB; sampled process-tree RSS3505.0MiB. Prefill button cancellation took
392.8ms and recovered. The synchronous entry took2309.2ms and still blocked.
These are additional single-run observations, not a new frame or memory guarantee.
An initial test phase omitted the harness's FINAL label and therefore did not run
prefill-cancel/oversize subprobes; it is retained as incomplete evidence. The
correctly labeled run passed them without a production change.

## Isolation limits and retained evidence

Thread.Start returned but its delegate did not enter over30s; Task.Run made no
sentinel progress. Unity2021 threadsSupport failed the actual build and required
Unity2022.2+ for experimental native C/C++ threads, which does not prove managed
thread support. The official loader imported in a Worker but createUnityInstance
failed with `document is not defined`, including with cross-origin isolation,
SharedArrayBuffer and OffscreenCanvas available. No DOM shim was added.
The existing archive also failed shared-memory linking because objects lacked
atomics/bulk-memory; the separate full-pthread attempt encountered a toolchain
stdatomic.h failure. None of these experiments ran verified threaded inference.
Worker filesystem/handle ownership and recoverable cancellation remain unverified.

A four-token Pocket prefill candidate was rejected: wall1881→1652ms but maximum
frame gap86→254ms. No skipped-operator callback is used as a resumable native stack.
No upgrade, re-export, quantization change or model replacement was performed.

The local project evidence is under `TestArtifacts~/WebGLInference20261114/`:
`Isolation20261115`, `PrefillCost20261115`, and `Transaction` contain exact inputs,
checksums, browser receipts/screenshots, BASELINE/MODIFIED/ROLLBACK results,
failed experiments and byte-exact rollback/reapply evidence. These artifacts and
model weights are excluded from the Git package. See [test scope](Testing.md).
