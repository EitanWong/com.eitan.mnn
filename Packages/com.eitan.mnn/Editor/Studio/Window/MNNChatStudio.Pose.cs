using System;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        [SerializeField] private float _poseScoreThreshold = .2f;
        private MNNImageTaskResult _poseSource;
        private MNNGeneratedImage _poseOverlay;
        private float _poseOverlayThreshold = -1;
        private static readonly int[,] PoseBones = {{0, 1}, {0, 2}, {1, 3}, {2, 4}, {5, 6}, {5, 7}, {7, 9}, {6, 8}, {8, 10}, {5, 11}, {6, 12}, {11, 12}, {11, 13}, {13, 15}, {12, 14}, {14, 16}};

        private void DrawPoseTool()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label("Human pose", MNNStudioUI.Heading);
                    GUILayout.Label("Estimate 17 COCO joints for one primary person. Choose a full-body photo or crop around one person. Low scores and occluded joints may be inaccurate; the model also returns estimates on empty scenes.", MNNStudioUI.Body);
                    GUILayout.Space(16);
                    using (new EditorGUI.DisabledScope(Busy))
                    {
                        DrawTaskAttachment(true);
                        string reason = GetTaskRunBlockedReason();
                        if (reason != null) EditorGUILayout.HelpBox(reason, MessageType.Info);
                        using (new EditorGUI.DisabledScope(reason != null))
                            if (MNNStudioUI.Button("Estimate pose", 150, primary: true)) RunTask();
                    }
                    if (Busy && MNNStudioUI.Button("Stop", 68)) StopReply();
                    var source = _retrieval?.ImageTask;
                    var pose = source?.Pose;
                    if (pose != null)
                    {
                        GUILayout.Space(16);
                        GUILayout.Label("Result · " + _retrieval.Seconds.ToString("F2") + " s · " + (_retrieval.BackendDescription ?? ""), MNNStudioUI.Caption);
                        _poseScoreThreshold = EditorGUILayout.Slider("Joint display threshold", _poseScoreThreshold, 0, 1);
                        var overlay = _poseSource == source && _poseOverlayThreshold == _poseScoreThreshold
                            ? _poseOverlay : RenderPoseOverlay(source, _poseScoreThreshold);
                        DrawGeneratedImage(overlay, showSeed: false);
                        // DrawGeneratedImage releases the previous preview/cache.
                        _poseOverlay = overlay; _poseSource = source; _poseOverlayThreshold = _poseScoreThreshold;
                        GUILayout.Label("Joint · source X / Y · confidence", MNNStudioUI.Caption);
                        foreach (var point in pose.Keypoints)
                            GUILayout.Label(point.Joint + "   " + point.X.ToString("F1") + " / " + point.Y.ToString("F1") + "   " + point.Confidence.ToString("F3") + (point.IsInBounds ? "" : "   outside image"), MNNStudioUI.Body);
                    }
                    else
                    {
                        _poseSource = null; _poseOverlay = null;
                    }
                    GUILayout.Space(24);
                }
            }
        }

        internal static MNNGeneratedImage RenderPoseOverlay(MNNImageTaskResult source, float threshold)
        {
            if (source?.Pose == null || source.Image == null) throw new ArgumentException("A pose and its source image are required.", nameof(source));
            if (float.IsNaN(threshold) || threshold < 0 || threshold > 1) throw new ArgumentOutOfRangeException(nameof(threshold));
            int width = source.Image.Width, height = source.Image.Height;
            if (source.Pose.Width != width || source.Pose.Height != height) throw new ArgumentException("Pose/image size mismatch.", nameof(source));
            var rgb = (byte[])source.Image.Rgb.Clone();
            int radius = Math.Max(1, Math.Min(width, height) / 120);
            bool Visible(MNNPoseKeypoint p) => p.IsInBounds && p.Confidence >= threshold;
            void Dot(int x, int y, bool joint)
            {
                for (int yy = Math.Max(0, y - radius); yy <= Math.Min(height - 1, y + radius); ++yy)
                    for (int xx = Math.Max(0, x - radius); xx <= Math.Min(width - 1, x + radius); ++xx)
                    {
                        if ((xx - x) * (xx - x) + (yy - y) * (yy - y) > radius * radius) continue;
                        int i = (yy * width + xx) * 3;
                        rgb[i] = joint ? (byte)255 : (byte)40; rgb[i + 1] = joint ? (byte)80 : (byte)240; rgb[i + 2] = 40;
                    }
            }
            for (int b = 0; b < PoseBones.GetLength(0); ++b)
            {
                var a = source.Pose.Keypoints[PoseBones[b, 0]]; var z = source.Pose.Keypoints[PoseBones[b, 1]];
                if (!Visible(a) || !Visible(z)) continue;
                // Bounds were checked in continuous source coordinates. Floor
                // keeps points just inside the last pixel within the image.
                int x = (int)a.X, y = (int)a.Y, x1 = (int)z.X, y1 = (int)z.Y;
                int dx = Math.Abs(x1 - x), dy = -Math.Abs(y1 - y), sx = x < x1 ? 1 : -1, sy = y < y1 ? 1 : -1, error = dx + dy;
                while (true)
                {
                    Dot(x, y, false);
                    if (x == x1 && y == y1) break;
                    int e = 2 * error;
                    if (e >= dy) { error += dy; x += sx; }
                    if (e <= dx) { error += dx; y += sy; }
                }
            }
            foreach (var point in source.Pose.Keypoints)
                if (Visible(point)) Dot((int)point.X, (int)point.Y, true);
            return new MNNGeneratedImage(rgb, width, height, "MoveNet SinglePose · joint threshold " + threshold.ToString("F2"), 0);
        }
    }
}
