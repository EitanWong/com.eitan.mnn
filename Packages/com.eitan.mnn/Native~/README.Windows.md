# Windows x86_64 native build

`build-windows.py` rebuilds the bundled Windows x86_64 DLL from pristine
official MNN commit `7e3c6fb5225d3ccf22807ee641332669df34650d` (3.6.2), using
MinGW-w64 GCC 16.2.0, the POSIX thread model and the libstdc++ C++11 ABI.
The checked build uses MinGW-w64 14.0.0 and C++17. Windows 10 with UCRT is the
platform contract. CMake, Ninja, Python 3 and the named cross tools must exist.

From the Unity project root:

```sh
python3 Packages/com.eitan.mnn/Native~/build-windows.py \
  --source TestArtifacts~/MNN-3.6.2-source \
  --output TestArtifacts~/WindowsMNN \
  --jobs 8 \
  --opencl
```

The default leaves the package unchanged. Add `--install` to replace this
package's `Runtime/Plugins/Windows/x86_64/MNN.dll` after its export/import audit
passes. Existing Unity `.meta` files are preserved. Build output and logs stay
under the selected `TestArtifacts~` directory. The script does not download
sources, models or dependencies. Add `--opencl` to compile the official optional
OpenCL backend; an audit alone does not certify a device or model.

The build retains definitions from four existing official translation units.
The linker exports existing C++ and libstdc++ symbols by their real names; it
does not introduce a bridge, a new C++ translation unit or custom C ABI exports.
Header-supported declaration macros prevent unused Windows/runtime inline
wrappers from introducing new API requirements or duplicate static definitions.
libstdc++, libgcc and winpthreads are statically linked. The resulting DLL only
imports Windows system and UCRT APIs.

Bindings depend on this exact compiler ABI: GNU Itanium names, Microsoft x64
register calling convention, LLP64 `size_t` names ending in `y`, 32-byte GNU
strings, 24-byte vectors, 16-byte shared pointers and 8-byte `halide_type_t`.
Nontrivial member return values pass their output pointer first, followed by
`this`; `Tensor::getType` returns eight bytes directly. Official constructors,
accessors and destructors manage string ownership. These declarations are a
package-specific compiler ABI contract, not a portable official MNN C# API.

The build manifest records the pristine source revision, public-header hashes,
compiler identity, DLL hash, dependencies and required exports. The build tool
checks source identity before and after compilation. Successful construction
and symbol audits do not establish inference behavior; run the package's Mono and IL2CPP
Player tests on the target platform. Wine Mono results are compatibility evidence and
do not substitute for an actual Windows Unity Player run.

## Unity Player contract

The managed declarations support Windows x86_64 Mono and IL2CPP with the bundled
DLL and the exact GNU ABI above. Windows IL2CPP was exercised with Unity
2021.3.45f2, Visual Studio 2022 Build Tools 17.14 and Windows SDK 10.0.22621.
IL2CPP resolves these DLL imports at runtime; the generated MSVC function-pointer
signatures use the Microsoft x64 register and indirect-return conventions.
This does not provide an MSVC-STL binding or accept an arbitrary replacement DLL.
Other desktop IL2CPP platforms and Windows ARM64 remain unsupported.

Windows file paths use the Windows ACP conversion API directly, with strict
conversion and the existing DOS short-path fallback. A Player does not need
managed I18N assemblies merely to load an ASCII/ANSI model directory. Prompt
and generated text remain UTF-8; model path support is still bounded by MNN's
Windows FileLoader and the availability of a representable path or DOS alias.

Windows 11 hardware runs on an i7-10700F passed 157 Mono and 157 IL2CPP
Player tests without failures or skips, including SmolLM2 and Qwen3.5 generation. These counts include
regressions and platform policy checks; they do not certify every model or GPU.
The Player writes NUnit results independently of the Editor connection. The
platform runner accepts that file only when its build identifier matches.

The bundled variant enables CPU and official OpenCL. On Windows 11 / RTX 3060,
Mono and IL2CPP Players each pass 157 selected regressions with no failures or
skips. Reproduction, measured workload and limits are in
[Windows Player validation](../Documentation~/WindowsPlayer.md). Other GPUs,
models and driver versions require their own validation.
