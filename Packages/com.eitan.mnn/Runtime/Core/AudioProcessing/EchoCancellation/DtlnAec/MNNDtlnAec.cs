using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Two-stage 16 kHz mono echo cancellation with a synchronized playback reference.
    /// One instance owns one stream. Device/reference delay alignment belongs to the caller.</summary>
    public sealed partial class MNNDtlnAec : IDisposable
    {
        public const int SampleRate = 16000, BlockLength = 512, HopLength = 128, OutputDelaySamples = 384;
        private readonly object _gate = new object();
        private readonly MNNFftPlan _fft = new MNNFftPlan(BlockLength);
        private readonly MNNDtlnAecStage[] _stages = new MNNDtlnAecStage[2];
        private readonly float[][] _states;
        private readonly float[] _microphone = new float[512], _reference = new float[512], _overlap = new float[512];
        private readonly float[] _micMagnitude = new float[257], _refMagnitude = new float[257], _estimated = new float[512];
        private readonly double[] _real = new double[512], _imaginary = new double[512];
        private bool _disposed;
        public int Units { get; }
        public MNNBackendType Backend => _stages[0].Backend;

        private MNNDtlnAec(string directory, int threadCount, MNNBackendType backend)
        {
            var config = MNNDtlnAecConfig.Read(directory);
            Units = config.units;
            if (threadCount == 0) threadCount = Units == 512 ? 2 : 1;
            if (backend != MNNBackendType.CPU)
            {
                if (!config.gpuSplit) throw new NotSupportedException("Install the verified DTLN AEC split-GPU profile to use a GPU backend.");
                MNNDtlnAecModel.VerifyGpu(directory, Units);
            }
            MNNDtlnAecModel.Verify(directory, Units);
            _states = new[] {new float[Units * 4], new float[Units * 4]};
            try
            {
                for (int stage = 0; stage < 2; ++stage)
                    _stages[stage] = new MNNDtlnAecStage(directory, Units, stage + 1, threadCount, backend);
                if (_stages[0].Backend != _stages[1].Backend)
                    throw new NotSupportedException("DTLN AEC stages resolved to different backends.");
            }
            catch { Dispose(); throw; }
        }
        /// <summary>Auto uses CPU/High. Thread count0 selects one thread for128/256 and two for512.
        /// Verified GPU profiles retain source normalization on CPU and run recurrent/decoder work on GPU.</summary>
        public static MNNDtlnAec Load(string directory, int threadCount = 0, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 0 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType == MNNBackendType.NN) throw new NotSupportedException("DTLN AEC NNAPI graph/device compatibility has not been verified.");
            MNNPlatformSupport.RequireSupported();
            return new MNNDtlnAec(Path.GetFullPath(directory), threadCount,
                MNNAcceleration.Resolve(backendType == MNNBackendType.Auto ? MNNBackendType.CPU : backendType));
        }
        /// <summary>Clear buffers and both recurrent states between unrelated streams.</summary>
        public void Reset()
        {
            lock (_gate) { ThrowIfDisposed(); ResetCore(); }
        }
        private void ResetCore()
        {
            Array.Clear(_microphone, 0, 512); Array.Clear(_reference, 0, 512); Array.Clear(_overlap, 0, 512);
            foreach (var state in _states) Array.Clear(state, 0, state.Length);
        }
        /// <summary>Process exactly 128 normalized samples per input into a caller-owned 128-sample buffer.
        /// Output retains the upstream 384-sample (24 ms) delay. Calls are serialized; invoke on a worker, not an audio callback.</summary>
        public void ProcessHop(float[] microphone, float[] playbackReference, float[] output, CancellationToken cancellationToken = default)
        {
            ValidateAudio(microphone, playbackReference, HopLength);
            if (output == null || output.Length != HopLength) throw new ArgumentException("Output must contain exactly 128 samples.", nameof(output));
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                try { ProcessHopCore(microphone, playbackReference, output); }
                catch { ResetCore(); throw; }
            }
        }
        private void ProcessHopCore(ReadOnlySpan<float> microphone, ReadOnlySpan<float> reference, Span<float> output)
        {
            Array.Copy(_microphone, 128, _microphone, 0, 384);
            Array.Copy(_reference, 128, _reference, 0, 384);
            microphone.CopyTo(_microphone.AsSpan(384)); reference.CopyTo(_reference.AsSpan(384));
            for (int i = 0; i < 512; ++i)
            {
                // Pack two real signals into one complex FFT: Z = FFT(mic + i*ref).
                _real[i] = _microphone[i];
                _imaginary[i] = _reference[i];
            }
            _fft.Transform(_real, _imaginary);
            for (int i = 0; i < 257; ++i)
            {
                // X[k] = (Z[k] + conj(Z[-k]))/2, Y[k] = (Z[k] - conj(Z[-k]))/(2i).
                // Only the lower half is overwritten; each mirrored upper bin stays intact.
                int mirror = (512 - i) & 511;
                // Upstream rounds rfft to complex64 before magnitude and masking.
                float r = (float)((_real[i] + _real[mirror]) * 0.5);
                float im = (float)((_imaginary[i] - _imaginary[mirror]) * 0.5);
                float rr = (float)((_imaginary[i] + _imaginary[mirror]) * 0.5);
                float ri = (float)((_real[mirror] - _real[i]) * 0.5);
                _real[i] = r; _imaginary[i] = im;
                _micMagnitude[i] = (float)Math.Sqrt(r * r + im * im);
                _refMagnitude[i] = (float)Math.Sqrt(rr * rr + ri * ri);
            }
            RunStage(0, _micMagnitude, _refMagnitude);
            var mask = _stages[0].Signal.MapForRead<float>();
            try
            {
                for (int i = 0; i < 257; ++i)
                {
                    _real[i] = (float)((float)_real[i] * mask[i]);
                    _imaginary[i] = i == 0 || i == 256 ? 0 : (float)((float)_imaginary[i] * mask[i]);
                    if (i > 0 && i < 256) { _real[512 - i] = _real[i]; _imaginary[512 - i] = -_imaginary[i]; }
                }
            }
            finally { _stages[0].Signal.Unmap(); }
            _fft.Transform(_real, _imaginary, true);
            for (int i = 0; i < 512; ++i) _estimated[i] = (float)_real[i];
            RunStage(1, _estimated, _reference);
            Array.Copy(_overlap, 128, _overlap, 0, 384); Array.Clear(_overlap, 384, 128);
            var frame = _stages[1].Signal.MapForRead<float>();
            try
            {
                for (int i = 0; i < 512; ++i)
                {
                    _overlap[i] += frame[i];
                    if (float.IsNaN(_overlap[i]) || float.IsInfinity(_overlap[i])) throw new InvalidDataException("DTLN AEC produced nonfinite audio.");
                }
            }
            finally { _stages[1].Signal.Unmap(); }
            _overlap.AsSpan(0, 128).CopyTo(output);
        }
        private void RunStage(int stage, float[] first, float[] second) => _stages[stage].Run(first, second, _states[stage]);
        private static void ValidateAudio(float[] microphone, float[] reference, int length)
        {
            if (microphone == null || reference == null || microphone.Length != length || reference.Length != length || length == 0)
                throw new ArgumentException("Supply equal-length nonempty mono microphone and playback reference arrays.");
            for (int i = 0; i < length; ++i)
                if (float.IsNaN(microphone[i]) || float.IsInfinity(microphone[i]) || Math.Abs(microphone[i]) > 1 ||
                    float.IsNaN(reference[i]) || float.IsInfinity(reference[i]) || Math.Abs(reference[i]) > 1)
                    throw new ArgumentException("Inputs must contain finite normalized PCM in [-1,1].");
        }
        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MNNDtlnAec));
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                foreach (var stage in _stages) stage?.Dispose();
            }
        }
    }
}
