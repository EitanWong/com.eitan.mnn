from workspace import workspace
"""Lower the two single-tick ONNX LSTMs to their exact gate equations."""
from pathlib import Path
import onnx
from onnx import helper as h,numpy_helper as nh
import numpy as np
A,R=workspace();m=onnx.load(R/'Assets/StreamingAssets/MNN/Models/DualTurn-Source/stream_tick.onnx');arrays={v.name:nh.to_array(v) for v in m.graph.initializer};nodes=[]
for node in m.graph.node:
 if node.op_type!='LSTM':nodes.append(node);continue
 p=node.name+'/explicit';idx=p+'/zero';axis=p+'/axis';split=p+'/split'
 for name,arr in [(idx,np.array(0,np.int64)),(axis,np.array([0],np.int64)),(split,np.array([256]*4,np.int64))]:m.graph.initializer.append(nh.from_array(arr,name))
 def op(kind,inputs,name,**attrs):
  out=p+'/'+name;nodes.append(h.make_node(kind,inputs,[out],name=out,**attrs));return out
 x=op('Gather',[node.input[0],idx],'x',axis=0);old_h=op('Gather',[node.input[5],idx],'h',axis=0);old_c=op('Gather',[node.input[6],idx],'c',axis=0)
 w,r,b=(arrays[n] for n in node.input[1:4]);bias=b[0,:1024]+b[0,1024:]
 for name,arr in [('w',w[0].T.copy()),('r',r[0].T.copy()),('b',bias)]:m.graph.initializer.append(nh.from_array(arr,p+'/'+name))
 gates=op('Add',[op('Add',[op('MatMul',[x,p+'/w'],'wx'),op('MatMul',[old_h,p+'/r'],'rh')],'sum'),p+'/b'],'gates')
 gate_names=[p+'/'+n for n in ['i','o','f','g']];nodes.append(h.make_node('Split',[gates,split],gate_names,axis=1,name=p+'/split_gates'))
 i,o,f,g=gate_names;i=op('Sigmoid',[i],'si');o=op('Sigmoid',[o],'so');f=op('Sigmoid',[f],'sf');g=op('Tanh',[g],'tg');c=op('Add',[op('Mul',[f,old_c],'fc'),op('Mul',[i,g],'ig')],'newc');newh=op('Mul',[o,op('Tanh',[c],'tc')],'newh')
 for value,target in [(newh,node.output[1]),(c,node.output[2])]:nodes.append(h.make_node('Unsqueeze',[value,axis],[target]))
 nodes.append(h.make_node('Unsqueeze',[node.output[1],axis],[node.output[0]]))
del m.graph.node[:];m.graph.node.extend(nodes);onnx.checker.check_model(m);out=A/'DualTurn/source-single-tick.onnx';onnx.save(m,out);print(out)
