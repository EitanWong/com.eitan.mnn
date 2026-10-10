namespace MNN.Unity.Editor
{
    internal enum MNNStudioVoiceState
    {
        Ready,
        Listening,
        Thinking,
        Speaking,
        Muted,
        Error
    }

    /// <summary>Deterministic local silence endpointing; consumes real microphone RMS.
    /// This is level-based turn detection, not a speech-recognition/VAD model.</summary>
    internal sealed class MNNStudioVoiceActivity
    {
        private double _started, _lastVoice;
        private bool _heard;
        internal void Reset(double now)
        {
            _started = _lastVoice = now;
            _heard = false;
        }

        internal bool ShouldSend(double now, float level)
        {
            if (level >= .045f)
            {
                _heard = true;
                _lastVoice = now;
            }

            return _heard && now - _started >= .7 && now - _lastVoice >= 1.15;
        }
    }
}
