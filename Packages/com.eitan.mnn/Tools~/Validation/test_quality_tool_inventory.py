"""Tool presence must not be reported as measured software quality."""

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[4]
TOOL = ROOT / "Packages/com.eitan.mnn/Tools~/Testing/probe_quality_tools.py"


class QualityToolInventoryTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location("quality_inventory", TOOL)
        self.tool = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.tool)

    def test_missing_requested_tools_block_without_measured_claim(self):
        report = self.tool.inventory(ROOT, ["crapper", "dryer", "mutator"], lambda name: None)
        self.assertFalse(report["ready"])
        self.assertEqual(["crapper", "dryer", "mutator"], report["missing_required"])
        self.assertFalse(report["quality_metrics_verified"])

    def test_presence_is_only_presence_and_never_execution(self):
        report = self.tool.inventory(ROOT, ["crapper"], lambda name: "/synthetic/tools/" + name)
        self.assertTrue(report["ready"])
        self.assertTrue(report["tools"]["crapper"]["available"])
        self.assertFalse(report["tools"]["crapper"]["executed"])
        self.assertFalse(report["quality_metrics_verified"])

    def test_workflow_requires_graphics_and_quality_checks_cannot_gate_test_execution(self):
        workflow = (ROOT / ".github/workflows/shared-managed.yml").read_text()
        shared, quality = workflow.split("  quality_prerequisites:\n")
        self.assertIn('--graphics', shared)
        self.assertNotIn('needs:', shared)
        self.assertNotIn('continue-on-error:', quality)
        self.assertIn('needs: shared', quality)
        self.assertIn('if: always() &&', quality)
        self.assertIn('--require crapper dryer mutator unity-coverage', quality)
        self.assertIn('if: always()', quality)


if __name__ == "__main__":
    unittest.main()
