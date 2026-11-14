#!/usr/bin/env python3
"""Build the pinned Windows GNU C++ ABI from unmodified official MNN sources."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys


REVISION = "7e3c6fb5225d3ccf22807ee641332669df34650d"
COMPILER_VERSION = "16.2.0"
HEADERS = (
    "include/MNN/MNNDefine.h",
    "include/MNN/Interpreter.hpp",
    "include/MNN/Tensor.hpp",
    "transformers/llm/engine/include/llm/llm.hpp",
)
STRING = "_ZNSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEE"
RUNTIME_EXPORTS = (
    STRING + "C1EPKcyRKS3_",
    STRING + "D1Ev",
    STRING + "7reserveEy",
    "_ZNKSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEE4dataEv",
    "_ZNKSt7__cxx1112basic_stringIcSt11char_traitsIcESaIcEE4sizeEv",
    "_Znwy",
    "_ZdlPv",
    "_ZdlPvy",
)
REQUIRED_EXPORTS = RUNTIME_EXPORTS + (
    "_ZN3MNN10getVersionEv",
    "_ZN3MNN11Transformer4Omni4loadEv",
    "_ZN3MNN7Express8Variable7readMapIfEEPKT_v",
    "_ZNSt6vectorIiSaIiEED1Ev",
    "_ZNSt6vectorIN3MNN7Express4VARPESaIS2_EED1Ev",
    "_ZNSt16_Sp_counted_baseILN9__gnu_cxx12_Lock_policyE2EE10_M_releaseEv",
)
RETAINED_SOURCES = (
    "source/core/Tensor.cpp",
    "express/Expr.cpp",
    "transformers/llm/engine/src/llm.cpp",
    "transformers/llm/engine/src/omni.cpp",
)


def capture(command):
    return subprocess.check_output(command, text=True).strip()


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def run_logged(output, name, command):
    with (output / (name + ".stdout")).open("w") as stdout:
        with (output / (name + ".stderr")).open("w") as stderr:
            result = subprocess.run(command, stdout=stdout, stderr=stderr)
    write_json(output / (name + ".json"), {
        "command": command, "exit_status": result.returncode,
    })
    if result.returncode:
        raise RuntimeError(f"{name} failed ({result.returncode}); see {output}")


def verify_source(source):
    revision = capture(["git", "-C", str(source), "rev-parse", "HEAD"])
    status = capture(["git", "-C", str(source), "status", "--porcelain"])
    if revision != REVISION or status:
        raise ValueError(f"Require pristine official MNN revision {REVISION}")
    return {
        "source": str(source), "revision": revision, "status": status,
        "tree": capture(["git", "-C", str(source), "rev-parse", "HEAD^{tree}"]),
        "headers": {name: digest(source / name) for name in HEADERS},
    }


def compiler_tools(prefix):
    tools = {}
    for name in ("gcc", "g++", "windres", "objdump"):
        path = shutil.which(prefix + name)
        if not path:
            raise ValueError(f"Missing {prefix + name}")
        tools[name] = str(Path(path).absolute())
    if capture([tools["g++"], "-dumpfullversion"]) != COMPILER_VERSION:
        raise ValueError(f"Require MinGW-w64 GCC {COMPILER_VERSION}")
    if capture([tools["g++"], "-dumpmachine"]) != "x86_64-w64-mingw32":
        raise ValueError("Require x86_64-w64-mingw32 target")
    description = subprocess.check_output([tools["g++"], "-v"], text=True, stderr=subprocess.STDOUT)
    if "Thread model: posix" not in description:
        raise ValueError("Require the MinGW-w64 POSIX thread model")
    return tools


def write_cmake_inputs(output, tools):
    toolchain = output / "mingw-toolchain.cmake"
    lines = ["set(CMAKE_SYSTEM_NAME Windows)", "set(CMAKE_SYSTEM_PROCESSOR AMD64)"]
    for cmake_name, tool in (("C", "gcc"), ("CXX", "g++"), ("RC", "windres")):
        path = tools[tool].replace("\\", "/")
        lines.append(f'set(CMAKE_{cmake_name}_COMPILER "{path}")')
    for kind, mode in (("PROGRAM", "NEVER"), ("LIBRARY", "ONLY"), ("INCLUDE", "ONLY")):
        lines.append(f"set(CMAKE_FIND_ROOT_PATH_MODE_{kind} {mode})")
    toolchain.write_text("\n".join(lines) + "\n", encoding="utf-8")

    retention = output / "retain-windows-gnu-abi.cmake"
    lines = ["# Retain existing official definitions; do not add a translation unit.",
             "set_property(SOURCE"]
    lines += ['    "${CMAKE_CURRENT_SOURCE_DIR}/' + name + '"' for name in RETAINED_SOURCES]
    lines += ["    APPEND PROPERTY COMPILE_OPTIONS",
              "    -fkeep-inline-functions -fno-inline-functions",
              "    -fno-visibility-inlines-hidden -fvisibility=default",
              "    -D_WIN32_WINNT=0x0A00 -DNTDDI_VERSION=0x0A000000"]
    # The MinGW header explicitly allows these declaration overrides. Internal
    # linkage prevents retained wrappers from colliding with static winpthreads.
    for name in ("CLOCK", "COND", "MUTEX", "NANOSLEEP", "RWLOCK", "SEM", "THREAD"):
        lines.append(f'    "-DWINPTHREAD_{name}_DECL=static __inline__ __attribute__((__always_inline__))"')
    lines[-1] += ")"
    retention.write_text("\n".join(lines) + "\n", encoding="utf-8")

    exports = output / "official-runtime-exports.def"
    exports.write_text("EXPORTS\n" + "".join("    " + x + "\n" for x in RUNTIME_EXPORTS), encoding="utf-8")
    return toolchain, retention, exports


def configure_command(source, output, inputs, opencl=False):
    toolchain, retention, exports = inputs
    export_path = str(exports).replace("\\", "/")
    options = {
        "CMAKE_TOOLCHAIN_FILE": str(toolchain), "CMAKE_BUILD_TYPE": "Release",
        "CMAKE_CXX_STANDARD": "17", "CMAKE_PROJECT_MNN_INCLUDE": str(retention),
        "CMAKE_CXX_FLAGS": "-include cmath",
        "CMAKE_CXX_FLAGS_RELEASE": "-O3 -DNDEBUG -fno-visibility-inlines-hidden -fvisibility=default",
        "CMAKE_SHARED_LINKER_FLAGS": '-static-libgcc -static-libstdc++ -Wl,--export-all-symbols "' + export_path + '"',
        "CMAKE_CXX_STANDARD_LIBRARIES": "-lkernel32 -luser32 -lgdi32 -lwinspool -lshell32 -lole32 -loleaut32 -luuid -lcomdlg32 -ladvapi32 -Wl,-Bstatic -lwinpthread -Wl,-Bdynamic",
    }
    enabled = ("CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS", "MNN_BUILD_SHARED_LIBS", "MNN_BUILD_LLM",
               "MNN_BUILD_LLM_OMNI", "MNN_BUILD_AUDIO", "MNN_BUILD_OPENCV", "MNN_USE_THREAD_POOL",
               "MNN_LOW_MEMORY", "MNN_SUPPORT_TRANSFORMER_FUSE")
    disabled = ("MNN_SEP_BUILD", "MNN_BUILD_TOOLS", "MNN_BUILD_TEST", "MNN_LLM_BUILD_DEMO",
                "LLM_SUPPORT_HTTP_RESOURCE", "MNN_OPENCL", "MNN_VULKAN", "MNN_AVX512", "MNN_OPENMP", "MNN_HIDDEN")
    options.update({name: "ON" for name in enabled})
    options.update({name: "OFF" for name in disabled})
    if opencl:
        options["MNN_OPENCL"] = "ON"
    return ["cmake", "-G", "Ninja", "-S", str(source), "-B", str(output / "build")] + [
        f"-D{name}={value}" for name, value in options.items()
    ]


def audit_binary(output, tools, dll):
    run_logged(output, "export-audit", [tools["objdump"], "-p", str(dll)])
    text = (output / "export-audit.stdout").read_text(encoding="utf-8")
    exports = set(re.findall(r"^\s*\[\s*\d+\] \+base\[\s*\d+\]\s+\S+\s+(\S+)\s*$", text, re.M))
    missing = sorted(set(REQUIRED_EXPORTS) - exports)
    custom = sorted(name for name in exports if name.startswith("MNN_"))
    imports = re.findall(r"DLL Name: (.*)", text)
    runtime_imports = [name for name in imports if name.lower().startswith(("libwinpthread", "libgcc", "libstdc++"))]
    if missing or custom or runtime_imports:
        raise RuntimeError(f"ABI audit failed: missing={missing}, custom={custom}, runtime_imports={runtime_imports}")
    return {"sha256": digest(dll), "bytes": dll.stat().st_size,
            "export_count": len(exports), "required_exports": list(REQUIRED_EXPORTS),
            "imports": imports, "missing_exports": missing, "custom_C_exports": custom}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="Pristine official MNN Git checkout")
    parser.add_argument("--output", type=Path, default=Path.cwd() / "TestArtifacts~/WindowsMNN")
    parser.add_argument("--toolchain-prefix", default="x86_64-w64-mingw32-")
    parser.add_argument("--jobs", type=int, default=os.cpu_count() or 1)
    parser.add_argument("--opencl", action="store_true", help="Enable the official OpenCL backend; validate actual device inference before deployment.")
    parser.add_argument("--install", action="store_true", help="Install the audited DLL into this package")
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    if "TestArtifacts~" not in output.parts or output == source or source in output.parents:
        parser.error("--output must be under TestArtifacts~ and outside the official source tree")
    if args.jobs < 1:
        parser.error("--jobs must be positive")
    before = verify_source(source)
    tools = compiler_tools(args.toolchain_prefix)
    output.mkdir(parents=True, exist_ok=True)
    write_json(output / "source-baseline.json", before)
    command = configure_command(source, output, write_cmake_inputs(output, tools), args.opencl)
    run_logged(output, "configure-reproducible", command)
    run_logged(output, "build-reproducible", ["cmake", "--build", str(output / "build"), "--target", "MNN", "-j", str(args.jobs)])
    dll = output / "build/libMNN.dll"
    audit = audit_binary(output, tools, dll)
    after = verify_source(source)
    if before != after:
        raise RuntimeError("Official source identity changed during the build")
    manifest = {"source": before, "compiler": capture([tools["g++"], "--version"]),
                "target": "Windows x86_64 GNU Itanium names / Microsoft x64 calling convention / libstdc++ C++11 ABI",
                "binary": {"path": str(dll), **audit}, "installed_path": None, "opencl_enabled": args.opencl}
    if args.install:
        target = Path(__file__).resolve().parents[1] / "Runtime/Plugins/Windows/x86_64/MNN.dll"
        shutil.copy2(dll, target)
        if digest(target) != audit["sha256"]:
            raise RuntimeError("Installed DLL hash mismatch")
        manifest["installed_path"] = str(target)
    path = output / "windows-gnu-build-manifest.json"
    write_json(path, manifest)
    print(path)
    print(f"SHA256={audit['sha256']} BYTES={audit['bytes']}")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, RuntimeError, subprocess.CalledProcessError) as error:
        print(error, file=sys.stderr)
        raise SystemExit(1)
