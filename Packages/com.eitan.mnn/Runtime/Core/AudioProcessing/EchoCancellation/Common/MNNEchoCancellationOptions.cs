using System;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNEchoCancellationOptions
    {
        public bool automaticDelay = true;
        public int referenceDelayMilliseconds = 80;
        public void Validate()
        {
            if (referenceDelayMilliseconds < 0 || referenceDelayMilliseconds > 500)
                throw new ArgumentOutOfRangeException(nameof(referenceDelayMilliseconds));
        }
    }
    /// <summary>One worker-owned 16 kHz/128-sample echo stage with 384 samples of output delay.
    /// Calls are serialized by MNNEchoCancellationWorker. Implementations must permit release retry.</summary>
    public interface IMNNEchoHopProcessor : IDisposable
    {
        void Reset();
        void ProcessHop(float[] microphone, float[] reference, float[] output);
    }
    public sealed class MNNDtlnEchoHopProcessor : IMNNEchoHopProcessor
    {
        private readonly MNNDtlnAec _model;
        public MNNDtlnEchoHopProcessor(MNNDtlnAec model) => _model = model ?? throw new ArgumentNullException(nameof(model));
        public void Reset() => _model.Reset();
        public void ProcessHop(float[] microphone, float[] reference, float[] output) => _model.ProcessHop(microphone, reference, output);
        public void Dispose() => _model.Dispose();
    }
}
