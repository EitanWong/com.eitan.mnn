using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>CueLight Nano and Nano-SSM causal 20 ms speech and turn detection with author replay and endpoint policies.</summary>
    public sealed class MNNCueLightTurnDetector : IDisposable
    {
        public const int SampleRate = 16000, FrameSamples = 320, DefaultThreadCount = 1;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNCueLightProfile _profile;
        private readonly MNNCueLightFeatures _features;
        private readonly Queue<float[]> _history = new Queue<float[]>();
        private float[][] _state;
        private MNNCueLightOnset _onset;
        private MNNCueLightEndpoint _snappy, _careful;
        private int _resets;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;
        public string ModelName => _profile.Name;

        private MNNCueLightTurnDetector(string directory, int threads, MNNBackendType backend)
        {
            _profile = MNNCueLightModel.ReadVerified(directory) ?? throw new InvalidDataException("CueLight profile or runtime file verification failed.");
            if (backend == MNNBackendType.Metal && !_profile.SupportsMetal)
                throw new NotSupportedException("CueLight Nano-SSM Metal execution has not passed source parity and repeatability checks.");
            _features = new MNNCueLightFeatures(directory);
            if (backend == MNNBackendType.Auto) backend = MNNBackendType.CPU;
            string file = backend == MNNBackendType.Metal ? MNNCueLightModel.MetalGraph : MNNCueLightModel.CpuGraph;
            _graph = new MNNGenerationGraph(Path.Combine(directory, file), threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None);
            ResetCore();
        }
        public static MNNCueLightTurnDetector Load(string directory, int threadCount = DefaultThreadCount, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal)
                throw new NotSupportedException("CueLight execution is unverified on this backend.");
            MNNPlatformSupport.RequireSupported();
            return new MNNCueLightTurnDetector(Path.GetFullPath(directory), threadCount, backendType);
        }
        /// <summary>Consume exactly 320 finite mono samples at 16 kHz. State persists until Reset or Analyze.</summary>
        public MNNCueLightFrame Push(float[] chunk, bool resetOnTurnStart = true, CancellationToken cancellationToken = default)
        {
            Validate(chunk, FrameSamples, cancellationToken);
            if (chunk.Length != FrameSamples) throw new ArgumentException("Push exactly 320 samples.", nameof(chunk));
            lock (_gate) { RequireAlive(); return PushCore(chunk, resetOnTurnStart, cancellationToken); }
        }
        /// <summary>Analyze 1–960000 finite 16 kHz mono samples with fresh state. The last frame is zero padded.</summary>
        public MNNCueLightAnalysis Analyze(float[] samples, int sampleRate = SampleRate, bool resetOnTurnStart = true, CancellationToken cancellationToken = default)
        {
            if (sampleRate != SampleRate) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            Validate(samples, 960000, cancellationToken);
            lock (_gate)
            {
                RequireAlive(); ResetCore();
                var frames = new MNNCueLightFrame[(samples.Length + 319) / 320]; var chunk = new float[320];
                for (int i = 0; i < frames.Length; ++i)
                {
                    cancellationToken.ThrowIfCancellationRequested(); Array.Clear(chunk, 0, 320);
                    Array.Copy(samples, i * 320, chunk, 0, Math.Min(320, samples.Length - i * 320));
                    frames[i] = PushCore(chunk, resetOnTurnStart, cancellationToken);
                }
                return new MNNCueLightAnalysis(frames, (double)samples.Length / SampleRate);
            }
        }
        public MNNCueLightAnalysis AnalyzeWaveFile(string path, bool resetOnTurnStart = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pcm = MNNWaveAudio.Read(path, out int rate); return Analyze(pcm, rate, resetOnTurnStart, cancellationToken);
        }
        private MNNCueLightFrame PushCore(float[] chunk, bool resetOnTurnStart, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var feature = _features.Push(chunk); _history.Enqueue(feature);
            if (_history.Count > 18) _history.Dequeue();
            var probabilities = Run(feature); float speech = probabilities[0];
            if (resetOnTurnStart && _onset.Update(speech))
            {
                _state = InitialState();
                foreach (var previous in _history) { token.ThrowIfCancellationRequested(); probabilities = Run(previous); }
                ++_resets;
            }
            // The author's p_speech is measured before replay, while p_end is measured after replay.
            probabilities[0] = speech;
            return new MNNCueLightFrame(probabilities, _resets, _snappy.Update(speech, probabilities[1]), _careful.Update(speech, probabilities[1]));
        }
        private float[] Run(float[] feature)
        {
            var result = _graph.RunOutputs(new[] { "logits", "next_conv1", "next_conv2", "next_hidden0", "next_hidden1", "next_hidden2" },
                new MNNGenerationGraph.Input("feature", feature, 1, 128),
                new MNNGenerationGraph.Input("conv1", _state[0], 1, 128, 2),
                new MNNGenerationGraph.Input("conv2", _state[1], 1, _profile.HiddenDimension, 2),
                new MNNGenerationGraph.Input("hidden0", _state[2], 1, _profile.StateSize),
                new MNNGenerationGraph.Input("hidden1", _state[3], 1, _profile.StateSize),
                new MNNGenerationGraph.Input("hidden2", _state[4], 1, _profile.StateSize));
            var logits = Require(result[0], 1, 6); var probability = new float[6];
            _state[0] = Require(result[1], 1, 128, 2); _state[1] = Require(result[2], 1, _profile.HiddenDimension, 2);
            for (int i = 0; i < 3; ++i) _state[i + 2] = Require(result[i + 3], 1, _profile.StateSize);
            for (int i = 0; i < 6; ++i)
            {
                if (float.IsNaN(logits[i]) || float.IsInfinity(logits[i])) throw new InvalidDataException("Nonfinite CueLight output.");
                probability[i] = (float)(1 / (1 + Math.Exp(-(double)logits[i])));
            }
            return probability;
        }
        private static float[] Require(MNNGenerationGraph.Output output, params int[] shape)
        {
            if (output.Shape.Length != shape.Length) throw new InvalidDataException("Unexpected CueLight tensor rank.");
            for (int i = 0; i < shape.Length; ++i)
                if (output.Shape[i] != shape[i]) throw new InvalidDataException("Unexpected CueLight tensor shape.");
            return output.Values;
        }
        private float[][] InitialState() => new[] { new float[256], new float[_profile.HiddenDimension * 2],
            new float[_profile.StateSize], new float[_profile.StateSize], new float[_profile.StateSize] };
        private void ResetCore()
        {
            _features.Reset(); _state = InitialState(); _history.Clear(); _resets = 0;
            _onset = new MNNCueLightOnset();
            _snappy = new MNNCueLightEndpoint(_profile.SnappyThreshold, .5, 1.5);
            _careful = new MNNCueLightEndpoint(_profile.CarefulThreshold, .7, 2);
        }
        public void Reset() { lock (_gate) { RequireAlive(); ResetCore(); } }
        private static void Validate(float[] samples, int maximum, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (samples == null || samples.Length < 1 || samples.Length > maximum) throw new ArgumentException("Supply a supported nonempty mono PCM array.", nameof(samples));
            for (int i = 0; i < samples.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i])) throw new ArgumentException("Supply finite floating-point PCM.", nameof(samples));
            }
        }
        private void RequireAlive() { if (_disposed) throw new ObjectDisposedException(nameof(MNNCueLightTurnDetector)); }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); } }
    }
}
