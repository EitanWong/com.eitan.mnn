#!/usr/bin/env python3
"""Small-sample endpoint distillation: refine two-step Flow toward ten-step mel.

The original Flow estimator stays frozen. This trains a small residual mel
refiner, not a replacement AR model or a fully distilled Flow estimator.
Sentence-disjoint train/dev/test splits are collected by the Unity fixture.
No candidate is accepted based on training loss or RTF alone.
"""
import argparse
import json
from pathlib import Path
import time

import numpy as np
import torch
from torch import nn


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ['data', 'work', 'models']:
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    args.work.mkdir(parents=True, exist_ok=True)
    torch.set_num_threads(2)
    torch.manual_seed(20261028)
    rng = np.random.default_rng(20261028)
    data = {split: [] for split in ['train', 'dev', 'test']}
    records = []
    for split in data:
        for path in sorted(args.data.glob(split + '-*.input.bin')):
            prefix = str(path)[:-len('.input.bin')]
            x = np.fromfile(path, np.float32).reshape(1, 80, -1)
            y = np.fromfile(prefix + '.target.bin', np.float32).reshape(1, 80, -1)
            assert x.shape == y.shape and np.isfinite(x).all() and np.isfinite(y).all()
            metadata = json.loads(Path(prefix + '.json').read_text())
            data[split].append((torch.from_numpy(x.copy()), torch.from_numpy(y.copy())))
            records.append(dict(split=split, prefix=prefix, **metadata))
    assert [len(data[key]) for key in data] == [16, 8, 8], 'Require all fixed 32 pairs before training'
    means = torch.cat([x for x, _ in data['train']], -1).mean((0, 2)).reshape(1, 80, 1)
    scales = torch.cat([x for x, _ in data['train']], -1).std((0, 2)).clamp_min(.1).reshape(1, 80, 1)

    class Refiner(nn.Module):
        def __init__(self):
            super().__init__()
            self.register_buffer('mean', means)
            self.register_buffer('scale', scales)
            self.register_buffer('strength', torch.ones(1))
            self.net = nn.Sequential(nn.Conv1d(80, 64, 1), nn.SiLU(),
                nn.Conv1d(64, 64, 3, padding=1, groups=64), nn.SiLU(), nn.Conv1d(64, 80, 1))
            nn.init.zeros_(self.net[-1].weight); nn.init.zeros_(self.net[-1].bias)

        def forward(self, mel):
            return mel + self.strength * self.scale * self.net((mel - self.mean) / self.scale)

    model = Refiner()
    optimizer = torch.optim.AdamW(model.parameters(), lr=.001, weight_decay=.01)

    @torch.no_grad()
    def score(split):
        error = baseline = energy = 0.
        for x, y in data[split]:
            actual = model(x)
            error += float(((actual - y) ** 2).sum())
            baseline += float(((x - y) ** 2).sum())
            energy += float((y ** 2).sum())
        return dict(relative_rmse=(error / energy) ** .5, baseline_relative_rmse=(baseline / energy) ** .5,
                    mse_ratio_vs_two_step=error / baseline)

    before = {split: score(split) for split in ['train', 'dev']}
    best = float('inf')
    history = []
    start = time.perf_counter()
    for step in range(1200):
        model.train()
        xs, ys = [], []
        for _ in range(8):
            x, y = data['train'][int(rng.integers(len(data['train'])))]; width = min(96, x.shape[-1])
            at = int(rng.integers(x.shape[-1] - width + 1))
            xx, yy = x[:, :, at:at + width], y[:, :, at:at + width]
            if width < 96:
                xx = torch.nn.functional.pad(xx, (0, 96 - width), mode='replicate')
                yy = torch.nn.functional.pad(yy, (0, 96 - width), mode='replicate')
            xs.append(xx); ys.append(yy)
        x, y = torch.cat(xs), torch.cat(ys)
        prediction = model(x)
        loss = (((prediction - y) / scales) ** 2).mean()
        optimizer.zero_grad(); loss.backward(); optimizer.step()
        if step % 40 == 0 or step == 1199:
            model.eval(); dev = score('dev')['mse_ratio_vs_two_step']
            history.append(dict(step=step, loss=float(loss.detach()), dev_mse_ratio=dev))
            if dev < best:
                best = dev
                torch.save(model.state_dict(), args.work / 'best.pt')
    model.load_state_dict(torch.load(args.work / 'best.pt', weights_only=True))
    model.eval()
    strengths = []
    for strength in [0., .25, .5, 1.]:
        model.strength.fill_(strength)
        strengths.append(dict(strength=strength, **score('dev')))
    selected = min(strengths, key=lambda row: row['mse_ratio_vs_two_step'])['strength']
    model.strength.fill_(selected)
    results = {split: score(split) for split in data}  # Test is evaluated only after all choices are fixed.
    destination = args.models / 'experimental-flow-distill'
    destination.mkdir(exist_ok=True)
    onnx = args.work / 'mel_refiner.onnx'
    torch.onnx.export(model, data['train'][0][0], str(onnx), input_names=['mel'], output_names=['refined_mel'],
                      dynamic_axes={'mel': {2: 'frames'}, 'refined_mel': {2: 'frames'}}, opset_version=17, dynamo=False)
    for row, (x, _) in zip([r for r in records if r['split'] == 'test'], data['test']):
        model(x).detach().numpy().astype(np.float32).tofile(args.work / (Path(row['prefix']).name + '.torch.bin'))
    report = dict(scope='two frozen Flow steps + trained mel endpoint refiner; not a complete distilled estimator or AR model',
                  seed=20261028, parameters=sum(p.numel() for p in model.parameters()), steps=1200,
                  training_seconds=time.perf_counter() - start, records=records, before=before, history=history,
                  strength_choices_dev=strengths, selected_strength=selected, results=results,
                  quality_accepted=False, limitations='single built-in speaker, 16 sentences, no MOS or broad generalization claim')
    (args.work / 'training.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({k: v for k, v in report.items() if k not in ['records', 'history']}, indent=2))


if __name__ == '__main__':
    main()
