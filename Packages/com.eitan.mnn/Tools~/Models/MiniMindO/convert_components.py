#!/usr/bin/env python3
"""Convert numerically checked component ONNX files using the official MNN converter."""
import argparse,json,subprocess,sys,hashlib
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--onnx-directory',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--stage',choices=['mimi_decoder','vision_encoder','audio_encoder','multimodal_prefill'],required=True);a=p.parse_args()
a.output=a.output.resolve();a.onnx_directory=a.onnx_directory.resolve()
a.output.mkdir(parents=True,exist_ok=True)
work=a.onnx_directory/('convert-'+a.stage);work.mkdir(exist_ok=True)
source=a.onnx_directory/(a.stage+'.onnx');checks=source.with_suffix('.checks.json')
if not checks.is_file() or not json.loads(checks.read_text()):raise ValueError('Missing numerical export checks: '+str(checks))
output=a.output/(a.stage+'.mnn')
subprocess.run([sys.executable,'-m','MNN.tools.mnnconvert','-f','ONNX','--modelFile',str(source),'--MNNModel',str(output),'--fp16','--saveExternalData'],check=True,cwd=work)
for file in [output,Path(str(output)+'.weight')]:
 if not file.is_file() or file.stat().st_size==0:raise ValueError('Missing converter output: '+str(file))
 print(json.dumps({'path':str(file.resolve()),'bytes':file.stat().st_size,'sha256':hashlib.sha256(file.read_bytes()).hexdigest()}),flush=True)
