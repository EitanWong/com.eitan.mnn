#!/usr/bin/env python3
"""Export Hojo V2 to official MNN with bounded cache IO and last-position logits.

Sources stay immutable. Work/ONNX/logs belong in TestArtifacts~; output is deployable.
Requires onnx, numpy and the official MNN converter in this Python environment.
"""
import argparse
import copy
import json
import hashlib
import io
import pickletools
import re
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path
import numpy as np
import onnx
from onnx import AttributeProto, TensorProto, helper, numpy_helper

SOURCE_REVISION = '7541fea0b0fdd53ca001ff8a7518de093f381b48'

def promote(model):
    """BF16 -> FLOAT is exact, unlike blindly reinterpreting its 16-bit payload."""
    for tensor in model.graph.initializer:
        promote_tensor(tensor)
    for value in list(model.graph.input)+list(model.graph.output)+list(model.graph.value_info):
        if value.type.tensor_type.elem_type == TensorProto.BFLOAT16:
            value.type.tensor_type.elem_type = TensorProto.FLOAT
    for node in model.graph.node:
        for attr in node.attribute:
            if node.op_type == 'Cast' and attr.name == 'to' and attr.i == TensorProto.BFLOAT16:
                attr.i = TensorProto.FLOAT
            if attr.type == AttributeProto.TENSOR:
                promote_tensor(attr.t)
            elif attr.type == AttributeProto.TENSORS:
                for tensor in attr.tensors: promote_tensor(tensor)
    del model.graph.value_info[:]
    return model

def promote_tensor(tensor):
    if tensor.data_type == TensorProto.BFLOAT16:
        tensor.CopyFrom(numpy_helper.from_array(numpy_helper.to_array(tensor).astype(np.float32), tensor.name))

def prune(model):
    """Keep only nodes reachable from exported outputs, in original topological order."""
    needed = {v.name for v in model.graph.output}
    nodes = []
    for node in reversed(model.graph.node):
        if needed.intersection(node.output):
            nodes.append(node); needed.update(node.input)
    del model.graph.node[:]; model.graph.node.extend(reversed(nodes))
    inputs = [x for x in model.graph.input if x.name in needed]
    weights = [x for x in model.graph.initializer if x.name in needed]
    del model.graph.input[:]; model.graph.input.extend(inputs)
    del model.graph.initializer[:]; model.graph.initializer.extend(weights)
    return model

def cache_inputs(model):
    nodes = []
    for index in range(20):
        name = f'past_key_values.{index//2}.{"key" if index%2==0 else "value"}'
        constants = []
        for field,value in [('start',index),('end',index+1),('axis',0)]:
            key=f'cache_{index}_{field}'
            model.graph.initializer.append(numpy_helper.from_array(np.array([value],dtype=np.int64),key))
            constants.append(key)
        nodes.append(helper.make_node('Slice',['cache']+constants,[name],name=f'cache_slice_{index}'))
    return nodes

def optimize_lm(original, decode):
    model = copy.deepcopy(original)
    old_inputs = {x.name:x for x in model.graph.input}
    del model.graph.input[:]
    model.graph.input.extend([old_inputs['inputs_embeds'],old_inputs['position_ids']])
    if decode:
        for value in model.graph.input:
            for d,v in zip(value.type.tensor_type.shape.dim,[1,1,512]):
                d.ClearField('dim_param');d.dim_value=v
        model.graph.input.append(helper.make_tensor_value_info('cache',TensorProto.FLOAT,[20,1,'capacity',128]))
    model.graph.input.append(helper.make_tensor_value_info('attention_mask',TensorProto.FLOAT,
        [1,1,1,'capacity_plus_one'] if decode else [1,1,'sequence','sequence']))
    before = cache_inputs(model) if decode else []
    updates = []
    body=[]
    for node in model.graph.node:
        node=copy.deepcopy(node)
        for i,name in enumerate(node.input):
            if name == '/lm/model/Where_output_0': node.input[i]='attention_mask'
        if node.output and node.output[0].startswith('present.'):
            if node.op_type != 'Concat' or len(node.input)!=2:
                raise ValueError('Unexpected source KV operation; refusing different export')
            current=node.input[1]
            updates.append(current if decode else node.output[0])
            if not decode:
                node=helper.make_node('Identity',[current],list(node.output),name=node.name)
        if 'last_hidden_state' in node.output:
            node.output[0]='all_hidden_states';body.append(node)
            model.graph.initializer.append(numpy_helper.from_array(np.array([-1],dtype=np.int64),'last_position'))
            body.append(helper.make_node('Gather',['all_hidden_states','last_position'],['last_hidden_state'],axis=1))
        else:body.append(node)
    if len(updates)!=20:raise ValueError('Expected ten KV layers')
    body.append(helper.make_node('Concat',updates,['cache_updates'],axis=0))
    del model.graph.node[:];model.graph.node.extend(before+body)
    del model.graph.output[:]
    model.graph.output.extend([
        helper.make_tensor_value_info('logits',TensorProto.FLOAT,[1,1,17685]),
        helper.make_tensor_value_info('last_hidden_state',TensorProto.FLOAT,[1,1,512]),
        helper.make_tensor_value_info('cache_updates',TensorProto.FLOAT,[20,1,1 if decode else 'sequence',128])])
    prune(model);onnx.checker.check_model(model)
    return model

