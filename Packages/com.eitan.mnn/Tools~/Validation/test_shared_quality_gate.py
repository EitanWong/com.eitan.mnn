"""Exercise the shared Unity result gate without starting Unity."""

import importlib.util
import html
import json
import re
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[4]
TOOL = ROOT / "Packages/com.eitan.mnn/Tools~/Testing/run_shared_managed_tests.py"
ARTIFACTS = ROOT / "TestArtifacts~/SharedQualityToolTests"


class SharedQualityGateTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location("shared_gate", TOOL)
        self.gate = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.gate)
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        self.directory = tempfile.TemporaryDirectory(dir=ARTIFACTS)
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def xml(self, state="Passed", **counts):
        attributes = dict(total="1", passed="1", failed="0", skipped="0", inconclusive="0", result="Passed")
        attributes.update(counts)
        path = self.root / "results.xml"
        text = " ".join(f'{key}="{value}"' for key, value in attributes.items())
        path.write_text(f'<test-run {text}><test-suite type="Assembly" name="MNN.Unity.Tests.dll" testcasecount="1">'
                        f'<test-suite type="TestFixture" fullname="MNN.Unity.Tests.Fixture">'
                        f'<test-case fullname="MNN.Unity.Tests.Fixture.Behavior" result="{state}"/>'
                        '</test-suite></test-suite></test-run>')
        return path

    def evaluate(self, path, exit_code=0, fixtures=None):
        return self.gate.evaluate_results(path, exit_code, fixtures or ["MNN.Unity.Tests.Fixture"], external_network_cases=())

    def test_complete_passed_results_satisfy_gate(self):
        report = self.evaluate(self.xml())
        self.assertTrue(report["passed"], report)
        self.assertEqual(1, report["cases"])
        self.assertFalse(report["native_inference_verified"])

    def test_case_failure_cannot_be_hidden_by_green_root(self):
        report = self.evaluate(self.xml("Failed"))
        self.assertFalse(report["passed"])
        self.assertIn("MNN.Unity.Tests.Fixture.Behavior", " ".join(report["errors"]))

    def test_skip_and_inconclusive_are_incomplete_regression(self):
        for state in ["Skipped", "Inconclusive", "Ignored", "NotRunnable"]:
            with self.subTest(state=state):
                self.assertFalse(self.evaluate(self.xml(state))["passed"])

    def test_missing_xml_or_malformed_xml_never_passes(self):
        path = self.root / "missing.xml"
        self.assertFalse(self.evaluate(path)["passed"])
        path.write_text("<test-run")
        self.assertFalse(self.evaluate(path)["passed"])

    def test_nonzero_exit_code_cannot_be_hidden_by_passing_xml(self):
        self.assertFalse(self.evaluate(self.xml(), 1)["passed"])

    def test_zero_or_inconsistent_case_counts_fail(self):
        for count in ["0", "2", "invalid"]:
            with self.subTest(count=count):
                self.assertFalse(self.evaluate(self.xml(total=count))["passed"])

    def test_missing_required_fixture_fails(self):
        report = self.evaluate(self.xml(), fixtures=["MNN.Unity.Tests.OtherFixture"])
        self.assertFalse(report["passed"])
        self.assertIn("OtherFixture", " ".join(report["errors"]))

    def test_discovered_assembly_cannot_be_reduced_to_a_filtered_subset(self):
        path = self.xml()
        path.write_text(path.read_text().replace('testcasecount="1"', 'testcasecount="2"'))
        self.assertFalse(self.evaluate(path)["passed"])
        path.write_text(path.read_text().replace(' testcasecount="2"', ''))
        self.assertFalse(self.evaluate(path)["passed"])

    def test_local_explicit_cases_are_required_when_assembly_filter_selects_them(self):
        path = self.xml()
        optional = self.gate.LOCAL_EXPLICIT_CASES[0]
        text = path.read_text().replace('total="1"', 'total="2"').replace('skipped="0"', 'skipped="1"').replace('testcasecount="1"', 'testcasecount="2"')
        leaf = f'<test-case fullname="{optional}" result="Skipped" label="Explicit" runstate="Explicit"/>'
        path.write_text(text.replace('</test-suite></test-suite></test-run>', leaf + '</test-suite></test-suite></test-run>'))
        report = self.evaluate(path)
        self.assertFalse(report["passed"], report)

    def test_new_explicit_skip_requires_contract_review(self):
        path = self.xml("Skipped")
        path.write_text(path.read_text().replace('result="Skipped"', 'result="Skipped" label="Explicit" runstate="Explicit"'))
        self.assertFalse(self.evaluate(path)["passed"])

    def test_optional_imported_sample_absence_requires_exact_case_and_reason(self):
        path = self.xml()
        optional = html.escape(self.gate.OPTIONAL_IMPORTED_SAMPLE_CASES[0], quote=True)
        text = path.read_text().replace('total="1"', 'total="2"').replace('skipped="0"', 'skipped="1"').replace('testcasecount="1"', 'testcasecount="2"')
        leaf = (f'<test-case fullname="{optional}" result="Skipped" label="Ignored">'
                '<reason><message>This sample has not been imported into this project.</message></reason></test-case>')
        path.write_text(text.replace('</test-suite></test-suite></test-run>', leaf + '</test-suite></test-suite></test-run>'))
        self.assertTrue(self.evaluate(path)["passed"])
        path.write_text(path.read_text().replace('This sample has not been imported into this project.', 'An unexpected failure.'))
        self.assertFalse(self.evaluate(path)["passed"])

    def test_duplicate_case_identity_or_foreign_assembly_fails(self):
        path = self.xml()
        text = path.read_text()
        marker = '<test-case fullname="MNN.Unity.Tests.Fixture.Behavior" result="Passed"/>'
        path.write_text(text.replace(marker, marker * 2).replace('total="1"', 'total="2"').replace('passed="1"', 'passed="2"'))
        self.assertFalse(self.evaluate(path)["passed"])
        path.write_text(text.replace("MNN.Unity.Tests.dll", "MNN.Samples.SiriAI.Tests.dll"))
        self.assertFalse(self.evaluate(path)["passed"])

    def test_command_scope_is_fixed_and_only_network_scope_is_excluded(self):
        command = self.gate.unity_command("Unity", self.root, self.root / "results.xml", self.root / "Unity.log", False)
        self.assertNotIn("-testCategory", command)
        expression = command[command.index("-testFilter") + 1]
        for name in self.gate.NETWORK_CASES:
            self.assertIsNone(re.match(expression, name))
        for name in self.gate.LOCAL_EXPLICIT_CASES:
            self.assertIsNotNone(re.match(expression, name))
        self.assertIsNotNone(re.match(expression, "MNN.Unity.Tests.NewFixture.NewBehavior"))
        self.assertEqual("MNN.Unity.Tests", command[command.index("-assemblyNames") + 1])
        self.assertEqual("EditMode", command[command.index("-testPlatform") + 1])
        network = self.gate.unity_command("Unity", self.root, self.root / "results.xml", self.root / "Unity.log", False, scope="network")
        self.assertEqual("RequiresNetwork", network[network.index("-testCategory") + 1])

    def test_source_scope_audits_all_existing_explicit_methods_and_exact_network_set(self):
        report = self.gate.audit_suite_sources(ROOT / "Packages/com.eitan.mnn")
        self.assertTrue(report["passed"], report)
        self.assertEqual(22, len(report["shared_explicit_cases"]))
        self.assertEqual(7, len(report["network_cases"]))
        self.assertGreater(len(report["all_explicit_declarations"]), 22)

    def test_new_network_classification_or_explicit_test_is_not_automatically_exempted(self):
        package = self.root / "Package"
        tests = package / "Tests/Editor"
        tests.mkdir(parents=True)
        (tests / "Shared.asmdef").write_text('{"name":"MNN.Unity.Tests"}')
        (tests / "NewTest.cs").write_text('namespace MNN.Unity.Tests {public class NewTest {'
                                           '[Test,Category("RequiresNetwork"),Explicit("New external dependency")]'
                                           'public void NewNetwork(){}}}')
        report = self.gate.audit_suite_sources(package)
        self.assertFalse(report["passed"])
        self.assertIn("reviewed seven-case", " ".join(report["errors"]))

    def offline_xml(self):
        path = self.xml()
        leaves = ''.join(f'<test-case fullname="{name}" result="Skipped" label="Explicit" runstate="Explicit"/>'
                         for name in self.gate.NETWORK_CASES)
        text = path.read_text().replace('total="1"', 'total="8"').replace('skipped="0"', 'skipped="7"')
        text = text.replace('testcasecount="1"', 'testcasecount="8"').replace('result="Passed"><test-suite', 'result="Skipped:Explicit"><test-suite', 1)
        path.write_text(text.replace('</test-suite></test-suite></test-run>', leaves + '</test-suite></test-suite></test-run>'))
        return path

    def offline_report(self, path):
        return self.gate.evaluate_results(path, 0, ["MNN.Unity.Tests.Fixture"])

    def test_offline_xml_retains_exactly_seven_unselected_network_leaves(self):
        report = self.offline_report(self.offline_xml())
        self.assertTrue(report["passed"], report)
        self.assertEqual(8, report["cases"])
        self.assertEqual(8, report["discovered_cases"])
        self.assertEqual(set(self.gate.NETWORK_CASES), set(report["not_selected_network_cases"]))
        self.assertEqual([], report["unresolved_cases"])

    def test_network_nonselection_requires_exact_explicit_result_label_and_runstate(self):
        for replacement in ('result="Passed"', 'result="Failed"', 'result="Skipped" label="Ignored" runstate="Explicit"',
                            'result="Skipped" label="Explicit" runstate="Runnable"', 'result="Skipped" label="Explicit"'):
            with self.subTest(replacement=replacement):
                path = self.offline_xml()
                path.write_text(path.read_text().replace('result="Skipped" label="Explicit" runstate="Explicit"', replacement, 1))
                self.assertFalse(self.offline_report(path)["passed"])

    def test_missing_network_leaf_cannot_be_hidden_by_matching_total_and_discovery(self):
        path = self.offline_xml()
        leaf = f'<test-case fullname="{self.gate.NETWORK_CASES[0]}" result="Skipped" label="Explicit" runstate="Explicit"/>'
        text = path.read_text().replace(leaf, '').replace('total="8"', 'total="7"')
        path.write_text(text.replace('testcasecount="8"', 'testcasecount="7"').replace('skipped="7"', 'skipped="6"'))
        report = self.offline_report(path)
        self.assertFalse(report["passed"], report)
        self.assertIn(self.gate.NETWORK_CASES[0], ' '.join(report["errors"]))

    def test_local_resource_or_allocation_skips_remain_unresolved(self):
        path = self.offline_xml()
        text = path.read_text().replace('result="Passed"/>', 'result="Skipped" label="Ignored" runstate="Runnable"/>')
        path.write_text(text.replace('passed="1"', 'passed="0"').replace('skipped="7"', 'skipped="8"'))
        report = self.offline_report(path)
        self.assertFalse(report["passed"], report)
        self.assertEqual(7, len(report["not_selected_network_cases"]))
        self.assertEqual(["MNN.Unity.Tests.Fixture.Behavior"], [case["name"] for case in report["unresolved_cases"]])

    def test_network_scope_requires_exactly_every_live_test(self):
        path = self.xml()
        report = self.gate.evaluate_results(path, 0, scope="network")
        self.assertFalse(report["passed"])

    def test_network_download_destinations_do_not_reuse_installed_model_inputs(self):
        environment = self.gate.network_download_environment(self.root / "UnityProject")
        self.assertEqual(4, len(environment))
        for value in environment.values():
            self.assertTrue(Path(value).is_relative_to(self.root / "UnityProject/TestArtifacts~/NetworkDownloads"))

    def test_existing_model_link_can_be_reused_but_not_retargeted_silently(self):
        models = self.root / "Models"
        models.mkdir()
        project = self.root / "UnityProject"
        for _ in range(2):
            self.gate.link_models(project, models)
        self.assertEqual(models, (project / "Assets/StreamingAssets/MNN/Models").resolve())
        other = self.root / "OtherModels"
        other.mkdir()
        with self.assertRaises(ValueError):
            self.gate.link_models(project, other)

    def test_reference_environment_cannot_override_process_or_cloud_credentials(self):
        path = self.root / "references.json"
        references = {"MNN_TEST_SILERO_ROOT": "/existing/reference", "MNN_TEST_KEYWORD_ROOT": "/existing/keywords",
                      "MNN_TEST_TOKENIZER_ROOT": "/existing/tokenizer"}
        path.write_text(json.dumps(references))
        self.assertEqual(references, self.gate.read_reference_environment(path))
        for field in ["LUNFLUX_API_KEY", "PATH", "MNN_TEST_API_KEY", "MNN_TEST_PASSWORD"]:
            path.write_text(json.dumps({field: "not-an-allowed-reference"}))
            with self.assertRaises(ValueError):
                self.gate.read_reference_environment(path)


if __name__ == "__main__":
    unittest.main()
