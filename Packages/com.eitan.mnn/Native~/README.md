# Direct official MNN C++ API

The package calls official C++ symbols from `libMNN.dylib` directly using C#
P/Invoke. It has no companion native library, custom C exports, or package C++
sources. The build script compiles the existing official MNN checkout directly;
it does not patch that checkout or download anything.

This is a compiler-specific binding, not an official portable MNN C# SDK.
The contract is MNN 3.6.1, Apple clang, Apple libc++ ABI v1, 64-bit macOS Mono.
This contract is maintained by this project for the bundled build; MNN does not
provide or guarantee this managed ABI. STL layouts, aggregate-return conventions
and virtual slots below are implementation assumptions for that build, not
portable public C# API guarantees. Native inference tests exercise those
assumptions but do not make them an upstream-supported ABI.
Actual CPU inference is tested in Unity 2021.3.45f2, including macOS Mono
Standalone Players on arm64 and x86_64 (the latter under Rosetta, not Intel hardware).
Other operating systems and IL2CPP are rejected before inference; they need
separate ABI implementations and real tests. New Metal checks use actual GPU
sessions and models; see [acceleration](../Documentation~/Acceleration.md) and
[test evidence](../Documentation~/Testing.md). The bundled build enables Metal
but not CoreML; no Apple Neural Engine support is claimed.

## Build

With installed CMake, Python 3 and Xcode, run from this project:

```bash
Packages/com.eitan.mnn/Native~/build-macos.sh /path/to/existing/MNN
```

The current build uses MNN 3.6.1 at local revision
`024a946b0b8fcf87c8a418229fadd4cd7858ffba`, including the checkout's pre-existing
local changes. This is not a claim that the checkout was pristine.

The script builds arm64 and x86_64 separately with Express, LLM, image and audio
support, merges them, signs the dylib, and replaces the package binary atomically.
Artifacts stay under `TestArtifacts~/MNNValidation/OfficialNative`. Restart Unity
after replacing the native library; domain reload does not unload it.

`-femit-all-decls -fno-inline-functions` retains definitions of inline functions
used by official source. `_LIBCPP_DISABLE_VISIBILITY_ANNOTATIONS` allows the
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

## Managed interop

`Runtime/Interop/MNNInterop.cs` shares library/calling-convention settings.
Partial files separate backend probing, Interpreter/Session, Tensor, LLM, Multimodal, Embedding,
Reranker and Version. `Abi` owns libc++ string/vector marshalling and destruction;
`Express` owns executor scopes and variable results; `Waveform` owns the managed
implementation of the libc++ callback functor. SafeHandles own models and managed
core classes serialize inference and disposal.

The ABI includes the official public `ScheduleConfig`, `BackendConfig`,
`LlmContext` and `Variable::Info` layouts, libc++ alternate string layout and
nontrivial aggregate return convention. The current `halide_type_t` is 8 bytes
(code enum: 4, bits: 1, lanes: 2 with padding), verified from clang record layouts
and real tensor tests. Strings use official libc++ initialization/destruction;
VARP results use the official destructor. Private MNN object offsets are not read.
Returned integer vectors use the STL destructor compiled into MNN. Calling
system `operator delete` directly is unsafe in Unity Players, whose allocator
can override C++ allocation; a real Player regression test covers this failure.
Virtual dispatch uses verified Llm slots: load 2, waveform callback 12, waveform
generation 13. Apple libc++ `std::string` has distinct arm64 and x86_64 layouts.
Its Omni `std::function` callback storage also differs because x86_64 uses a
16-byte `max_align_t`; Player tests cover both contracts. Revalidate these
assumptions when updating MNN, clang, libc++ or Mono.

The official Omni tokenizer handles `<img>local-path</img>` and
`<audio>local-path</audio>` inputs. Model configuration selects the native model
implementation. Capabilities are read from the official configuration. Speech
output currently exposes only the validated Qwen2.5-Omni mono 24 kHz contract;
Qwen3 reranking uses its yes/no prompt and stable softmax. Additional talker or
reranker architectures require their own tested contracts.

See [test evidence](../Documentation~/Testing.md) for model and platform coverage.
Keep intermediate builds and audits in `TestArtifacts~/`; only the distributable
plugin and its Unity metadata belong in `Runtime/Plugins`. See
[repository maintenance](../Documentation~/RepositoryManagement.md) for commit rules.
