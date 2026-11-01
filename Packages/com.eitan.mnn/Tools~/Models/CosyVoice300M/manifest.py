#!/usr/bin/env python3
"""Write graph contracts and SHA256 checksums for the converted assets."""
import argparse
import json
from pathlib import Path

from fetch import digest, SOURCES
from runtime import RUNTIME_REVISION

CONTRACTS = {
    'text_encoder': {'inputs': {'tokens': 'int32[1,T]', 'lengths': 'int32[1]'}, 'outputs': {'encoded': 'float32[1,T,1024]'}},
    'llm_cached_projected': {'inputs': {'embeddings': 'float32[1,Q,1024]', 'cache': 'float32[14,16,P,128]',
        'positions': 'float32[14,16,2*(P+Q)-1,64]', 'mask': 'int32[1,Q,P+Q]'},
        'outputs': {'logits': 'float32[1,4097]', 'updated_cache': 'float32[14,16,P+Q,128]'}},
    'flow_encoder': {'inputs': {'tokens': 'int32[1,N]', 'lengths': 'int32[1]'}, 'outputs': {'encoded': 'float32[1,N,80]'}},
    'flow_regulator': {'inputs': {'features': 'float32[1,80,F]'}, 'outputs': {'mu': 'float32[1,80,F]'}},
    'flow_estimator': {'inputs': {'x': 'float32[2,80,F]', 'mask': 'float32[2,1,F]', 'mu': 'float32[2,80,F]',
        't': 'float32[2]', 'spks': 'float32[2,80]', 'cond': 'float32[2,80,F]'}, 'outputs': {'estimator_out': 'float32[2,80,F]'}},
    'hift_f0': {'inputs': {'mel': 'float32[1,80,F]'}, 'outputs': {'f0': 'float32[1,F]'}},
    'hift_core': {'inputs': {'mel': 'float32[1,80,F]', 'source_stft': 'float32[1,18,64*F+1]'}, 'outputs': {'spectrum': 'float32[1,18,64*F+1]'}},
    'campplus': {'inputs': {'input': 'float32[1,frames,80]'}, 'outputs': {'output': 'float32[1,192]'}},
    'speech_tokenizer': {'inputs': {'feats': 'float32[1,128,frames]', 'feats_length': 'int32[1]'}, 'outputs': {'indices': 'int32[1,1,tokens]'}},
}


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--models', type=Path, required=True)
    a = p.parse_args()
    root = a.models
    data = {'model': 'iic/CosyVoice-300M', 'generation': 1, 'sample_rate_hz': 22050, 'speech_token_rate_hz': 50,
            'mnn_target': '3.6.1', 'onnx_opset': 17, 'license': 'Apache-2.0 (upstream)',
            'source_code_revision': 'd2dea3d92846b17e89e25513e855caed64bfc2f5',
            'graphs': CONTRACTS, 'max_context_including_prompt_and_sentinel': 2048,
            'cache': {'initial_P': 1, 'sentinel_masked': True, 'layout_last_dimension': 'K64,V64'},
            'pipeline': {'batch_size': 1, 'offline_utterance': True, 'text_tokenizer': 'Whisper multilingual 100-language',
                         'runtime_revision': RUNTIME_REVISION,
                         'sampling': 'official RAS: top-p 0.8 / exact partial top-k 25, stable descending order with token-index ties; inverse-CDF categorical draws verified against NumPy2.5.4 choice token/RNG states; recent10 repeat fallback reuses unchanged full distribution; legacy flag reproduces earlier exclusion behavior',
                         'generation_limit': 'default max_token_text_ratio=20; explicit 40 tested for short English; natural EOS required, never force truncate',
                         'flow': 'classifier-free guidance 0.7; cosine Euler; 10-step baseline; 5/3/2-step experiments; guidance0 changes conditioning and quality',
                         'experimental_reference': 'prompt-1.0-fullspeaker.npz: original first 1s speech/mel/text with full 3.48s speaker embedding; one speaker only',
                         'gpu_tuning_cache': 'optional official RuntimeManager set_cache/update_cache; device/runtime-specific test artifacts; not model assets',
                         'dsp': 'harmonic source + STFT(n16,hop4) + neural HiFT + ISTFT; see runtime.py',
                         'cached_reference_preprocessing': 'official ONNX Runtime frontends; not timed in synthesis'},
            'profiles': {'fp16': 'baseline weights; computation precision and backend must be validated per device',
                         'int8': 'experimental text encoder and LLM only; other graphs reuse fp16; content differences observed',
                         'mixed8': 'experimental FP16 text encoder + INT8 LLM; other graphs fp16',
                         'int4': 'REJECTED: audible content corruption; retained for reproducibility only'},
            'platform_validation': {'macOS_arm64': 'end-to-end Python official MNN bindings on Apple M5',
                                    'Android_arm64': 'complete device-local Python official MNN bindings on OPPO PELM00; CPU and rejected GPU configurations measured; RTF>0.3; not Unity or quality acceptance',
                                    'Windows': 'not executed', 'iOS': 'not executed'},
            'unity_integration': 'not integrated; existing CosyVoice3 runner is incompatible; no additional C++/bridge written',
            'source_assets': {}, 'files': []}
    for name, (revision, sha) in SOURCES.items():
        data['source_assets']['source/' + name] = {'sha256': sha, 'url': 'https://modelscope.cn/api/v1/models/iic/CosyVoice-300M/repo?Revision=' + revision + '&FilePath=' + name}
    data['source_assets']['source/flow_estimator.onnx'] = {
        'url': 'https://huggingface.co/FunAudioLLM/CosyVoice-300M/resolve/24c40509c3c5ea6fe06b5f8790ff99e3714a6bee/flow.decoder.estimator.fp32.onnx',
        'sha256': digest(root / 'source/flow_estimator.onnx')}
    config_path = root / 'experimental-decode/config.json'
    if config_path.exists():
        config = json.loads(config_path.read_text())
        data['decode_profiles'] = config
        data['decode_profiles']['default'] = 'baseline'
        data['decode_profiles']['validation_platform'] = (
            'Apple M5 complete Python TTS; OPPO PELM00 complete Python TTS only for minmax64 fixedshape, '
            'its Key-transposed variant and HQQ128 fixedshape; Android fixed-input logits checks '
            'against M5 failed and OpenCL warm audio failed ASR; no general audio acceptance; '
            'Windows/iOS untested. See TestArtifacts~/CosyVoice300M/RTF_TARGET_STUDY.md.')
        data['decode_profiles']['memory'] = 'normal=0; low=2 applies only to the dedicated decode module; text and prefill remain normal memory FP16'
        for profile, filename in config.items():
            if not isinstance(filename, str) or not filename.endswith('.mnn') or profile == 'prefill':
                continue
            contract = config.get('decode_contracts', {}).get(profile)
            if contract:
                capacity = contract.get('capacity')
                past = str(capacity) if capacity else 'P'
                total = str(capacity + 1) if capacity else 'P+1'
                inputs = {'embeddings': 'float32[1,1,1024]',
                          'positions': 'int32[1]' if contract.get('internal_positions') else 'float32[14,16,' + total + ',64]',
                          'mask': 'int32[1,1,' + total + ']'}
                if 'mask' not in contract['inputs']:
                    del inputs['mask']
                outputs = {'logits': 'float32[1,4097]'}
                for layer in range(14):
                    for kind in ['key', 'value']:
                        transposed = kind == 'key' and contract.get('key_layout') == 'B,H,D,P'
                        output_length = past if capacity else total
                        inputs[kind + '_' + str(layer)] = ('float32[1,16,64,' + past + ']' if transposed else
                                                           'float32[1,16,' + past + ',64]')
                        outputs['next_' + kind + '_' + str(layer)] = (
                            'float32[1,16,64,' + output_length + ']' if transposed else
                            'float32[1,16,' + output_length + ',64]')
                data['graphs'][Path(filename).stem] = {'inputs': inputs, 'outputs': outputs,
                    'weights': config.get('decode_weights', {}).get(profile, 'fp16'),
                    'status': config.get('profile_validation', {}).get(profile, 'experimental')}
                if contract.get('requires_valid_key'):
                    data['graphs'][Path(filename).stem]['preconditions'] = [
                        'Q=1 and at least one valid key per query; all-masked rows are unsupported']
                if contract.get('fixed_query_length'):
                    data['graphs'][Path(filename).stem].setdefault('preconditions', []).append(
                        'batch1/Q=1 only; projection shapes and final token Gather are specialized, KV length remains dynamic')
                if contract.get('key_layout') == 'B,H,D,P':
                    data['graphs'][Path(filename).stem].setdefault('preconditions', []).append(
                        'transpose Keys once after unchanged prefill; retain B,H,D,P native Keys in subsequent steps; Values/positions unchanged')
                if contract.get('excludes_sentinel'):
                    data['graphs'][Path(filename).stem]['cache'] = (
                        'P excludes the initial prefill sentinel; strip it once, and supply positions for valid keys only')
                continue
            fixed = profile.startswith('static')
            past = str(config['capacity']) if fixed else 'P'
            total = str(config['capacity'] + 1) if fixed else 'P+1'
            data['graphs'][Path(filename).stem] = {
                'inputs': {'embeddings': 'float32[1,1,1024]', 'cache': 'float32[14,16,' + past + ',128]',
                           'positions': 'float32[14,16,' + total + ',64]', 'mask': 'int32[1,1,' + total + ']'},
                'outputs': {'logits': 'float32[1,4097]', 'updated_cache': 'float32[14,16,' + (past if fixed else total) + ',128]'},
                'weights': config.get('decode_weights', {}).get(profile, 'fp16'),
                'status': config.get('profile_validation', {}).get(profile, 'experimental')}
    flow_config_path = root / 'experimental-flow/config.json'
    if flow_config_path.exists():
        data['experimental_flow'] = json.loads(flow_config_path.read_text())
    verify_config_path = root / 'experimental-verify/config.json'
    if verify_config_path.exists():
        verify = json.loads(verify_config_path.read_text())
        data['experimental_verify'] = {
            'scope': 'fixed-Q component probes; no integrated speculative sampler or end-to-end speculative TTS RTF',
            'profiles': verify}
        for item in verify.values():
            query = item['query']
            total = f'P+{query}'
            inputs = {'embeddings': f'float32[1,{query},1024]',
                      'positions': f'float32[14,16,P+{2 * query - 1},64]',
                      'mask': f'int32[1,{query},{total}]'}
            outputs = {'logits': f'float32[1,{query},4097]'}
            for layer in range(14):
                for kind in ['key', 'value']:
                    inputs[f'{kind}_{layer}'] = 'float32[1,16,P,64]'
                    outputs[f'next_{kind}_{layer}'] = f'float32[1,16,{total},64]'
            data['graphs'][Path(item['file']).stem] = {
                'inputs': inputs, 'outputs': outputs, 'weights': 'original mixed INT4/INT8 asymmetric min/max block64 bytes; historical HQQ label corrected',
                'status': item['status'], 'preconditions': [
                    f'batch1/fixedQ={query}; masked sentinel retained and at least one valid key per query',
                    'causal mask and descending relative-position window required; P+Q<=2048',
                    'multi-query kernels can differ numerically from sequential Q=1; not a verified speculative sampler']}
    draft_config_path = root / 'experimental-draft/config.json'
    if draft_config_path.exists():
        drafts = json.loads(draft_config_path.read_text())
        data['experimental_draft'] = {
            'scope': 'untrained early-exit screening; no measured branch acceptance or speculative TTS RTF',
            'profiles': drafts}
        for item in drafts.values():
            inputs = {'embeddings': 'float32[1,1,1024]',
                      'positions': 'float32[14,16,P+1,64]', 'mask': 'int32[1,1,P+1]'}
            outputs = {'logits': 'float32[1,4097]'}
            for layer in range(item['layers']):
                for kind in ['key', 'value']:
                    inputs[f'{kind}_{layer}'] = 'float32[1,16,P,64]'
                    outputs[f'next_{kind}_{layer}'] = 'float32[1,16,P+1,64]'
            data['graphs'][Path(item['file']).stem] = {
                'inputs': inputs, 'outputs': outputs, 'weights': 'original mixed INT4/INT8 asymmetric min/max block64 bytes; historical HQQ label corrected',
                'status': item['status'], 'preconditions': [
                    'batch1/Q=1; original masked sentinel and 14-layer position table retained; P+1<=2048',
                    'untrained early hidden state feeds original final norm/head; not an accepted TTS decoder']}
    for folder in ['fp16', 'int8', 'int4', 'common', 'experimental-decode', 'experimental-flow', 'experimental-verify', 'experimental-draft']:
        for file in sorted((root / folder).rglob('*')):
            if not file.is_file() or file.suffix == '.meta':
                continue
            data['files'].append({'path': file.relative_to(root).as_posix(), 'bytes': file.stat().st_size,
                                  'sha256': digest(file), 'status': 'rejected' if folder == 'int4' else
                                  'experimental' if folder in ['int8', 'experimental-decode', 'experimental-flow', 'experimental-verify', 'experimental-draft'] or
                                  (folder == 'common' and (file.name.startswith('prompt-') or 'references' in file.parts)) else 'baseline'})
    (root / 'manifest.json').write_text(json.dumps(data, indent=2, ensure_ascii=False) + '\n')


if __name__ == '__main__':
    main()
