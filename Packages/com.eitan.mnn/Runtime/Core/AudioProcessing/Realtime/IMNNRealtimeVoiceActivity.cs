namespace MNN.Unity
{
    /// <summary>Single-worker fixed-frame detector: 512 normalized mono samples at 16 kHz.</summary>
    public interface IMNNRealtimeVoiceActivity
    {
        MNNRealtimeVadUpdate ProcessFrame(float[] samples, int offset);
        void Reset();
    }
}
