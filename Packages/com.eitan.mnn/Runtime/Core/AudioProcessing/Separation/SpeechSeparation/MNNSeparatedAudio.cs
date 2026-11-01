using System;

namespace MNN.Unity
{
    /// <summary>Two speaker waveforms returned by a source-separation graph.</summary>
    public sealed class MNNSeparatedAudio
    {
        public readonly float[] Speaker0;
        public readonly float[] Speaker1;
        public readonly int SampleRate;

        internal MNNSeparatedAudio(float[] speaker0, float[] speaker1, int sampleRate)
        {
            Speaker0 = speaker0 ?? throw new ArgumentNullException(nameof(speaker0));
            Speaker1 = speaker1 ?? throw new ArgumentNullException(nameof(speaker1));
            if (speaker0.Length != speaker1.Length || sampleRate <= 0)
                throw new ArgumentException("Separated waveforms must have equal lengths and a positive sample rate.");
            SampleRate = sampleRate;
        }
    }
}
