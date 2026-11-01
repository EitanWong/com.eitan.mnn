namespace MNN.Unity
{
    /// <summary>One detected text region and its CTC recognition result.</summary>
    public sealed class MNNTextRegion
    {
        public MNNTextBox Box { get; }
        public string Text => Recognition.Text;
        public float Confidence => Recognition.Confidence;
        public MNNTextRecognition Recognition { get; }
        internal MNNTextRegion(MNNTextBox box, MNNTextRecognition recognition) { Box = box; Recognition = recognition; }
    }
}
