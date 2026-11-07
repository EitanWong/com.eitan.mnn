# Desktop Mono

MNN 3.6.2 supports desktop Mono on macOS ARM64/x64, Windows x64 and Linux x64.
Select **Mono** in Unity Player Settings for Standalone and choose the matching
64-bit architecture. Editor support follows the host OS even when the selected
Player backend is IL2CPP. Desktop IL2CPP and Windows/Linux ARM64 are unsupported.

## Compiler and feature contracts

| Target | Bundled compiler ABI | Managed native scope |
| --- | --- | --- |
| macOS 11+ ARM64/x64 | Apple clang / libc++ ABI v1 | Core inference, Express, LLM, retrieval and media input |
| Windows x64 | MinGW-w64 GCC 16.2.0 POSIX / libstdc++ C++11 / Win64 LLP64 | Same task bindings; CPU native build |
| Linux x64 | GCC 13.3.0 / libstdc++ C++11 / x86_64 SysV LP64 | Same task bindings; CPU native build |

These are project-maintained calls to official C++ symbols. MNN does not provide
an official portable C# ABI. No C++ source, bridge library, custom C export or
official source patch is added. Keep each bundled binary, compiler, STL and
managed declaration set together; a same-version DLL is not interchangeable.

Both GNU desktop builds use clean official revision
`7e3c6fb5225d3ccf22807ee641332669df34650d`. Build options retain existing official
inline definitions. Windows replaces the earlier MSVC release binary with a GNU
build, statically linking its matching libstdc++, libgcc and winpthread runtimes;
only Windows/UCRT system dependencies remain. Use Windows 10+ with UCRT.
Linux needs GLIBC 2.38+ and `libstdc++.so.6` providing GLIBCXX_3.4.32.

| Library | SHA-256 |
| --- | --- |
| `Runtime/Plugins/Windows/x86_64/MNN.dll` | `32fe998751b3044bebc25a6a9e7229b9e9b3a0dc9f0338b01fca72aa5995b268` |
| `Runtime/Plugins/Linux/x86_64/libMNN.so` | `4be060faab8603fd4f5c29498dec1033b7eef5afff9e6fe2ad6a6afd1fc16a40` |

Core includes file/buffer loading, session/backend configuration, tensor copies,
resize, synchronous/asynchronous execution and disposal. Advanced task bindings
include Express Variable/Module graphs and constants; LLM configuration,
tokenization, text/chat generation, streaming and cancellation; native embedding
and reranking; Omni image/audio input; and the existing CosyVoice token/logit path.
Waveform callbacks remain unavailable on every platform. The x64 conversation
cache deliberately uses full prefill because native KV continuation failed the
existing quality gate. Task bindings do not establish every model's compatibility.
Existing mobile bindings have separate [ARM64 IL2CPP contracts](MobileInterop.md).

## Native data, ownership and paths

The pinned GNU release contracts use `ScheduleConfig` 104 bytes, `BackendConfig`
24 bytes, `halide_type_t` 8 bytes, string 32 bytes, vector 24 bytes and shared/VARP
16 bytes. These are compiler-specific representations, not a portable STL API.
Windows `size_t` uses the GNU `y` mangling and Linux uses `m`.
GNU nontrivial results use native result storage before other arguments, including
member calls. The previous MSVC return convention does not apply to this DLL.

Strings are constructed, reserved, read and destroyed by official STL members.
Input strings retain stable native storage and reserve heap space before any
borrowed header copy. Owned string results use stable explicit result storage,
including short strings with a self-pointer. Borrowed context strings are copied
between synchronous model calls. Owned vectors and VARP values use their exact
native destructors; shared owners use the matching native release method. No
private object offsets or virtual-table calls implement inference.
Module string vectors and chat pairs use the active string size rather than a
fixed Apple string stride. x64 native CPU_EXTENSION (13) is normalized to managed
`MNNBackendType.CPU`.

Windows native file paths use the process ANSI code page, with the public DOS
short-path API for existing Unicode paths it cannot encode. If neither
representation is available, the adapter fails explicitly; use a representable
model/cache directory. Arbitrary Unicode and long paths are not guaranteed by
the native file APIs. Prompts, configuration JSON and tensor names remain UTF-8.

## Observed execution

Windows Wine 9 / Wine Mono 9.4.0 and Linux Mono in amd64 containers run the actual
production C# APIs against the real native libraries. Tests cover Express affine
`2*x+1`, constants, Modules, dynamic shapes, repeated owners and GC; SmolLM2-135M
text/chat generation, reset, UTF-8 tokens, streaming, cancellation recovery and
disposal; BGE-small embedding and Qwen3 reranking; and SmolVLM image input.
Each desktop platform passes 79 task assertions and 22 core assertions.
The LLM returns Paris for the France question, embedding separates the matching
sentence, reranking scores the relevant passage higher, and Omni identifies red.

Windows execution uses Wine compatibility, and Linux uses Mono `--interpreter`
because JIT failed in the emulated container. Neither is a Windows/Linux Unity
Player result. Native desktop Unity Player builds still need validation on their
own hosts. macOS ARM64 and x64 (Rosetta) Unity Mono Player regressions each pass 120/120
selected core/platform/Express tests, with no failed or skipped tests; see
[test evidence](Testing.md) for exact current and historical results. CPU model
checks do not certify Windows/Linux GPU/NPU execution or every speech/image model.

## Rebuild and reproduction

`Native~/build-linux.sh` and `Native~/build-windows.py` build from an existing clean
pinned official checkout. Outputs stay under the owning project's `TestArtifacts~/`;
installation requires explicit `--install`. See the [native guide](../Native~/README.md).
`Tools~/Testing/run_desktop_mono_tasks.py` compiles production sources and records
native host-Mono task checks without downloading weights. Its Unity API adapters
are tooling only. Runtime `ExpressPlayerTests` exercise constants, Modules,
shape changes, independent owners and copied output in real Unity Players.

Local evidence is under `TestArtifacts~/MonoSupport-2026-11-07/Advanced/`, including
`Tests/`, `LinuxAbi/`, `WindowsBuild/`, full import audits and rollback records.
The cumulative four-role transaction and action log remain in the parent folder.
These local artifacts are excluded from package source control.
