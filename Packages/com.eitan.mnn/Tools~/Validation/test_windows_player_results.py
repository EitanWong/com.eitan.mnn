#!/usr/bin/env python3
"""Player-side evidence must belong to this build; failures remain observable."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[4]
RUNNER = Path(__file__).resolve().parents[1] / "Testing/run_platform_tests.py"
spec = importlib.util.spec_from_file_location("platform_runner", RUNNER)
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class WindowsPlayerResultsTests(unittest.TestCase):
    def setUp(self):
        directory = ROOT / "TestArtifacts~/WindowsPlayerResultChecks"
        directory.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(dir=directory)
        self.path = Path(self.directory.name) / "results.xml"
        self.addCleanup(self.directory.cleanup)

    def test_accepts_only_the_current_build(self):
        data = b'<test-suite mnn-build-id="current" failed="0" />'
        self.path.write_bytes(data)
        self.assertEqual(data, runner.read_player_result(self.path, "current"))
        self.assertIsNone(runner.read_player_result(self.path, "previous"))

    def test_keeps_failed_test_results_for_reporting(self):
        data = b'<test-suite mnn-build-id="current" failed="1" />'
        self.path.write_bytes(data)
        self.assertEqual(data, runner.read_player_result(self.path, "current"))

    def test_rejects_missing_or_incomplete_results(self):
        self.assertIsNone(runner.read_player_result(self.path, "current"))
        self.path.write_bytes(b'<test-suite mnn-build-id="current"')
        self.assertIsNone(runner.read_player_result(self.path, "current"))

    def test_rejects_unidentified_or_unexpected_xml(self):
        for data in (b'<test-suite/>', b'<unrelated mnn-build-id="current"/>'):
            self.path.write_bytes(data)
            self.assertIsNone(runner.read_player_result(self.path, "current"))


if __name__ == "__main__":
    unittest.main()
