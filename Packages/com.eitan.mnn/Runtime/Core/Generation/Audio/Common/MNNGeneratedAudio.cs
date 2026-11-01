namespace MNN.Unity
{
    public sealed class MNNGeneratedAudio
    {
        public float[] Waveform { get; }

        public int SampleRate { get; }

        public string Text { get; }

        internal MNNGeneratedAudio(float[] waveform, int rate, string text)
        {
            Waveform = waveform;
            SampleRate = rate;
            Text = text;
        }
    }
}
