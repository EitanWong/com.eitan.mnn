"""Python reference driver for official MNN Module APIs and NumPy audio DSP."""
import importlib.util
import json
from pathlib import Path
import time

import numpy as np

RUNTIME_REVISION = 'cosyvoice300m-20261028-keylayout'

CONTRACT = {
    'text_encoder': (['tokens', 'lengths'], ['encoded']),
    'llm_cached': (['embeddings', 'cache', 'positions', 'mask'], ['logits', 'updated_cache']),
    'flow_encoder': (['tokens', 'lengths'], ['encoded']),
    'flow_regulator': (['features'], ['mu']),
    'flow_estimator': (['x', 'mask', 'mu', 't', 'spks', 'cond'], ['estimator_out']),
    'hift_f0': (['mel'], ['f0']),
    'hift_core': (['mel', 'source_stft'], ['spectrum']),
    'campplus': (['input'], ['output']),
    'speech_tokenizer': (['feats', 'feats_length'], ['indices']),
}


def position_encoding(size, dim=1024):
    position = np.arange(size - 1, -size, -1, dtype=np.float32)[:, None]
    angles = position * np.exp(np.arange(0, dim, 2, dtype=np.float32) * (-np.log(10000.) / dim))
    result = np.empty((1, 2 * size - 1, dim), np.float32)
    result[0, :, 0::2] = np.sin(angles)
    result[0, :, 1::2] = np.cos(angles)
    return result


def partition_topk(logits, count=25):
    """Exact descending top-k, resolving equal scores by ascending token index.

    Only the selected candidates are sorted. The cutoff tie is resolved over
    the whole vocabulary so argpartition's arbitrary tie order cannot change
    the sampling distribution or random-number consumption.
    """
    if not 0 < count <= logits.size:
        raise ValueError('Top-k count must be within the vocabulary')
    selected = np.argpartition(-logits, count - 1)[:count]
    cutoff = logits[selected].min()
    higher = np.flatnonzero(logits > cutoff)
    tied = np.flatnonzero(logits == cutoff)[:count - higher.size]
    selected = np.concatenate([higher, tied])
    return selected[np.lexsort((selected, -logits[selected]))]


def stable_topk(logits, count=25):
    return partition_topk(logits, count)


def categorical_index(rng, probabilities):
    """Draw from the pipeline's valid float64 probabilities using inverse CDF.

    The cumulative normalization and right-sided search match NumPy choice
    for this driver, including its consumption of one uniform random number.
    Callers supply nonnegative finite probabilities with a positive sum.
    """
    cumulative = probabilities.cumsum()
    cumulative /= cumulative[-1]
    return int(cumulative.searchsorted(rng.random(), side='right'))


