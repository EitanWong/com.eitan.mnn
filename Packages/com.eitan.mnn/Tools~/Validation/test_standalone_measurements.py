import csv
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "Testing/summarize_standalone_acceptance.py"
spec = importlib.util.spec_from_file_location("standalone_measurements", SCRIPT)
measurements = importlib.util.module_from_spec(spec)
spec.loader.exec_module(measurements)


class StandaloneMeasurementsTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.summary = {"status": "passed", "sessions": 2, "turns": 3, "elapsedSeconds": 200,
                        "requestedSeconds": 190, "rssGrowthBudgetMiB": 0, "managedGrowthBudgetMiB": 32}
        self.rows = [self.row(1, 1, 10), self.row(1, 2, 20), self.row(2, 3, 30)]

    @staticmethod
    def row(session, turn, token):
        return {"session": session, "turn": turn, "startup_ms": 100 * session,
                "first_token_ms": token, "first_pcm_ms": token + 40,
                "first_nonzero_render_ms": token + 45, "presentation_ms": 50,
                "total_ms": token + 100, "render_hop_p95_ms": .01,
                "render_hop_p99_ms": .02, "rss_bytes": 1024 * turn,
                "managed_bytes": 128 * turn, "unity_tracked_bytes": 512 * turn, "reply": "private text"}

    def write(self):
        (self.directory / "summary.json").write_text(json.dumps(self.summary))
        with (self.directory / "turns.csv").open("w", newline="") as stream:
            writer = csv.DictWriter(stream, fieldnames=self.rows[0].keys())
            writer.writeheader()
            writer.writerows(self.rows)

    def test_reports_session_startups_once_and_keeps_inference_and_render_scope_distinct(self):
        self.write()
        result = measurements.summarize(self.directory)
        self.assertEqual(2, result["latency_ms"]["startup"]["samples"])
        self.assertEqual(3, result["latency_ms"]["first_token"]["samples"])
        self.assertEqual(20, result["latency_ms"]["first_token"]["p50"])
        self.assertEqual(30, result["latency_ms"]["first_token"]["p95"])
        self.assertEqual(.02, result["simulated_render_ms"]["maximum_session_p99"])
        self.assertFalse(result["performance_targets_verified"])
        self.assertNotIn("private text", json.dumps(result))

    def test_refuses_unfinished_runs(self):
        self.summary["status"] = "running"
        self.write()
        with self.assertRaisesRegex(ValueError, "completed"):
            measurements.summarize(self.directory)

    def test_rejects_missing_turn_even_when_row_count_matches(self):
        self.rows[1]["turn"] = 3
        self.write()
        with self.assertRaisesRegex(ValueError, "turn"):
            measurements.summarize(self.directory)

    def test_rejects_nonfinite_measurement(self):
        self.rows[1]["first_pcm_ms"] = "NaN"
        self.write()
        with self.assertRaisesRegex(ValueError, "finite"):
            measurements.summarize(self.directory)

    def test_rejects_source_counter_mismatch(self):
        self.summary["turns"] = 4
        self.write()
        with self.assertRaisesRegex(ValueError, "counter"):
            measurements.summarize(self.directory)

    def test_rejects_inconsistent_startup_within_session(self):
        self.rows[1]["startup_ms"] = 999
        self.write()
        with self.assertRaisesRegex(ValueError, "startup"):
            measurements.summarize(self.directory)


if __name__ == "__main__":
    unittest.main()
