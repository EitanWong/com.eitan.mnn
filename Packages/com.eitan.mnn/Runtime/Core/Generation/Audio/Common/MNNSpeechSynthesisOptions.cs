namespace MNN.Unity
{
    /// <summary>Inputs shared by profiled speech synthesis. Reference paths are local WAV files.</summary>
    public sealed class MNNSpeechSynthesisOptions
    {
        public string Voice { get; set; }
        public string Language { get; set; }
        public string Precision { get; set; }
        public string ReferenceWavePath { get; set; }
        public string ReferenceText { get; set; }
        public int Steps { get; set; }
        public int Seed { get; set; } = 42;
        public float Speed { get; set; } = 1;
    }
}
