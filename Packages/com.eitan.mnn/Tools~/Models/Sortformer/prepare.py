#!/usr/bin/env python3
"""Reproduce original-checkpoint Sortformer v2.1 FP32 graphs using local official sources and converter. No network or native-code additions."""
from pathlib import Path
import argparse,hashlib,importlib.util,json,subprocess,sys,tarfile,uuid
import numpy as np
import torch
import onnxruntime as ort
from original import load
sys.dont_write_bytecode=True
SOURCE_HASHES={'nemo/collections/common/parts/transformer_utils.py': '47f5e337230e7b4e176877f01c2ae85f75c024942dc567f27d8429c3e60e67c0', 'nemo/collections/common/parts/__init__.py': '602520caf0f9fe04183e54e897b8086d9a8e397d3b921386f9df639073e40130', 'nemo/collections/asr/models/configs/asr_models_config.py': 'f8b0d2e98ab4fefc65a01d98222c97e61a7bdac63e969e830a4325eb77c59464', 'nemo/collections/common/parts/utils.py': '502b87e1c15f430f31584960c24f7d026ebb26f908b205ff70d8da3f8d455fd2', 'nemo/collections/asr/parts/utils/regularization_utils.py': '4a9897a29ae0d459a4c722de9a25af4315b2811fbe320842e525a9eb54b3d709', 'nemo/collections/asr/parts/utils/activations.py': 'ef4ef29e6341128f220db3240337cb1483af31f81c0ca0c58d80966e349aecc8', 'nemo/collections/asr/models/configs/__init__.py': '0836a1164b4b7dcaf80e06a6a242eccd45bfbac175ab240e11f756e94fa85b5c', 'nemo/collections/asr/parts/submodules/batchnorm.py': '85e7c3b8919b116d7519ad66b5142f007159bd2d71da949f5a61ff03e8e02453', 'nemo/collections/asr/models/sortformer_diar_models.py': '4a30bfcd7ee221907f01500eb3a17f961021a69595b6898be554654af29e3dd6', 'nemo/collections/asr/modules/sortformer_modules.py': '3d136c245e3bf7a88c47fdd2eae1edb9189bbeddc3ff779cb5679a29d890b7eb', 'nemo/collections/asr/modules/conformer_encoder.py': 'c7d615e1398484939db5bf8472423f59fef73eece9ee71e089089a97b5344f70', 'nemo/collections/asr/modules/transformer/transformer_encoders.py': 'a2859c86c8389f1954d5c8be04dc2bc422452517ef15e069cf42bfab5d304759', 'nemo/collections/asr/modules/transformer/transformer_modules.py': '2564d95365cfafd486b1a3d10e2e2f438702907076f3716dd4c42d568b3bcc72', 'nemo/collections/asr/parts/submodules/conformer_modules.py': 'eb6fa21ff503817577c0b8c90dc85f9e6ed5df1d3a8dfd3b1fd32bc85142738d', 'nemo/collections/asr/parts/submodules/multi_head_attention.py': '5891e68f35ff21ce869c796ce883bcdcee5460ea229c81b8a1755665c7f14929', 'nemo/collections/asr/parts/submodules/subsampling.py': 'a7a8d1be9812a17873b980058e7d2e40721c9d0c64f70761f77b01145f8ede05', 'nemo/collections/asr/parts/submodules/causal_convs.py': '1271e9d4e42c57025101d52c07c6ec5e5912c5ac0197284b5ace40478105f46c', 'nemo/collections/asr/parts/preprocessing/features.py': '1f879ea5272430b7ec559bfd68567d1cf475a3cac0746836efd96248f6c8d236', 'nemo/collections/asr/modules/audio_preprocessing.py': '6c4c4aa7ae28b01e192f5f20edc7b7f37972cabdd0ac35e5d8d5242a8c2badd1', 'nemo/collections/asr/parts/utils/asr_confidence_utils.py': '7597d6fdf410340cc75c79aca0482adf32f05ac883d874ddbe71ea65973ad141', 'LICENSE': '43070e2d4e532684de521b885f385d0841030efa2b1a20bafb76133a5e1379c1'}
CHECKPOINT_SHA='8abd32832159c6ac1148c926b7276f35ba34582c444e559dce1f1253fea42ef8'
REVISION='cd03eee90fbec18297ac31b8c21546e596b7f71c'

