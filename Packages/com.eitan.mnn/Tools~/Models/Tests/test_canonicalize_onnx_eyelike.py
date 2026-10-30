"""Requires optional conversion dependencies: numpy, onnx and onnxruntime."""
from pathlib import Path
import importlib.util
import os
import unittest

try:
    import numpy as np
    import onnx
    import onnxruntime as ort
    from onnx import helper
except ImportError:
    onnx = None


@unittest.skipIf(onnx is None, 'requires numpy, onnx and onnxruntime conversion environment')
class EyeLikeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        path = Path(os.environ.get('MNN_EYE_TOOL_PATH', Path(__file__).resolve().parents[1] / 'canonicalize_onnx_eyelike.py'))
        spec = importlib.util.spec_from_file_location('eye_tool', path)
        cls.tool = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.tool)

    @staticmethod
    def model(dtype=None, offset=0, input_dtype=None):
        input_dtype = input_dtype or onnx.TensorProto.FLOAT
        attributes = {'k': offset}
        if dtype is not None:
            attributes['dtype'] = dtype
        graph = helper.make_graph([helper.make_node('EyeLike', ['x'], ['y'], name='eye', **attributes)], 'test',
            [helper.make_tensor_value_info('x', input_dtype, ['rows', 'cols'])],
            [helper.make_tensor_value_info('y', dtype or input_dtype, ['rows', 'cols'])])
        model = helper.make_model(graph, opset_imports=[helper.make_opsetid('', 17)])
        model.ir_version = 10
        return model

    def test_dynamic_rectangular_matrices_offsets_and_types(self):
        for dtype in [onnx.TensorProto.BOOL, onnx.TensorProto.FLOAT, onnx.TensorProto.INT64]:
            for offset in [-3, -1, 0, 1, 3]:
                model = self.model(dtype, offset)
                self.assertEqual(1, self.tool.canonicalize(model))
                onnx.checker.check_model(model)
                session = ort.InferenceSession(model.SerializeToString(), providers=['CPUExecutionProvider'])
                for rows, cols in [(1, 1), (2, 5), (5, 2), (7, 7)]:
                    with self.subTest(dtype=dtype, offset=offset, shape=(rows, cols)):
                        actual = session.run(None, {'x': np.zeros((rows, cols), np.float32)})[0]
                        expected = np.eye(rows, cols, k=offset, dtype=helper.tensor_dtype_to_np_dtype(dtype))
                        np.testing.assert_array_equal(expected, actual)

    def test_implicit_dtype_preserves_non_float_inputs(self):
        for dtype in [onnx.TensorProto.BOOL, onnx.TensorProto.INT64, onnx.TensorProto.DOUBLE]:
            model = self.model(input_dtype=dtype)
            self.assertEqual(1, self.tool.canonicalize(model))
            session = ort.InferenceSession(model.SerializeToString(), providers=['CPUExecutionProvider'])
            numpy_dtype = helper.tensor_dtype_to_np_dtype(dtype)
            actual = session.run(None, {'x': np.zeros((3, 5), dtype=numpy_dtype)})[0]
            self.assertEqual(numpy_dtype, actual.dtype)
            np.testing.assert_array_equal(np.eye(3, 5, dtype=numpy_dtype), actual)

    def test_implicit_dtype_is_inferred_for_intermediate_tensor(self):
        model = self.model(input_dtype=onnx.TensorProto.BOOL)
        model.graph.node.insert(0, helper.make_node('Cast', ['x'], ['cast'], to=onnx.TensorProto.INT64))
        model.graph.node[1].input[0] = 'cast'
        model.graph.output[0].type.tensor_type.elem_type = onnx.TensorProto.INT64
        self.assertEqual(1, self.tool.canonicalize(model))
        session = ort.InferenceSession(model.SerializeToString(), providers=['CPUExecutionProvider'])
        actual = session.run(None, {'x': np.zeros((2, 4), np.bool_)})[0]
        self.assertEqual(np.dtype('int64'), actual.dtype)
        np.testing.assert_array_equal(np.eye(2, 4, dtype=np.int64), actual)

    def test_second_run_preserves_serialized_graph(self):
        model = self.model(onnx.TensorProto.BOOL)
        self.tool.canonicalize(model)
        expected = model.SerializeToString()
        self.assertEqual(0, self.tool.canonicalize(model))
        self.assertEqual(expected, model.SerializeToString())

    def test_unrelated_graph_is_preserved(self):
        graph = helper.make_graph([helper.make_node('Identity', ['x'], ['y'])], 'identity',
            [helper.make_tensor_value_info('x', onnx.TensorProto.FLOAT, [2, 3])],
            [helper.make_tensor_value_info('y', onnx.TensorProto.FLOAT, [2, 3])])
        model = helper.make_model(graph)
        expected = model.SerializeToString()
        self.assertEqual(0, self.tool.canonicalize(model))
        self.assertEqual(expected, model.SerializeToString())


if __name__ == '__main__':
    unittest.main()
