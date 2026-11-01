"""Exercise repository checks against temporary Git repositories, without Unity."""

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


VALIDATOR = Path(__file__).with_name("validate_repository.py")
ARTIFACTS = Path(__file__).resolve().parents[4] / "TestArtifacts~/RepositoryValidationTests"


class RepositoryValidationTests(unittest.TestCase):
    def setUp(self):
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=ARTIFACTS)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.git("init", "--quiet")
        self.write("Packages/manifest.json", '{"dependencies": {}}\n')
        self.write("README.md", "# Example\n\n[Guide](guide.md#usage)\n")
        self.write("guide.md", "# Guide\n\n## Usage\n")
        self.git("add", ".")

    def git(self, *arguments):
        return subprocess.run(
            ["git", "-C", str(self.root), *arguments], check=True,
            capture_output=True, text=True,
        )

    def write(self, relative, content):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")

    def validate(self):
        return subprocess.run(
            [sys.executable, str(VALIDATOR), "--root", str(self.root)],
            capture_output=True, text=True, env=dict(os.environ, PYTHONDONTWRITEBYTECODE="1"),
        )

    def test_allows_tracked_docs_native_plugins_and_affine_fixture(self):
        for path in (
            "Packages/com.eitan.mnn/Runtime/Plugins/macOS/libMNN.dylib",
            "Packages/com.eitan.mnn/Tests/Fixtures/affine.mnn",
        ):
            self.write(path, "small fixture\n")
        self.git("add", ".")
        result = self.validate()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_rejects_local_only_assets_even_when_force_added(self):
        for path in (
            "Assets/Samples/Example/Scene.unity",
            "Assets/Samples.meta",
            "Assets/StreamingAssets/MNN/Models/model.bin",
            "Packages/com.eitan.easymic/package.json",
            "Packages/com.eitan.easymic-v1.0.0/package.json",
            "Packages/com.eitan.easymic.apm-v0.1.4/package.json",
            "MNN/SiriAI/online-models.json",
            "nested/mono_crash.123.json",
            "mono_crash.mem.123.blob",
            "Library/cache.bin",
            "TestArtifacts~/results.xml",
            "model.mnn",
        ):
            with self.subTest(path=path):
                self.write(path, "local-only\n")
                self.git("add", "--force", path)
                result = self.validate()
                self.assertNotEqual(0, result.returncode)
                self.assertIn(path, result.stdout + result.stderr)
                self.git("rm", "--cached", "--force", path)

    def test_rejects_easymic_manifest_dependencies(self):
        for name in ("com.eitan.easymic", "com.eitan.easymic.apm"):
            with self.subTest(name=name):
                self.write("Packages/manifest.json", json.dumps({"dependencies": {name: "file:local"}}))
                result = self.validate()
                self.assertNotEqual(0, result.returncode)
                self.assertIn(name, result.stdout + result.stderr)

    def test_rejects_untracked_link_targets_present_only_on_the_author_machine(self):
        self.write("local.md", "# Not committed\n")
        self.write("guide.md", "# Guide\n\n[Local](local.md)\n")
        result = self.validate()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("local.md", result.stdout + result.stderr)

    def test_handles_reference_links_encoded_paths_and_directory_links(self):
        self.write("notes/with spaces.md", "# Notes\n")
        self.write("guide.md", "# Guide\n\n[Notes][notes]\n\n[notes]: notes/with%20spaces.md\n\n[Directory](notes/)\n")
        self.git("add", ".")
        result = self.validate()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_ignores_example_links_in_fenced_code(self):
        self.write("guide.md", "# Guide\n\n```md\n[Example](not-a-real-file.md)\n```\n")
        result = self.validate()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_rejects_files_that_are_tracked_despite_gitignore(self):
        self.write(".gitignore", "private-data.txt\n")
        self.write("private-data.txt", "local\n")
        self.git("add", ".gitignore")
        self.git("add", "--force", "private-data.txt")
        result = self.validate()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("private-data.txt", result.stdout + result.stderr)

    def test_reports_the_path_of_a_malformed_manifest(self):
        self.write("Packages/manifest.json", "{invalid JSON\n")
        result = self.validate()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Packages/manifest.json", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
