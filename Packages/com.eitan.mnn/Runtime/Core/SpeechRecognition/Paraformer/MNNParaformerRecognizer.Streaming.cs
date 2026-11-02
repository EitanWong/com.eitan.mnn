using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNParaformerRecognizer
    {
        /// <summary>Create independent recurrent/CIF state sharing this recognizer's native graphs.
        /// Supply mono 16 kHz PCM. Native operations serialize; dispose streams before their recognizer.</summary>
        public Stream CreateStream()
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNParaformerRecognizer));
                return new Stream(this);
            }
        }

        /// <summary>Bounded incremental PCM, 600 ms acoustic advances and 25 ms feature context.
        /// Text is cumulative and provisional until InputFinished; no timestamp alignment is claimed.</summary>
        public sealed class Stream : IDisposable
        {
            private readonly MNNParaformerRecognizer _owner;
            private readonly float[] _pcm = new float[10000], _overlap = new float[5600];
            private readonly float[] _chunk = new float[11200], _encoded = new float[10240], _hidden = new float[512];
            private readonly MNNGenerationGraph.Input[] _states = new MNNGenerationGraph.Input[16];
            private readonly List<float> _embeddings = new List<float>(5632);
            private readonly List<int> _ids = new List<int>();
            private int _buffered, _position, _accepted;
            private float _integral;
            private bool _finished, _faulted, _closed;
            private MNNSpeechRecognition _result = Empty();

            private static MNNSpeechRecognition Empty() => new MNNSpeechRecognition("", Array.Empty<int>(), Array.Empty<float>());
            internal Stream(MNNParaformerRecognizer owner) { _owner = owner; Reset(); }

            /// <summary>Consume at most 30 seconds per call. The caller retains ownership of its array.
            /// Argument rejection leaves state intact; interrupted inference requires Reset.</summary>
            public string AcceptWaveform(float[] samples, int offset, int count, CancellationToken token = default)
            {
                lock (_owner._gate)
                {
                    RequireOpen();
                    if (_finished || _faulted) throw new InvalidOperationException("Reset the finished or interrupted Paraformer stream.");
                    if (samples == null) throw new ArgumentNullException(nameof(samples));
                    if (offset < 0 || count < 0 || offset > samples.Length - count || count > 480000 || _accepted > 9600000 - count)
                        throw new ArgumentOutOfRangeException(nameof(count), "Supply a valid slice, at most 30 seconds per call and ten minutes per stream.");
                    for (int i = offset; i < offset + count; ++i)
                    {
                        if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                        float value = samples[i];
                        if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1)
                            throw new ArgumentException("Supply finite normalized PCM in [-1,1].", nameof(samples));
                    }
                    token.ThrowIfCancellationRequested();
                    try { Feed(samples, offset, count, false, token); _accepted += count; return Snapshot().Text; }
                    catch { _faulted = true; throw; }
                }
            }

            /// <summary>Drain one second of acoustic context and the CIF tail exactly once.</summary>
            public MNNSpeechRecognition InputFinished(CancellationToken token = default)
            {
                lock (_owner._gate)
                {
                    RequireOpen();
                    if (_faulted) throw new InvalidOperationException("Reset the interrupted Paraformer stream.");
                    if (_finished) return _result;
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (_accepted > 0) Feed(new float[16000], 0, 16000, true, token);
                        _finished = true;
                        return Snapshot();
                    }
                    catch { _faulted = true; throw; }
                }
            }

            public void Reset()
            {
                lock (_owner._gate)
                {
                    RequireOpen();
                    _buffered = _position = _accepted = 0; _integral = 0;
                    _finished = _faulted = false;
                    Array.Clear(_overlap, 0, _overlap.Length);
                    Array.Clear(_hidden, 0, _hidden.Length);
                    _ids.Clear(); _result = Empty();
                    for (int i = 0; i < _states.Length; ++i)
                        _states[i] = new MNNGenerationGraph.Input("in_cache_" + i, new float[5120], 1, 512, 10);
                }
            }

            private void Feed(float[] samples, int offset, int count, bool final, CancellationToken token)
            {
                int end = offset + count;
                while (offset < end)
                {
                    token.ThrowIfCancellationRequested();
                    int copy = Math.Min(_pcm.Length - _buffered, end - offset);
                    Array.Copy(samples, offset, _pcm, _buffered, copy);
                    _buffered += copy; offset += copy;
                    if (_buffered < _pcm.Length) continue;
                    int remainingFeatures = (_buffered + end - offset - 1360) / 960 + 1;
                    ProcessChunk(final && remainingFeatures < 20, token);
                    Array.Copy(_pcm, 9600, _pcm, 0, 400);
                    _buffered = 400;
                }
            }

            private void ProcessChunk(bool final, CancellationToken token)
            {
                var features = MNNSenseVoiceFeatures.Extract(_pcm, out int frames, token);
                if (frames != 10) throw new InvalidDataException("Expected ten Paraformer feature frames.");
                Array.Copy(_overlap, _chunk, _overlap.Length);
                for (int t = 0; t < 10; ++t)
                    for (int k = 0; k < 560; ++k)
                    {
                        float position = (_position + t + 1) * PositionScales[k % 280];
                        _chunk[(10 + t) * 560 + k] = features[t * 560 + k] * 22.62741699796952f +
                            (float)(k < 280 ? Math.Sin(position) : Math.Cos(position));
                    }
                Array.Copy(_chunk, 5600, _overlap, 0, 5600);
                token.ThrowIfCancellationRequested();
                var packed = _owner._encoder.Run(_chunk, 1, 20, 560);
                if (packed.Length != 20 * 513 || packed.Any(x => float.IsNaN(x) || float.IsInfinity(x)))
                    throw new InvalidDataException("Invalid Paraformer encoder output.");
                for (int t = 0; t < 20; ++t) Array.Copy(packed, t * 513, _encoded, t * 512, 512);
                _embeddings.Clear();
                Integrate(packed);
                if (final && _integral + .45f >= 1) _embeddings.AddRange(_hidden);
                if (_embeddings.Count > 0) DecodeChunk(token);
                _position += 10;
            }

            private void Integrate(float[] packed)
            {
                for (int t = 5; t < 15; ++t)
                {
                    float alpha = packed[t * 513 + 512];
                    if (alpha < 0 || alpha > 1) throw new InvalidDataException("Invalid Paraformer CIF weight.");
                    float contribution = Math.Min(alpha, 1 - _integral);
                    for (int k = 0; k < 512; ++k) _hidden[k] += contribution * _encoded[t * 512 + k];
                    if (_integral + alpha < 1) _integral += alpha;
                    else
                    {
                        _embeddings.AddRange(_hidden);
                        _integral += alpha - 1;
                        for (int k = 0; k < 512; ++k) _hidden[k] = _integral * _encoded[t * 512 + k];
                    }
                }
            }

            private void DecodeChunk(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                int tokens = _embeddings.Count / 512;
                var inputs = new List<MNNGenerationGraph.Input>
                {
                    new MNNGenerationGraph.Input("enc", _encoded, 1, 20, 512),
                    new MNNGenerationGraph.Input("enc_len", new[] {20}, 1),
                    new MNNGenerationGraph.Input("acoustic_embeds", _embeddings.ToArray(), 1, tokens, 512),
                    new MNNGenerationGraph.Input("acoustic_embeds_len", new[] {tokens}, 1)
                };
                inputs.AddRange(_states);
                var outputs = _owner._decoder.RunOutputs(_owner._outputs, inputs.ToArray());
                if (!outputs[0].Shape.SequenceEqual(new[] {1, tokens, 8404}))
                    throw new InvalidDataException("Invalid Paraformer logits shape.");
                for (int t = 0; t < tokens; ++t)
                {
                    int offset = t * 8404, best = 0;
                    for (int k = 1; k < 8404; ++k)
                        if (outputs[0].Values[offset + k] > outputs[0].Values[offset + best]) best = k;
                    if (best > 2 && _owner._tokens[best] != "<unk>") _ids.Add(best);
                }
                for (int i = 0; i < 16; ++i)
                {
                    if (!outputs[i + 1].Shape.SequenceEqual(new[] {1, 512, 10}))
                        throw new InvalidDataException("Invalid Paraformer decoder cache.");
                    _states[i] = outputs[i + 1].AsInput("in_cache_" + i);
                }
                token.ThrowIfCancellationRequested();
            }

            private MNNSpeechRecognition Snapshot()
            {
                if (_result.TokenIds.Count != _ids.Count)
                    _result = new MNNSpeechRecognition(Decode(_ids.Select(id => _owner._tokens[id])), _ids.ToArray(), Array.Empty<float>());
                return _result;
            }
            private void RequireOpen()
            {
                if (_closed || _owner._disposed) throw new ObjectDisposedException(nameof(Stream));
            }
            public void Dispose()
            {
                lock (_owner._gate) { _closed = true; _ids.Clear(); _embeddings.Clear(); }
            }
        }
    }
}
