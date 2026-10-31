"""Verify deployment bytes and bounded external-weight reads through the public tool."""
from pathlib import Path
import base64
import importlib.util
import json
import tempfile
import unittest
from unittest.mock import patch

BASE = Path(__file__).resolve().parent
ROOT = BASE.parents[4]
ARTIFACTS = ROOT / 'TestArtifacts~/SpeechModels/StreamingWeightTests'


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, BASE.parent / filename)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def sources(folder):
    data = json.loads((BASE / 'Fixtures/external_scale.json').read_text())
    result = []
    for name in ['cpu.mnn', 'metal.mnn']:
        graph = folder / name
        graph.write_bytes(base64.b64decode(data['graph']))
        Path(str(graph) + '.weight').write_bytes(base64.b64decode(data['weights']))
        result.append(graph)
    return result


class StreamingWeightTests(unittest.TestCase):
    def setUp(self):
        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=ARTIFACTS)
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)

    def test_exact_outputs_match_existing_compaction_without_reading_whole_weight_files(self):
        stream = module('stream_weights', 'stream_mnn_external_weights.py')
        existing = module('existing_weights', 'deduplicate_mnn_external_weights.py')
        graphs = sources(self.folder)
        expected = existing.compact_models(graphs, self.folder / 'expected', 'cpu.mnn.weight')
        read_bytes = Path.read_bytes

        def bounded(path):
            if path.suffix == '.weight':
                raise AssertionError('Whole external-weight reads are forbidden')
            return read_bytes(path)

        with patch.object(Path, 'read_bytes', bounded):
            actual = stream.compact_models(graphs, self.folder / 'actual', 'cpu.mnn.weight')
            repeated = stream.compact_models(graphs, self.folder / 'actual', 'cpu.mnn.weight')
        self.assertEqual(expected, actual)
        self.assertEqual(actual, repeated)
        self.assertLess(actual['weight_bytes'], actual['source_weight_bytes'])
        for name in ['cpu.mnn', 'metal.mnn', 'cpu.mnn.weight']:
            self.assertEqual((self.folder / 'actual' / name).read_bytes(), (self.folder / 'expected' / name).read_bytes())

    def test_unreferenced_weight_bytes_fail_before_writing_output(self):
        stream = module('stream_invalid_weights', 'stream_mnn_external_weights.py')
        graphs = sources(self.folder)
        with Path(str(graphs[0]) + '.weight').open('ab') as output:
            output.write(b'unreferenced')
        target = self.folder / 'output'
        with self.assertRaisesRegex(ValueError, 'unreferenced'):
            stream.compact_models(graphs, target, 'cpu.mnn.weight')
        self.assertFalse(target.exists())

    def test_source_alias_and_duplicate_graph_names_fail_without_overwriting_input(self):
        stream = module('stream_alias_weights', 'stream_mnn_external_weights.py')
        graphs = sources(self.folder)
        before = {str(path): path.read_bytes() for graph in graphs for path in [graph, Path(str(graph) + '.weight')]}
        with self.assertRaises(ValueError):
            stream.compact_models(graphs, self.folder, 'cpu.mnn.weight')
        with self.assertRaises(ValueError):
            stream.compact_models([graphs[0], graphs[0]], self.folder / 'output', 'cpu.mnn.weight')
        for name, data in before.items():
            self.assertEqual(Path(name).read_bytes(), data)


if __name__ == '__main__':
    unittest.main()
