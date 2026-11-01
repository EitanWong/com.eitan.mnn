using System;
using System.Collections.Generic;
using System.IO;

namespace MNN.Unity
{
    // Official Interpreter/Tensor calls under the package's existing compiler ABI.
    // Reuse host state buffers; never retain a tensor view across resize/run.
    internal sealed class MNNCosyVoice300MDecoder : IDisposable
    {
        private readonly MNNInterpreter _prefillModel, _decodeModel;
        private readonly MNNSession _prefill, _decode;
        private readonly float[] _positions;
        private float[][] _state;
        private readonly float[] _logits = new float[4097];
        private readonly bool _quantized;
        private readonly bool _compact;
        internal bool UsesCompactGraph => _compact;

        internal MNNCosyVoice300MDecoder(string root, int threads, bool quantized, float[] positions,
            MNNCosyVoice300MDecoderMode mode = MNNCosyVoice300MDecoderMode.Float16)
        {
            _positions = positions; _quantized = quantized;
            string conservative = MNNCosyVoice300MModel.ConservativeGraph(mode);
            string compactGraph = conservative == null ? MNNCosyVoice300MModel.ResolveCompactGraph(root) : Path.Combine(root, conservative);
            try
            {
                _prefillModel = MNNInterpreter.CreateFromFile(Path.Combine(root, "fp16/llm_cached_projected.mnn"));
                var precision = quantized ? MNNPrecisionMode.Low : MNNPrecisionMode.High;
                _prefill = _prefillModel.CreateSession(new MNNSessionConfig {BackendType = MNNBackendType.CPU, ThreadCount = threads, PrecisionMode = precision});
                if (quantized)
                {
                    _decodeModel = MNNInterpreter.CreateFromFile(Path.Combine(root, MNNCosyVoice300MModel.QuantizedGraph));
                    var saved = new string[29]; saved[0] = "logits";
                    for (int i = 0; i < 28; ++i) saved[i + 1] = StateName(i, true);
                    _decode = _decodeModel.CreateSession(new MNNSessionConfig {BackendType = MNNBackendType.CPU, ThreadCount = threads,
                        PrecisionMode = precision, MemoryMode = MNNMemoryMode.Low, SavedTensors = saved});
                }
                else if (compactGraph != null)
                {
                    // Q=1 needs only the positive position window and no relative shift.
                    // The original FP16 full graph still handles prefill; only
                    // an explicitly selected candidate quantizes decode weights.
                    _compact = true;
                    _decodeModel = MNNInterpreter.CreateFromFile(compactGraph);
                    _decode = _decodeModel.CreateSession(new MNNSessionConfig {BackendType = MNNBackendType.CPU,
                        ThreadCount = threads, PrecisionMode = precision,
                        MemoryMode = conservative == null ? MNNMemoryMode.Normal : MNNMemoryMode.Low});
                }
            }
            catch { Dispose(); throw; }
        }

        internal void Reset()
        {
            _state = new[] {new float[14 * 16 * 128]}; // Initial masked sentinel.
        }

        internal float[] Forward(float[] embeddings, int query, int past, bool first)
        {
            int total = checked(past + query);
            if (query < 1 || total > 2048 || embeddings.Length != query * 1024 || _state == null)
                throw new InvalidDataException("CosyVoice-300M context exceeds 2048 or has invalid state.");
            bool split = _quantized && !first;
            bool decode = !first && (split || _compact);
            var session = decode ? _decode : _prefill;
            int positions = decode ? total : 2 * total - 1;
            var shapes = new Dictionary<string, int[]> {
                {"embeddings", new[] {1, query, 1024}}, {"positions", new[] {14, 16, positions, 64}},
                {"mask", new[] {1, query, total}}
            };
            if (split)
                for (int i = 0; i < 28; ++i) shapes.Add(StateName(i, false), new[] {1, 16, past, 64});
            else shapes.Add("cache", new[] {14, 16, past, 128});
            session.ResizeInputs(shapes);
            session.GetInput("embeddings").CopyFromArray(embeddings);
            var positionTensor = session.GetInput("positions");
            var positionData = positionTensor.MapForWrite<float>();
            try
            {
                int sourceStride = 4095 * 64, size = positions * 64, start = (2047 - total + 1) * 64;
                for (int i = 0; i < 14 * 16; ++i)
                    new ReadOnlySpan<float>(_positions, i * sourceStride + start, size).CopyTo(positionData.Slice(i * size, size));
            }
            finally { positionTensor.Unmap(); }
            var maskTensor = session.GetInput("mask");
            var mask = maskTensor.MapForWrite<int>();
            try
            {
                for (int q = 0; q < query; ++q)
                    for (int k = 0; k < total; ++k) mask[q * total + k] = k != 0 && k <= past + q ? 1 : 0;
            }
            finally { maskTensor.Unmap(); }
            for (int i = 0; i < _state.Length; ++i)
            {
                int size = split ? 16 * past * 64 : 14 * 16 * past * 128;
                session.GetInput(split ? StateName(i, false) : "cache").CopyFromSpan(new ReadOnlySpan<float>(_state[i], 0, size));
            }
            session.Run();
            var logits = session.GetOutput("logits");
            var values = logits.MapForRead<float>();
            try
            {
                if (values.Length != 4097) throw new InvalidDataException("Invalid speech decoder output.");
                values.CopyTo(_logits);
            }
            finally { logits.Unmap(); }
            MNNGenerationGraph.Validate(_logits, "speech logits");
            if (first && _quantized)
            {
                var packed = session.GetOutput("updated_cache");
                var data = packed.MapForRead<float>();
                try
                {
                    _state = new float[28][];
                    for (int layer = 0; layer < 14; ++layer)
                    for (int kind = 0; kind < 2; ++kind)
                    {
                        var destination = new float[checked(16 * total * 64)];
                        for (int head = 0; head < 16; ++head)
                        for (int t = 0; t < total; ++t)
                            data.Slice(((layer * 16 + head) * total + t) * 128 + kind * 64, 64)
                                .CopyTo(new Span<float>(destination, (head * total + t) * 64, 64));
                        _state[layer * 2 + kind] = destination;
                    }
                }
                finally { packed.Unmap(); }
            }
            else
            {
                for (int i = 0; i < _state.Length; ++i)
                {
                    var tensor = session.GetOutput(split ? StateName(i, true) : "updated_cache");
                    var data = tensor.MapForRead<float>();
                    try
                    {
                        int expected = split ? 16 * total * 64 : 14 * 16 * total * 128;
                        if (data.Length != expected) throw new InvalidDataException("Invalid speech KV shape.");
                        if (_state[i].Length < expected) _state[i] = new float[Math.Max(expected, checked(_state[i].Length * 2))];
                        data.CopyTo(_state[i]);
                    }
                    finally { tensor.Unmap(); }
                }
            }
            return _logits;
        }

        private static string StateName(int i, bool output) => (output ? "next_" : "") + (i % 2 == 0 ? "key_" : "value_") + i / 2;
        public void Dispose()
        {
            _decode?.Dispose(); _prefill?.Dispose(); _decodeModel?.Dispose(); _prefillModel?.Dispose(); _state = null;
        }
    }
}
