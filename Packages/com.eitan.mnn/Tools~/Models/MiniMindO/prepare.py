#!/usr/bin/env python3
"""Prepare the complete MiniMind-o installation with pinned source hashes and official MNN tools.

No C++ is compiled or modified. Existing validated assets are reused. Downloads
require --download; Python dependencies must be provisioned separately.
"""
import argparse,hashlib,json,os,shutil,subprocess,sys,urllib.request
from pathlib import Path


def sha(path):
    value=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):value.update(block)
    return value.hexdigest()


def fetch(path,item,download):
    if path.is_file() and sha(path)==item['sha256']:return
    if not download:raise ValueError('Missing or mismatched pinned input: '+str(path)+'; use --download to retrieve it')
    path.parent.mkdir(parents=True,exist_ok=True);temporary=path.with_name(path.name+'.part')
    urllib.request.urlretrieve(item['url'],temporary)
    if sha(temporary)!=item['sha256'] or 'bytes' in item and temporary.stat().st_size!=item['bytes']:
        raise ValueError('Downloaded input failed its source lock: '+str(path))
    temporary.replace(path)


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--reference',type=Path,required=True);p.add_argument('--work',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True);p.add_argument('--download',action='store_true')
    p.add_argument('--verify-only',action='store_true',help='Verify installed hashes without downloading or converting')
    a=p.parse_args();a.reference=a.reference.resolve();a.work=a.work.resolve();a.output=a.output.resolve()
    tools=Path(__file__).resolve().parent
    if a.verify_only:
        manifest=json.loads((a.output/'files.sha256.json').read_text())
        for name,item in manifest.items():
            path=a.output/name
            if not path.is_file() or sha(path)!=item['sha256']:raise ValueError('Installed file hash mismatch: '+str(path))
        print('VERIFIED_INSTALL='+str(a.output));return
    a.work.mkdir(parents=True,exist_ok=True);a.output.mkdir(parents=True,exist_ok=True)
    lock=json.loads((tools/'source-lock.json').read_text())
    for item in lock['files']:fetch(a.reference/item['path'],item,a.download)
    source=a.work/'sensevoice_model.py';fetch(source,lock['sensevoice_source'],a.download)
    # Serialize conversion. The official converter uses working-directory temporary files.
    def run(name,args):
        command=[sys.executable,str(tools/name)]+[str(x) for x in args]
        result=subprocess.run(command,capture_output=True,text=True,cwd=a.work)
        with (a.work/'executions.jsonl').open('a') as log:
            log.write(json.dumps({'command':command,'stdout':result.stdout,'stderr':result.stderr,'exit_status':result.returncode})+'\n')
        if result.returncode:raise RuntimeError(result.stderr[-3000:])
        print(name+' PASS',flush=True)
    onnx=a.work/'onnx'
    run('export_core.py',['--reference',a.reference,'--work',onnx])
    run('convert_core.py',['--onnx-directory',onnx,'--work',a.work/'core-conversion','--output',a.output])
    for stage,graph in [('mimi','mimi_decoder'),('vision','vision_encoder'),('audio','audio_encoder'),('prefill','multimodal_prefill')]:
        args=['--reference',a.reference,'--work',onnx,'--stage',stage]
        if stage=='audio':args+=['--sensevoice-source',source]
        run('export_components.py',args)
        run('convert_components.py',['--onnx-directory',onnx,'--output',a.output,'--stage',graph])
        run('check_components.py',['--onnx-directory',onnx,'--model',a.output,'--stage',graph,'--report',a.work/(graph+'-check.json')])
    shutil.copy2(a.reference/'tokenizer.json',a.output/'tokenizer.json')
    (a.output/'mnn_minimind_o.json').write_text(json.dumps({'schema_version':1,'model_type':'minimind-o','core_revision':lock['revision'],
        'recipe':'FP16 prefill; Thinker W8 asymmetric block64; Talker/heads and auxiliary components FP16',
        'native_full_duplex':False,'sample_rate':24000,'codebooks':8,'max_context_tokens':512},indent=2)+'\n')
    manifest={p.name:{'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(a.output.iterdir()) if p.suffix!='.meta' and p.is_file() and p.name!='files.sha256.json'}
    (a.output/'files.sha256.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print('INSTALLED='+str(a.output))

if __name__=='__main__':main()
