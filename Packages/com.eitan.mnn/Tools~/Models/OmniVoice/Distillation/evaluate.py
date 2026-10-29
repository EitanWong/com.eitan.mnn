"""Held-out free-running ablations; latency here is PyTorch, not MNN or mobile."""
import argparse
import json
from pathlib import Path
import time
import torch
from modeling import load_model, apply_patch, target_logits, set_target, sha256
from objectives import MASK, advance, cfg_logits, grouped_schedule
from train import examples, evaluate as evaluate_states


def synchronize(device):
    if device=='mps': torch.mps.synchronize()
    elif device.startswith('cuda'): torch.cuda.synchronize()


@torch.no_grad()
def synthesize(model, inputs, seed, count, guided, device):
    frames=inputs['frames']
    tokens=torch.full((1,8,frames),MASK,dtype=torch.long)
    generator=torch.Generator().manual_seed(seed)
    budgets=grouped_schedule(tokens.numel(),32,count)
    synchronize(device)
    start=time.perf_counter()
    for budget in budgets:
        conditional=target_logits(model,set_target(inputs,tokens).to(device),inputs['audio_mask'].to(device),frames)
        if guided:
            unconditional=target_logits(model,tokens.to(device),torch.ones((1,frames),dtype=torch.bool,device=device),frames)
            logits=cfg_logits(conditional,unconditional)
        else:
            logits=conditional
        tokens=advance(tokens,logits,budget,generator)
    synchronize(device)
    seconds=time.perf_counter()-start
    if bool((tokens==MASK).any()): raise RuntimeError('Incomplete generation.')
    return tokens,seconds


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for key in ('upstream','model','cache','cfg-patch','trajectory-patch','output'):
        parser.add_argument('--'+key,type=Path,required=True)
    parser.add_argument('--device',default='mps')
    args=parser.parse_args()
    if args.output.exists(): raise FileExistsError('Use a new evaluation path.')
    cache=torch.load(args.cache,map_location='cpu',weights_only=True)
    if cache['base_sha256']!=sha256(args.model/'model.safetensors'): raise ValueError('Wrong base model.')
    cases=[row for row in cache['cases'] if row['case']['split']=='test']
    model=load_model(args.upstream,args.model,args.device)
    # Make one warmup per sequence shape; times still exclude codec/reference.
    modes=[('teacher32',None,32,True),('teacher16',None,16,True),('single32',None,32,False),
        ('cfg32',args.cfg_patch,32,False),('cfg16',args.cfg_patch,16,False),
        ('trajectory16',args.trajectory_patch,16,False)]
    output={'kind':'omnivoice-free-running-evaluation-v1','reference_rms':cache['reference_rms'],
        'scope':cache['scope'],'test_states':{},'results':[]}
    current=None
    for mode,patch,steps,guided in modes:
        if patch is not None and current!=patch:
            apply_patch(model,patch,cache['base_sha256']); current=patch
        if mode in ('single32','cfg32','trajectory16'):
            output['test_states'][mode]=evaluate_states(model,examples(cache,'test','cfg'),args.device)
        for case in cases:
            inputs=case['inputs']
            with torch.no_grad():
                target_logits(model,inputs['input_ids'].to(args.device),inputs['audio_mask'].to(args.device),inputs['frames']).cpu()
            tokens,seconds=synthesize(model,inputs,case['case']['seed'],steps,guided,args.device)
            match=(tokens==case['final_tokens']).float().mean().item()
            row={'id':case['case']['id'],'text':case['case']['text'],'mode':mode,'steps':steps,'guided':guided,
                'lm_seconds':seconds,'lm_rtf_without_padding':seconds/(inputs['frames']/25),
                'teacher_token_agreement':match,'tokens':tokens}
            output['results'].append(row)
            print(json.dumps({k:v for k,v in row.items() if k!='tokens'},ensure_ascii=False),flush=True)
    torch.save(output,args.output)
    report={k:v for k,v in output.items() if k!='results'}
    report['results']=[{k:v for k,v in row.items() if k!='tokens'} for row in output['results']]
    args.output.with_suffix('.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')


if __name__=='__main__': main()
