"""A host-Mono allocation result must never stand in for Unity measurements."""

import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[4]
TOOL = ROOT / "Packages/com.eitan.mnn/Tools~/Testing/run_mono_allocation_tests.py"
ARTIFACTS = ROOT / "TestArtifacts~/MonoAllocationGateTests"


class MonoAllocationGateTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location("allocation_gate", TOOL)
        self.gate = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.gate)
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(dir=ARTIFACTS)
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def xml(self, state="Passed", names=None, assembly="MNN.Unity.Tests.dll"):
        names = self.gate.CASES if names is None else names
        cases = "".join(f'<test-case fullname="{name}" result="{state}"/>' for name in names)
        path = self.root / "result.xml"
        path.write_text(f'<test-suite type="Assembly" name="{assembly}" total="{len(names)}" '
                        f'passed="{len(names)}" failed="0" skipped="0" inconclusive="0" result="Passed">{cases}</test-suite>')
        return path

    def test_exact_original_eight_cases_pass_only_for_host_mono(self):
        report = self.gate.evaluate_results(self.xml(), 0)
        self.assertTrue(report["passed"], report)
        self.assertTrue(report["host_mono_allocation_verified"])
        self.assertFalse(report["unity_allocation_verified"])
        self.assertFalse(report["native_inference_verified"])

    def test_graph_allocation_suite_requires_both_real_integration_cases(self):
        names = ("MNN.Unity.Tests.AdditionalTtsUnitTests.GraphSpecializationDoesNotDuplicateTheEntireSourceBeforeWritingItsResult",
                 "MNN.Unity.Tests.AdditionalTtsUnitTests.MultipleGraphInputsShareOneResultBuffer")
        path = self.xml(names=names, assembly="MNN.Unity.Integration.Tests.dll")
        report = self.gate.evaluate_results(path, 0, suite="graph-specialization")
        self.assertTrue(report["passed"], report)
        self.assertFalse(report["unity_allocation_verified"])
        self.assertFalse(report["native_inference_verified"])
        for selected in (names[:1], names[:1] * 2):
            path = self.xml(names=selected, assembly="MNN.Unity.Integration.Tests.dll")
            self.assertFalse(self.gate.evaluate_results(path, 0, suite="graph-specialization")["passed"])
        path = self.xml("Skipped", names=names, assembly="MNN.Unity.Integration.Tests.dll")
        self.assertFalse(self.gate.evaluate_results(path, 0, suite="graph-specialization")["passed"])
        self.assertFalse(self.gate.evaluate_results(self.xml(), 0, suite="graph-specialization")["passed"])

    def test_a_skip_or_failure_cannot_hide_behind_green_counts(self):
        for state in ("Skipped", "Failed", "Inconclusive"):
            self.assertFalse(self.gate.evaluate_results(self.xml(state), 0)["passed"])

    def test_missing_duplicate_or_different_cases_fail(self):
        for names in (self.gate.CASES[:-1], self.gate.CASES[:-1] + (self.gate.CASES[0],),
                      self.gate.CASES[:-1] + ("MNN.Unity.Tests.OtherTest",)):
            self.assertFalse(self.gate.evaluate_results(self.xml(names=names), 0)["passed"])

    def test_nonzero_process_exit_or_invalid_xml_fails(self):
        self.assertFalse(self.gate.evaluate_results(self.xml(), 1)["passed"])
        path = self.root / "bad.xml"
        path.write_text("invalid")
        self.assertFalse(self.gate.evaluate_results(path, 0)["passed"])

    def test_reference_resolution_uses_the_selected_project_not_an_unrelated_cache(self):
        cache = self.root / "Library/PackageCache/com.unity.ext.nunit@fixture/net35/unity-custom"
        cache.mkdir(parents=True)
        (cache / "nunit.framework.dll").touch()
        assemblies = self.root / "Library/ScriptAssemblies"
        assemblies.mkdir(parents=True)
        (assemblies / "UnityEngine.TestRunner.dll").touch()
        nunit, runner = self.gate.resolve_test_references(self.root)
        self.assertEqual(cache / "nunit.framework.dll", nunit)
        self.assertEqual(assemblies / "UnityEngine.TestRunner.dll", runner)

    def test_incomplete_or_empty_compilation_summary_is_not_a_compiler_pass(self):
        self.assertFalse(self.gate.complete_compilation({"assemblies": []}))
        self.assertFalse(self.gate.complete_compilation({"expected_assemblies": 2, "assemblies": [{"passed": True}]}))
        self.assertTrue(self.gate.complete_compilation({"expected_assemblies": 1, "assemblies": [{"passed": True}]}))


if __name__ == "__main__":
    unittest.main()
