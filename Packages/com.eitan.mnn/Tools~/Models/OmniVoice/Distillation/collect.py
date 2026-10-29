"""Record same-state CFG targets and discrete two-step trajectory endpoints."""
import argparse
import json
from pathlib import Path
import time
import torch
from modeling import load_model, sha256, target_logits, set_target
from objectives import MASK, cfg_logits, advance, grouped_schedule


def read_cases(path):
    cases = json.loads(path.read_text())
    ids, texts = set(), set()
    for row in cases:
        if row['split'] not in ('train','validation','test') or row['id'] in ids:
            raise ValueError('Invalid split or duplicate ID.')
        normalized=''.join(row['text'].split())
        if not normalized or normalized in texts or not 0.4 <= row['seconds'] <= 15:
            raise ValueError('Text leakage or invalid duration.')
        ids.add(row['id']); texts.add(normalized)
    if {r['split'] for r in cases} != {'train','validation','test'}:
        raise ValueError('Require separate train, validation and test texts.')
    return cases


def prepare(model, case, reference, transcript):
    if model.text_tokenizer is None:
        raise RuntimeError('Attach the local tokenizer before preparation.')
    frames=int(case['seconds']*25)
    inputs=model._prepare_inference_inputs(case['text'],frames,transcript,
        reference['codes'][0],lang='zh',instruct=None,denoise=True)
    ids=inputs['input_ids'].cpu()
    if ids.shape[-1] > 1024: raise ValueError('Sequence exceeds deployed limit.')
    return {'input_ids':ids, 'audio_mask':inputs['audio_mask'].cpu(), 'frames':frames}


@torch.no_grad()
def collect_case(model, case, inputs, device, steps):
    frames=inputs['frames']
    tokens=torch.full((1,8,frames),MASK,dtype=torch.long)
    generator=torch.Generator().manual_seed(case['seed'])
    schedule=grouped_schedule(tokens.numel(),steps,steps)
    records=[]
    started=time.perf_counter()
    for step,count in enumerate(schedule):
        before=tokens.clone()
        ids=set_target(inputs,before).to(device)
        conditional=target_logits(model,ids,inputs['audio_mask'].to(device),frames)
        unconditional=target_logits(model,before.to(device),torch.ones((1,frames),dtype=torch.bool,device=device),frames)
        guided=cfg_logits(conditional,unconditional).cpu()
        tokens=advance(before,guided,count,generator)
        records.append({'step':step, 'tokens':before, 'teacher_logits':guided, 'after':tokens.clone()})
        if step%8==0: print(case['id'],'step',step,'seconds',round(time.perf_counter()-started,2),flush=True)
    print(case['id'],'DONE',round(time.perf_counter()-started,2),flush=True)
    return {'case':case,'inputs':inputs,'records':records,'final_tokens':tokens}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--upstream',type=Path,required=True)
    parser.add_argument('--model',type=Path,required=True)
    parser.add_argument('--reference',type=Path,required=True)
    parser.add_argument('--reference-text',required=True)
    parser.add_argument('--cases',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--device',default='mps')
    args=parser.parse_args()
    if args.output.exists(): raise FileExistsError('Use a new cache path; existing traces are immutable.')
    cases=read_cases(args.cases)
    reference=torch.load(args.reference,map_location='cpu',weights_only=True)
    model=load_model(args.upstream,args.model,args.device)
    from transformers import AutoTokenizer
    model.text_tokenizer=AutoTokenizer.from_pretrained(str(args.model.resolve()),local_files_only=True)
    payload={'kind':'omnivoice-teacher-traces-v1','teacher_steps':32,'guidance':2.,
        'base_sha256':sha256(args.model/'model.safetensors'),
        'reference_sha256':sha256(args.reference), 'reference_rms':reference['rms'],
        'scope':'Single-reference generated-teacher PoC; text-disjoint, not speaker-disjoint.',
        'cases':[]}
    for case in cases:
        inputs=prepare(model,case,reference,args.reference_text)
        payload['cases'].append(collect_case(model,case,inputs,args.device,32))
    args.output.parent.mkdir(parents=True,exist_ok=True)
    temporary=args.output.with_suffix('.tmp')
    torch.save(payload,temporary)
    temporary.replace(args.output)
    print('cache_bytes',args.output.stat().st_size,flush=True)


if __name__=='__main__': main()
