#!/usr/bin/env python3
"""Compile Runtime with platform defines using an installed macOS Unity compiler.

This is a C# conditional-compilation check against the local Unity reference
assemblies, NOT a target Player build, native link, or device inference test.
Nothing is downloaded. All outputs stay in the repository's TestArtifacts~.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[4]
    contents = args.unity.resolve().parents[1]
    dotnet = contents / "NetCoreRuntime/dotnet"
    csc = contents / "DotNetSdkRoslyn/csc.dll"
    monodis = contents / "MonoBleedingEdge/bin/monodis"
    reference_dir = contents / "NetStandard/ref/2.1.0"
    if not all(p.exists() for p in (dotnet, csc, monodis, reference_dir)):
        parser.error("Requires an installed macOS Unity with bundled .NET/Roslyn, monodis and .NET Standard 2.1 references.")
    output = root / "TestArtifacts~/MNNValidation/PlatformValidation/CompileMatrix"
    output.mkdir(parents=True, exist_ok=True)
    sources = sorted((root / "Packages/com.eitan.mnn/Runtime").rglob("*.cs"))
    references = sorted(reference_dir.glob("*.dll")) + sorted(
        (contents / "NetStandard/compat/2.1.0/shims/netfx").glob("*.dll")) + sorted(
        (contents / "Managed/UnityEngine").glob("UnityEngine*.dll"))
    common = ["UNITY_2021_3", "UNITY_2021_3_OR_NEWER", "UNITY_2020_1_OR_NEWER",
              "UNITY_2019_1_OR_NEWER", "UNITY_2018_1_OR_NEWER", "NET_STANDARD_2_1"]
    cases = []
    for platform, symbol in [("macOS", "UNITY_STANDALONE_OSX"), ("Windows", "UNITY_STANDALONE_WIN"),
                             ("Linux", "UNITY_STANDALONE_LINUX"), ("Android", "UNITY_ANDROID"),
                             ("iOS", "UNITY_IOS"), ("WebGL", "UNITY_WEBGL")]:
        backends = ["IL2CPP"] if platform in ("iOS", "WebGL") else ["Mono", "IL2CPP"]
        for backend in backends:
            cases.append((platform + "-" + backend, [symbol, "ENABLE_" + backend.upper()],
                          60 if platform == "macOS" and backend == "Mono" else 0))
    # A macOS Editor may target an IL2CPP Player while running Mono itself.
    for platform in ("OSX", "WIN", "LINUX"):
        cases.append(("Editor-" + platform, ["UNITY_EDITOR", "UNITY_EDITOR_" + platform,
                                            "ENABLE_IL2CPP"], 60 if platform == "OSX" else 0))
    summary = []
    for label, symbols, expected in cases:
        assembly = output / (label + ".dll")
        assembly.unlink(missing_ok=True)
        command = [str(dotnet), str(csc), "-nologo", "-noconfig", "-nostdlib+", "-target:library",
                   "-unsafe+", "-langversion:latest", "-define:" + ";".join(common + symbols),
                   "-out:" + str(assembly)]
        command += ["-r:" + str(p) for p in references] + [str(p) for p in sources]
        compiled = subprocess.run(command, capture_output=True, text=True, timeout=120, cwd=output)
        (output / (label + ".log")).write_text(compiled.stdout + compiled.stderr, encoding="utf-8")
        imports = None
        if compiled.returncode == 0:
            metadata = subprocess.run([str(monodis), "--implmap", str(assembly)],
                                      capture_output=True, text=True, check=True, timeout=30).stdout
            (output / (label + "-imports.txt")).write_text(metadata, encoding="utf-8")
            match = re.search(r"ImplMap Table \(1\.\.(\d+)\)", metadata)
            if match:
                imports = int(match.group(1))
        success = compiled.returncode == 0 and imports == expected
        summary.append({"case": label, "compiled": compiled.returncode == 0,
                        "native_imports": imports, "expected_imports": expected, "passed": success})
        print(label + ": " + ("PASS" if success else "FAIL") + "; native imports=" + str(imports))
    report = {"kind": "conditional_csharp_compilation_only", "target_player_built": False,
              "device_inference_verified": False, "cases": summary}
    (output / "summary.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    return 0 if all(case["passed"] for case in summary) else 1


if __name__ == "__main__":
    raise SystemExit(main())
