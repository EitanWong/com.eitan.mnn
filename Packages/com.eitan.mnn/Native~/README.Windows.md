# Windows Mono native build

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
  --jobs 8
```

The default leaves the package unchanged. Add `--install` to replace this
package's `Runtime/Plugins/Windows/x86_64/MNN.dll` after its export/import audit
passes. Existing Unity `.meta` files are preserved. Build output and logs stay
under the selected `TestArtifacts~` directory. The script does not download
sources, models or dependencies.

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
and symbol audits do not establish inference behavior; run the package's Mono
tests on the target platform. Wine Mono results are compatibility evidence and
do not substitute for an actual Windows Unity Player run.
