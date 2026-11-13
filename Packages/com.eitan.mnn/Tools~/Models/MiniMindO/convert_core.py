#!/usr/bin/env python3
"""Reproduce FP16 prefill + 56 Thinker W8/block64 projections + FP16 remaining decoder ops."""
import argparse,copy,json,re,subprocess,sys,shutil
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--onnx-directory',type=Path,required=True);p.add_argument('--work',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
a.work=a.work.resolve();a.output=a.output.resolve();a.onnx_directory=a.onnx_directory.resolve()
a.work.mkdir(parents=True,exist_ok=True);a.output.mkdir(parents=True,exist_ok=True)
def convert(source,output,flags,framework='ONNX'):
 subprocess.run([sys.executable,'-m','MNN.tools.mnnconvert','-f',framework,'--modelFile',str(source),'--MNNModel',str(output)]+flags,check=True,cwd=a.work)
guide=a.work/'quant-guide.json'
if guide.exists():guide.unlink()
convert(a.onnx_directory/'decode.onnx',a.work/'guide.mnn',['--weightQuantBits','8','--weightQuantBlock','64','--weightQuantAsymmetric','--compressionParamsFile',str(guide)])
strategy=json.loads(guide.read_text());strategy['forGuide']=True;selected=[]
for layer in strategy['algo'][0]['quantParams']['layer']:
 use=bool(re.match(r'^/net/layers\.\d+/(?:mlp|self_attn)/[a-z_]+/MatMul',layer['opName']))
 for weight in layer['weight']:weight.update(bits=8 if use else 0,blockSize=64,asymmetric=True)
 if use:selected.append(layer['opName'])
assert len(selected)==56,('Unexpected Thinker graph; refusing a different quantization recipe',len(selected))
strategy_path=a.work/'thinker-w8-talker-fp16.json';strategy_path.write_text(json.dumps(strategy,indent=2)+'\n')
convert(a.onnx_directory/'prefill.onnx',a.work/'prefill.mnn',['--fp16','--saveExternalData'])
convert(a.onnx_directory/'decode.onnx',a.work/'mixed.mnn',['--weightQuantBits','8','--weightQuantBlock','64','--weightQuantAsymmetric','--compressionParamsFile',str(strategy_path)])
convert(a.work/'mixed.mnn',a.work/'decode.mnn',['--fp16','--saveExternalData'],'MNN')
for stage in ['prefill','decode']:
 for suffix in ['.mnn','.mnn.weight']:
  source=a.work/(stage+suffix);assert source.is_file() and source.stat().st_size>0
  temporary=a.output/(stage+suffix+'.installing');shutil.copy2(source,temporary);temporary.replace(a.output/(stage+suffix))
print('SELECTED_THINKER_W8_PROJECTIONS='+str(len(selected)))
