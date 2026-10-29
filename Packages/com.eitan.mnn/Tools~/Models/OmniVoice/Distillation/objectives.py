"""Discrete masked-token distillation; no flow/velocity or causal KV assumptions."""
import math
import torch
import torch.nn.functional as F

MASK = 1024
WEIGHTS = (8, 8, 6, 6, 4, 4, 2, 2)


def cfg_logits(conditional, unconditional, guidance=2.0):
    # Inner log-softmax constants cancel under the final log-softmax.
    return (1 + guidance) * conditional.float() - guidance * unconditional.float()


def codebook_mean(values, mask):
    if values.shape != mask.shape or values.ndim != 3 or values.shape[1] != 8:
        raise ValueError('Expected [batch, 8, frames] values and mask.')
    counts = mask.sum(dim=(0, 2))
    active = counts > 0
    if not bool(active.any()):
        raise ValueError('At least one supervised token is required.')
    means = (values * mask).sum(dim=(0, 2)) / counts.clamp_min(1)
    weights = torch.tensor(WEIGHTS, device=values.device, dtype=values.dtype) * active
    return (means * weights).sum() / weights.sum()


def masked_kl(student, teacher, mask, temperature=1.0):
    if student.shape != teacher.shape or student.shape[:-1] != mask.shape or temperature <= 0:
        raise ValueError('Mismatched KL shapes or temperature.')
    target = F.softmax(teacher.detach().float() / temperature, dim=-1)
    loss = F.kl_div(F.log_softmax(student.float() / temperature, -1), target, reduction='none').sum(-1)
    return codebook_mean(loss, mask) * temperature ** 2


def trajectory_ce(student, endpoint, newly_committed):
    loss = F.cross_entropy(student.float().reshape(-1, student.shape[-1]), endpoint.clamp_max(MASK - 1).reshape(-1), reduction='none')
    return codebook_mean(loss.reshape_as(endpoint), newly_committed)


def grouped_schedule(total, teacher_steps=32, student_steps=16):
    if total < 1 or teacher_steps < 1 or student_steps < 1 or teacher_steps % student_steps:
        raise ValueError('Student steps must divide teacher steps; positive token budget required.')
    def shift(time): return 0.1 * time / (1 - 0.9 * time)
    fine, remaining = [], total
    for step in range(teacher_steps):
        count = remaining if step == teacher_steps - 1 else min(math.ceil(total * (shift((step+1)/teacher_steps) - shift(step/teacher_steps))), remaining)
        fine.append(count)
        remaining -= count
    group = teacher_steps // student_steps
    return [sum(fine[i:i+group]) for i in range(0,teacher_steps,group)]


def advance(tokens, logits, count, generator):
    if count < 0 or count > int((tokens == MASK).sum()):
        raise ValueError('Unmask budget exceeds masked positions.')
    # Confidence includes the mask probability, exactly as the existing sampler.
    log_probs = F.log_softmax(logits.detach().float().cpu(), -1)
    confidence, codes = log_probs[..., :MASK].max(-1)
    layer = torch.arange(8).view(1,8,1)
    uniform = torch.rand(tokens.shape, generator=generator).clamp_min(1e-10)
    gumbel = -torch.log(-torch.log(uniform))
    score = (confidence - layer * 5.0) / 5.0 + gumbel
    score[tokens != MASK] = -float('inf')
    result = tokens.clone()
    if count:
        selected = score.flatten().topk(count).indices
        result.view(-1)[selected] = codes.flatten()[selected]
    return result
