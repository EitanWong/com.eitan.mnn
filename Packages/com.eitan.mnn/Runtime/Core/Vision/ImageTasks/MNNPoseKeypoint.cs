namespace MNN.Unity
{
    /// <summary>COCO's 17 joint indices. Left/right refer to the person's body.</summary>
    public enum MNNPoseJoint
    {
        Nose, LeftEye, RightEye, LeftEar, RightEar, LeftShoulder, RightShoulder,
        LeftElbow, RightElbow, LeftWrist, RightWrist, LeftHip, RightHip,
        LeftKnee, RightKnee, LeftAnkle, RightAnkle
    }

    /// <summary>Unclipped source pixel coordinates, top-left origin. Confidence
    /// is the model's joint score, not a calibrated person-presence probability.</summary>
    public sealed class MNNPoseKeypoint
    {
        public MNNPoseJoint Joint { get; }
        public float X { get; }
        public float Y { get; }
        public float Confidence { get; }
        public bool IsInBounds { get; }
        internal MNNPoseKeypoint(MNNPoseJoint joint, float x, float y, float confidence, int width, int height)
        {
            Joint = joint; X = x; Y = y; Confidence = confidence;
            IsInBounds = x >= 0 && x < width && y >= 0 && y < height;
        }
    }
}
