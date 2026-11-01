using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>SinglePose estimates one primary person, even on an empty scene.
    /// All 17 COCO joints are retained, including low scores and padding points.</summary>
    public sealed class MNNPoseResult
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<MNNPoseKeypoint> Keypoints { get; }
        internal MNNPoseResult(int width, int height, MNNPoseKeypoint[] points)
        {
            Width = width; Height = height;
            Keypoints = Array.AsReadOnly((MNNPoseKeypoint[])points.Clone());
        }
        public MNNPoseKeypoint GetKeypoint(MNNPoseJoint joint)
        {
            if ((int)joint < 0 || (int)joint >= Keypoints.Count) throw new ArgumentOutOfRangeException(nameof(joint));
            return Keypoints[(int)joint];
        }
    }
}
