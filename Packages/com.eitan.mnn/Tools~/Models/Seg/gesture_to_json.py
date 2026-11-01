#!/usr/bin/env python3
"""Convert existing author-compatible SEG NumPy gesture features to Studio JSON."""
import argparse
import json
from pathlib import Path
import numpy as np


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    values = np.load(args.input, allow_pickle=False)
    if values.ndim < 2 or values.shape[0] < 1 or values.shape[0] > 150:
        raise ValueError('Expected 1–150 frame-major author-compatible frames')
    values = values.reshape(values.shape[0], -1).astype(np.float32)
    if values.shape[1] != 30 or not np.isfinite(values).all():
        raise ValueError('Expected 30 finite features per frame')
    data = dict(format='SEG-YGD-gesture-v1', fps=15, feature_size=30, values=values.reshape(-1).tolist())
    args.output.write_text(json.dumps(data, allow_nan=False) + '\n')


if __name__ == '__main__':
    main()
