#!/usr/bin/env python3
"""Check tracked repository files, local-only assets, manifests and Markdown links.

Uses Git's index as the file inventory so ignored or untracked local files cannot
hide broken links. This is a static check; it does not run Unity or native code.
"""

import argparse
import fnmatch
import json
from pathlib import Path, PurePosixPath
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit


LOCAL_DIRECTORIES = {
    "Library", "Temp", "Logs", "UserSettings", "TestArtifacts~", "__pycache__",
    ".venv", ".idea", ".vscode", ".claude", ".codex", "node_modules",
}
LOCAL_SUFFIXES = {".csproj", ".sln", ".slnx", ".pyc", ".log", ".part", ".partial"}
MODEL_PATTERNS = (
    "*.mnn", "*.mnn.weight", "*.safetensors", "*.onnx", "*.tflite", "*.gguf",
    "*.pt", "*.pth", "pytorch_model*.bin",
)
FIXTURE = "Packages/com.eitan.mnn/Tests/Fixtures/affine.mnn"
LOCAL_PACKAGES = {"com.eitan.easymic", "com.eitan.easymic.apm"}


def git(root, *arguments):
    return subprocess.run(
        ["git", "-C", str(root), *arguments], check=True, capture_output=True,
    ).stdout.decode("utf-8").split("\0")


def inside(name, directory):
    return name == directory or name.startswith(directory + "/")


def local_package(path):
    if len(path.parts) < 2 or path.parts[0] != "Packages":
        return False
    name = path.parts[1].removesuffix(".meta")
    return any(name == package or name.startswith(package + "-") for package in LOCAL_PACKAGES)


def local_only(name):
    path = PurePosixPath(name)
    plain = name.removesuffix(".meta")
    return any((
        any(part in LOCAL_DIRECTORIES for part in path.parts),
        inside(plain, "Assets/Samples"),
        inside(plain, "Assets/StreamingAssets/MNN/Models"),
        inside(plain, "MNN"),
        local_package(path),
        path.name == ".DS_Store",
        path.suffix in LOCAL_SUFFIXES,
        fnmatch.fnmatch(path.name, "mono_cras*.json"),
        fnmatch.fnmatch(path.name, "mono_cras*.blob"),
        plain != FIXTURE and any(fnmatch.fnmatch(PurePosixPath(plain).name, pattern) for pattern in MODEL_PATTERNS),
    ))


def manifest_errors(root, name):
    try:
        metadata = json.loads((root / name).read_text(encoding="utf-8"))
        dependencies = metadata.get("dependencies", {})
        if not isinstance(dependencies, dict):
            return ["Dependencies must be an object in " + name]
    except (ValueError, AttributeError) as error:
        return [f"Invalid manifest {name}: {error}"]
    return [f"Local-only dependency in {name}: {package}" for package in sorted(LOCAL_PACKAGES & dependencies.keys())]


def markdown_targets(content):
    # Ignore examples in fenced blocks and HTML comments.
    content = re.sub(r"(?ms)^(`{3,}|~{3,}).*?^\1[^\n]*$", "", content)
    content = re.sub(r"(?s)<!--.*?-->", "", content)
    inline = re.findall(r"\[[^\]]*\]\((<[^>]+>|[^\s)]+)(?:\s+[^)]*)?\)", content)
    references = re.findall(r"(?m)^\s*\[[^\]]+\]:\s*(<[^>]+>|\S+)", content)
    return [target.strip("<>") for target in inline + references]


def link_errors(root, tracked):
    errors = []
    tracked_paths = {root / name for name in tracked}
    directories = {parent for path in tracked_paths for parent in path.parents if parent.is_relative_to(root)}
    for name in sorted(tracked):
        if not name.endswith(".md") or not (root / name).is_file():
            continue
        for target in markdown_targets((root / name).read_text(encoding="utf-8")):
            url = urlsplit(target)
            if url.scheme or url.netloc or not url.path:
                continue
            resolved = ((root if url.path.startswith("/") else (root / name).parent) / unquote(url.path).lstrip("/")).resolve()
            if resolved not in tracked_paths and resolved not in directories:
                errors.append(f"Untracked or missing link in {name}: {target}")
    return errors


def inspect(root):
    tracked = set(filter(None, git(root, "ls-files", "-z")))
    errors = ["Local-only tracked file: " + name for name in sorted(tracked) if local_only(name)]
    ignored = filter(None, git(root, "ls-files", "-ci", "--exclude-standard", "-z"))
    errors.extend("Tracked file matches ignore rules: " + name for name in ignored)
    for name in sorted(tracked):
        if not (root / name).is_file():
            errors.append("Tracked file missing from working tree: " + name)
            continue
        if local_only(name):
            continue
        if name.endswith(("/manifest.json", "/package.json")):
            errors.extend(manifest_errors(root, name))
    errors.extend(link_errors(root, tracked))
    return errors, len(tracked)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[4])
    args = parser.parse_args()
    try:
        errors, count = inspect(args.root.resolve())
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print("Repository validation failed: " + str(error), file=sys.stderr)
        return 1
    for error in errors:
        print("ERROR: " + error)
    if errors:
        print(f"Repository validation failed: {len(errors)} issue(s).", file=sys.stderr)
        return 1
    print(f"Repository valid: {count} tracked files; asset policy, manifests and local document links checked.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