def convert(source, dest, flags, work):
    command=[sys.executable,'-m','MNN.tools.mnnconvert','-f','ONNX','--modelFile',str(source),
             '--MNNModel',str(dest)] + flags
    print(json.dumps(command),flush=True)
    subprocess.run(command,check=True,cwd=work)

def package_frontend(source, output):
    # The release uses a pickled object *string list*. Inspect opcodes; never unpickle.
    with zipfile.ZipFile(source/'Hojo-TTS-Light-40M-voice.npz') as archive:
        raw=archive.read('voice_ids.npy'); major=raw[6]
        offset=10+int.from_bytes(raw[8:10],'little') if major==1 else 12+int.from_bytes(raw[8:12],'little')
        voices=[arg for op,arg,_ in pickletools.genops(raw[offset:])
                if op.name in {'SHORT_BINUNICODE','BINUNICODE','BINUNICODE8','UNICODE'} and isinstance(arg,str) and re.fullmatch(r'hojo_(zh|en)_[fm]_\d{2}',arg)]
    if len(voices)!=17 or len(set(voices))!=17:raise ValueError('Unexpected Hojo voice IDs')
    bank=np.load(source/'Hojo-TTS-Light-40M-voice.npz',allow_pickle=False)
    expected={'token_embedding':(17685,512),'speaker_embeds':(17,16,512),'speaker_vecs':(17,192)}
    for name,shape in expected.items():
        data=bank[name]
        if data.shape!=shape or not np.isfinite(data).all():raise ValueError('Invalid '+name)
        data.astype('<f4').tofile(output/(name+'.bin'))
    for name in ['tokenizer.json','tokenizer_config.json']:shutil.copy2(source/name,output/name)
    config={'format':'hojo-tts-v2-mnn','version':1,'sample_rate':24000,'context_tokens':4096,
            'hidden_size':512,'layers':10,'head_dim':128,'voices':voices,
            'source_repository':'HojoAI/Hojo-TTS-Light-40M-V2','source_revision':SOURCE_REVISION,
            'frontend_dtype':'little-endian-float32','n_fft':1920,'hop_length':480,'window':'periodic-hann'}
    (output/'mnn_hojo_tts.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n')

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source',type=Path,required=True);p.add_argument('--work',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--precision',choices=['fp32','fp16','w8','w4'],default='fp16')
    a=p.parse_args();a.work=a.work.resolve();a.output=a.output.resolve()
    locked=json.loads(Path(__file__).with_name('source-lock.json').read_text())
    for name,row in locked['files'].items():
        with (a.source/name).open('rb') as stream:digest=hashlib.file_digest(stream,'sha256').hexdigest()
        if digest!=row['sha256']:raise ValueError('Source hash mismatch: '+name)
    a.work.mkdir(parents=True,exist_ok=True);a.output.mkdir(parents=True,exist_ok=True)
    flags={'fp32':[],'fp16':['--fp16'],'w8':['--weightQuantBits','8','--weightQuantBlock','64'],
           'w4':['--weightQuantBits','4','--weightQuantBlock','64']}[a.precision]
    lm=promote(onnx.load(a.source/'Hojo-TTS-Light-40M-llm.onnx'))
    graphs={'prefill':optimize_lm(lm,False),'decode':optimize_lm(lm,True)}
    for stage in ['fine_local','decoder']:
        graphs[stage]=promote(onnx.load(a.source/f'Hojo-TTS-Light-40M-{stage}.onnx'))
    for stage,model in graphs.items():
        path=a.work/(stage+'.onnx');onnx.save(model,path)
        convert(path,a.output/(stage+'.mnn'),flags,a.work)
    package_frontend(a.source,a.output)

if __name__=='__main__':main()
