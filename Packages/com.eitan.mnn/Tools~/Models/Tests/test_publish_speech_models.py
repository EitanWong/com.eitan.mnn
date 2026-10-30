"""Publication gates are packaging checks, not independent inference validation."""
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import sys

sys.dont_write_bytecode = True

TOOL = Path(__file__).resolve().parents[1] / 'publish_speech_models.py'
spec = importlib.util.spec_from_file_location('publisher', TOOL)
publisher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publisher)
ARTIFACTS = TOOL.parents[3].parent / 'TestArtifacts~/SpeechModels/PublisherTests'


class PublicationAuditTests(unittest.TestCase):
    def setUp(self):
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=ARTIFACTS)
        self.addCleanup(self.temporary.cleanup)
        self.model = Path(self.temporary.name) / 'Example-MNN'
        self.model.mkdir()
        (self.model / 'model.mnn').write_bytes(b'fixture-only graph')
        for name in ('README.md', 'LICENSE'):
            (self.model / name).write_text('test fixture')
        graph_hash = hashlib.sha256((self.model / 'model.mnn').read_bytes()).hexdigest()
        self.optimization = {'player_validation': {'platform': 'test-only', 'unity': 'test-only', 'build_id': 'a'*32,
            'result_xml_sha256': 'b'*64, 'graphs': [{'path': 'model.mnn', 'sha256': graph_hash}]}}
        self.refresh()

    def refresh(self):
        (self.model / 'mnn_optimization.json').write_text(json.dumps(self.optimization))
        files = [{'path': p.name, 'bytes': p.stat().st_size, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
                 for p in self.model.iterdir() if p.is_file() and p.name != 'manifest.json' and p.suffix != '.meta']
        self.manifest = {'name': self.model.name, 'format': 'mnn', 'preferred_graph_cpu': 'model.mnn', 'files': files}
        self.save_manifest()

    def save_manifest(self):
        (self.model / 'manifest.json').write_text(json.dumps(self.manifest))

    def errors(self):
        return '\n'.join(publisher.audit_model(self.model))

    def test_complete_package_passes_and_unity_metadata_is_ignored(self):
        (self.model / 'model.mnn.meta').write_text('Unity metadata')
        self.assertEqual('', self.errors())

    def test_modified_graph_is_rejected(self):
        (self.model / 'model.mnn').write_bytes(b'changed')
        self.assertIn('hash', self.errors())

    def test_unlisted_file_is_rejected(self):
        (self.model / 'diagnostic.log').write_text('unexpected')
        self.assertIn('unlisted', self.errors())

    def test_source_weights_are_rejected_even_if_listed(self):
        (self.model / 'original.onnx').write_bytes(b'original')
        self.refresh()
        self.assertIn('non-deployment', self.errors())

    def test_symlink_is_rejected_even_if_listed(self):
        (self.model / 'linked.mnn').symlink_to('model.mnn')
        self.refresh()
        self.assertIn('symlink', self.errors())

    def test_manifest_path_traversal_is_rejected(self):
        self.manifest['files'][0]['path'] = '../outside'
        self.save_manifest()
        self.assertIn('unsafe', self.errors())

    def test_manifest_cannot_read_through_a_directory_symlink(self):
        outside = self.model.parent / 'outside'
        outside.mkdir()
        (outside / 'graph.mnn').write_bytes(b'outside')
        (self.model / 'linked').symlink_to(outside, target_is_directory=True)
        self.manifest['files'].append({'path': 'linked/graph.mnn', 'bytes': 7, 'sha256': 'c'*64})
        self.save_manifest()
        self.assertIn('unsafe', self.errors())

    def test_missing_preferred_graph_is_rejected(self):
        self.manifest['preferred_graph_gpu'] = 'removed.mnn'
        self.save_manifest()
        self.assertIn('preferred_graph_gpu', self.errors())

    def test_missing_player_evidence_is_rejected(self):
        self.optimization = {}
        self.refresh()
        self.assertIn('Player', self.errors())

    def test_player_graph_hash_must_match_current_deployment(self):
        self.optimization['player_validation']['graphs'][0]['sha256'] = 'c'*64
        self.refresh()
        self.assertIn('Player graph hash', self.errors())

    def test_every_deployed_graph_needs_player_evidence(self):
        (self.model / 'extra.mnn').write_bytes(b'extra fixture')
        self.refresh()
        self.assertIn('Player graph coverage', self.errors())

    def test_duplicate_manifest_entries_are_rejected(self):
        self.manifest['files'].append(self.manifest['files'][0])
        self.save_manifest()
        self.assertIn('duplicate', self.errors())

    def test_missing_file_is_reported(self):
        (self.model / 'model.mnn').unlink()
        self.assertIn('missing', self.errors())


if __name__ == '__main__':
    unittest.main()
