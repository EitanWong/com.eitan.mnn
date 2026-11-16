using System;

namespace MNN.Unity
{
    /// <summary>Camera requests are negotiated by the device. Automatic mode balances face detail,
    /// capture latency and inference cost; it is not a measured universal accuracy optimum.</summary>
    [Serializable]
    public sealed class MNNCameraSettings
    {
        public string deviceName = "";
        public bool automaticQuality = true;
        public int width = 1280;
        public int height = 720;
        public int framesPerSecond = 30;
        public int detectionFramesPerSecond = 10;
        public int sleepFramesPerSecond = 2;
        public int maximumImageEdge = 640;
        public float startupTimeoutSeconds = 15;
        /// <summary>Legacy persisted preference. SiriAI now always previews its active camera in Vision settings.</summary>
        public bool showPreview;
        public int RequestedWidth => automaticQuality ? 1280 : width;
        public int RequestedHeight => automaticQuality ? 720 : height;
        public int RequestedFramesPerSecond => automaticQuality ? 30 : framesPerSecond;
        public int DetectionFramesPerSecond => automaticQuality ? 10 : detectionFramesPerSecond;
        public int SleepFramesPerSecond => automaticQuality ? 2 : sleepFramesPerSecond;
        public int MaximumImageEdge => automaticQuality ? 640 : maximumImageEdge;
        public MNNCameraSettings Snapshot() => (MNNCameraSettings)MemberwiseClone();
        public void Validate()
        {
            if (width < 160 || width > 3840 || height < 120 || height > 2160 || framesPerSecond < 1 || framesPerSecond > 60 ||
                detectionFramesPerSecond < 1 || detectionFramesPerSecond > 30 || sleepFramesPerSecond < 1 || sleepFramesPerSecond > 10 ||
                sleepFramesPerSecond > detectionFramesPerSecond || maximumImageEdge < 160 || maximumImageEdge > 1280 ||
                float.IsNaN(startupTimeoutSeconds) || float.IsInfinity(startupTimeoutSeconds) || startupTimeoutSeconds < 1 || startupTimeoutSeconds > 120)
                throw new ArgumentException("Select valid camera dimensions, capture/detection rates and startup timeout.");
        }
        /// <summary>Exact saved choice wins; a disconnected explicit choice is not silently replaced.</summary>
        public static int SelectDevice(string[] names, bool[] frontFacing, string requested)
        {
            if (names == null || frontFacing == null || names.Length != frontFacing.Length) throw new ArgumentException("Supply matching camera metadata.");
            if (!string.IsNullOrEmpty(requested)) return Array.IndexOf(names, requested);
            for (int i = 0; i < names.Length; ++i) if (frontFacing[i]) return i;
            return names.Length == 0 ? -1 : 0;
        }
    }
}
