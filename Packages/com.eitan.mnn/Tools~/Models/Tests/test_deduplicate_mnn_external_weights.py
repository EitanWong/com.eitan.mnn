"""MNN external storage compaction must preserve every logical weight record."""
import importlib.util
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
TOOL = Path(__file__).resolve().parents[1] / 'deduplicate_mnn_external_weights.py'
spec = importlib.util.spec_from_file_location('deduplicate', TOOL)
deduplicate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(deduplicate)


class ExternalWeightCompactionTests(unittest.TestCase):
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
