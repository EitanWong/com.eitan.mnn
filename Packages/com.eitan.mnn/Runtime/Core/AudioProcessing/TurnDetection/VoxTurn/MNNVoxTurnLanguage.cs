using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    // GPU language sessions have one immutable shape per supported batch size.
    // Causal padding does not alter any valid option position. At most two Metal
    // pairs and one shared CPU pair are retained across changing requests.
    internal sealed class MNNVoxTurnLanguage : IDisposable
    {
        private readonly string _directory;
        private readonly int _threads;
        private readonly MNNBackendType _backend;
        private readonly Dictionary<int, Pair> _pairs = new Dictionary<int, Pair>();
        private Pair _active;
        internal int MinimumLength(bool hasAudio) => Backend(hasAudio) == MNNBackendType.Metal ? 512 : 0;
        internal MNNBackendType FirstBackend => _active.First.Backend;
        internal MNNBackendType SecondBackend => _active.Second.Backend;

        internal MNNVoxTurnLanguage(string directory, int threads, MNNBackendType backend)
        {
            _directory = directory;
            _threads = threads;
            _backend = backend;
            _active = Get(1, backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend);
        }

        private MNNBackendType Backend(bool hasAudio) => _backend == MNNBackendType.Auto
            ? (hasAudio ? MNNBackendType.Metal : MNNBackendType.CPU) : _backend;

        private Pair Get(int batch, MNNBackendType backend)
        {
            if (batch != 1 && batch != 2) throw new InvalidDataException("Invalid VoxTurn batch.");
            int key = backend == MNNBackendType.Metal ? batch : 0;
            if (!_pairs.TryGetValue(key, out var pair))
            {
                pair = new Pair(_directory, _threads, backend);
                _pairs.Add(key, pair);
            }
            return pair;
        }

        internal float[] Run(float[] hidden, MNNVoxTurnPrompt.Batch batch, float[] cos, float[] sin, CancellationToken token)
        {
            var backend = Backend(Array.Exists(batch.AudioOffsets, offset => offset >= 0));
            if (backend == MNNBackendType.Metal && batch.Length != 512)
                throw new InvalidDataException("The fixed Metal profile supports up to 512 positions.");
            _active = Get(batch.Count, backend);
            hidden = Run(_active.First, hidden, batch, cos, sin, token);
            return Run(_active.Second, hidden, batch, cos, sin, token);
        }

        private static float[] Run(MNNGenerationGraph graph, float[] hidden, MNNVoxTurnPrompt.Batch batch, float[] cos, float[] sin, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int count = batch.Count, length = batch.Length;
            var result = graph.Run("output", new MNNGenerationGraph.Input("values", hidden, count, length, 1024),
                new MNNGenerationGraph.Input("attention_mask", batch.Mask, count, 1, length, length),
                new MNNGenerationGraph.Input("cos", cos, count, length, 128),
                new MNNGenerationGraph.Input("sin", sin, count, length, 128));
            if (result.Length != count * length * 1024) throw new InvalidDataException("Invalid VoxTurn language shape.");
            return result;
        }

        public void Dispose()
        {
            foreach (var pair in _pairs.Values) pair.Dispose();
            _pairs.Clear();
        }

        private sealed class Pair : IDisposable
        {
            internal readonly MNNGenerationGraph First, Second;
            internal Pair(string directory, int threads, MNNBackendType backend)
            {
                try
                {
                    First = Graph(directory, "decoder_0.mnn", threads, backend);
                    Second = Graph(directory, "decoder_1.mnn", threads, backend);
                }
                catch { Dispose(); throw; }
            }
            private static MNNGenerationGraph Graph(string directory, string file, int threads, MNNBackendType backend) =>
                new MNNGenerationGraph(Path.Combine(directory, file), threads, backendType: backend,
                    precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
            public void Dispose()
            {
                Second?.Dispose();
                First?.Dispose();
            }
        }
    }
}
