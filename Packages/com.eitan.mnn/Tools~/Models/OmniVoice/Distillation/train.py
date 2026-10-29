"""Bounded offline student training; original checkpoint stays immutable.

CFG: KL to the guided teacher distribution at the same masked state.
Trajectory: hard labels from two teacher updates, plus 0.2 same-state CFG KL.
The latter is endpoint imitation, not a proven diffusion consistency method.
"""
import argparse
import json
from pathlib import Path
import random
import time
import torch
from modeling import load_model, set_target, target_logits, apply_patch, sha256
from objectives import MASK, masked_kl, trajectory_ce, codebook_mean


def examples(cache, split, objective):
    rows=[]
    for case in cache['cases']:
        if case['case']['split'] != split: continue
        for i,record in enumerate(case['records']):
            if objective=='trajectory' and i%2: continue
            endpoint=case['records'][i+1]['after'] if objective=='trajectory' else record['after']
            rows.append((case['inputs'],record,endpoint))
    if not rows: raise ValueError('Missing split examples: ' + split)
    return rows


def predict(model, example, device):
    inputs,record,endpoint=example
    ids=set_target(inputs,record['tokens']).to(device)
    logits=target_logits(model,ids,inputs['audio_mask'].to(device),inputs['frames'])
    return logits,record['teacher_logits'].to(device),(record['tokens']==MASK).to(device),endpoint.to(device)


def loss_for(model, example, objective, device):
    logits,target,mask,endpoint=predict(model,example,device)
    kl=masked_kl(logits,target,mask)
    if objective=='cfg': return kl
    committed=mask & (endpoint!=MASK)
    return trajectory_ce(logits,endpoint,committed) + 0.2*kl


@torch.no_grad()
def evaluate(model, rows, device):
    kl,agreement=[] ,[]
    for example in rows:
        logits,target,mask,_=predict(model,example,device)
        kl.append(masked_kl(logits,target,mask).item())
        matches=logits[...,:MASK].argmax(-1)==target[...,:MASK].argmax(-1)
        agreement.append(codebook_mean(matches.float(),mask).item())
    return {'mean_weighted_kl':sum(kl)/len(kl),'mean_weighted_argmax_agreement':sum(agreement)/len(agreement)}


def configure_trainable(model, count):
    if count<1 or count>len(model.llm.layers): raise ValueError('Invalid trainable layer count.')
    model.requires_grad_(False)
    for layer in model.llm.layers[-count:]: layer.requires_grad_(True)
    model.llm.norm.requires_grad_(True)
    model.audio_heads.requires_grad_(True)
    return {name:param for name,param in model.named_parameters() if param.requires_grad}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for key in ('upstream','model','cache','output'): parser.add_argument('--'+key,type=Path,required=True)
    parser.add_argument('--objective',choices=['cfg','trajectory'],default='cfg')
    parser.add_argument('--initial-patch',type=Path)
    parser.add_argument('--device',default='mps')
    parser.add_argument('--steps',type=int,default=128)
    parser.add_argument('--last-blocks',type=int,default=2)
    parser.add_argument('--learning-rate',type=float,default=2e-5)
    parser.add_argument('--seed',type=int,default=20261029)
    args=parser.parse_args()
    if args.steps<1 or args.learning_rate<=0: raise ValueError('Invalid training budget.')
    if args.output.exists(): raise FileExistsError('Use a fresh experiment output directory.')
    args.output.mkdir(parents=True)
    torch.manual_seed(args.seed)
    rng=random.Random(args.seed)
    cache=torch.load(args.cache,map_location='cpu',weights_only=True)
    if cache['kind']!='omnivoice-teacher-traces-v1' or sha256(args.model/'model.safetensors')!=cache['base_sha256']:
        raise ValueError('Teacher cache/base checkpoint mismatch.')
    model=load_model(args.upstream,args.model,args.device)
    if args.initial_patch: apply_patch(model,args.initial_patch,cache['base_sha256'])
    train=examples(cache,'train',args.objective)
    validation=examples(cache,'validation',args.objective)
    parameters=configure_trainable(model,args.last_blocks)
    optimizer=torch.optim.AdamW(list(parameters.values()),lr=args.learning_rate,weight_decay=0.01)
    report={'objective':args.objective,'device':args.device,'trainable_parameters':sum(p.numel() for p in parameters.values()),
        'scope':cache['scope'],'steps':args.steps,'learning_rate':args.learning_rate,'seed':args.seed,
        'last_blocks':args.last_blocks,'base_sha256':cache['base_sha256'],'cache_sha256':sha256(args.cache),
        'initial_patch_sha256':sha256(args.initial_patch) if args.initial_patch else None,'initial_validation':evaluate(model,validation,args.device),'history':[]}
    # Model stays eval to disable dropout while autograd remains enabled.
    with torch.no_grad():
        best_value=sum(loss_for(model,row,args.objective,args.device).item() for row in validation)/len(validation)
    report['initial_validation_objective']=best_value
    best_step=0
    initial={'kind':'omnivoice-distillation-patch-v1','base_sha256':cache['base_sha256'],'objective':args.objective,
        'teacher_steps':32,'student_steps':16 if args.objective=='trajectory' else 32,
        'selected_step':0,'state_dict':{name:param.detach().cpu().clone() for name,param in parameters.items()}}
    torch.save(initial,args.output/'best.pt')
    del initial
    start=time.perf_counter()
    pool=[]
    for step in range(1,args.steps+1):
        if not pool:
            pool=list(train); rng.shuffle(pool)
        example=pool.pop()
        optimizer.zero_grad(set_to_none=True)
        loss=loss_for(model,example,args.objective,args.device)
        if not torch.isfinite(loss): raise ValueError('Non-finite loss.')
        loss.backward()
        norm=torch.nn.utils.clip_grad_norm_(list(parameters.values()),1.0,error_if_nonfinite=True)
        optimizer.step()
        if step==1 or step%16==0 or step==args.steps:
            with torch.no_grad():
                val_loss=sum(loss_for(model,row,args.objective,args.device).item() for row in validation)/len(validation)
            metrics={'step':step,'training_loss':loss.item(),'validation_objective':val_loss,'gradient_norm':float(norm),'seconds':time.perf_counter()-start}
            report['history'].append(metrics)
            if val_loss<best_value:
                best_value,best_step=val_loss,step
                patch={'kind':'omnivoice-distillation-patch-v1','base_sha256':cache['base_sha256'],'objective':args.objective,
                    'teacher_steps':32,'student_steps':16 if args.objective=='trajectory' else 32,
                    'selected_step':step,'state_dict':{name:param.detach().cpu().clone() for name,param in parameters.items()}}
                torch.save(patch,args.output/'best.pt')
            (args.output/'metrics.json').write_text(json.dumps(report,indent=2)+'\n')
            print(json.dumps(metrics),flush=True)
    apply_patch(model,args.output/'best.pt',cache['base_sha256'])
    report['best_step']=best_step
    report['final_validation']=evaluate(model,validation,args.device)
    report['elapsed_seconds']=time.perf_counter()-start
    report['mps_allocated_bytes']=torch.mps.current_allocated_memory() if args.device=='mps' else None
    (args.output/'metrics.json').write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps(report['final_validation']),flush=True)


if __name__=='__main__': main()
