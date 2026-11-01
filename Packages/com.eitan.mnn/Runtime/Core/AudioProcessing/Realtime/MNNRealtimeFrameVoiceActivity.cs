using System;

namespace MNN.Unity
{
    /// <summary>Adapts a bounded frame detector to the shared capture processor. Turn completion stays independent.</summary>
    public sealed class MNNRealtimeFrameVoiceActivity : IMNNRealtimeVoiceActivity
    {
        private readonly Func<float[], int, float> _detect;
        private readonly Func<float[], int, MNNRealtimeVadUpdate> _detectUpdate;
        private readonly Action _reset;
        private readonly float _threshold;
        private long _position;
        private bool _active;
        public MNNRealtimeFrameVoiceActivity(Func<float[], int, float> detect, float threshold = .5f, Action reset = null)
        {
            if (float.IsNaN(threshold) || threshold <= 0 || threshold > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            _detect = detect ?? throw new ArgumentNullException(nameof(detect));
            _threshold = threshold;
            _reset = reset;
        }
        /// <summary>Preserves detector probability and hysteresis separately. The adapter owns
        /// sample positions so a detector-only reset does not reset the continuous PCM timeline.</summary>
        public MNNRealtimeFrameVoiceActivity(Func<float[], int, MNNRealtimeVadUpdate> detect, Action reset = null)
        {
            _detectUpdate = detect ?? throw new ArgumentNullException(nameof(detect));
            _reset = reset;
        }
        public static MNNRealtimeFrameVoiceActivity Energy(float rmsThreshold)
        {
            if (float.IsNaN(rmsThreshold) || rmsThreshold <= 0 || rmsThreshold > 1) throw new ArgumentOutOfRangeException(nameof(rmsThreshold));
            return new MNNRealtimeFrameVoiceActivity((samples, offset) =>
            {
                double sum = 0;
                for (int i = offset; i < offset + 512; ++i) sum += samples[i] * (double)samples[i];
                return Math.Sqrt(sum / 512) >= rmsThreshold ? 1 : 0;
            });
        }
        public MNNRealtimeVadUpdate ProcessFrame(float[] samples, int offset)
        {
            if (samples == null || offset < 0 || offset > samples.Length - 512) throw new ArgumentException("Supply a complete VAD frame.");
            var detected = _detectUpdate != null ? _detectUpdate(samples, offset) : default;
            float probability = _detectUpdate != null ? detected.SpeechProbability : _detect(samples, offset);
            if (float.IsNaN(probability) || probability < 0 || probability > 1) throw new InvalidOperationException("Invalid VAD probability.");
            bool active = _detectUpdate != null ? detected.IsSpeechActive : probability >= _threshold;
            var result = new MNNRealtimeVadUpdate(_position, probability, active, active && !_active, !active && _active);
            _active = active;
            _position = checked(_position + 512);
            return result;
        }
        public void Reset() { _reset?.Invoke(); _active = false; _position = 0; }
    }
}
