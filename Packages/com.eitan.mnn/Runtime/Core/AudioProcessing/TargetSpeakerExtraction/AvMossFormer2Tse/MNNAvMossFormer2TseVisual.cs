namespace MNN.Unity
{
    /// <summary>Compatible AV-MossFormer2 entry point for ClearerVoice target-face preparation.</summary>
    public static class MNNAvMossFormer2TseVisual
    {
        public static float[] PrepareFrame(MNNRgbImage crop) => MNNClearerVoiceFaceFrames.PrepareFrame(crop);
    }
}
