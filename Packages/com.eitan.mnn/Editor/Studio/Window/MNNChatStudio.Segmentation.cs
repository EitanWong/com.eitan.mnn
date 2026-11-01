using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private void DrawSegmentationTool()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label("Semantic segmentation", MNNStudioUI.Heading);
                    GUILayout.Label("Separate a photo into 20 Pascal VOC object classes and background. Small objects may be lost at the model's 257 × 257 resolution.", MNNStudioUI.Body);
                    GUILayout.Space(16);
                    using (new EditorGUI.DisabledScope(Busy))
                    {
                        DrawTaskAttachment(true);
                        string reason = GetTaskRunBlockedReason();
                        if (reason != null) EditorGUILayout.HelpBox(reason, MessageType.Info);
                        using (new EditorGUI.DisabledScope(reason != null))
                            if (MNNStudioUI.Button("Segment image", 150, primary: true)) RunTask();
                    }
                    if (Busy && MNNStudioUI.Button("Stop", 68)) StopReply();
                    var mask = _retrieval?.ImageTask?.Segmentation;
                    if (mask != null)
                    {
                        GUILayout.Space(16);
                        GUILayout.Label("Result · " + _retrieval.Seconds.ToString("F2") + " s · " + (_retrieval.BackendDescription ?? ""), MNNStudioUI.Caption);
                        DrawGeneratedImage(_retrieval.ImageTask.Image, showSeed: false);
                        string[] labels = mask.Labels;
                        int[] counts = mask.ClassPixelCounts;
                        GUILayout.Space(12);
                        for (int c = 0; c < labels.Length; ++c)
                            if (counts[c] > 0)
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    var color = new Color32(MNNImageTask.SegmentationColor(c, 0), MNNImageTask.SegmentationColor(c, 1), MNNImageTask.SegmentationColor(c, 2), 255);
                                    Rect swatch = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
                                    EditorGUI.DrawRect(swatch, color);
                                    GUILayout.Label(labels[c], MNNStudioUI.Body);
                                    GUILayout.FlexibleSpace();
                                    GUILayout.Label((100.0 * counts[c] / ((double)mask.Width * mask.Height)).ToString("F1") + "%", MNNStudioUI.Caption);
                                }
                            }
                    }
                    GUILayout.Space(24);
                }
            }
        }
    }
}
