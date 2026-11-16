from workspace import workspace
from pathlib import Path
import subprocess,sys
A,R=workspace();w=A/'Easy'
for name,flags in {'FP32':[],'FP16':['--fp16'],'W8-HQQ':['--weightQuantBits','8','--weightQuantBlock','64','--weightQuantAsymmetric','--hqq'],'W4-HQQ':['--weightQuantBits','4','--weightQuantBlock','64','--weightQuantAsymmetric','--hqq']}.items():
 p=w/('decoder-'+name+'.mnn')
 if p.exists():continue
 with (w/('decoder-'+name+'-convert.log')).open('w') as log:
  subprocess.run([sys.executable,'-m','MNN.tools.mnnconvert','-f','ONNX','--modelFile',str(w/'decoder.onnx'),'--MNNModel',str(p),'--bizCode','EasyTurnDecoder','--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']+flags,check=True,stdout=log,stderr=subprocess.STDOUT)
 print('DECODER_CONVERTED',name,p.stat().st_size,flush=True)
