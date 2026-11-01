#!/usr/bin/env python3
"""Build the pinned official mobile inference ABI without generating C++ code.

Requires an existing, unmodified MNN 3.6.2 source tree. Does not download tools
or sources. Artifacts and logs stay in this project's TestArtifacts~ directory.
Only --install replaces the package's existing plugins (preserving Unity meta).
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess


def run(command, log, **kwargs):
    with log.open("w") as stream:
        subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT, check=True, **kwargs)


def source_version(source):
    defines = (source / "include/MNN/MNNDefine.h").read_text()
    return ".".join(re.search(r"#define MNN_VERSION_" + part + r"\s+(\d+)", defines)[1]
                    for part in ("MAJOR", "MINOR", "PATCH"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--platform", choices=["android", "ios"], required=True)
    parser.add_argument("--ndk", type=Path, help="Android NDK r25c (25.2.9519653)")
    parser.add_argument("--jobs", type=int, default=6)
    features = parser.add_mutually_exclusive_group()
    features.add_argument("--llm", dest="llm", action="store_true", default=True,
                          help="Include official LLM/Omni inference (default) and audit its ABI.")
    features.add_argument("--basic-only", dest="llm", action="store_false",
                          help="Build the historical Interpreter-only ABI for audit; cannot install it.")
    parser.add_argument("--install", action="store_true")
    parser.add_argument("--benchmarks", action="store_true",
                        help="Also build official llm_bench/llm_demo in TestArtifacts~ (never install them).")
    parser.add_argument("--legacy-global-noinline", action="store_true",
                        help="Reproduce the old global no-inlining build for performance comparisons.")
    parser.add_argument("--ios-arm82", action=argparse.BooleanOptionalAction, default=True,
                        help="Compile official iOS FP16 CPU kernels (default); native runtime selects supported hardware.")
    parser.add_argument("--ios-sme2", action=argparse.BooleanOptionalAction, default=True,
                        help="Compile official iOS SME2 kernels (default); requires a compatible Apple toolchain and device validation.")
    parser.add_argument("--android-accelerators", action="store_true", default=True,
                        help="Enable official OpenCL, Vulkan and NNAPI Android runtimes (default).")
    parser.add_argument("--android-cpu-only", dest="android_accelerators", action="store_false",
                        help="Build an explicit CPU-only Android variant.")
    parser.add_argument("--output", type=Path, help="Build artifacts directory (must be inside project TestArtifacts~)")
    args = parser.parse_args()
    if args.install and not args.llm:
        parser.error("--basic-only cannot replace the package plugin: managed mobile bindings require LLM/Omni symbols.")
    if args.benchmarks and not args.llm:
        parser.error("--benchmarks requires LLM support.")
    if args.benchmarks and args.platform != "android":
        parser.error("--benchmarks builds Android command-line runners; iOS uses a signed Unity test Player.")
    package = Path(__file__).resolve().parents[1]
    source = args.source.resolve()
    if source_version(source) != "3.6.2":
        parser.error("The mobile ABI contract requires MNN 3.6.2.")
    if (source / ".git").exists():
        changes = subprocess.check_output(
            ["git", "-C", str(source), "status", "--porcelain", "--untracked-files=no"], text=True)
        if changes:
            parser.error("Use unmodified official sources; the supplied checkout has tracked changes.")
    root = package.parents[1] / ("TestArtifacts~/MobileLlmDirectCpp" if args.llm else "TestArtifacts~/MobileDirectCpp")
    if args.output:
        candidate = args.output.resolve()
        if (package.parents[1] / "TestArtifacts~").resolve() not in candidate.parents:
            parser.error("--output must be inside this project's TestArtifacts~")
        root = candidate
    root.mkdir(parents=True, exist_ok=True)
    retention = ["-DCMAKE_PROJECT_MNN_INCLUDE=" + str(package / "Native~/retain-abi.cmake")]
    inline_flag = " -fno-inline-functions" if args.legacy_global_noinline else ""
    if args.legacy_global_noinline:
        retention = ["-DCMAKE_PROJECT_MNN_INCLUDE="]
    common = ["-DCMAKE_POLICY_VERSION_MINIMUM=3.5", "-DCMAKE_BUILD_TYPE=Release", "-DCMAKE_CXX_STANDARD=17",
              "-DCMAKE_CXX_FLAGS=-include cmath -femit-all-decls" + inline_flag + " -D_LIBCPP_DISABLE_VISIBILITY_ANNOTATIONS",
              "-DCMAKE_CXX_FLAGS_RELEASE=-O3 -DNDEBUG -fno-visibility-inlines-hidden -fvisibility=default", "-DMNN_SEP_BUILD=OFF", "-DMNN_BUILD_LLM=" + ("ON" if args.llm else "OFF"),
              "-DMNN_BUILD_LLM_OMNI=" + ("ON" if args.llm else "OFF"),
              "-DMNN_LLM_BUILD_DEMO=" + ("ON" if args.benchmarks else "OFF"), "-DLLM_SUPPORT_HTTP_RESOURCE=OFF",
              "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON",
              "-DMNN_BUILD_TOOLS=OFF", "-DMNN_BUILD_TEST=OFF", "-DMNN_BUILD_CONVERTER=OFF",
              "-DMNN_OPENMP=OFF", "-DMNN_USE_THREAD_POOL=ON",
              "-DMNN_LOW_MEMORY=ON", "-DMNN_SUPPORT_TRANSFORMER_FUSE=ON",
              "-DMNN_ARM82=" + ("ON" if args.ios_arm82 else "OFF"),
              "-DMNN_SME2=" + ("ON" if args.platform == "ios" and args.ios_sme2 else "OFF")] + retention
    targets = ["MNN"] + (["llm_bench", "llm_demo"] if args.benchmarks and args.platform == "android" else [])
    if args.platform == "android":
        if not args.ndk:
            parser.error("--ndk is required for Android.")
        ndk = args.ndk.resolve()
        if "25.2.9519653" not in (ndk / "source.properties").read_text():
            parser.error("Use NDK r25c; other libc++ compiler ABIs need separate validation.")
        build = root / "android-arm64"
        build.mkdir(exist_ok=True)
        options = ["-DCMAKE_TOOLCHAIN_FILE=" + str(ndk / "build/cmake/android.toolchain.cmake"),
                   "-DANDROID_ABI=arm64-v8a", "-DANDROID_PLATFORM=android-21",
                   "-DANDROID_STL=c++_static", "-DMNN_BUILD_SHARED_LIBS=ON",
                   "-DCMAKE_SHARED_LINKER_FLAGS=-Wl,-z,max-page-size=16384",
                   "-DMNN_ARM82=ON", "-DMNN_METAL=OFF", "-DMNN_OPENCL=" + ("ON" if args.android_accelerators else "OFF"),
                   "-DMNN_VULKAN=" + ("ON" if args.android_accelerators else "OFF"),
                   "-DMNN_NNAPI=" + ("ON" if args.android_accelerators else "OFF"),
                   "-DMNN_BUILD_FOR_ANDROID_COMMAND=ON"]
        run(["cmake", "-S", str(source), "-B", str(build), "-G", "Ninja"] + common + options,
            build / "configure.log")
        run(["cmake", "--build", str(build), "--target"] + targets + ["-j", str(args.jobs)], build / "build.log")
        libraries = list(build.rglob("libMNN.so"))
        if len(libraries) != 1:
            raise RuntimeError("Expected exactly one Android MNN library: " + str(libraries))
        library = libraries[0]
        toolchain = ndk / "toolchains/llvm/prebuilt/darwin-x86_64/bin"
        subprocess.run([str(toolchain / "llvm-strip"), "--strip-debug", str(library)], check=True)
        nm = toolchain / "llvm-nm"
        symbols = subprocess.check_output([str(nm), "--defined-only", "--dynamic", str(library)], text=True)
        audit(library, symbols, "_", "__ndk1", build, args.llm)
        if args.install:
            destination = package / "Runtime/Plugins/Android/libs/arm64-v8a/libMNN.so"
            shutil.copy2(library, destination)
    else:
        frameworks = []
        for sdk, label in [("iphoneos", "ios-arm64"), ("iphonesimulator", "ios-arm64-simulator")]:
            build = root / label
            build.mkdir(exist_ok=True)
            options = ["-DCMAKE_SYSTEM_NAME=iOS", "-DCMAKE_OSX_SYSROOT=" + sdk,
                       "-DCMAKE_OSX_ARCHITECTURES=arm64", "-DARCHS=arm64", "-DCMAKE_SYSTEM_PROCESSOR=arm64", "-DCMAKE_OSX_DEPLOYMENT_TARGET=15.0",
                       "-DMNN_BUILD_SHARED_LIBS=OFF", "-DMNN_AAPL_FMWK=ON", "-DMNN_METAL=ON"]
            run(["cmake", "-S", str(source), "-B", str(build), "-G", "Ninja"] + common + options,
                build / "configure.log")
            run(["cmake", "--build", str(build), "--target", "MNN", "-j", str(args.jobs)], build / "build.log")
            framework = build / "MNN.framework"
            symbols = subprocess.check_output(["nm", "-gU", str(framework / "MNN")], text=True)
            audit(framework / "MNN", symbols, "__", "__1", build, args.llm)
            frameworks += ["-framework", str(framework)]
        output = root / "MNN.xcframework"
        if output.exists():
            shutil.rmtree(output)
        run(["xcodebuild", "-create-xcframework"] + frameworks + ["-output", str(output)], root / "xcframework.log")
        if args.install:
            destination = package / "Runtime/Plugins/iOS/MNN.xcframework"
            shutil.copytree(output, destination, dirs_exist_ok=True)
    print("Built and audited " + args.platform + ("; installed package plugin." if args.install else "; pass --install to update package plugin."))


def active_declarations(path, namespace):
    """Apply the small C# define subset used by the interop partials."""
    defines = {"ENABLE_IL2CPP", "UNITY_ANDROID" if namespace == "__ndk1" else "UNITY_IOS"}
    active, stack, lines = True, [], []
    def condition(expression):
        expression = re.sub(r"\b[A-Za-z_]\w*\b", lambda match: str(match[0] in defines), expression)
        expression = expression.replace("&&", " and ").replace("||", " or ").replace("!", " not ")
        return bool(eval(expression, {"__builtins__": {}}, {}))
    for line in path.read_text().splitlines():
        directive = line.strip().split(maxsplit=1)
        keyword = directive[0] if directive else ""
        argument = directive[1] if len(directive) > 1 else ""
        if keyword == "#if":
            branch = condition(argument)
            stack.append((active, branch))
            active = active and branch
        elif keyword == "#elif":
            parent, taken = stack[-1]
            branch = not taken and condition(argument)
            stack[-1] = (parent, taken or branch)
            active = parent and branch
        elif keyword == "#else":
            parent, taken = stack[-1]
            active = parent and not taken
            stack[-1] = (parent, True)
        elif keyword == "#endif":
            active, _ = stack.pop()
        elif keyword == "#define" and active:
            defines.add(argument)
        elif active:
            lines.append(line)
    if stack:
        raise ValueError("Unbalanced preprocessor directives: " + str(path))
    return "\n".join(lines)


