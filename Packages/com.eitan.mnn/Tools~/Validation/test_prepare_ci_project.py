"""Check isolated Unity CI project preparation without starting Unity."""

import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[4]
PREPARE = ROOT / "Packages/com.eitan.mnn/Tools~/Testing/prepare_ci_project.py"
ARTIFACTS = ROOT / "TestArtifacts~/CiPreparationTests"


class CiProjectPreparationTests(unittest.TestCase):
    def test_prepares_reusable_project_with_only_package_and_test_dependencies(self):
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=ARTIFACTS) as directory:
            project = Path(directory) / "UnityProject"
            for _ in range(2):
                result = subprocess.run(
                    [sys.executable, str(PREPARE), "--project", str(project)],
                    capture_output=True, text=True,
                )
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            manifest = json.loads((project / "Packages/manifest.json").read_text())
            self.assertEqual({"com.unity.test-framework"}, set(manifest["dependencies"]))
            self.assertEqual(["com.eitan.mnn"], manifest["testables"])
            self.assertTrue((project / "Packages/com.eitan.mnn/package.json").is_file())
            self.assertTrue((project / "ProjectSettings/ProjectVersion.txt").is_file())

    def test_rejects_output_outside_test_artifacts(self):
        result = subprocess.run(
            [sys.executable, str(PREPARE), "--project", str(ROOT / "Assets/UnsafeCiOutput")],
            capture_output=True, text=True,
        )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("TestArtifacts~", result.stdout + result.stderr)
        self.assertFalse((ROOT / "Assets/UnsafeCiOutput").exists())


if __name__ == "__main__":
    unittest.main()
