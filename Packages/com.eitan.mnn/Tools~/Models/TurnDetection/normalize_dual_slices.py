from workspace import workspace
from pathlib import Path
import onnx
from onnx import helper as h,numpy_helper as nh
import numpy as np
A,R=workspace();m=onnx.load(A/'DualTurn/source-single-tick.onnx');constants={i.name:nh.to_array(i) for i in m.graph.initializer}
for n in m.graph.node:
 if n.op_type=='Constant':
  for a in n.attribute:
   if a.name=='value':constants[n.output[0]]=nh.to_array(a.t)
nodes=[];count=0
for n in m.graph.node:
 if n.op_type=='Slice' and n.input[1] in constants and np.any(constants[n.input[1]]<0) and np.all(constants[n.input[4]]==1):
  starts=constants[n.input[1]].astype(np.int64);axes=constants[n.input[3]].astype(np.int64);p=n.name+'/nonnegative';count+=1
  # ONNX positive-step slice semantics clamp every negative start to max(dim+start,0).
  assert np.all(starts<0) and np.all(constants[n.input[4]]==1)
  nodes.extend([h.make_node('Shape',[n.input[0]],[p+'/shape']),h.make_node('Gather',[p+'/shape',n.input[3]],[p+'/dims'],axis=0),h.make_node('Add',[p+'/dims',n.input[1]],[p+'/signed']),h.make_node('Max',[p+'/signed',p+'/zero'],[p+'/starts'])]);m.graph.initializer.append(nh.from_array(np.zeros_like(starts),p+'/zero'));n.input[1]=p+'/starts'
 nodes.append(n)
del m.graph.node[:];m.graph.node.extend(nodes);onnx.checker.check_model(m);onnx.save(m,A/'DualTurn/source-positive-slices.onnx');print('Normalized negative Slice starts',count)
