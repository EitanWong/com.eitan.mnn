"""Pin cloud acceptance prerequisites and secret-free diagnostics; no network."""

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import wave

ROOT = Path(__file__).resolve().parents[4]
TOOL = ROOT / "Packages/com.eitan.mnn/Tools~/Testing/preflight_cloud_acceptance.py"
ARTIFACTS = ROOT / "TestArtifacts~/CloudPreflightToolTests"


class CloudAcceptancePreflightTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location("cloud_preflight", TOOL)
        self.tool = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.tool)
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(dir=ARTIFACTS)
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def scenario(self, **changes):
        item = {"id": "chat", "task": "chat", "protocol": "OpenAIChat", "endpoint": "https://api.example.com/v1",
                "model": "fixture-model", "credential_env": "MNN_TEST_SERVICE_KEY", "timeout_seconds": 30,
                "maximum_latency_seconds": 20, "prompt": "Reply with FOUR", "expected_text": "FOUR"}
        item.update(changes)
        return item

    def check(self, scenarios=None, environment=None):
        return self.tool.preflight({"schema_version": 1, "scenarios": scenarios or [self.scenario()]},
                                   self.root, {} if environment is None else environment)

    def test_missing_and_empty_credentials_block_without_request(self):
        for environment in [{}, {"MNN_TEST_SERVICE_KEY": ""}, {"MNN_TEST_SERVICE_KEY": "  "}]:
            report = self.check(environment=environment)
            self.assertFalse(report["ready"])
            self.assertEqual(0, report["network_requests"])
            self.assertFalse(report["acceptance_verified"])

    def test_present_credential_is_never_serialized_and_is_only_a_preflight(self):
        secret = "never-print-this-synthetic-value"
        report = self.check(environment={"MNN_TEST_SERVICE_KEY": secret})
        self.assertTrue(report["ready"], report)
        self.assertNotIn(secret, json.dumps(report))
        self.assertFalse(report["acceptance_verified"])

    def test_endpoint_credentials_query_and_remote_plain_http_are_rejected(self):
        for endpoint in ["https://user:secret@example.com/v1", "https://example.com?key=secret",
                         "https://example.com/#secret", "http://example.com/v1"]:
            with self.subTest(endpoint=endpoint):
                report = self.check([self.scenario(endpoint=endpoint)], {"MNN_TEST_SERVICE_KEY": "key"})
                self.assertFalse(report["ready"])
                self.assertNotIn("secret", json.dumps(report))

    def test_protocol_task_mismatch_or_missing_meaningful_assertion_blocks(self):
        for changes in [{"protocol": "MiniMaxSpeech"}, {"expected_text": ""}, {"maximum_latency_seconds": 31}]:
            self.assertFalse(self.check([self.scenario(**changes)], {"MNN_TEST_SERVICE_KEY": "key"})["ready"])

    def test_inline_secret_or_unknown_configuration_field_is_rejected_without_echo(self):
        report = self.check([self.scenario(api_key="synthetic-secret")])
        self.assertFalse(report["ready"])
        self.assertNotIn("synthetic-secret", json.dumps(report))

    def test_recognition_requires_existing_bounded_pcm_reference(self):
        item = self.scenario(task="recognition", protocol="DashScopeRecognition", audio="reference.wav")
        self.assertFalse(self.check([item], {"MNN_TEST_SERVICE_KEY": "key"})["ready"])
        with wave.open(str(self.root / "reference.wav"), "wb") as audio:
            audio.setnchannels(1)
            audio.setsampwidth(2)
            audio.setframerate(16000)
            audio.writeframes(b"\0\0" * 16000)
        report = self.check([item], {"MNN_TEST_SERVICE_KEY": "key"})
        self.assertTrue(report["ready"], report)
        self.assertEqual(16000, report["scenarios"][0]["audio"]["frames"])

    def test_synthesis_requires_voice_and_input_text(self):
        item = self.scenario(task="synthesis", protocol="MiniMaxSpeech", text="Acceptance fixture", voice="test-voice")
        self.assertTrue(self.check([item], {"MNN_TEST_SERVICE_KEY": "key"})["ready"])
        item["voice"] = ""
        self.assertFalse(self.check([item], {"MNN_TEST_SERVICE_KEY": "key"})["ready"])

    def test_duplicate_ids_and_invalid_schema_are_blocked(self):
        self.assertFalse(self.check([self.scenario(), self.scenario()])["ready"])
        self.assertFalse(self.tool.preflight({"schema_version": 9, "scenarios": []}, self.root, {})["ready"])

    def test_unadvertised_models_and_placeholder_models_block(self):
        plan = {"schema_version": 1, "scenarios": [self.scenario()]}
        report = self.tool.preflight(plan, self.root, {"MNN_TEST_SERVICE_KEY": "key"}, {"other-model"})
        self.assertFalse(report["ready"])
        self.assertFalse(report["acceptance_verified"])
        self.assertFalse(self.check([self.scenario(model="REPLACE_WITH_DEPLOYED_MODEL")],
                                    {"MNN_TEST_SERVICE_KEY": "key"})["ready"])

    def test_invalid_json_field_types_never_raise_or_echo_values(self):
        for changes in [{"protocol": []}, {"task": {}}, {"endpoint": 8}, {"model": {}}, {"voice": []}]:
            report = self.check([self.scenario(**changes)])
            self.assertFalse(report["ready"])


if __name__ == "__main__":
    unittest.main()
