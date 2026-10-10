#!/usr/bin/env python3
"""Create deterministic color images and local speech fixtures; no downloads/dependencies.
Usage: python3 prepare_multimodal_fixtures.py /project/TestArtifacts~/MNNValidation/Multimodal/Fixtures
Speech requires macOS's installed Samantha voice, say and afconvert.
"""
import argparse
from pathlib import Path
import struct
import subprocess
import zlib


def png(path, rgb):
    def chunk(kind, payload):
        return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload) & 0xffffffff)
    pixels = (b'\x00' + bytes(rgb) * 512) * 512
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', 512, 512, 8, 2, 0, 0, 0)) +
                     chunk(b'IDAT', zlib.compress(pixels)) + chunk(b'IEND', b''))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    output = parser.parse_args().output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    png(output / 'red.png', (255, 0, 0))
    png(output / 'blue.png', (0, 0, 255))
    subprocess.run(['say', '-v', 'Samantha', '-r', '145', '-o', str(output / 'speech.aiff'),
                    'The capital of France is Paris.'], check=True)
    subprocess.run(['afconvert', '-f', 'WAVE', '-d', 'LEI16@16000', str(output / 'speech.aiff'),
                    str(output / 'speech.wav')], check=True)
    print(output)


if __name__ == '__main__':
    main()
