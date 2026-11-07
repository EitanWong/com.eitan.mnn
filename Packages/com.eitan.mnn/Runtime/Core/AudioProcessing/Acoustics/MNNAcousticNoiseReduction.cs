using System;
using System.Diagnostics;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Capture-worker streaming enhancement and separately owned utterance enhancement.</summary>
    public sealed class MNNAcousticNoiseReduction : IDisposable
    {
        private IMNNAcousticEnhancer _model;
        private readonly IMNNAcousticModelFactory _factory;
        private long _frames, _ticks;
        public MNNAcousticNoiseReduction(IMNNAcousticModelFactory factory = null) => _factory = factory ?? new MNNAcousticMnnModelFactory();
        public int DelaySamples => _model?.DelaySamples ?? 0;
        public bool Enabled => _model?.Streaming == true;
        public string Diagnostics => "Denoising: " + (_model?.Diagnostics ?? "off") + "; denoiser compute RTF: " +
            (Interlocked.Read(ref _ticks) * 16000.0 / Math.Max(1, Interlocked.Read(ref _frames)) / Stopwatch.Frequency).ToString("F3");
        public void Load(string root, string directory)
        {
            if (_model != null) throw new InvalidOperationException("Release the previous enhancement model before loading again.");
            _model = _factory.CreateEnhancer(MNNAcousticModelResources.ResolvePath(root, directory)) ??
                throw new InvalidOperationException("Enhancement factory returned no model.");
            if (_model.DelaySamples < 0 || _model.DelaySamples > 512)
                throw new NotSupportedException("Streaming enhancement must delay at most one 512-sample frame.");
        }
        public float[] EnhanceUtterance(float[] samples, CancellationToken token) =>
            _model == null ? samples : _model.EnhanceUtterance(samples, token);
        public void Observe(int samples) => Interlocked.Add(ref _frames, samples);
        public void Enhance(float[] samples, int offset)
        {
            if (samples == null || offset < 0 || offset > samples.Length - 512) throw new ArgumentException("Supply a complete 512-sample enhancement frame.");
            if (!Enabled) throw new InvalidOperationException("No streaming enhancer is loaded.");
            long started = Stopwatch.GetTimestamp();
            _model.EnhanceFrame(samples, offset);
            Interlocked.Add(ref _ticks, Stopwatch.GetTimestamp() - started);
        }
        public void Reset() => _model?.Reset();
        public void Dispose()
        {
            System.Collections.Generic.List<Exception> errors = null;
            MNNAcousticModelResources.Release(ref _model, ref errors);
            MNNAcousticModelResources.ThrowIfFailed(errors);
        }
    }
}
