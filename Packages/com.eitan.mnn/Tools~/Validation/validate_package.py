#!/usr/bin/env python3
"""Validate UPM layout, assembly boundaries, Unity metadata and local doc links.

Uses only the Python standard library. Does not modify files, load native
libraries, download dependencies, or claim C# compilation/inference coverage.
"""

import argparse
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote, urlsplit


ASSEMBLIES = {
    "MNN.Unity": ("Runtime/MNN.Unity.asmdef", set()),
    "MNN.Unity.Editor": ("Editor/MNN.Unity.Editor.asmdef", {"MNN.Unity"}),
    "MNN.Unity.Tests": (
        "Tests/Editor/MNN.Unity.Tests.asmdef", {"MNN.Unity", "MNN.Unity.Editor"}
    ),
    "MNN.Unity.Runtime.Tests": (
        "Tests/Runtime/MNN.Unity.Runtime.Tests.asmdef", {"MNN.Unity"}
    ),
    "MNN.Unity.Samples.BasicExample": (
        "Samples~/BasicExample/MNN.Unity.Samples.BasicExample.asmdef", {"MNN.Unity"}
    ),
}
ROOT_DIRECTORIES = {
    "Runtime", "Editor", "Tests", "Samples~", "Documentation~", "Native~", "Tools~"
}
ROOT_FILES = {"package.json", "README.md", "CHANGELOG.md", "LICENSE.md"}
CORE_DOMAINS = {
    "Common", "Inference", "Models", "Language", "Retrieval", "Generation", "Performance"
}
OPAQUE_SUFFIXES = (".framework", ".xcframework", ".bundle")
GENERATED_DIRECTORIES = {
    "Library", "Temp", "Logs", "obj", "bin", "__pycache__", "TestArtifacts~"
}


