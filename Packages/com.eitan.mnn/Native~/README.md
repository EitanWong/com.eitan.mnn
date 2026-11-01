# Direct official MNN C++ API

The package calls official C++ symbols from platform-specific MNN libraries directly using C#
P/Invoke. It has no companion native library, custom C exports, or package C++
sources. The build script compiles the existing official MNN checkout directly;
it does not patch that checkout or download anything.

This is a compiler-specific binding, not an official portable MNN C# SDK.
The contract is MNN 3.6.2, Apple clang, Apple libc++ ABI v1, 64-bit macOS Mono.
This contract is maintained by this project for the bundled build; MNN does not
provide or guarantee this managed ABI. STL layouts, aggregate-return conventions
are implementation assumptions for that build, not
portable public C# API guarantees. Native inference tests exercise those
assumptions but do not make them an upstream-supported ABI.
Actual CPU inference is tested in Unity 2021.3.45f2, including macOS Mono
Standalone Players on arm64 and x86_64 (the latter under Rosetta, not Intel hardware).
Android ARM64 IL2CPP and iOS ARM64 IL2CPP implement
Interpreter/Session/Tensor, LLM text/chat/streaming/media input, Express and retrieval in the shared partials; their compiler contracts, build commands and
limitations are documented in [mobile interop](../Documentation~/MobileInterop.md).
The remaining non-macOS targets and macOS IL2CPP are rejected before inference. New Metal checks use actual GPU
sessions and models; see [acceleration](../Documentation~/Acceleration.md) and
[test evidence](../Documentation~/Testing.md). The bundled build enables Metal
but not CoreML; no Apple Neural Engine support is claimed.

## Build

With installed CMake, Python 3 and Xcode, run from this project:

```bash
Packages/com.eitan.mnn/Native~/build-macos.sh /path/to/existing/MNN
```

The optimized build uses unmodified official MNN 3.6.2 at revision
`7e3c6fb5225d3ccf22807ee641332669df34650d`. The newer upstream revision
`f0b6105c5c7f901b1531dd4c23afa8a00b454db3` was built and evaluated, but failed
the Apple ARM64 CPU embedding quality gate. Disabling SME2 did not resolve it.
See [LLM performance](../Documentation~/LlmPerformance.md) before updating the pin.

The script builds arm64 and x86_64 separately with Express, LLM, image and audio
support, merges them, signs the dylib, and replaces the package binary atomically.
Artifacts stay under `TestArtifacts~/MNNValidation/OfficialNative`. Restart Unity
after replacing the native library; domain reload does not unload it.

`-femit-all-decls` emits inline API definitions. `retain-abi.cmake` limits
`-fno-inline-functions` to the existing official `Tensor.cpp` and `omni.cpp`
translation units; compute kernels and the remaining runtime retain normal O3
inlining. `_LIBCPP_DISABLE_VISIBILITY_ANNOTATIONS` allows the
existing `std::vector<int>` destructor instantiation to remain externally visible.
`export-official-api.py` relinks those same object files with the existing public
symbols plus seven retained definitions used by C#:
`Llm::getContext`, `Tensor::getType`, `Tensor::dimensions`, `Tensor::elementSize`,
`Variable::readMap<float>`, `VARP::~VARP` and `std::vector<int>::~vector`.
The latter carries the bundled compiler's `B9nqn220106` ABI tag. It generates no function bodies and
rejects custom `MNN_*` C exports. Do not replace this binary with an arbitrary
same-version MNN binary: its compiler ABI and retained symbols must match.
The small committed affine test model is reused; no custom C++ model generator
is built.

Use `--no-install --output=TestArtifacts~/MyBuild` to prepare a candidate,
`--benchmarks` to build official matching `llm_bench`/`llm_demo`, and
`--legacy-global-noinline` only to reproduce the earlier optimization restriction.
Low-memory kernels, transformer fusion, the native thread pool, ARM82 and SME2
are explicitly enabled. Hardware dispatch remains owned by official MNN.

