"""Reuse the already converted official-MNN CPU codec for research WAVs."""
import argparse
import json
from pathlib import Path
import numpy as np
import soundfile as sf
import torch


def run_graph(root, graph, input_name, value, output_name, integer=False):
    import MNN
    runtime = MNN.nn.create_runtime_manager(({'backend':0, 'precision':1, 'numThread':4},))
    module = MNN.nn.load_module_from_file(str(root / (graph + '.mnn')), [input_name], [output_name], runtime_manager=runtime, rearrange=True)
    value = np.asarray(value, dtype=np.int32 if integer else np.float32)
    tensor = MNN.expr.const(value, list(value.shape), MNN.expr.NCHW, MNN.expr.int if integer else MNN.expr.float)
    return MNN.expr.convert(module.forward([tensor])[0], MNN.expr.NCHW).read().copy()


def encode(root, wav, output):
    samples, rate = sf.read(wav, dtype='float32')
    if rate != 24000 or samples.ndim != 1 or not np.isfinite(samples).all() or not 0.5 <= len(samples)/rate <= 15:
        raise ValueError('Reference must be finite 24 kHz mono, 0.5-15 seconds.')
    rms = float(np.sqrt(np.mean(samples.astype(np.float64)**2)))
    if 0 < rms < 0.1: samples *= 0.1 / rms
    samples = samples[:len(samples)//960*960]
    codes = run_graph(root,'higgs_encoder','audio',samples.reshape(1,1,-1),'audio_codes')
    if codes.ndim != 3 or codes.shape[:2] != (1,8) or np.any(codes<0) or np.any(codes>=1024):
        raise ValueError('Invalid reference codes.')
    output.parent.mkdir(parents=True, exist_ok=True)
    torch.save({'codes':torch.from_numpy(codes.astype(np.int64)), 'rms':rms}, output)


def decode(root, token_file, output):
    payload = torch.load(token_file, map_location='cpu', weights_only=True)
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for row in payload['results']:
        tokens = row['tokens']
        if tokens.shape[:2] != (1,8) or bool(((tokens<0)|(tokens>=1024)).any()):
            raise ValueError('Invalid generated codes.')
        wave = run_graph(root,'higgs_decoder','audio_codes',tokens.numpy(),'audio',True).reshape(-1)
        if not np.isfinite(wave).all(): raise ValueError('Non-finite waveform.')
        rms = payload['reference_rms']
        if rms < 0.1: wave *= rms / 0.1
        fade = min(2400,len(wave)//2)
        envelope=np.ones_like(wave)
        envelope[:fade]=np.linspace(0,1,fade); envelope[-fade:]=np.linspace(1,0,fade)
        # Keep outputs unpadded; RTF denominators must count actual codec audio.
        name = row['id'] + '-' + row['mode'] + '.wav'
        sf.write(output/name,wave*envelope,24000,subtype='PCM_16')
        records.append({k:v for k,v in row.items() if k!='tokens'} | {'wav':str((output/name).resolve()), 'codec_audio_seconds':len(wave)/24000})
    (output/'manifest.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n')


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['encode','decode'])
    parser.add_argument('--model',type=Path,required=True)
    parser.add_argument('--input',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    a=parser.parse_args()
    if a.mode=='encode': encode(a.model,a.input,a.output)
    else: decode(a.model,a.input,a.output)
