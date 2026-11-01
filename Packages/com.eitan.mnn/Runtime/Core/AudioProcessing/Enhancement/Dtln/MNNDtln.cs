using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Two-stage 16 kHz DTLN enhancement. One instance owns one stream.
    /// Fixed graph sessions and DSP buffers are reused across 128-sample hops.</summary>
    public sealed partial class MNNDtln : IDisposable
    {
        public const int SampleRate = 16000, FrameLength = 512, HopLength = 128, OutputDelaySamples = 384;
        private readonly object _gate = new object();
        private readonly MNNFftPlan _fft = new MNNFftPlan(FrameLength);
        private readonly MNNInterpreter[] _interpreters = new MNNInterpreter[2];
        private readonly MNNSession[] _sessions = new MNNSession[2];
        private readonly MNNTensor[,] _inputs = new MNNTensor[2, 2], _outputs = new MNNTensor[2, 2];
        private readonly bool[] _cachePending = new bool[2];
        private readonly float[][] _states = {new float[512], new float[512]};
        private readonly float[] _window = new float[512], _overlap = new float[512];
        private readonly float[] _magnitude = new float[257], _estimated = new float[512];
        private readonly double[] _real = new double[512], _imaginary = new double[512];
        private bool _disposed;
        public MNNBackendType Stage1Backend => _sessions[0].ActualBackend;
        public MNNBackendType Stage2Backend => _sessions[1].ActualBackend;

        private MNNDtln(string directory, int threads, MNNBackendType backend)
        {
            if (!MNNDtlnModel.IsComplete(directory)) throw new InvalidDataException("DTLN enhancement graph or profile verification failed.");
            backend = backend == MNNBackendType.Auto ? MNNBackendType.CPU : MNNAcceleration.Resolve(backend);
            bool compact = backend != MNNBackendType.CPU;
            try
            {
                for (int stage = 0; stage < 2; ++stage) LoadStage(directory, stage, threads, backend, compact);
            }
            catch { Dispose(); throw; }
        }
        /// <summary>Auto uses CPU/High for short recurrent hops. Explicit GPU uses compact weights/High.
        /// Backend properties report each session; individual operators may use MNN's CPU fallback.</summary>
        public static MNNDtln Load(string directory, int threadCount = 1, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType == MNNBackendType.NN) throw new NotSupportedException("The DTLN NNAPI graph contract has not passed numeric validation.");
            MNNPlatformSupport.RequireSupported();
            return new MNNDtln(Path.GetFullPath(directory), threadCount, backendType);
        }
        private void LoadStage(string directory, int stage, int threads, MNNBackendType backend, bool compact)
        {
            string path = Path.Combine(directory, MNNDtlnModel.Graph(stage + 1, compact));
            var interpreter = MNNInterpreter.CreateFromFile(path);
            _interpreters[stage] = interpreter;
            if (backend != MNNBackendType.CPU)
            {
                try
                {
                    interpreter.SetCacheFile(MNNInferenceCache.PathFor(path, backend, MNNPrecisionMode.High));
                    _cachePending[stage] = true;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is MNNException)
                {
                    UnityEngine.Debug.Log("[MNN] DTLN GPU cache unavailable: " + error.Message);
                }
            }
            var session = interpreter.CreateSession(new MNNSessionConfig{BackendType = backend, ThreadCount = threads, PrecisionMode = MNNPrecisionMode.High});
            _sessions[stage] = session;
            _inputs[stage, 0] = session.GetInput(stage == 0 ? "input_2" : "input_4");
            _inputs[stage, 1] = session.GetInput(stage == 0 ? "input_3" : "input_5");
            _outputs[stage, 0] = session.GetOutput(stage == 0 ? "activation_2" : "conv1d_3");
            _outputs[stage, 1] = session.GetOutput(stage == 0 ? "tf_op_layer_stack_2" : "tf_op_layer_stack_5");
            CheckTensor(_inputs[stage, 0], new[] {1, 1, stage == 0 ? 257 : 512});
            CheckTensor(_outputs[stage, 0], new[] {1, 1, stage == 0 ? 257 : 512});
            CheckTensor(_inputs[stage, 1], new[] {1, 2, 128, 2});
            CheckTensor(_outputs[stage, 1], new[] {1, 2, 128, 2});
        }
        private static void CheckTensor(MNNTensor tensor, int[] shape)
        {
            if (tensor.DataType != MNNDataType.Float || tensor.Shape.Length != shape.Length)
                throw new InvalidDataException("Unexpected DTLN tensor interface.");
            for (int i = 0; i < shape.Length; ++i)
                if (tensor.Shape[i] != shape[i]) throw new InvalidDataException("Unexpected DTLN tensor shape.");
        }
        /// <summary>Clear both recurrent states and DSP history between unrelated streams.</summary>
        public void Reset()
        {
            lock (_gate) { ThrowIfDisposed(); ResetCore(); }
        }
        private void ResetCore()
        {
            Array.Clear(_window, 0, FrameLength);
            Array.Clear(_overlap, 0, FrameLength);
            foreach (var state in _states) Array.Clear(state, 0, state.Length);
        }
        /// <summary>Consume exactly 128 normalized mono samples. Output retains the 384-sample delay.
        /// Feed three zero hops to drain the synthesis tail. Invoke on a worker, not an audio callback.</summary>
        public void ProcessHop(float[] input, float[] output, CancellationToken cancellationToken = default)
        {
            ValidateAudio(input, HopLength, cancellationToken);
            if (output == null || output.Length != HopLength) throw new ArgumentException("Output must contain exactly 128 samples.", nameof(output));
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                try { ProcessHopCore(input, output); }
                catch { ResetCore(); throw; }
            }
        }
        private void ProcessHopCore(ReadOnlySpan<float> input, Span<float> output)
        {
            Array.Copy(_window, HopLength, _window, 0, OutputDelaySamples);
            input.CopyTo(_window.AsSpan(OutputDelaySamples));
            for (int i = 0; i < FrameLength; ++i) { _real[i] = _window[i]; _imaginary[i] = 0; }
            _fft.Transform(_real, _imaginary);
            for (int bin = 0; bin < _magnitude.Length; ++bin)
                _magnitude[bin] = (float)Math.Sqrt(_real[bin] * _real[bin] + _imaginary[bin] * _imaginary[bin]);
            RunStage(0, _magnitude);
            ApplyMask();
            _fft.Transform(_real, _imaginary, true);
            for (int i = 0; i < FrameLength; ++i) _estimated[i] = (float)_real[i];
            RunStage(1, _estimated);
            Array.Copy(_overlap, HopLength, _overlap, 0, OutputDelaySamples);
            Array.Clear(_overlap, OutputDelaySamples, HopLength);
            var frame = _outputs[1, 0].MapForRead<float>();
            try
            {
                for (int i = 0; i < FrameLength; ++i)
                {
                    _overlap[i] += frame[i];
                    if (float.IsNaN(_overlap[i]) || float.IsInfinity(_overlap[i])) throw new InvalidDataException("DTLN produced nonfinite audio.");
                }
            }
            finally { _outputs[1, 0].Unmap(); }
            _overlap.AsSpan(0, HopLength).CopyTo(output);
        }
        private void ApplyMask()
        {
            var mask = _outputs[0, 0].MapForRead<float>();
            try
            {
                for (int bin = 0; bin < _magnitude.Length; ++bin)
                {
                    // Upstream rounds magnitude*mask to FP32, retaining double-precision FFT phase.
                    double original = Math.Sqrt(_real[bin] * _real[bin] + _imaginary[bin] * _imaginary[bin]);
                    double gain = original == 0 ? 0 : (float)(_magnitude[bin] * mask[bin]) / original;
                    _real[bin] *= gain;
                    _imaginary[bin] = bin == 0 || bin == 256 ? 0 : _imaginary[bin] * gain;
                    if (bin > 0 && bin < 256) { _real[512 - bin] = _real[bin]; _imaginary[512 - bin] = -_imaginary[bin]; }
                }
            }
            finally { _outputs[0, 0].Unmap(); }
        }
        private void RunStage(int stage, float[] features)
        {
            _inputs[stage, 0].CopyFromArray(features);
            _inputs[stage, 1].CopyFromArray(_states[stage]);
            _sessions[stage].Run();
            var state = _outputs[stage, 1].MapForRead<float>();
            try { state.CopyTo(_states[stage]); }
            finally { _outputs[stage, 1].Unmap(); }
            if (!_cachePending[stage]) return;
            _cachePending[stage] = false;
            try { _sessions[stage].UpdateCache(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is MNNException)
            {
                UnityEngine.Debug.Log("[MNN] DTLN GPU cache update unavailable: " + error.Message);
            }
        }
        private static void ValidateAudio(float[] input, int length, CancellationToken token)
        {
            if (input == null || length == 0 || input.Length != length) throw new ArgumentException("Supply nonempty mono PCM with the required sample count.", nameof(input));
            for (int i = 0; i < input.Length; ++i)
            {
                if ((i & 16383) == 0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(input[i]) || float.IsInfinity(input[i]) || Math.Abs(input[i]) > 1)
                    throw new ArgumentException("Audio must contain finite normalized PCM in [-1,1].", nameof(input));
            }
        }
        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MNNDtln));
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                foreach (var interpreter in _interpreters) interpreter?.Dispose();
            }
        }
    }
}
