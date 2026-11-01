using System;

namespace MNN.Unity
{
    /// <summary>Worker-owned streaming audio processor that emits fixed VAD frames from bounded PCM blocks.</summary>
    public interface IMNNRealtimeVoiceInputProcessor
    {
        MNNPcmFormat InputFormat { get; }
        int MaximumInputBlockFrames { get; }
        void Process(float[] monoPcm, int offset, int inputFrames, Action<MNNRealtimeVadUpdate> onUpdate,
            bool discontinuity = false, Action<float[], int, MNNRealtimeVadUpdate> onFrame = null);
        bool Complete(Action<MNNRealtimeVadUpdate> onUpdate,
            Action<float[], int, MNNRealtimeVadUpdate> onFrame = null);
        void Reset();
    }
}
