namespace MNN.Unity
{
    /// <summary>Unity runtime environment used by model loading and cache selection.</summary>
    public static class MNNRuntime
    {
        /// <summary>Capture Unity paths and device information on the Unity main thread before loading on workers.
        /// Safe to repeat after a device change. Player startup also performs this automatically.</summary>
        public static void Initialize() => MNNInferenceCache.CaptureEnvironment();
        /// <summary>Captured GPU and driver identity; empty before initialization.</summary>
        public static string GraphicsDevice => MNNInferenceCache.GraphicsDevice;
    }
}
