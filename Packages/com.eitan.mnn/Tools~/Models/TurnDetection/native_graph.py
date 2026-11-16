"""Official MNN Python API runner; no custom native code or ABI bindings."""
import time
import MNN
import numpy as np

class Graph:
    def __init__(self, path, backend='CPU', threads=4, precision='high'):
        self.engine = MNN.Interpreter(str(path))
        self.session = self.engine.createSession({'backend': backend, 'numThread': threads, 'precision': precision})
        self.inputs = self.engine.getSessionInputAll(self.session)
        self.shapes = None
        self.calls = 0
        self.elapsed = 0

    def run(self, names, feed):
        started = time.perf_counter()
        shapes = {k:tuple(v.shape) for k,v in feed.items() if k in self.inputs}
        if shapes != self.shapes:
            for k,v in shapes.items(): self.engine.resizeTensor(self.inputs[k], v)
            self.engine.resizeSession(self.session)
            self.shapes = shapes
        for k,tensor in self.inputs.items():
            v = np.ascontiguousarray(feed[k], dtype=np.float32 if feed[k].dtype.kind=='f' else np.int32)
            host = MNN.Tensor(tuple(v.shape), MNN.Halide_Type_Float if v.dtype.kind=='f' else MNN.Halide_Type_Int, v, MNN.Tensor_DimensionType_Caffe)
            tensor.copyFrom(host)
        status = self.engine.runSession(self.session)
        if status != 0: raise RuntimeError(f'runSession status {status}')
        outputs = self.engine.getSessionOutputAll(self.session)
        if names is None: names = list(outputs)
        result=[]
        for name in names:
            t=outputs[name]; shape=tuple(t.getShape())
            kind = t.getDataType()
            dtype = np.int32 if kind == MNN.Halide_Type_Int else np.float32
            host = MNN.Tensor(shape, kind, np.zeros(shape,dtype), MNN.Tensor_DimensionType_Caffe)
            t.copyToHostTensor(host)
            result.append(np.asarray(host.getData(),dtype=dtype).reshape(shape))
        self.elapsed += time.perf_counter()-started; self.calls+=1
        return result

class ModuleGraph:
    """Express Module runner for graphs containing recurrent subgraphs."""
    def __init__(self, path, inputs, outputs, threads=4):
        import MNN.nn as nn
        import MNN.expr as expr
        self.expr = expr
        self.inputs, self.outputs = inputs, outputs
        self.module = nn.load_module_from_file(str(path), inputs, outputs,
                                               thread_num=threads, precision_mode=expr.PrecisionMode.High)

    def run(self, names, feed):
        expr = self.expr
        variables = [expr.const(np.ascontiguousarray(feed[name], dtype=np.int32 if feed[name].dtype.kind != 'f' else np.float32),
                                list(feed[name].shape), expr.NCHW,
                                expr.int if feed[name].dtype.kind != 'f' else expr.float) for name in self.inputs]
        outputs = self.module.forward(variables)
        values = {name: np.array(value.read(), copy=True).reshape(value.shape) for name, value in zip(self.outputs, outputs)}
        return [values[name] for name in (names or self.outputs)]