def audit(library, listing, prefix, namespace, build, llm=False):
    symbols = {line.split()[-1] for line in listing.splitlines() if line.split()}
    interop = Path(__file__).resolve().parents[1] / "Runtime/Interop"
    required = set()
    tasks = ["Interpreter", "Tensor", "Backend", "Version"]
    if llm:
        tasks += ["Abi", "Llm", "Conversation", "PromptCache", "Streaming", "Multimodal", "Express", "Embedding", "CosyVoice"]
    for task in tasks:
        declarations = active_declarations(interop / ("MNNInterop." + task + ".cs"), namespace)
        required.update(prefix[:-1] + symbol for symbol in re.findall(r'EntryPoint = "([^"]+)"', declarations))
        required.update(prefix[:-1] + symbol for symbol in re.findall(r'AppleStringDestructorSymbol = "([^"]+)"', declarations))
    # iOS links SDK libc++ alongside the static MNN archive. Only accept exact
    # compiler runtime symbols declared by that SDK; never ignore MNN methods.
    if llm and namespace == "__1":
        sdk = subprocess.check_output(["xcrun", "--sdk", "iphoneos", "--show-sdk-path"], text=True).strip()
        runtime = (Path(sdk) / "usr/lib/libc++.tbd").read_text()
        runtime_symbols = set(re.findall(r"__[A-Za-z0-9_]+", runtime))
        symbols.update(required & runtime_symbols)
    missing = sorted(set(required) - symbols)
    custom = sorted(s for s in symbols if s.startswith(prefix[:-1] + "MNN_"))
    report = {"library": str(library), "stl_namespace": namespace,
              "missing": missing, "custom_exports": custom, "bound_official_symbols": len(required),
              "library_sha256": hashlib.sha256(library.read_bytes()).hexdigest(),
              "device_inference_verified": False, "llm_enabled": llm}
    cache = dict(re.findall(r"^([A-Za-z0-9_]+):[^=\r\n]*=(.*)$", (build / "CMakeCache.txt").read_text(), re.MULTILINE))
    report["build_options"] = {key: value for key, value in cache.items()
                               if key.startswith("MNN_") or key in ("CMAKE_CXX_FLAGS", "CMAKE_CXX_FLAGS_RELEASE", "CMAKE_PROJECT_MNN_INCLUDE")}
    source = Path(cache["CMAKE_HOME_DIRECTORY"])
    report["mnn_version"] = source_version(source)
    report["source_headers_sha256"] = {
        name: hashlib.sha256((source / name).read_bytes()).hexdigest()
        for name in ("include/MNN/MNNDefine.h", "include/MNN/Tensor.hpp", "include/MNN/Interpreter.hpp",
                     "transformers/llm/engine/include/llm/llm.hpp")}
    (build / "symbols.json").write_text(json.dumps(report, indent=2) + "\n")
    if missing or custom:
        raise RuntimeError("Mobile official symbol audit failed: " + json.dumps(report))


if __name__ == "__main__":
    main()
