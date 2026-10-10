using System;

namespace MNN.Unity
{
    [Flags]
    public enum MNNModelCapabilities
    {
        Text = 0,
        Vision = 1,
        AudioInput = 2,
        SpeechOutput = 4
    }
}
