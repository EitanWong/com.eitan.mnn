namespace MNN.Unity
{
    /// <summary>Task data, not just a visualization. Alpha has source-image dimensions.
    /// Face vectors are unit normalized; recognition expects an aligned 112x112 face.
    /// Segmentation classes/confidence and its colored image use source-image dimensions.</summary>
    public sealed class MNNImageTaskResult
    {
        public string[] Labels { get; internal set; }

        public float[] Probabilities { get; internal set; }

        public MNNGeneratedImage Image { get; internal set; }

        public float[] Alpha { get; internal set; }

        public MNNImageSegmentationResult Segmentation { get; internal set; }

        public MNNPoseResult Pose { get; internal set; }

        public MNNImageDepthResult Depth { get; internal set; }

        public float[] Vector { get; internal set; }

        public MNNImagePoint[] Points { get; internal set; }

        public MNNDetection[] Detections { get; internal set; }

        public MNNImagePoint[][] FaceKeypoints { get; internal set; }
    }
}
