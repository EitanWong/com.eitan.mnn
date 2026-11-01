using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Stateful 24 kHz dual-channel endpointing. Feed exactly one 1920-sample tick per Push.</summary>
    public sealed class MNNDualTurnEndpointing : IDisposable
    {
        public const int SampleRate = 24000;
        public const int TickSamples = 1920;
        public const int DefaultThreadCount = 4;
        private const long MaximumTicks = int.MaxValue / 2;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private MNNGenerationGraph.Input[] _state;
        private long _ticks;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;
        public long TickCount { get { lock (_gate) return _ticks; } }

        private MNNDualTurnEndpointing(string directory, int threads)
        {
            if (!MNNDualTurnEndpointingModel.IsComplete(directory))
                throw new InvalidDataException("DualTurn endpointing model package failed verification.");
            _graph = new MNNGenerationGraph(Path.Combine(directory, MNNDualTurnEndpointingModel.Graph), threads,
                backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High,
                externalWeightsPath: Path.Combine(directory, MNNDualTurnEndpointingModel.Weights),
                gpuTuningMode: MNNGpuTuningMode.None);
            _state = MNNDualTurnEndpointingState.Initial();
        }

        public static MNNDualTurnEndpointing Load(string directory, int threadCount = DefaultThreadCount, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64)
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (backendType != MNNBackendType.Auto && backendType != MNNBackendType.CPU)
                throw new NotSupportedException("DualTurn endpointing currently supports the qualified CPU path.");
            MNNPlatformSupport.RequireSupported();
            return new MNNDualTurnEndpointing(Path.GetFullPath(directory), threadCount);
        }

        /// <summary>CH0 is user; omit agent to feed silence on CH1. PCM gain and signs are preserved.</summary>
        public MNNDualTurnEndpointingResult Push(float[] user, float[] agent = null, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                ValidateChannel(user, nameof(user));
                if (agent != null)
                    ValidateChannel(agent, nameof(agent));
                cancellationToken.ThrowIfCancellationRequested();
                if (_ticks == MaximumTicks)
                    throw new InvalidOperationException("Reset DualTurn before the native position counter exceeds Int32.");
                var pcm = new float[2 * TickSamples];
                Array.Copy(user, 0, pcm, 0, TickSamples);
                if (agent != null)
                    Array.Copy(agent, 0, pcm, TickSamples, TickSamples);
                var inputs = new MNNGenerationGraph.Input[_state.Length + 1];
                inputs[0] = new MNNGenerationGraph.Input("chunk", pcm, 2, TickSamples);
                Array.Copy(_state, 0, inputs, 1, _state.Length);
                var outputs = _graph.RunOutputs(MNNDualTurnEndpointingState.OutputNames, inputs);
                long tick = _ticks + 1;
                var state = MNNDualTurnEndpointingState.Next(outputs, tick);
                var result = ReadResult(outputs, tick);
                cancellationToken.ThrowIfCancellationRequested();
                _state = state;
                _ticks = tick;
                return result;
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                _state = MNNDualTurnEndpointingState.Initial();
                _ticks = 0;
            }
        }

        private static MNNDualTurnEndpointingResult ReadResult(MNNGenerationGraph.Output[] outputs, long tick)
        {
            float[] eot = MNNDualTurnEndpointingState.RequireFloat(outputs[0], "eot", 1, 1, 1);
            float[] vad = MNNDualTurnEndpointingState.RequireFloat(outputs[1], "vad", 1, 1, 2);
            float[] fvad = MNNDualTurnEndpointingState.RequireFloat(outputs[2], "fvad", 1, 1, 2, 4);
            RequireProbabilities(eot); RequireProbabilities(vad); RequireProbabilities(fvad);
            return new MNNDualTurnEndpointingResult(eot[0], vad, fvad, tick);
        }

        private static void RequireProbabilities(float[] values)
        {
            foreach (float value in values)
                if (!(value >= 0 && value <= 1))
                    throw new InvalidDataException("DualTurn prediction must be a finite probability.");
        }

        private static void ValidateChannel(float[] values, string name)
        {
            if (values == null)
                throw new ArgumentNullException(name);
            if (values.Length != TickSamples)
                throw new ArgumentException("DualTurn requires exactly 1920 samples at 24 kHz.", name);
            foreach (float value in values)
                if (!(value >= -float.MaxValue && value <= float.MaxValue))
                    throw new ArgumentException("DualTurn PCM must be finite.", name);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNDualTurnEndpointing));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _graph.Dispose();
                _state = Array.Empty<MNNGenerationGraph.Input>();
            }
        }
    }
}
