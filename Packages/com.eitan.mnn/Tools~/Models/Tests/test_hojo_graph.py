"""Native-independent tests for Hojo cache export semantics; run with onnx/ORT."""
import importlib.util
from pathlib import Path
import unittest
import numpy as np
import onnx
from onnx import TensorProto, helper, numpy_helper
import onnxruntime as ort

SPEC = importlib.util.spec_from_file_location('hojo_prepare', Path(__file__).parents[1] / 'HojoTts/prepare.py')
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

class HojoGraphTests(unittest.TestCase):
    def test_bfloat_promotion_preserves_every_weight_value(self):
        from ml_dtypes import bfloat16
        weight=np.asarray([0, -0.5, 1.25, 128], dtype=bfloat16)
        m=helper.make_model(helper.make_graph([
            helper.make_node('Identity', ['weight'], ['output'])], 'bf16', [],
            [helper.make_tensor_value_info('output', TensorProto.BFLOAT16, [4])],
            [numpy_helper.from_array(weight, 'weight')]), opset_imports=[helper.make_opsetid('',17)])
        MODULE.promote(m)
        self.assertEqual(m.graph.output[0].type.tensor_type.elem_type, TensorProto.FLOAT)
        np.testing.assert_array_equal(numpy_helper.to_array(m.graph.initializer[0]), weight.astype('float32'))

    def test_prefill_and_padded_decode_match_original_real_export(self):
        import os
        work=Path(os.environ['HOJO_TEST_WORK'])
        fixture=np.load(work/'fixtures/llm-1.npz')
        model=onnx.load(work/'llm-fp32.onnx')
        optimized=MODULE.optimize_lm(model, decode=True)
        optimized.ir_version=min(optimized.ir_version,10)
        session=ort.InferenceSession(optimized.SerializeToString(), providers=['CPUExecutionProvider'])
        valid=fixture['past_key_values.0.key'].shape[2]
        # Poisoned padding must have no effect on attention or current KV update.
        cache=np.full((20,1,256,128),77,dtype=np.float32)
        for layer in range(10):
            for offset,kind in enumerate(['key','value']):
                cache[layer*2+offset,0,:valid]=fixture[f'past_key_values.{layer}.{kind}'][0,0]
        mask=np.full((1,1,1,257),-1e9,dtype=np.float32);mask[...,:valid]=0;mask[...,-1]=0
        output=dict(zip([x.name for x in session.get_outputs()], session.run(None, {
            'inputs_embeds':fixture['inputs_embeds'],'position_ids':fixture['position_ids'],
            'attention_mask':mask,'cache':cache})))
        np.testing.assert_allclose(output['logits'], fixture['ref:logits'][:,-1:], atol=3e-5,rtol=1e-5)
        np.testing.assert_allclose(output['last_hidden_state'],fixture['ref:last_hidden_state'][:,-1:],atol=3e-5,rtol=1e-5)
        for layer in range(10):
            for offset,kind in enumerate(['key','value']):
                np.testing.assert_allclose(output['cache_updates'][layer*2+offset],fixture[f'ref:present.{layer}.{kind}'][0,:,-1:],atol=3e-5,rtol=1e-5)

if __name__=='__main__': unittest.main()
