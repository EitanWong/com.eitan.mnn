#!/usr/bin/env python3
"""Compatibility entry point for the original-checkpoint ERes2NetV2 exporter."""
from prepare_3d_speaker import prepare as prepare_model, main as main_model


def prepare(checkpoint, source, converter, output):
    return prepare_model('eres2netv2', checkpoint, source, converter, output)


if __name__ == '__main__':
    main_model(default_model='eres2netv2')