def inspect(package):
    errors = []

    def require(condition, message):
        if not condition:
            errors.append(message)

    require((package / "package.json").is_file(), "Missing package.json")
    if errors:
        return errors, 0
    metadata = json.loads((package / "package.json").read_text(encoding="utf-8"))
    require(metadata.get("name") == "com.eitan.mnn", "Unexpected package name")
    for path in package.iterdir():
        if path.suffix == ".meta":
            continue
        allowed = ROOT_DIRECTORIES if path.is_dir() else ROOT_FILES
        require(path.name in allowed, "Unexpected package root entry: " + path.name)

    paths = []
    for path in sorted(package.rglob("*")):
        relative = path.relative_to(package)
        if any(part.endswith(OPAQUE_SUFFIXES) for part in relative.parts[:-1]):
            continue  # Native plugin internals are not individual Unity assets.
        paths.append(path)
        require(not any(part in GENERATED_DIRECTORIES for part in relative.parts),
                "Generated directory in package: " + str(relative))
        require(path.name != ".DS_Store" and path.suffix not in {".log", ".pyc", ".csproj", ".sln", ".slnx"},
                "Generated file in package: " + str(relative))
        require(path.suffix not in {".c", ".cc", ".cpp", ".cxx"},
                "Custom native source is not allowed in this package: " + str(relative))
        if path.suffix == ".meta":
            require(path.with_suffix("").exists(), "Orphan meta: " + str(relative))
        elif relative.parts[0] in {"Runtime", "Editor", "Tests", "Samples~"} and relative.parts != ("Samples~",):
            require(Path(str(path) + ".meta").is_file(), "Missing meta: " + str(relative))

    guids = {}
    for path in paths:
        if path.suffix != ".meta":
            continue
        match = re.search(r"^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8"), re.M)
        require(match is not None, "Invalid GUID: " + str(path.relative_to(package)))
        if match:
            guid = match.group(1)
            require(guid not in guids, "Duplicate GUID: " + str(path.relative_to(package)))
            guids[guid] = path

    definitions = {}
    for path in paths:
        if path.suffix == ".asmdef":
            definition = json.loads(path.read_text(encoding="utf-8"))
            name = definition.get("name")
            require(name not in definitions, "Duplicate assembly name: " + str(name))
            definitions[name] = (path, definition)
    require(set(definitions) == set(ASSEMBLIES), "Assembly inventory differs from the documented layout")
    for name, (relative, dependencies) in ASSEMBLIES.items():
        if name not in definitions:
            continue
        path, definition = definitions[name]
        require(path == package / relative, "Assembly in wrong folder: " + name)
        references = set(definition.get("references", []))
        require({ref for ref in references if ref.startswith("MNN.Unity")} == dependencies,
                "Incorrect package assembly dependencies: " + name)
        if name in {"MNN.Unity.Editor", "MNN.Unity.Tests"}:
            require(definition.get("includePlatforms") == ["Editor"], "Assembly must be Editor-only: " + name)
        else:
            require(not definition.get("includePlatforms"), "Assembly must allow Player compilation: " + name)
            require(not any("Editor" in ref for ref in references), "Editor dependency in Player assembly: " + name)
        if name.endswith("Tests"):
            require("UNITY_INCLUDE_TESTS" in definition.get("defineConstraints", []),
                    "Missing test constraint: " + name)
            require(definition.get("autoReferenced") is False, "Test assembly is auto-referenced: " + name)

    for path in paths:
        if path.suffix != ".cs":
            continue
        relative = path.relative_to(package)
        text = path.read_text(encoding="utf-8-sig")
        require(not path.read_bytes().startswith(b"\xef\xbb\xbf") and b"\r" not in path.read_bytes(),
                "C# source must use UTF-8 without BOM and LF: " + str(relative))
        require(text.endswith("\n") and not re.search(r"[ \t]+$", text, re.M),
                "C# source has a missing final newline or trailing whitespace: " + str(relative))
        require("ProjectScope.ProjectName" not in text, "Template namespace: " + str(relative))
        if relative.parts[0].endswith("~") and relative.parts[0] != "Samples~":
            continue  # Standalone tooling is deliberately not a package assembly.
        owner = next((parent for parent in path.parents if list(parent.glob("*.asmdef"))), None)
        require(owner is not None and package in owner.parents,
                "Source has no assembly boundary: " + str(relative))
        if relative.parts[0] == "Tests":
            require(len(relative.parts) > 3 and relative.parts[1] in {"Editor", "Runtime"},
                    "Test source must have a platform and domain: " + str(relative))
        if relative.parts[:2] == ("Runtime", "Core"):
            require(len(relative.parts) > 3 and relative.parts[2] in CORE_DOMAINS,
                    "Managed source lacks a Core domain: " + str(relative))
            require(not re.search(r"\bclass\s+\w+\s*:\s*MonoBehaviour\b", text),
                    "MonoBehaviour belongs in Runtime/Components: " + str(relative))
        if relative.parts[0] == "Runtime":
            require(not re.search(r"^using\s+(?:NUnit|UnityEditor)\b", text, re.M),
                    "Editor/test dependency in Runtime: " + str(relative))
            if "[DllImport(" in text:
                require(relative.parts[1] == "Interop" and path.name.startswith("MNNInterop."),
                        "Native import outside MNNInterop partials: " + str(relative))
        if relative.parts[:3] == ("Runtime", "Interop", "Handles"):
            require("namespace MNN.Unity.Interop.Handles" in text,
                    "Handle namespace differs from its folder: " + str(relative))

    for sample in metadata.get("samples", []):
        directory = package / sample["path"]
        require(directory.is_dir() and (directory / "README.md").is_file(),
                "Sample lacks a directory or README: " + sample["path"])
    for path in paths:
        if path.suffix != ".md":
            continue
        for target in re.findall(r"\[[^\]]*\]\(([^)]+)\)", path.read_text(encoding="utf-8")):
            target = target.strip().split(' "', 1)[0].strip("<>")
            url = urlsplit(target)
            if url.scheme or url.netloc or not url.path:
                continue
            require((path.parent / unquote(url.path)).exists(),
                    "Broken local link in " + str(path.relative_to(package)) + ": " + target)
    return errors, len(paths)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    try:
        errors, count = inspect(args.package.resolve())
    except (OSError, ValueError, KeyError) as error:
        print("Package validation failed: " + str(error), file=sys.stderr)
        return 1
    if errors:
        for error in errors:
            print("ERROR: " + error, file=sys.stderr)
        print(f"Package validation failed: {len(errors)} issue(s).", file=sys.stderr)
        return 1
    print(f"Package structure valid: {count} entries, {len(ASSEMBLIES)} assembly boundaries.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
