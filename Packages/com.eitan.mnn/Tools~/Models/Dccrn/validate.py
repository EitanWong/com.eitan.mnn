"""Independent complete PCM validation; imports MNN/NumPy, never Torch."""
from pathlib import Path
import argparse
import gc
import json
import numpy as np
import MNN


class Graph:
    def __init__(self, directory, name, threads):
        self.owner = MNN.Interpreter(str(directory / (name + '.mnn')))
        self.owner.setExternalFile(str(directory / 'dccrn.mnn.weight'))
        self.session = self.owner.createSession(dict(backend='CPU', numThread=threads, precision='high'))

    def run(self, inputs, names):
        for name,value in inputs.items():
            self.owner.resizeTensor(self.owner.getSessionInput(self.session,name), value.shape)
        self.owner.resizeSession(self.session)
        for name,value in inputs.items():
            self.owner.getSessionInput(self.session,name).copyFrom(MNN.Tensor(value.shape,MNN.Halide_Type_Float,value.copy(),MNN.Tensor_DimensionType_Caffe))
        if self.owner.runSession(self.session) != 0:
            raise RuntimeError('MNN session failed')
        result = []
        for name in names:
            output = self.owner.getSessionOutput(self.session,name)
            host = MNN.Tensor(output.getShape(),MNN.Halide_Type_Float,np.zeros(output.getShape(),np.float32),MNN.Tensor_DimensionType_Caffe)
            if not output.copyToHostTensor(host):
                raise RuntimeError('MNN output readback failed')
            result.append(np.asarray(host.getData(),np.float32).reshape(output.getShape()).copy())
        return result


def main():
    parser = argparse.ArgumentParser()
    for key in ['directory','oracle','output']:
        parser.add_argument('--'+key,type=Path,required=True)
    args = parser.parse_args()
    cases = json.loads((args.oracle/'cases.json').read_text())['cases']
    if len(cases) != 22:
        raise ValueError('Require all22 original conditions')
    rows = []
    for split,threads in [(False,1),(False,4),(True,4)]:
        graphs = {name:Graph(args.directory,name,threads) for name in (['analysis','encoder','recurrent','decoder'] if split else ['dccrn'])}
        for item in cases + cases[:1]:
            name = item['case']
            raw = np.fromfile(args.oracle/(name+'-input.f32'),np.float32)
            target = np.fromfile(args.oracle/(name+'-target.f32'),np.float32).astype(np.float64)
            pcm = np.pad(raw,(0,(-raw.size)%100)).reshape(1,1,-1)
            for repeat in range(2):
                if split:
                    features,magnitude,phase = graphs['analysis'].run({'pcm':pcm},['features','magnitude','phase'])
                    keys = ['skip'+str(i) for i in range(6)]
                    skips = graphs['encoder'].run({'features':features},keys)
                    recurrent = graphs['recurrent'].run({'encoded':skips[5]},['recurrent'])[0]
                    value = graphs['decoder'].run(dict(recurrent=recurrent,**dict(zip(keys,skips)),magnitude=magnitude,phase=phase),['enhanced'])[0]
                else:
                    value = graphs['dccrn'].run({'pcm':pcm},['enhanced'])[0]
                if value.shape != pcm.shape or not np.isfinite(value).all():
                    raise ValueError('Invalid original PCM output: '+name)
                actual = value.reshape(-1)[:raw.size].astype(np.float64)
                nrmse = float(np.linalg.norm(actual-target)/(np.linalg.norm(target) or 1.))
                row = dict(case=name,split=split,threads=threads,repeat=repeat,waveform_nrmse=nrmse)
                rows.append(row)
                args.output.write_text(json.dumps(rows,indent=2)+'\n')
                print(row,flush=True)
                if nrmse >= .001:
                    raise ValueError(row)
        graphs = None
        gc.collect()


if __name__ == '__main__':
    main()