Mobile builds use the unmodified official commit above, enable LLM/Omni by default,
and audit 69 official MNN/libc++ imports in the current bindings. `build-mobile.py --llm --install` builds
and installs the matching plugins; see the mobile document for platform arguments.
`--basic-only` retains the historical 26-import audit and cannot install over the
current plugins. Artifacts stay under `TestArtifacts~/MobileLlmDirectCpp/`.

## Managed interop

`Runtime/Interop/MNNInterop.cs` shares library/calling-convention settings.
Partial files separate backend probing, Interpreter/Session, Tensor, LLM, Multimodal, Embedding,
Reranker and Version. `Abi` owns compiler-specific libc++ string/vector marshalling and destruction; mobile tensor
shape access uses the official scalar `Tensor::length` API instead of STL returns;
`Express` owns executor scopes and variable results, including official
`Variable::load(path)` and `Variable::load(buffer, length)` for prepared serialized graphs.
`Waveform` rejects speech requests because no supported std::function marshalling
contract is implemented; the previous fabricated callback object has been removed. SafeHandles own models and managed
core classes serialize inference and disposal.

The ABI includes the official public `ScheduleConfig` (104 bytes, including nested Path padding), `BackendConfig`,
`LlmContext` and `Variable::Info` layouts, libc++ alternate string layout and
nontrivial aggregate return convention. The current `halide_type_t` is 8 bytes
(code enum: 4, bits: 1, lanes: 2 with padding), verified from clang record layouts
and real tensor tests. Strings use official libc++ initialization/destruction;
VARP results use the official destructor. Private MNN object offsets are not read.
Returned integer vectors use the STL destructor compiled into MNN. Calling
system `operator delete` directly is unsafe in Unity Players, whose allocator
can override C++ allocation; a real Player regression test covers this failure.
All supported platforms call official Llm/Omni/Embedding load symbols directly;
no model virtual-table slots are invoked. Mobile strings use official data/size
methods; no SSO layout is decoded. The public LlmContext prefix and aggregate return
contract are checked for each mobile compiler. An iOS managed autorelease scope
drains Foundation/Metal temporaries on worker threads.

Express/retrieval use the same classes and partials across supported platforms.
Mobile vector<VARP> results use their exact official vector destructor. The legacy
macOS binary lacks this export: it destroys VARP elements and frees storage through
the pinned libc++ default allocator's vector<int> destructor. This remains a
compiler-specific allocator dependency, not a portable vector conversion.
Current mobile compilation and symbol audits pass; updated Express/retrieval
inference has not yet been verified on devices. Waveform output is unavailable
on every platform. Revalidate ABI assumptions when updating MNN, clang, libc++,
Mono or IL2CPP.

The official Omni tokenizer handles `<img>local-path</img>` and
`<audio>local-path</audio>` inputs. Model configuration selects the native model
implementation. Input capabilities are read from the official configuration;
SpeechOutput is not advertised. Qwen3 reranking uses its yes/no prompt and stable softmax. Additional talker or
reranker architectures require their own tested contracts.

See [test evidence](../Documentation~/Testing.md) for model and platform coverage.
Keep intermediate builds and audits in `TestArtifacts~/`; only the distributable
plugin and its Unity metadata belong in `Runtime/Plugins`. See
[repository maintenance](../Documentation~/RepositoryManagement.md) for commit rules.

### Android accelerator build

`build-mobile.py` now enables the official OpenCL/Vulkan/NNAPI runtimes and ARMv8.2
kernels by default (`--android-cpu-only` builds the CPU variant). It keeps NDK r25c,
MNN 3.6.2 and the existing direct C++ ABI contract. C++17 is required for official
OpenCL constexpr definitions with the exported-inline build; `-include cmath`
supplies a missing standard header in the official ARM82 translation unit without
editing upstream sources. The symbol audit still rejects missing official symbols
or custom `MNN_*` exports. `--install` replaces only the audited plugin, preserving
Unity metadata. This does not certify graph compatibility or NPU placement.

iOS ARM82 and SME2 are now enabled by default after CPU/Metal device validation.
`--no-ios-arm82` and `--no-ios-sme2` build diagnostic alternatives; these are not
claims that every Apple CPU executes SME2. Android keeps SME2 off with NDK r25c.
