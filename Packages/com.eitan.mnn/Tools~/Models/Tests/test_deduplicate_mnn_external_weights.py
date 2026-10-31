"""MNN external storage compaction must preserve every logical weight record."""
import importlib.util
import base64
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.dont_write_bytecode = True
TOOL = Path(__file__).resolve().parents[1] / 'deduplicate_mnn_external_weights.py'
spec = importlib.util.spec_from_file_location('deduplicate', TOOL)
deduplicate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deduplicate)


class ExternalWeightCompactionTests(unittest.TestCase):
    def test_multiple_official_graphs_share_one_exact_weight_file(self):
        fixture = json.loads((Path(__file__).parent / 'Fixtures/external_scale.json').read_text())
        artifacts = TOOL.parents[4] / 'TestArtifacts~/SpeechModels/ToolTests'
        artifacts.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=artifacts) as temporary:
            root = Path(temporary)
            sources = [root / 'cpu.mnn', root / 'gpu.mnn']
            original = base64.b64decode(fixture['weights'])
            graph = base64.b64decode(fixture['graph'])
            for source in sources:
                source.write_bytes(graph)
                Path(str(source) + '.weight').write_bytes(original)
            target = root / 'shared'
            report = deduplicate.compact_models(sources, target, 'cpu.mnn.weight')
            shared = (target / 'cpu.mnn.weight').read_bytes()
            self.assertEqual(report['source_weight_bytes'], len(original) * 2)
            self.assertLess(len(shared), len(original))
            self.assertEqual(len(report['graphs']), 2)
            for source in sources:
                self.assertEqual(source.read_bytes(), graph)
                self.assertEqual(Path(str(source) + '.weight').read_bytes(), original)
                before = deduplicate.Model(graph).external_records()
                after = deduplicate.Model((target / source.name).read_bytes()).external_records()
                for (old, old_sizes, _), (new, new_sizes, _) in zip(before, after):
                    self.assertEqual(old_sizes, new_sizes)
                    self.assertEqual(original[old:old + sum(old_sizes)], shared[new:new + sum(new_sizes)])
            again = root / 'again'
            self.assertEqual(report, deduplicate.compact_models(sources, again, 'cpu.mnn.weight'))
            for name in ['cpu.mnn', 'gpu.mnn', 'cpu.mnn.weight']:
                self.assertEqual((target / name).read_bytes(), (again / name).read_bytes())

    def test_group_rejects_unreferenced_bytes_before_writing(self):
        fixture = json.loads((Path(__file__).parent / 'Fixtures/external_scale.json').read_text())
        artifacts = TOOL.parents[4] / 'TestArtifacts~/SpeechModels/ToolTests'
        artifacts.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=artifacts) as temporary:
            source = Path(temporary) / 'source.mnn'
            source.write_bytes(base64.b64decode(fixture['graph']))
            Path(str(source) + '.weight').write_bytes(base64.b64decode(fixture['weights']) + b'unreferenced')
            target = Path(temporary) / 'output'
            with self.assertRaisesRegex(ValueError, 'unreferenced'):
                deduplicate.compact_models([source], target)
            self.assertFalse(target.exists())

    def test_official_scale_graph_compacts_without_losing_weights(self):
        fixture = json.loads((Path(__file__).parent / 'Fixtures/external_scale.json').read_text())
        artifacts = TOOL.parents[4] / 'TestArtifacts~/SpeechModels/ToolTests'
        artifacts.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=artifacts) as temporary:
            source = Path(temporary) / 'source.mnn'
            target = Path(temporary) / 'compacted.mnn'
            original = base64.b64decode(fixture['weights'])
            source.write_bytes(base64.b64decode(fixture['graph']))
            Path(str(source) + '.weight').write_bytes(original)
            report = deduplicate.compact_model(source, target)
            compacted = Path(str(target) + '.weight').read_bytes()
            self.assertLess(report['weight_bytes'], report['source_weight_bytes'])
            before = deduplicate.Model(source.read_bytes()).external_records()
            after = deduplicate.Model(target.read_bytes()).external_records()
            self.assertEqual(len(before), 2)
            self.assertEqual(len(before), len(after))
            self.assertEqual(after[0][0], after[1][0])
            for (old_offset, old_sizes, _), (new_offset, new_sizes, _) in zip(before, after):
                self.assertEqual(old_sizes, new_sizes)
                self.assertEqual(original[old_offset:old_offset + sum(old_sizes)],
                                 compacted[new_offset:new_offset + sum(new_sizes)])

    def test_every_record_retains_exact_weights_and_bias(self):
        data = b'ABCD1234ABCD1234WXYZ'
        records = [(0, (4, 4)), (8, (4, 4)), (16, (4,))]
        compact, offsets = deduplicate.compact_records(data, records)
        self.assertEqual(offsets[0], offsets[1])
        self.assertLess(len(compact), len(data))
        for (original, sizes), offset in zip(records, offsets):
            self.assertEqual(data[original:original+sum(sizes)], compact[offset:offset+sum(sizes)])

    def test_record_boundaries_are_preserved(self):
        compact, offsets = deduplicate.compact_records(b'abcdabcd', [(0, (2,2)), (4, (4,))])
        self.assertNotEqual(offsets[0], offsets[1])

    def test_out_of_bounds_is_rejected(self):
        with self.assertRaises(ValueError):
            deduplicate.compact_records(b'abcd', [(2, (4,))])

    def test_unreferenced_source_bytes_are_rejected(self):
        with self.assertRaises(ValueError):
            deduplicate.compact_records(b'abcdunused', [(0, (4,))])

    def test_repeated_compaction_is_stable(self):
        compact, offsets = deduplicate.compact_records(b'abcdabcd', [(0, (4,)), (4, (4,))])
        again, repeated = deduplicate.compact_records(compact, [(offset, (4,)) for offset in offsets])
        self.assertEqual(compact, again)
        self.assertEqual(offsets, repeated)


if __name__ == '__main__':
    unittest.main()
