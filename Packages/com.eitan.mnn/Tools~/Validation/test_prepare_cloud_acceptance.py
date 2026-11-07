"""Prepare the real C# acceptance fixture without contacting a cloud service."""

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[4]
TOOL = ROOT / "Packages/com.eitan.mnn/Tools~/Testing/run_cloud_acceptance.py"
ARTIFACTS = ROOT / "TestArtifacts~/CloudAcceptancePreparationTests"


class CloudAcceptancePreparationTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location("cloud_runner", TOOL)
        self.tool = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.tool)
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(dir=ARTIFACTS)
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def test_preparation_injects_only_shared_provider_fixture_and_nonsecret_plan(self):
        plan = {"schema_version": 1, "scenarios": [{"id": "chat", "audio": "reference.wav"}]}
        project = self.root / "UnityProject"
        project.mkdir()
        prepared = self.tool.inject_fixture(project, plan, self.root, self.root)
        source = project / "Assets/CloudAcceptance/Editor/MNNCloudAcceptanceTests.cs"
        self.assertTrue(source.is_file())
        text = source.read_text()
        self.assertIn("MNNOpenAIConversationProvider", text)
        self.assertIn("MNNCloudSpeechProvider", text)
        self.assertNotIn("SiriAI", text)
        self.assertNotIn("HttpClient", text)
        document = json.loads(prepared.read_text())
        self.assertEqual(str(self.root / "reference.wav"), document["scenarios"][0]["audio"])
        self.assertEqual(plan["scenarios"][0]["audio"], "reference.wav", "Preparation must not mutate the caller's plan.")

    def test_zero_or_partial_cloud_result_cannot_pass(self):
        path = self.root / "results.xml"
        path.write_text('<test-run total="0" failed="0" passed="0" result="Passed"/>')
        self.assertFalse(self.tool.evaluate_results(path, 0, 3)["passed"])

    def test_cloud_results_must_belong_to_the_real_fixture_and_all_pass(self):
        path = self.root / "results.xml"
        document = ('<test-run total="1" failed="0" passed="1" skipped="0" inconclusive="0" result="Passed">'
                    '<test-suite type="Assembly" name="MNN.Unity.CloudAcceptance.Tests.dll">'
                    '<test-case fullname="MNN.Unity.CloudAcceptance.MNNCloudAcceptanceTests.Cloud_chat" name="Cloud_chat" result="Passed"/>'
                    '</test-suite></test-run>')
        path.write_text(document)
        self.assertTrue(self.tool.evaluate_results(path, 0, 1)["passed"])
        for changed in [document.replace(".CloudAcceptance.Tests.dll", ".Unrelated.Tests.dll"),
                        document.replace('result="Passed"/>', 'result="Skipped"/>')]:
            path.write_text(changed)
            self.assertFalse(self.tool.evaluate_results(path, 0, 1)["passed"])


if __name__ == "__main__":
    unittest.main()