class Graph:
    def __init__(self, path, name, threads=4, backend=0, precision='high', engine='mnn', onnx=None, gpu_record_batch=False, memory=0,
                 contract=None, kleidiai=False, commit_ops=None, quant_option=None, cache_dir=None):
        if threads < 1 or (commit_ops is not None and commit_ops < 1):
            raise ValueError('Threads and explicit GPU commit interval must be positive')
        self.inputs, self.outputs = contract if contract is not None else CONTRACT[name]
        self.engine = engine
        self._pending_native_inputs = []
        self._last_native_outputs = ()
        self.cache_enabled = cache_dir is not None and engine == 'mnn' and backend != 0
        if engine == 'onnx':
            import onnxruntime as ort
            options = ort.SessionOptions()
            options.intra_op_num_threads = threads
            self.module = ort.InferenceSession(str(onnx), options, providers=['CPUExecutionProvider'])
            self.inputs = [x.name for x in self.module.get_inputs()]
        else:
            import MNN
            self.MNN = MNN
            self.runtime = MNN.nn.create_runtime_manager(({
                # Normal is the default. Separate Q=1 decoders can explicitly
                # opt into low memory; the text encoder keeps normal memory.
                # Official Metal runtime uses MNN_GPU_RECORD_BATCH (1 << 9)
                # in numThread to permit deferred command encoding.
                'backend': backend, 'numThread': threads | (512 if backend == 1 and gpu_record_batch else 0),
                'precision': precision, 'memory': memory,
            },))
            if kleidiai:
                self.runtime.set_hint(16, 1)  # Official CPU_ENABLE_KLEIDIAI.
            if commit_ops is not None:
                self.runtime.set_hint(9, int(commit_ops))  # OP_ENCODER_NUMBER_FOR_COMMIT.
            if quant_option is not None:
                self.runtime.set_hint(5, int(quant_option))  # DYNAMIC_QUANT_OPTIONS.
            if self.cache_enabled:
                cache_dir = Path(cache_dir)
                cache_dir.mkdir(parents=True, exist_ok=True)
                cache_name = f'{Path(path).stem}.b{backend}.{precision}.t{threads}.m{memory}.cache'
                self.runtime.set_cache(str(cache_dir / cache_name))
            self.module = MNN.nn.load_module_from_file(str(path), self.inputs, self.outputs,
                                                     runtime_manager=self.runtime, rearrange=True)

    def var(self, x):
        if not isinstance(x, np.ndarray):
            return x
        f = self.MNN.expr
        dtype = f.float if x.dtype.kind == 'f' else f.int
        x = np.ascontiguousarray(x, dtype=np.float32 if x.dtype.kind == 'f' else np.int32)
        return f.const(x, list(x.shape), f.NCHW, dtype)

    def array(self, x):
        if self.engine == 'onnx' or isinstance(x, np.ndarray):
            return x
        # read() exposes native memory without retaining its Var owner.
        converted = self.MNN.expr.convert(x, self.MNN.expr.NCHW)
        result = converted.read().copy()
        if any(x is output for output in self._last_native_outputs):
            self._pending_native_inputs.clear()
            self._last_native_outputs = ()
        return result

    def __call__(self, *values, native=False):
        if self.engine == 'onnx':
            types = {v.name: v.type for v in self.module.get_inputs()}
            feeds = {k: v.astype(np.int64) if types[k] == 'tensor(int64)' else
                     v.astype(np.bool_) if types[k] == 'tensor(bool)' else v for k, v in zip(self.inputs, values)}
            result = self.module.run(None, feeds)
        else:
            # Retain Python input Var owners until copied outputs are read.
            inputs = [self.var(v) for v in values]
            result = self.module.forward(inputs)
            if native:
                # Native outputs can be read after this function returns. Keep
                # all queued input owners until one of the latest outputs is read.
                self._pending_native_inputs.append(inputs)
                self._last_native_outputs = result
        if native:
            return result
        arrays = [self.array(v) for v in result]
        self._pending_native_inputs.clear()
        self._last_native_outputs = ()
        return arrays


def tokenizer(dependencies):
    path = dependencies / 'whisper/tokenizer.py'
    spec = importlib.util.spec_from_file_location('cosy_whisper_tokenizer', path)
    module = importlib.util.module_from_spec(spec)
    import sys
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module.get_tokenizer(multilingual=True, num_languages=100, language='en', task='transcribe')


def interpolate(x, size):
    """PyTorch interpolate(mode=linear, align_corners=False), along time."""
    positions = np.maximum(0, (np.arange(size) + .5) * x.shape[1] / size - .5)
    lower = np.floor(positions).astype(int)
    upper = np.minimum(lower + 1, x.shape[1] - 1)
    weight = (positions - lower).astype(np.float32)[None, :, None]
    return x[:, lower] * (1 - weight) + x[:, upper] * weight


def source_spectrum(f0, weights, rng):
    f0 = np.repeat(f0, 256, axis=1)
    harmonics = np.arange(1, 10, dtype=np.float32)[None, :, None]
    phase = rng.uniform(-np.pi, np.pi, (1, 9, 1)).astype(np.float32)
    phase[:, 0] = 0
    theta = 2 * np.pi * (np.cumsum(f0[:, None] * harmonics / 22050, axis=-1) % 1)
    voiced = (f0[:, None] > 10).astype(np.float32)
    sine = .1 * np.sin(theta + phase) * voiced
    sine += (voiced * .003 + (1 - voiced) * (.1 / 3)) * rng.standard_normal(sine.shape, dtype=np.float32)
    signal = np.tanh(sine.transpose(0, 2, 1) @ weights['weight'].T + weights['bias']).reshape(-1)
    # torch.stft(n_fft=16, hop=4, center=True, pad_mode=reflect).
    padded = np.pad(signal, (8, 8), mode='reflect')
    frames = np.lib.stride_tricks.sliding_window_view(padded, 16)[::4]
    window = (.5 - .5 * np.cos(2 * np.pi * np.arange(16) / 16)).astype(np.float32)
    spectrum = np.fft.rfft(frames * window, axis=-1).T
    return np.concatenate([spectrum.real, spectrum.imag], axis=0)[None].astype(np.float32)


