namespace MNN.Unity
{
    /// <summary>CTC text with mean confidence of the retained nonblank symbols.</summary>
    public sealed class MNNTextRecognition
    {
        public string Text { get; }
        public float Confidence { get; }
        internal MNNTextRecognition(string text, float confidence) { Text = text; Confidence = confidence; }
    }
}
