namespace MNN.Unity
{
    /// <summary>Explicit decoder candidates. INT8 modes retain High computation and
    /// the original FP16 prefill; complete speech quality requires evaluation.</summary>
    public enum MNNCosyVoice300MDecoderMode
    {
        Float16 = 0,
        ExperimentalInt8 = 1,
        ExperimentalFfnInt8 = 2
    }
}
