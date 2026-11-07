# Desktop Mono

MNN 3.6.2 core Interpreter/Session/Tensor APIs support macOS ARM64/x64 Mono,
Windows x64 Mono and Linux x64 Mono. Select **Mono** in Unity Player Settings for
Standalone and choose the matching 64-bit architecture. Editor support follows
the host OS even when the selected Player backend is IL2CPP. The binding requires
a 64-bit little-endian process; desktop IL2CPP remains unsupported.

## Compiler and feature contracts

| Target | Bundled compiler ABI | Implemented managed native scope |
| --- | --- | --- |
| macOS 11+ ARM64/x64 Mono | Apple clang / libc++ ABI v1, existing universal MNN build | Core inference and existing macOS language/Express/retrieval tasks |
| Windows x64 Mono | MSVC 19.51.36260.0, release MT, Microsoft x64 C++ calling convention | Core Interpreter/Session/Tensor |
| Linux x64 Mono | GCC 13.3.0, GNU libstdc++ C++11 ABI, x86_64 SysV | Core Interpreter/Session/Tensor; current native build enables CPU |

These are project-maintained bindings to official C++ symbols. MNN does not
provide an official portable C# ABI. The implementation adds no C++ sources,
bridge library, custom C export or official source patch. Keep the bundled
binary, compiler, STL and managed declarations together when updating them.

The Windows library is the official MNN 3.6.2 release's Dynamic/MT binary. Its
matching PDB identifies the MNN compiler and public configuration layouts; native
symbol inspection and execution check the Microsoft member-return convention.
The Linux library is rebuilt from the clean official revision
`7e3c6fb5225d3ccf22807ee641332669df34650d`, retaining existing inline definitions.
Its runtime needs GLIBC 2.38+ and `libstdc++.so.6` providing GLIBCXX_3.4.32.

| Library | SHA-256 |
| --- | --- |
| `Runtime/Plugins/Windows/x86_64/MNN.dll` | `d9ba792c5d1709db8df3016ccfbc02c676d1058bba23ab926c3e7e4e6cd0db9f` |
| `Runtime/Plugins/Linux/x86_64/libMNN.so` | `4be060faab8603fd4f5c29498dec1033b7eef5afff9e6fe2ad6a6afd1fc16a40` |

Core support includes version queries, file/buffer loading, session/backend
configuration, tensor metadata and copies, resize, synchronous/asynchronous
execution and disposal. Windows/Linux LLM, Express, native embedding/reranking
and multimodal entry points remain guarded before their native task calls. A
library containing task symbols does not implement the required managed ownership
and aggregate-return contract. Existing mobile bindings have their own
[ARM64 IL2CPP contracts](MobileInterop.md).

## Native data and paths

`ScheduleConfig` is 104 bytes and `BackendConfig` is 24 bytes under these bundled
64-bit contracts. Windows `halide_type_t` is 4 bytes; Apple/GNU use 8 bytes. The
Windows member call supplies explicit result storage, then converts public type
fields to the shared managed representation. Windows/Linux tensor shapes use
public scalar `Tensor::length` calls, avoiding owned STL vector returns. Native
x64 CPU_EXTENSION (13) is reported to managed callers as `MNNBackendType.CPU`.

The official Windows FileLoader interprets `char*` paths using the process ANSI
code page. The managed adapter uses that code page, with the public Windows DOS
short-path API for existing Unicode paths it cannot encode. Cache paths can use
a short path for an existing parent directory. If neither representation exists,
the adapter fails explicitly; use a representable model/cache directory. Arbitrary
Unicode paths and long paths are not guaranteed by this bundled native loader.
Saved tensor names remain UTF-8 values in the pinned borrowed native string contract.

## Observed execution

The committed 584-byte affine fixture computes `2*x+1`; tests check actual numeric
outputs, metadata, resize invalidation, asynchronous execution and lifetime.

| Environment | Observed result |
| --- | --- |
| macOS ARM64 Unity 2021.3.45f2 Mono Player | Candidate 118/118 passed, 0 failed/skipped, including 11 native tests; CPU and Auto→Metal affine inference |
| macOS x64 Unity 2021.3.45f2 Mono Player under Rosetta | Candidate 118/118 passed, 0 failed/skipped, including 11 native tests; CPU and Auto→Metal affine inference |
| Windows x64 Wine 9 / Wine Mono 9.4.0 JIT in Linux amd64 Docker | Production C# core smoke passed with official Windows DLL: CPU 1/4 threads, sync/async, resize, UTF-8 saved tensor, Unicode fixture path, disposal and Auto→CPU |
| Linux x64 host Mono `--interpreter` in Linux amd64 Docker | Production C# core smoke passed with the rebuilt official `.so`, including numeric CPU and Auto→CPU checks |

Windows/Linux evidence is execution of the production core binding against real
native libraries. Windows uses Wine compatibility execution; Linux uses Mono's
interpreter because early host-JIT attempts crashed in the container translation
environment. Neither is a Windows/Linux Unity Player result. macOS x64 was also checked in the current Player under Rosetta; physical Intel
hardware was not tested.
CPU affine checks do not establish all model pipelines or Windows/Linux GPU/NPU
support. See [test evidence](Testing.md) for exact retained logs and boundaries.

## Rebuild and reproduction

`Native~/build-linux.sh` builds and audits the Linux CPU binary from the existing
clean pinned checkout, with outputs under the owning project's `TestArtifacts~/`.
It installs only when `--install` is supplied. See the
[native build guide](../Native~/README.md). Windows retains the exact official
release DLL; a same-version replacement needs a new symbol, compiler and behavior
audit. Tests do not download model weights.

Local evidence for this change is under
`TestArtifacts~/MonoSupport-2026-11-07/`: `CrossPlatform/`, `WindowsAudit/`,
`WindowsRun/` and `LinuxBuild/`. It includes compiler/public layout observations,
commands, stdout/stderr, hashes, successful numerical runs and earlier failed
attempts. These local artifacts are excluded from package source control.
