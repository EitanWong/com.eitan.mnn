"""Split convolution and recurrence without changing the original complex forward."""
from pathlib import Path
import hashlib
import json
import subprocess
import uuid
import torch
from graph import cmul, block, rnn_layer

class Encoder(torch.nn.Module):

    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, mixture):
        spectrum = torch.nn.functional.conv1d(mixture.unsqueeze(1), self.model.encoder.filterbank.filters(), stride=100)
        r = spectrum[:, :256]
        i = spectrum[:, 257:513]
        pair = (r.unsqueeze(1), i.unsqueeze(1))
        saved = []
        for layer in self.model.masker.encoders[:6]:
            pair = block(layer, pair)
            saved.extend(pair)
        return (*saved, r, i)

class Recurrent(torch.nn.Module):

    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, real, imag):
        return rnn_layer(self.model.masker.encoders[6], (real, imag))

class Decoder(torch.nn.Module):

    def __init__(self, model):
        super().__init__()
        self.model = model

    def forward(self, rr, ri, *inputs):
        pair = (rr, ri)
        saved = [(inputs[i], inputs[i + 1]) for i in range(0, 12, 2)]
        real, imag = inputs[12:14]
        for enc, dec in zip(reversed(saved), self.model.masker.decoders):
            pair = block(dec, pair)
            pair = (torch.cat([pair[0], enc[0]], 1), torch.cat([pair[1], enc[1]], 1))
        r, i = cmul(self.model.masker.output_layer[0], pair)
        mag = torch.tanh(torch.sqrt(r * r + i * i + 1e-08))
        phase = torch.atan2(i, r)
        mr = (mag * torch.cos(phase))[:, 0]
        mi = (mag * torch.sin(phase))[:, 0]
        sr = mr * real - mi * imag
        si = mr * imag + mi * real
        spec = torch.cat([torch.nn.functional.pad(sr, (0, 0, 0, 1)), torch.nn.functional.pad(si, (0, 0, 0, 1))], 1)
        return torch.nn.functional.conv_transpose1d(spec, self.model.decoder.filterbank.filters(), stride=100).transpose(1, 2)
ENCODER_OUTPUTS = [f'encoder_{k}_{part}' for k in range(6) for part in ['real', 'imag']] + ['spectrum_real', 'spectrum_imag']

def convert_stage(converter, path, stage, canonical_uuid):
    graph = path.with_suffix('.mnn')
    command = [str(converter), '-f', 'ONNX', '--modelFile', str(path), '--MNNModel', str(graph), '--bizCode', 'DCCRNetHybrid' + stage, '--optimizeLevel', '1', '--optimizePrefer', '2', '--useOriginRNNImpl', '--transformerFuseC4', '0', '--transformerFuseQkvProj', '0', '--transformerFuseGateUpProj', '0', '--transformerFuseLnProj', '0']
    with (path.parent / (stage + '-conversion.log')).open('w') as log:
        subprocess.run(command, check=True, stdout=log, stderr=subprocess.STDOUT)
    canonical_uuid(graph, 'DCCRNet/00914c64/hybrid/' + stage)
    deployed_name = 'dccrnet_' + stage + '_fp32.mnn'
    graph.rename(graph.with_name(deployed_name))
    return dict(name=deployed_name, bytes=graph.with_name(deployed_name).stat().st_size, sha256=hashlib.sha256(graph.with_name(deployed_name).read_bytes()).hexdigest(), arguments=command)

def prepare(model, converter, output, canonical_uuid):
    output.mkdir(parents=True, exist_ok=True)
    encoder, recurrent, decoder = (Encoder(model).eval(), Recurrent(model).eval(), Decoder(model).eval())
    names = ENCODER_OUTPUTS
    with torch.inference_mode():
        value = torch.zeros(1, 16000)
        encoded = encoder(value)
        state = recurrent(encoded[10], encoded[11])
        feature_axes = {name: {3 if index < 12 else 2: 'frames_' + str(index // 2)} for index, name in enumerate(names)}
        descriptions = [('encoder', encoder, (value,), ['mixture'], names, {'mixture': {1: 'samples'}, **feature_axes}), ('recurrent', recurrent, (encoded[10], encoded[11]), ['real', 'imag'], ['rnn_real', 'rnn_imag'], {name: {3: 'frames'} for name in ['real', 'imag', 'rnn_real', 'rnn_imag']}), ('decoder', decoder, (*state, *encoded), ['rnn_real', 'rnn_imag', *names], ['enhanced'], {'rnn_real': {3: 'frames'}, 'rnn_imag': {3: 'frames'}, **feature_axes, 'enhanced': {1: 'samples'}})]
        graphs = []
        for stage, graph, values, inputs, outputs, axes in descriptions:
            path = output / (stage + '.onnx')
            torch.onnx.export(graph, values, path, opset_version=17, dynamo=False, input_names=inputs, output_names=outputs, dynamic_axes=axes)
            graphs.append(convert_stage(converter, path, stage, canonical_uuid))
    return graphs

def check_cpu(graph_directory, cases, compare):
    import MNN
    import numpy as np
    runtimes = {}
    for stage in ['encoder', 'recurrent', 'decoder']:
        interpreter = MNN.Interpreter(str(graph_directory / ('dccrnet_' + stage + '_fp32.mnn')))
        session = interpreter.createSession(dict(backend='CPU', numThread=2, precision='high'))
        runtimes[stage] = (interpreter, session)

    def run(stage, inputs, names):
        interpreter, session = runtimes[stage]
        tensors = {}
        for name, value in inputs.items():
            tensors[name] = interpreter.getSessionInput(session, name)
            interpreter.resizeTensor(tensors[name], value.shape)
        interpreter.resizeSession(session)
        for name, value in inputs.items():
            tensors[name].copyFrom(MNN.Tensor(value.shape, MNN.Halide_Type_Float, value.copy(), MNN.Tensor_DimensionType_Caffe))
        if interpreter.runSession(session) != 0:
            raise RuntimeError('MNN split inference failed: ' + stage)
        outputs = []
        for name in names:
            tensor = interpreter.getSessionOutput(session, name)
            host = MNN.Tensor(tensor.getShape(), MNN.Halide_Type_Float, np.zeros(tensor.getShape(), np.float32), MNN.Tensor_DimensionType_Caffe)
            tensor.copyToHostTensor(host)
            outputs.append(np.asarray(host.getData(), np.float32).reshape(tensor.getShape()))
        return outputs
    rows = []
    for value, expected in cases:
        encoded = run('encoder', {'mixture': value}, ENCODER_OUTPUTS)
        state = run('recurrent', dict(real=encoded[10], imag=encoded[11]), ['rnn_real', 'rnn_imag'])
        decoded = run('decoder', dict(zip(['rnn_real', 'rnn_imag', *ENCODER_OUTPUTS], [*state, *encoded])), ['enhanced'])[0]
        decoded = np.pad(decoded, ((0, 0), (0, value.shape[1] - decoded.shape[1]), (0, 0)))
        rows.append(dict(samples=value.shape[1], nrmse=compare(decoded, expected)))
    return rows