def waveform(spectrum):
    magnitude = np.minimum(np.exp(spectrum[0, :9]), 100)
    phase = np.sin(spectrum[0, 9:])
    frames = np.fft.irfft((magnitude * np.exp(1j * phase)).T, n=16, axis=-1)
    window = .5 - .5 * np.cos(2 * np.pi * np.arange(16) / 16)
    n = 16 + (len(frames) - 1) * 4
    out, norm = np.zeros(n), np.zeros(n)
    for i in range(16):
        out[i:i + 4 * len(frames):4] += frames[:, i] * window[i]
        norm[i:i + 4 * len(frames):4] += window[i] ** 2
    return np.clip((out / np.maximum(norm, 1e-10))[8:-8], -.99, .99).astype(np.float32)


class Pipeline:
    def __init__(self, models, work, precision='fp16', backend=0, threads=4, compute='high', engine='mnn', hift_backend=None, native_positions=True,
                 flow_backend=None, llm_backend=None, gpu_record_batch=False, decode_profile='baseline', decode_memory=0,
                 decode_kleidiai=False, flow_native=False, decode_threads=None, prefill_backend=None,
                 decode_commit_ops=None, flow_commit_ops=None, prompt_path=None, decode_quant_option=None, cache_dir=None):
        self.models, self.work = models, work
        self.graphs = {}
        self.flow_native = flow_native
        for name in ('text_encoder', 'llm_cached', 'flow_encoder', 'flow_regulator', 'flow_estimator', 'hift_f0', 'hift_core'):
            quantized = name == 'llm_cached' or (name == 'text_encoder' and precision != 'mixed8')
            folder = ('int8' if precision == 'mixed8' else precision) if quantized else 'fp16'
            file = name
            if name == 'llm_cached' and (models / folder / 'llm_cached_projected.mnn').exists():
                file = 'llm_cached_projected'
            onnx = models / 'source/flow_estimator.onnx' if name == 'flow_estimator' else work / 'ONNX' / (file + '.onnx')
            graph_backend = hift_backend if name.startswith('hift_') and hift_backend is not None else backend
            if name.startswith('flow_') and flow_backend is not None:
                graph_backend = flow_backend
            if name == 'llm_cached' and llm_backend is not None:
                graph_backend = llm_backend
            if name == 'llm_cached' and prefill_backend is not None:
                graph_backend = prefill_backend
            if name == 'flow_estimator' and flow_native:
                if engine != 'mnn':
                    raise ValueError('Native Flow experiment requires MNN')
                flow_config = json.loads((models / 'experimental-flow/config.json').read_text())
                self.graphs[name] = Graph(models / 'experimental-flow' / flow_config['file'], name, threads,
                    graph_backend, compute, engine, gpu_record_batch=gpu_record_batch,
                    contract=(flow_config['inputs'], flow_config['outputs']), commit_ops=flow_commit_ops, cache_dir=cache_dir)
            else:
                self.graphs[name] = Graph(models / folder / (file + '.mnn'), name, threads, graph_backend, compute, engine, onnx,
                                          gpu_record_batch, commit_ops=flow_commit_ops if name.startswith('flow_') else None,
                                          cache_dir=cache_dir)
        self.positions = None
        if (models / ('int8' if precision == 'mixed8' else precision) / 'llm_cached_projected.mnn').exists():
            table = np.load(models / 'common/projected_positions.npy')
            self.max_context = (table.shape[2] + 1) // 2
            self.positions = table
        self.native_positions = None
        if self.positions is not None and engine == 'mnn' and native_positions:
            self.native_positions = self.graphs['llm_cached'].var(self.positions)
        self.decode_profile = decode_profile
        self.decode_capacity = None
        self.split_state = False
        self.internal_positions = False
        self.decode_excludes_sentinel = False
        self.decode_key_transposed = False
        if decode_memory not in (0, 2) or (decode_memory and decode_profile == 'baseline'):
            raise ValueError('Low memory is an experiment limited to a separate decode graph')
        if decode_profile != 'baseline':
            if engine != 'mnn' or precision != 'fp16' or self.positions is None:
                raise ValueError('Experimental decode profiles require MNN, FP16 weights and projected positions')
            config = json.loads((models / 'experimental-decode/config.json').read_text())
            decode_contract = config.get('decode_contracts', {}).get(decode_profile)
            self.split_state = decode_contract is not None and decode_contract['state_layout'] == 'per_layer_kv'
            self.internal_positions = bool(decode_contract and decode_contract.get('internal_positions'))
            self.decode_excludes_sentinel = bool(decode_contract and decode_contract.get('excludes_sentinel'))
            key_layout = (decode_contract or {}).get('key_layout', 'B,H,P,D')
            if key_layout not in ['B,H,P,D', 'B,H,D,P']:
                raise ValueError('Unsupported decode Key layout')
            self.decode_key_transposed = key_layout == 'B,H,D,P'
            if self.decode_key_transposed and (not self.split_state or decode_contract.get('capacity')):
                raise ValueError('Transposed Keys require dynamic per-layer K/V state')
            self.graphs['llm_decode'] = Graph(models / 'experimental-decode' / config[decode_profile], 'llm_cached',
                decode_threads if decode_threads is not None else threads,
                llm_backend if llm_backend is not None else backend, compute, engine,
                gpu_record_batch=gpu_record_batch, memory=decode_memory,
                contract=(decode_contract['inputs'], decode_contract['outputs']) if decode_contract else None,
                kleidiai=decode_kleidiai, commit_ops=decode_commit_ops, quant_option=decode_quant_option, cache_dir=cache_dir)
            capacity = (decode_contract or {}).get('capacity') or (config['capacity'] if decode_profile.startswith('static') else None)
            if capacity:
                self.decode_capacity = int(capacity)
                middle = self.positions.shape[2] // 2
                positions = self.positions[:, :, middle - self.decode_capacity:middle + 1].copy()
                self.static_positions = self.graphs['llm_decode'].var(positions) if native_positions else positions
        self.embeddings = dict(np.load(models / 'common/llm_embeddings.npz'))
        self.speaker = dict(np.load(models / 'common/flow_speaker.npz'))
        self.source = dict(np.load(models / 'common/hift_source.npz'))
        self.prompt = dict(np.load(prompt_path or models / 'common/prompt.npz'))
        self.tokenizer = tokenizer(work / 'Dependencies')

    def update_caches(self):
        for graph in self.graphs.values():
            if graph.cache_enabled:
                graph.runtime.update_cache()

    def synthesize(self, text, steps=10, seed=42, greedy=False, flow_prompt_seconds=None, flow_guidance=.7,
                   reference_mode='zero-shot', sampling_policy='official-ras', sampling_temperature=1.,
                   max_token_text_ratio=20., flow_trace=None):
        if steps < 1:
            raise ValueError('Flow steps must be positive')
        if flow_prompt_seconds is not None and flow_prompt_seconds <= 0:
            raise ValueError('Flow reference duration must be positive')
        if not 0 <= flow_guidance <= 2 or (self.flow_native and flow_guidance != .7):
            raise ValueError('Guidance must be in [0,2]; fused Euler graph requires 0.7')
        if reference_mode not in ['zero-shot', 'cross-lingual']:
            raise ValueError('Unknown reference mode')
        if sampling_policy not in ['official-ras', 'legacy']:
            raise ValueError('Unknown sampling policy')
        if not 0 < sampling_temperature <= 2:
            raise ValueError('Sampling temperature must be in (0,2]')
        if not np.isfinite(max_token_text_ratio) or max_token_text_ratio <= 2:
            raise ValueError('Maximum speech/text token ratio must be finite and greater than 2')
        rng = np.random.default_rng(seed)
        times = {}
        start = previous = time.perf_counter()
        tokens = np.array([self.tokenizer.encode(text, allowed_special='all')], np.int32)
        # Match official frontend_cross_lingual: retain speaker and Flow prompt,
        # remove only AR prompt text/speech. Explicit opt-in, different conditioning.
        prompt_text = self.prompt['text_tokens'] if reference_mode == 'zero-shot' else np.zeros((1, 0), np.int32)
        text_tokens = np.concatenate([prompt_text, tokens], axis=1)
        encoded = self.graphs['text_encoder'](text_tokens, np.array([text_tokens.shape[1]], np.int32))[0]
        spk = self.prompt['speaker']
        spk = spk / np.linalg.norm(spk, axis=1, keepdims=True)
        embedding = spk @ self.embeddings['spk_embed_affine_layer_weight'].T + self.embeddings['spk_embed_affine_layer_bias']
        e = self.embeddings
        prompt_speech = self.prompt['speech_tokens'] if reference_mode == 'zero-shot' else np.zeros((1, 0), np.int32)
        x = np.concatenate([e['llm_embedding_weight'][0][None, None], embedding[:, None], encoded,
                            e['llm_embedding_weight'][1][None, None], e['speech_embedding_weight'][prompt_speech]], axis=1)
        times['text_encode_s'] = time.perf_counter() - previous
        previous = time.perf_counter()
        decoder = self.graphs['llm_cached']
        cache = np.zeros((14, 16, 1, 128), np.float32)
        past = 1
        generated = []
        eos = False
        max_speech_tokens = int(tokens.shape[1] * max_token_text_ratio)
        for i in range(max_speech_tokens):
            total = past + x.shape[1]
            if self.positions is not None and total > self.max_context:
                raise ValueError('Context exceeds exported position table; split the input text')
            if self.decode_capacity is not None and total > self.decode_capacity:
                raise ValueError('Static decode capacity exceeded; use baseline/compact or split the input')
            active_decoder = self.graphs['llm_decode'] if i > 0 and self.decode_profile != 'baseline' else decoder
            if i > 0 and self.decode_excludes_sentinel:
                middle = self.positions.shape[2] // 2
                # Prefill's initial masked slot is absent from this cache.
                # Relative lags for all valid keys are unchanged.
                if self.native_positions is not None:
                    positions = decoder.MNN.expr.slice(self.native_positions,
                        [0, 0, middle - total + 2, 0], [14, 16, total - 1, 64])
                else:
                    positions = self.positions[:, :, middle - total + 2:middle + 1].copy()
            elif i > 0 and self.internal_positions:
                mask = np.ones((1, 1, total), np.int32)
                mask[:, :, 0] = 0
                positions = np.array([self.positions.shape[2] // 2 - total + 1], np.int32)
            elif i > 0 and self.decode_capacity is not None:
                mask = np.zeros((1, 1, self.decode_capacity + 1), np.int32)
                mask[:, :, -past:] = 1
                positions = self.static_positions
            elif self.positions is not None:
                mask = np.ones((1, x.shape[1], total), np.int32)
                mask[:, :, 0] = 0
                mask[:, :, past:] = np.tri(x.shape[1], dtype=np.int32)
                middle = self.positions.shape[2] // 2
                count = total if i > 0 and (self.decode_profile.startswith('compact') or self.split_state) else 2 * total - 1
                if self.native_positions is not None:
                    # Keep the shared table in MNN and materialize only this window.
                    # Avoid a large NumPy copy and host-to-MNN copy for every token.
                    positions = decoder.MNN.expr.slice(self.native_positions,
                        [0, 0, middle - total + 1, 0], [14, 16, count, 64])
                else:
                    positions = self.positions[:, :, middle - total + 1:middle - total + 1 + count].copy()
            else:
                mask = np.ones((1, x.shape[1], total), np.int32)
                mask[:, :, 0] = 0
                mask[:, :, past:] = np.tri(x.shape[1], dtype=np.int32)
                positions = position_encoding(total)
            if i > 0 and self.split_state:
                result = (active_decoder(x, positions, *cache, native=True) if self.decode_excludes_sentinel else
                          active_decoder(x, positions, mask, *cache, native=True))
            else:
                result = active_decoder(x, cache, positions, mask, native=True)
            logits = active_decoder.array(result[0]).reshape(-1).astype(np.float64)
            if not np.isfinite(logits).all():
                raise RuntimeError('Non-finite decoder logits; reject this precision/memory profile')
            if self.split_state:
                if i == 0:
                    # One prefill transfer; subsequent K/V tensors remain native.
                    packed = decoder.array(result[1])
                    first_key = 1 if self.decode_excludes_sentinel else 0
                    cache = []
                    for j in range(14):
                        for k in (0, 64):
                            state = packed[j:j + 1, :, first_key:, k:k + 64]
                            if self.decode_key_transposed and k == 0:
                                # Convert once after prefill; later Keys remain native.
                                state = state.transpose(0, 1, 3, 2)
                            cache.append(active_decoder.var(state.copy()))
                    if self.decode_capacity is not None and total < self.decode_capacity:
                        cache = [active_decoder.var(np.pad(packed[j:j + 1, :, :, k:k + 64],
                            ((0, 0), (0, 0), (self.decode_capacity - total, 0), (0, 0))))
                            for j in range(14) for k in (0, 64)]
                else:
                    cache = result[1:]
            else:
                cache = result[1]
            if i == 0 and not self.split_state and self.decode_capacity is not None and total < self.decode_capacity:
                padding = active_decoder.var(np.zeros((14, 16, self.decode_capacity - total, 128), np.float32))
                cache = active_decoder.MNN.expr.concat([padding, cache], 2)
            if i < tokens.shape[1] * 2:
                logits[4096] = -np.inf
            logits /= sampling_temperature
            if greedy:
                token = int(np.argmax(logits))
            else:
                indices = (np.argsort(logits)[::-1] if sampling_policy == 'legacy' else
                           stable_topk(logits))[:25]
                full_prob = np.exp(logits - logits.max())
                full_prob /= full_prob.sum()
                prob = full_prob[indices]
                count = min(len(prob), np.searchsorted(np.cumsum(prob), .8) + 1)
                selected_prob = prob[:count] / prob[:count].sum()
                token = (int(rng.choice(indices[:count], p=selected_prob)) if sampling_policy == 'legacy' else
                         int(indices[categorical_index(rng, selected_prob)]))
                if generated[-10:].count(token) >= 1:
                    # Upstream random_sampling samples the original distribution,
                    # including the repeated candidate. Keep old behavior only for
                    # reproduction of earlier benchmark artifacts.
                    if sampling_policy == 'legacy':
                        logits[token] = -np.inf
                        prob = np.exp(logits - np.max(logits)); prob /= prob.sum()
                        token = int(rng.choice(len(prob), p=prob))
                    else:
                        token = categorical_index(rng, full_prob)
            if token == 4096:
                eos = True
                break
            generated.append(token)
            x = e['speech_embedding_weight'][token][None, None]
            past = total
            if i == 0:
                times['llm_prefill_s'] = time.perf_counter() - previous
        if not eos:
            raise RuntimeError(f'No EOS within {max_speech_tokens} speech tokens '
                               f'(text tokens={tokens.shape[1]}, max ratio={max_token_text_ratio}); refuse truncated speech')
        speech = np.array([generated], np.int32)
        times['llm_s'] = time.perf_counter() - previous
        times['llm_decode_s'] = times['llm_s'] - times['llm_prefill_s']
        times['llm_decode_iterations'] = i
        times['llm_decode_mean_ms'] = times['llm_decode_s'] * 1000 / max(i, 1)
        previous = time.perf_counter()
        flow_prompt = self.prompt['speech_tokens']
        prompt_mel = self.prompt['mel']
        if flow_prompt_seconds is not None:
            count = max(1, min(flow_prompt.shape[1], round(flow_prompt_seconds * 50)))
            mel_frames = max(1, round(prompt_mel.shape[1] * count / flow_prompt.shape[1]))
            flow_prompt = flow_prompt[:, :count]
            prompt_mel = prompt_mel[:, :mel_frames]
        both = np.concatenate([flow_prompt, speech], axis=1)
        encoded = self.graphs['flow_encoder'](both, np.array([both.shape[1]], np.int32))[0]
        prompt_count = flow_prompt.shape[1]
        times['flow_prompt_tokens'] = prompt_count
        times['flow_prompt_mel_frames'] = prompt_mel.shape[1]
        mel_count = int(speech.shape[1] / 50 * 22050 / 256)
        target = encoded[:, prompt_count:]
        if target.shape[1] > 40:
            target = np.concatenate([interpolate(target[:, :20], 34), interpolate(target[:, 20:-20], mel_count - 68), interpolate(target[:, -20:], 34)], axis=1)
        else:
            target = interpolate(target, mel_count)
        encoded = np.concatenate([interpolate(encoded[:, :prompt_count], prompt_mel.shape[1]), target], axis=1)
        mu = self.graphs['flow_regulator'](encoded.transpose(0, 2, 1).copy())[0]
        length = mu.shape[-1]
        cond = np.zeros_like(mu)
        cond[:, :, :prompt_mel.shape[1]] = prompt_mel.transpose(0, 2, 1)
        speaker = spk @ self.speaker['spk_embed_affine_layer_weight'].T + self.speaker['spk_embed_affine_layer_bias']
        z = rng.standard_normal(mu.shape, dtype=np.float32)
        if flow_trace is not None:
            # Optional quality diagnostics only. Copies are excluded from normal
            # benchmark runs by leaving this argument unset.
            flow_trace.update(speech_tokens=speech.copy(), mu=mu.copy(), cond=cond.copy(),
                              speaker=speaker.copy(), initial_noise=z.copy(),
                              prompt_mel_frames=prompt_mel.shape[1],
                              rng_state_after_noise=json.loads(json.dumps(rng.bit_generator.state)))
        span = (1 - np.cos(np.linspace(0, 1, steps + 1) * .5 * np.pi)).astype(np.float32)
        cfg_batch = 2 if flow_guidance else 1
        cfg_mu = np.concatenate([mu, np.zeros_like(mu)]) if flow_guidance else mu
        cfg_cond = np.concatenate([cond, np.zeros_like(cond)]) if flow_guidance else cond
        cfg_spk = np.concatenate([speaker, np.zeros_like(speaker)]) if flow_guidance else speaker
        if self.flow_native:
            graph = self.graphs['flow_estimator']
            z = graph.var(z)
            constant_inputs = {k: graph.var(v) for k, v in {
                'mask': np.ones((2, 1, length), np.float32), 'mu': cfg_mu, 'spks': cfg_spk, 'cond': cfg_cond}.items()}
            for i in range(steps):
                values = dict(constant_inputs, x=z, t=np.full(2, span[i], np.float32),
                              dt=np.array([span[i + 1] - span[i]], np.float32))
                z = graph(*(values[k] for k in graph.inputs), native=True)[0]
            z = graph.array(z)
        else:
            for i in range(steps):
                velocity = self.graphs['flow_estimator'](np.repeat(z, cfg_batch, axis=0), np.ones((cfg_batch, 1, length), np.float32),
                            cfg_mu, np.full(cfg_batch, span[i], np.float32), cfg_spk, cfg_cond)[0]
                guided = (1 + flow_guidance) * velocity[:1] - flow_guidance * velocity[1:] if flow_guidance else velocity
                z += (span[i + 1] - span[i]) * guided
        mel = z[:, :, prompt_mel.shape[1]:].copy()
        times['flow_s'] = time.perf_counter() - previous
        previous = time.perf_counter()
        f0 = self.graphs['hift_f0'](mel)[0]
        spectrum = self.graphs['hift_core'](mel, source_spectrum(f0, self.source, rng))[0]
        pcm = waveform(spectrum)
        if not np.isfinite(pcm).all() or np.sqrt(np.mean(pcm ** 2)) < 1e-4:
            raise RuntimeError('Invalid/silent waveform')
        times['hift_s'] = time.perf_counter() - previous
        times['synthesis_s'] = time.perf_counter() - start
        times['audio_s'] = len(pcm) / 22050
        times['rtf'] = times['synthesis_s'] / times['audio_s']
        times.update(speech_tokens=len(generated), eos=eos, flow_steps=steps, flow_guidance=flow_guidance,
                     reference_mode=reference_mode, sampling_policy=sampling_policy,
                     sampling_temperature=sampling_temperature, max_token_text_ratio=max_token_text_ratio, seed=seed)
        return pcm, times
