#!/usr/bin/env python3
"""Compile the API contract tool with an installed .NET 8 SDK, then check actual C# symbols.

No NuGet restore, dependency download, Unity launch or native inference. Generated
tool binaries and diagnostics stay in TestArtifacts~. --write refreshes the reviewed
API manifest/reference after a successful semantic boundary check.
"""

import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[4]
PACKAGE = ROOT / "Packages/com.eitan.mnn"


def sdk_paths(dotnet):
    result = subprocess.run([dotnet, "--list-sdks"], capture_output=True, text=True, check=True)
    versions = [match.groups() for match in re.finditer(r"^(8\.\d+\.\d+) \[(.+)\]$", result.stdout, re.MULTILINE)]
    if not versions:
        raise ValueError("Provision an existing .NET 8 SDK; this tool never installs it.")
    version, location = max(versions, key=lambda item: tuple(map(int, item[0].split("."))))
    sdk = Path(location) / version
    packs = list((Path(location).parent / "packs/Microsoft.NETCore.App.Ref").glob("8.*/ref/net8.0"))
    if not packs:
        raise ValueError("The installed .NET 8 SDK has no reference pack.")
    references = max(packs, key=lambda path: tuple(map(int, path.parents[1].name.split("."))))
    return sdk, references


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity", type=Path, help="Installed Unity executable for metadata references only")
    parser.add_argument("--package", type=Path, default=PACKAGE)
    parser.add_argument("--output", type=Path, default=ROOT / "TestArtifacts~/PublicApiContract")
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--dotnet", default=shutil.which("dotnet"))
    args = parser.parse_args()
    if not args.dotnet or not args.self_test and not args.unity:
        parser.error("An installed .NET 8 SDK and --unity are required (self-test needs only the SDK).")
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / "TestArtifacts~") or output == ROOT / "TestArtifacts~":
        parser.error("output must be a child of this repository's TestArtifacts~/")
    output.mkdir(parents=True, exist_ok=True)
    try:
        sdk, references = sdk_paths(args.dotnet)
    except (ValueError, subprocess.CalledProcessError) as error:
        parser.error(str(error))
    binary = output / "Tool"
    binary.mkdir(exist_ok=True)
    roslyn = sdk / "Roslyn/bincore"
    dependencies = [roslyn / "Microsoft.CodeAnalysis.dll", roslyn / "Microsoft.CodeAnalysis.CSharp.dll"]
    command = [args.dotnet, str(roslyn / "csc.dll"), "-nologo", "-noconfig", "-nostdlib+", "-nullable:enable",
               "-target:exe", "-out:" + str(binary / "ApiContract.dll")]
    command += ["-r:" + str(path) for path in sorted(references.glob("*.dll")) + dependencies]
    command.append(str(PACKAGE / "Tools~/ApiContract/ApiContract.cs"))
    result = subprocess.run(command, capture_output=True, text=True, timeout=60)
    (output / "tool-build.stdout").write_text(result.stdout, encoding="utf-8")
    (output / "tool-build.stderr").write_text(result.stderr, encoding="utf-8")
    if result.returncode:
        print(result.stdout + result.stderr, file=sys.stderr)
        return result.returncode
    for dependency in dependencies:
        shutil.copy2(dependency, binary / dependency.name)
    (binary / "ApiContract.runtimeconfig.json").write_text(json.dumps({"runtimeOptions": {
        "tfm": "net8.0", "framework": {"name": "Microsoft.NETCore.App", "version": "8.0.0"}}}) + "\n")
    command = [args.dotnet, str(binary / "ApiContract.dll")]
    command += ["--self-test"] if args.self_test else [str(args.package.resolve()),
        str(args.unity.resolve().parents[1]), str(output), "write" if args.write else "check"]
    result = subprocess.run(command, capture_output=True, text=True, timeout=180)
    (output / "contract.stdout").write_text(result.stdout, encoding="utf-8")
    (output / "contract.stderr").write_text(result.stderr, encoding="utf-8")
    print(result.stdout, end="")
    print(result.stderr, end="", file=sys.stderr)
    return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
