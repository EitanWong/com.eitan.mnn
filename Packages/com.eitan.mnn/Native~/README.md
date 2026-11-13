# Direct official MNN C++ API

The package calls official C++ symbols from platform-specific MNN libraries using
C# P/Invoke. It has no companion native library, custom C exports or package C++
sources. Native builds compile an existing official MNN checkout without patching
its sources. Linux Docker setup can acquire compiler dependencies; it downloads
neither MNN sources nor models.

Core Interpreter/Session/Tensor bindings support macOS ARM64/x64 Mono, Windows x64
Mono/IL2CPP Players and Linux x64 Mono. Their exact compiler contracts, binary hashes and observed
execution scope are in [desktop Mono](../Documentation~/DesktopMono.md). Windows
uses MinGW-w64 GCC 16.2.0 with static libstdc++/winpthreads; Linux uses the audited
GCC 13.3 build of the clean official source. These bindings are maintained by this
project and are not an upstream portable MNN C# ABI. Compiler-specific STL layouts
and aggregate returns must be revalidated when their dependencies change.

Windows/Linux native task bindings for LLM, Express, retrieval and multimodal
inputs use the bundled GNU compiler ABI and official C++ ownership functions.
Existing macOS language/Express tasks use Apple clang /
libc++ ABI v1. Android ARM64 IL2CPP and iOS ARM64 IL2CPP have their separate
Interpreter/Session/Tensor and task contracts; see
[mobile interop](../Documentation~/MobileInterop.md).

Actual macOS CPU inference is tested in Unity 2021.3.45f2, including ARM64 Players
and x64 Players under Rosetta: the current core/platform/Express selection passes
120/120 tests on each architecture. Windows 11 x64 Mono and IL2CPP Players each
pass 157 selected two-model LLM/core/Express/platform tests with both the original
CPU library and the CPU/OpenCL variant; see [Windows evidence](../Documentation~/WindowsPlayer.md).
Linux C# execution uses host Mono's interpreter in an amd64 Docker container;
Linux Unity Player validation remains pending. macOS/Linux IL2CPP and WebGL are
rejected before inference. Metal and model-specific evidence
is recorded in [acceleration](../Documentation~/Acceleration.md) and
[test evidence](../Documentation~/Testing.md). CoreML/NPU support is not claimed.

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

### Windows x64 Mono and IL2CPP

Use `build-windows.py` with the pinned MinGW-w64 GCC 16.2.0 POSIX toolchain,
CMake, Ninja, Git and Python 3. See [Windows build](README.Windows.md) for the
complete compiler contract and command. The script prepares a candidate by
default; `--install` explicitly replaces the audited package DLL while preserving
its Unity metadata. It adds no C++ translation units and leaves official source
bytes unchanged. The exported standard-library definitions own native strings
and collections; all runtime libraries are statically linked into the MNN DLL.

### Linux x64 Mono

Build and audit the Linux CPU candidate from the same pinned clean official
checkout, with Docker, Git and Python 3 installed:

```bash
Packages/com.eitan.mnn/Native~/build-linux.sh /path/to/existing/MNN --no-install
```

The default output is `TestArtifacts~/MNNValidation/OfficialNativeLinux`. Add
`--install` to replace the audited plugin; the `.meta` GUID is preserved. The
script requires GCC 13.3, records source/header hashes, dependencies and exported
symbols, and rejects custom `MNN_*` exports. `retain-linux-abi.cmake` retains
existing official inline definitions without adding C++ code or modifying sources.
The distributed candidate needs GLIBC 2.38+ and GLIBCXX_3.4.32. OpenCL/Vulkan are
disabled in this Linux build; its verified backend is CPU. Task bindings use the
pinned GNU C++11 string ABI and the retained official ownership exports.

## Managed interop

`Runtime/Interop/MNNInterop.cs` shares library/calling-convention settings.
Partial files separate backend probing, Interpreter/Session, Tensor, LLM, Multimodal, Embedding,
Reranker and Version. `Abi` and `DesktopAbi` own task-specific libc++/GNU marshalling
and borrowed desktop core values. `DesktopSymbols` selects official GNU/Apple core
symbols; task-specific partials such as `Llm`, `Express`, `Embedding` and `Reranker`
select their GNU/Apple task symbols. Windows/Linux and
mobile tensor shapes use public scalar `Tensor::length` rather than owned STL returns;
`Express` owns executor scopes and variable results, including official
`Variable::load(path)` and `Variable::load(buffer, length)` for prepared serialized graphs.
`Waveform` rejects speech requests because no supported std::function marshalling
contract is implemented; the previous fabricated callback object has been removed. SafeHandles own models and managed
core classes serialize inference and disposal.

The ABI includes the official public `ScheduleConfig` (104 bytes, including nested Path padding), `BackendConfig`,
`LlmContext` and `Variable::Info` layouts, libc++ alternate string layout and
nontrivial aggregate return convention. Apple/GNU `halide_type_t` is 8 bytes
(code enum: 4, bits: 1, lanes: 2 with padding). The bundled MinGW Windows build
returns it directly in RAX. GNU owning member results pass their output pointer
before `this`, verified in the actual DLL disassembly and Mono calls. Windows
`size_t` mangles as `y`; Linux uses `m`. GNU strings use stable native object
storage, official constructors/reserve/accessors/destructors and their pinned
32-byte ABI. Apple strings use official libc++ initialization/destruction.
VARP results use the official destructor. Private MNN object offsets are not read.
Returned integer vectors use the STL destructor compiled into MNN. Calling
system `operator delete` directly is unsafe in Unity Players, whose allocator
can override C++ allocation; a real Player regression test covers this failure.
Platforms with implemented task bindings call official Llm/Omni/Embedding load symbols directly;
no model virtual-table slots are invoked. Mobile strings use official data/size
methods; no SSO layout is decoded. The public LlmContext prefix and aggregate return
contract are checked for each mobile compiler. An iOS managed autorelease scope
drains Foundation/Metal temporaries on worker threads.

Implemented desktop/mobile Express/retrieval bindings share their classes and partials.
Windows, Linux and mobile vector<VARP> results use their exact official vector destructor. The legacy
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
