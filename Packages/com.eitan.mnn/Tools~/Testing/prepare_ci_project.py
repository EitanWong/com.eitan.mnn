#!/usr/bin/env python3
"""Prepare a minimal isolated Unity test project; never download model assets."""

import argparse
import json
from pathlib import Path
import shutil


def main():
    root = Path(__file__).resolve().parents[4]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=root / "TestArtifacts~/CI/UnityProject")
    project = parser.parse_args().project.resolve()
    artifacts = root / "TestArtifacts~"
    if not project.is_relative_to(artifacts) or project == artifacts:
        parser.error("The isolated project must be inside this repository's TestArtifacts~/.")
    if (project / "Temp/UnityLockfile").exists():
        parser.error("Close Unity before refreshing this isolated project.")
    marker = project / ".mnn-ci-project"
    if project.exists() and any(project.iterdir()) and not marker.is_file():
        parser.error("Refusing to overwrite a directory not created by this tool.")
    project.mkdir(parents=True, exist_ok=True)
    marker.touch()
    package = project / "Packages/com.eitan.mnn"
    if package.is_symlink():
        parser.error("The isolated package must not be a symlink.")
    if package.exists():
        shutil.rmtree(package)
    shutil.copytree(root / "Packages/com.eitan.mnn", package)
    manifest = {
        "dependencies": {"com.unity.test-framework": "1.1.33"},
        "testables": ["com.eitan.mnn"],
    }
    (project / "Packages/manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    (project / "ProjectSettings").mkdir(exist_ok=True)
    shutil.copyfile(root / "ProjectSettings/ProjectVersion.txt", project / "ProjectSettings/ProjectVersion.txt")
    (project / "Assets").mkdir(exist_ok=True)
    print("Prepared isolated Unity project: " + str(project))


if __name__ == "__main__":
    main()