def digest(path):
    with path.open('rb') as source:return hashlib.file_digest(source,'sha256').hexdigest()

def canonical_uuid(path,graph):
    spec=importlib.util.spec_from_file_location('mnn_schema',Path(__file__).resolve().parents[1]/'deduplicate_mnn_external_weights.py')
    schema=importlib.util.module_from_spec(spec);spec.loader.exec_module(schema)
    model=schema.Model(path.read_bytes());field=model.field(model.integer(0),11)
    if not field:raise ValueError('Missing public Net.mnn_uuid')
    position=field+model.integer(field)
    if model.integer(position)!=36:raise ValueError('Invalid public UUID')
    value=str(uuid.uuid5(uuid.NAMESPACE_URL,'nvidia/diar_streaming_sortformer_4spk-v2.1/'+REVISION+'/'+graph)).encode()
    model.data[position+4:position+40]=value;path.write_bytes(model.data)
    return value.decode()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ['source','checkpoint','converter','output']:parser.add_argument('--'+name,type=Path,required=True)
    args=parser.parse_args()
    for name,expected in SOURCE_HASHES.items():
        if digest(args.source/name)!=expected:raise ValueError('Original source differs: '+name)
    if digest(args.checkpoint)!=CHECKPOINT_SHA:raise ValueError('Original checkpoint differs')
    args.output.mkdir(parents=True,exist_ok=True)
    with tarfile.open(args.checkpoint) as archive:
        entry=archive.getmember('model_config.yaml');config_path=args.output/'original-model-config.yaml';config_path.write_bytes(archive.extractfile(entry).read())
    torch.set_num_threads(2);model,frontend,config=load(args.source,args.checkpoint,config_path)
    if (config.sample_rate,config.max_num_of_spks,config.encoder.feat_in,config.nemo_version)!=(16000,4,128,'2.6.0rc0'):raise ValueError('Unexpected checkpoint protocol')
    class PreEncode(torch.nn.Module):
        def __init__(self):super().__init__();self.pre_encode=model.encoder.pre_encode
        def forward(self,features,lengths):return self.pre_encode(features,lengths)
    class Core(torch.nn.Module):
        def __init__(self):super().__init__();self.encoder=model.encoder;self.transformer_encoder=model.transformer_encoder;self.sortformer_modules=model.sortformer_modules;self.spec_augmentation=None
        def forward(self,embeddings,lengths):
            embs,lens=model.frontend_encoder(embeddings,lengths,bypass_pre_encode=True)
            return model.forward_infer(embs,lens)
    pre=PreEncode().eval();core=Core().eval();rng=np.random.default_rng(20261031);cases=[]
    with torch.no_grad():
        for size,cached,fifo in [(1,0,0),(8,0,0),(15,0,0),(16,0,0),(17,0,0),(40,0,0),(40,10,4),(40,188,40),(240,188,40)]:
            x=rng.normal(0,.5,(1,size,128)).astype(np.float32);cache=rng.normal(0,.2,(1,cached,512)).astype(np.float32);queue=rng.normal(0,.2,(1,fifo,512)).astype(np.float32)
            tx=torch.from_numpy(x.copy());length=torch.tensor([size],dtype=torch.int32)
            expected=model.forward_for_export(tx,length,torch.from_numpy(cache),torch.tensor([cached]),torch.from_numpy(queue),torch.tensor([fifo]));encoded,valid=pre(tx,length)
            combined=torch.cat([torch.from_numpy(cache),torch.from_numpy(queue),encoded],1);predictions=core(combined,torch.tensor([combined.shape[1]],dtype=torch.int32))
            if not torch.equal(expected[0],predictions) or not torch.equal(expected[1],encoded):raise ValueError('Split graph changes original forward_for_export')
            cases.append((x,encoded.numpy(),combined.numpy(),predictions.numpy()))
        torch.onnx.export(pre,(torch.zeros(1,40,128),torch.tensor([40],dtype=torch.int32)),args.output/'preencode_dynamic.onnx',opset_version=17,dynamo=False,input_names=['features','lengths'],output_names=['preencoded','preencoded_lengths'],dynamic_axes={'features':{1:'feature_frames'},'preencoded':{1:'preencoded_frames'}})
        torch.onnx.export(core,(torch.zeros(1,5,512),torch.tensor([5],dtype=torch.int32)),args.output/'core_dynamic.onnx',opset_version=17,dynamo=False,input_names=['embeddings','lengths'],output_names=['predictions'],dynamic_axes={'embeddings':{1:'core_frames'},'predictions':{1:'core_frames'}})
    options=ort.SessionOptions();options.intra_op_num_threads=2
    pre_session=ort.InferenceSession(str(args.output/'preencode_dynamic.onnx'),options,providers=['CPUExecutionProvider']);core_session=ort.InferenceSession(str(args.output/'core_dynamic.onnx'),options,providers=['CPUExecutionProvider']);parity=[]
    for x,encoded,combined,predictions in cases:
        a,lens=pre_session.run(None,dict(features=x,lengths=np.array([x.shape[1]],np.int32)))
        b=core_session.run(None,dict(embeddings=combined,lengths=np.array([combined.shape[1]],np.int32)))[0]
        err=float(np.linalg.norm(a-encoded)/max(np.linalg.norm(encoded),1e-20));prob=float(abs(b-predictions).max())
        if not np.isfinite([err,prob]).all() or err>=.001 or prob>=.001 or int(lens[0])!=encoded.shape[1]:raise ValueError('Original/ONNX source parity failed')
        parity.append(dict(feature_frames=x.shape[1],core_frames=combined.shape[1],preencode_nrmse=err,prediction_max_absolute_error=prob))
    graphs=[]
    for name in ['preencode','core']:
        target=args.output/(name+'_fp32.mnn');command=[str(args.converter),'-f','ONNX','--modelFile',str(args.output/(name+'_dynamic.onnx')),'--MNNModel',str(target),'--bizCode','Sortformer4spk21','--optimizeLevel','1','--optimizePrefer','2','--transformerFuseC4','0','--transformerFuseQkvProj','0','--transformerFuseGateUpProj','0','--transformerFuseLnProj','0']
        with (args.output/(name+'-conversion.log')).open('w') as log:subprocess.run(command,check=True,stdout=log,stderr=subprocess.STDOUT)
        value=canonical_uuid(target,name);graphs.append(dict(graph=target.name,bytes=target.stat().st_size,sha256=digest(target),public_uuid=value,converter_arguments=command))
    window=frontend['preprocessor.featurizer.window'].numpy().reshape(-1);filters=frontend['preprocessor.featurizer.fb'].numpy().reshape(-1)
    profile=dict(repository='nvidia/diar_streaming_sortformer_4spk-v2.1',revision=REVISION,nemo_version='2.6.0',checkpoint_nemo_version='2.6.0rc0',preencode_graph='preencode_fp32.mnn',core_graph='core_fp32.mnn',sample_rate=16000,speakers=4,mel_bins=128,chunk_frames=188,cache_frames=188,maximum_samples=960000,analysis_window=window.tolist(),mel_filters=filters.tolist())
    (args.output/'mnn_sortformer.json').write_text(json.dumps(profile,indent=2)+'\n')
    report=dict(source_hashes=SOURCE_HASHES,checkpoint_sha256=CHECKPOINT_SHA,source_revision='4d313cf5f097529420f27278b28a569e15190b1b',source_tag='v2.6.0',checkpoint_nemo_version='2.6.0rc0',framework_adaptation='Lightning/training scaffolding omitted; metadata typecheck/no-adapter/no-access/FP32 autocast guards only; unchanged original mathematical classes/forwards',split_forward='bit-identical across nine feature/cache/FIFO conditions',onnx_parity=parity,graphs=graphs,coefficient_sha256=hashlib.sha256(window.astype('<f4').tobytes()+filters.astype('<f4').tobytes()).hexdigest())
    (args.output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
