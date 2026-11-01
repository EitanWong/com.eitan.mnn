using System;

namespace MNN.Unity
{
    /// <summary>HTTP multimodal message; borrowed audio must remain immutable until the request completes.</summary>
    public readonly struct MNNOpenAIMessage
    {
        public MNNChatRole Role { get; }
        public string Text { get; }
        public float[] Audio { get; }
        public int SampleRate { get; }
        public MNNOpenAIMessage(MNNChatRole role, string text)
        {
            if (!Enum.IsDefined(typeof(MNNChatRole), role) || text == null) throw new ArgumentException("Invalid text message.");
            Role = role; Text = text; Audio = null; SampleRate = 0;
        }
        public MNNOpenAIMessage(float[] audio, int sampleRate)
        {
            Role = MNNChatRole.User; Text = null; Audio = audio ?? throw new ArgumentNullException(nameof(audio)); SampleRate = sampleRate;
        }
    }
}
