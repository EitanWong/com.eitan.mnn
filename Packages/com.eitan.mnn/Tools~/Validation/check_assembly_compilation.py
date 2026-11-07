#!/usr/bin/env python3
"""Compile each package asmdef with its declared dependencies and local Unity references.

No downloads, Unity launch, native calls, or Player builds. This checks C# assembly
boundaries, including internal visibility, which a flattened compilation misses.
Outputs must stay in this repository's TestArtifacts~/ directory.
"""

import argparse
from graphlib import TopologicalSorter
import json
from pathlib import Path
import subprocess
import sys


def main():
    root = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", required=True, type=Path)
    parser.add_argument("--package", type=Path, default=root / "Packages/com.eitan.mnn")
    parser.add_argument("--sample", type=Path, action="append", default=[],
                        help="Additional imported sample source root to compile with the package; may be repeated.")
    parser.add_argument("--output", type=Path, default=root / "TestArtifacts~/PackageEngineering/Compilation")
    parser.add_argument("--burst-assembly", type=Path, help="Existing compiled Unity.Burst.dll; omit to check the no-Burst installation.")
    args = parser.parse_args()
    package = args.package.resolve()
    source_roots = [package] + [sample.resolve() for sample in args.sample]
    if any(not source.is_dir() for source in source_roots):
        parser.error("Every package/sample source root must exist.")
    output = args.output.resolve()
    if (root / "TestArtifacts~").resolve() not in output.parents:
        parser.error("Compilation outputs must be under this repository's TestArtifacts~/.")
    contents = args.unity.resolve().parents[1]
    compiler = contents / "DotNetSdkRoslyn/csc.dll"
    runtime = contents / "NetCoreRuntime/dotnet"
    if not compiler.is_file() or not runtime.is_file():
        parser.error("Requires an installed macOS Unity with bundled .NET and Roslyn.")
    output.mkdir(parents=True, exist_ok=True)
    definitions = {}
    sample_assemblies = set()
    folders = {}
    guids = {}
    for path in sorted({path for source in source_roots for path in source.rglob("*.asmdef")}):
        definition = json.loads(path.read_text())
        name = definition["name"]
        definitions[name] = definition
        if not path.is_relative_to(package):
            sample_assemblies.add(name)
        folders[path.parent] = name
        meta = Path(str(path) + ".meta").read_text()
        guids[next(line[6:] for line in meta.splitlines() if line.startswith("guid: "))] = name

    def resolve(reference):
        return guids[reference[5:]] if reference.startswith("GUID:") else reference

    for path in sorted({path for source in source_roots for path in source.rglob("*.asmref")}):
        folders[path.parent] = resolve(json.loads(path.read_text())["reference"])

    dependencies = {
        name: {resolve(ref) for ref in definition.get("references", [])
               if resolve(ref) in definitions}
        for name, definition in definitions.items()
    }
    sources = {name: [] for name in definitions}
    for source in sorted({path for source in source_roots for path in source.rglob("*.cs")}):
        owner = next((folders[parent] for parent in source.parents if parent in folders), None)
        if owner:
            sources[owner].append(source)
    standards = sorted((contents / "NetStandard/ref/2.1.0").glob("*.dll"))
    standards += sorted((contents / "NetStandard/compat/2.1.0/shims/netfx").glob("*.dll"))
    engines = sorted((contents / "Managed/UnityEngine").glob("UnityEngine*.dll"))
    editors = sorted((contents / "Managed").glob("UnityEditor*.dll"))
    editors += sorted((contents / "Managed/UnityEditor").glob("*.dll"))
    installed = {path.name: path for path in (contents / "UnityReferenceAssemblies/unity-4.8-api").glob("*.dll")}
    installed.update({path.name: path for path in standards})
    installed.update({path.name: path for path in (contents / "NetStandard/compat/2.1.0/shims/netstandard").glob("*.dll")})
    installed.update({path.name: path for path in (root / "Library/ScriptAssemblies").glob("*.dll")})
    if args.burst_assembly:
        installed["Unity.Burst.dll"] = args.burst_assembly.resolve()
    nunit = sorted((root / "Library/PackageCache").glob("com.unity.ext.nunit*/net35/unity-custom/nunit.framework.dll"))
    if nunit:
        installed["nunit.framework.dll"] = nunit[-1]
    symbols = ["UNITY_EDITOR", "UNITY_EDITOR_OSX", "UNITY_STANDALONE_OSX", "ENABLE_MONO",
               "UNITY_2021_3", "UNITY_2021_3_OR_NEWER", "UNITY_2020_1_OR_NEWER",
               "UNITY_2019_1_OR_NEWER", "UNITY_2018_1_OR_NEWER", "NET_STANDARD_2_1",
               "UNITY_INCLUDE_TESTS"]
    results = []
    for name in TopologicalSorter(dependencies).static_order():
        definition = definitions[name]
        if name == "MNN.Unity.Burst" and not args.burst_assembly:
            results.append({"assembly": name, "optional_absent": True, "passed": True})
            print(name + ": OPTIONAL (Burst absent)", flush=True)
            continue
        references = list(standards)
        if not definition.get("noEngineReferences", False):
            references += engines
        if definition.get("includePlatforms") == ["Editor"] or name == "MNN.Unity.Burst" or name in sample_assemblies:
            references += editors
        for reference in definition.get("references", []):
            dependency = resolve(reference)
            references.append(output / (dependency + ".dll") if dependency in definitions
                              else installed[dependency + ".dll"])
        for reference in definition.get("precompiledReferences", []):
            references.append(installed[reference])
        if "TestAssemblies" in definition.get("optionalUnityReferences", []):
            references += [installed["nunit.framework.dll"], installed["UnityEngine.TestRunner.dll"]]
        assembly = output / (name + ".dll")
        assembly.unlink(missing_ok=True)
        command = [str(runtime), str(compiler), "-nologo", "-noconfig", "-nostdlib+", "-target:library",
                   "-langversion:9", "-unsafe" + ("+" if definition.get("allowUnsafeCode") else "-"),
                   "-define:" + ";".join(symbols), "-out:" + str(assembly)]
        command += ["-r:" + str(path) for path in dict.fromkeys(references)]
        command += [str(path) for path in sources[name]]
        result = subprocess.run(command, capture_output=True, text=True, timeout=120, cwd=output)
        (output / (name + ".log")).write_text(result.stdout + result.stderr)
        passed = result.returncode == 0
        results.append({"assembly": name, "sources": len(sources[name]), "passed": passed})
        print(name + ": " + ("PASS" if passed else "FAIL"), flush=True)
        if not passed:
            print(result.stdout + result.stderr, file=sys.stderr)
            break
    report = {"kind": "csharp_assembly_compilation_only", "native_inference_verified": False,
              "expected_assemblies": len(definitions), "assemblies": results}
    (output / "summary.json").write_text(json.dumps(report, indent=2) + "\n")
    return 0 if len(results) == len(definitions) and all(item["passed"] for item in results) else 1


if __name__ == "__main__":
    raise SystemExit(main())
