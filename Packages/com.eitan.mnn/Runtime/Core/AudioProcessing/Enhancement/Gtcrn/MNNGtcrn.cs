using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>GTCRN with one fixed spectrum frame and three explicit recurrent caches.</summary>
    public sealed partial class MNNGtcrn : IDisposable
    {
        public const int SampleRate = 16000, FrameLength = 512, HopLength = 256, FrequencyBins = 257;
        private readonly object _gate = new object();
        private readonly MNNInterpreter _interpreter;
        private readonly MNNSession _session;
        private readonly MNNTensor[] _inputs = new MNNTensor[4], _outputs = new MNNTensor[4];
        private readonly float[][] _states = {new float[16896], new float[96], new float[1056]};
        private bool _disposed, _cachePending;
        public MNNBackendType Backend => _session.ActualBackend;

        private MNNGtcrn(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNGtcrnModel.IsComplete(directory)) throw new InvalidDataException("GTCRN graph or profile verification failed.");
            backend = backend == MNNBackendType.Auto ? MNNBackendType.CPU : MNNAcceleration.Resolve(backend);
            string path = Path.Combine(directory, backend == MNNBackendType.CPU ? MNNGtcrnModel.GraphCpu : MNNGtcrnModel.GraphAccelerator);
            _interpreter = MNNInterpreter.CreateFromFile(path);
            try
            {
                if (backend != MNNBackendType.CPU)
                {
                    _interpreter.SetCacheFile(MNNInferenceCache.PathFor(path, backend, MNNPrecisionMode.High));
                    _cachePending = true;
                }
                _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = backend, ThreadCount = threads, PrecisionMode = MNNPrecisionMode.High});
                string[] names = {"mix", "conv_cache", "tra_cache", "inter_cache"};
                string[] outputs = {"enh", "conv_cache_out", "tra_cache_out", "inter_cache_out"};
                int[][] shapes = {new[]{1,257,1,2}, new[]{2,1,16,16,33}, new[]{2,3,1,1,16}, new[]{2,1,33,16}};
                for (int i = 0; i < 4; ++i)
                {
                    _inputs[i] = _session.GetInput(names[i]);
                    _outputs[i] = _session.GetOutput(outputs[i]);
                    CheckTensor(_inputs[i], shapes[i]);
                    CheckTensor(_outputs[i], shapes[i]);
                }
            }
            catch { _interpreter.Dispose(); throw; }
        }

        /// <summary>Auto uses CPU for 16 ms recurrent hops; explicit GPUs retain High precision.</summary>
        public static MNNGtcrn Load(string directory, int threadCount = 1, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType == MNNBackendType.NN) throw new NotSupportedException("GTCRN NNAPI graph/device parity has not been verified.");
            MNNPlatformSupport.RequireSupported();
            return new MNNGtcrn(Path.GetFullPath(directory), threadCount, backendType);
        }

        /// <summary>Clear caches between unrelated streams. One instance owns one stream.</summary>
        public void Reset() { lock (_gate) { ThrowIfDisposed(); ResetCore(); } }
        private void ResetCore() { foreach (var state in _states) Array.Clear(state, 0, state.Length); }

        /// <summary>Consume 257 complex bins, real/imaginary interleaved. Caller owns STFT scheduling.
        /// Reuses fixed native tensors and managed state; invoke on a worker, not the audio callback.</summary>
        public void ProcessSpectrum(float[] spectrum, float[] enhanced, CancellationToken cancellationToken = default)
        {
            if (spectrum == null || spectrum.Length != FrequencyBins * 2) throw new ArgumentException("Supply 257 interleaved complex bins.", nameof(spectrum));
            if (enhanced == null || enhanced.Length != FrequencyBins * 2) throw new ArgumentException("Output must contain 257 complex bins.", nameof(enhanced));
            for (int i = 0; i < spectrum.Length; ++i)
                if (float.IsNaN(spectrum[i]) || float.IsInfinity(spectrum[i])) throw new ArgumentException("Spectrum must be finite.", nameof(spectrum));
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                try { RunSpectrum(spectrum, enhanced); }
                catch { ResetCore(); throw; }
            }
        }

        private void RunSpectrum(float[] spectrum, float[] enhanced)
        {
            _inputs[0].CopyFromArray(spectrum);
            for (int i = 0; i < 3; ++i) _inputs[i + 1].CopyFromArray(_states[i]);
            _session.Run();
            CopyOutput(0, enhanced);
            for (int i = 0; i < 3; ++i) CopyOutput(i + 1, _states[i]);
            if (!_cachePending) return;
            _cachePending = false;
            try { _session.UpdateCache(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is MNNException)
            { UnityEngine.Debug.Log("[MNN] GTCRN cache update unavailable: " + error.Message); }
        }

        private void CopyOutput(int index, float[] destination)
        {
            var span = _outputs[index].MapForRead<float>();
            try
            {
                for (int i = 0; i < span.Length; ++i)
                {
                    float value = span[i];
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("GTCRN returned non-finite output or state.");
                    destination[i] = value;
                }
            }
            finally { _outputs[index].Unmap(); }
        }

        private static void CheckTensor(MNNTensor tensor, int[] shape)
        {
            var actual = tensor.Shape;
            if (tensor.DataType != MNNDataType.Float || actual.Length != shape.Length) throw new InvalidDataException("Unexpected GTCRN tensor interface.");
            for (int i = 0; i < shape.Length; ++i)
                if (actual[i] != shape[i]) throw new InvalidDataException("Unexpected GTCRN tensor shape.");
        }

        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(MNNGtcrn)); }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _interpreter.Dispose(); } }
    }
}
