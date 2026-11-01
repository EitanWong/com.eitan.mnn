using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private void DrawDepthTool()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label("Depth estimation", MNNStudioUI.Heading);
                    GUILayout.Label("Estimate relative inverse depth from one image. Lighter areas are closer. Values have no metric unit; use them for relative ordering within this image. Very wide or tall images may need cropping to fit the 3072-patch limit.", MNNStudioUI.Body);
                    GUILayout.Space(16);
                    using (new EditorGUI.DisabledScope(Busy))
                    {
                        DrawTaskAttachment(true);
                        string reason = GetTaskRunBlockedReason();
                        if (reason != null) EditorGUILayout.HelpBox(reason, MessageType.Info);
                        using (new EditorGUI.DisabledScope(reason != null))
                            if (MNNStudioUI.Button("Estimate depth", 150, primary: true)) RunTask();
                    }
                    if (Busy && MNNStudioUI.Button("Stop", 68)) StopReply();
                    var depth = _retrieval?.ImageTask?.Depth;
                    if (depth != null)
                    {
                        GUILayout.Space(16);
                        GUILayout.Label("Result · " + _retrieval.Seconds.ToString("F2") + " s · " + (_retrieval.BackendDescription ?? ""), MNNStudioUI.Caption);
                        DrawGeneratedImage(_retrieval.ImageTask.Image, showSeed: false);
                        GUILayout.Label("Raw grid " + depth.GridWidth + " × " + depth.GridHeight + " · source " + depth.Width + " × " + depth.Height, MNNStudioUI.Body);
                        GUILayout.Label("Relative inverse depth range " + depth.Minimum.ToString("G6") + " – " + depth.Maximum.ToString("G6"), MNNStudioUI.Caption);
                        if (MNNStudioUI.Button("Export raw depth (PFM)", 190))
                        {
                            string path = EditorUtility.SaveFilePanel("Export relative inverse depth", "", "relative-inverse-depth", "pfm");
                            if (!string.IsNullOrEmpty(path)) File.WriteAllBytes(path, EncodeDepthPfm(depth));
                        }
                    }
                    GUILayout.Space(24);
                }
            }
        }

        // Standard grayscale PFM: bottom row first, little-endian float32,
        // scale -1. Keep raw model values, independent of preview normalization.
        internal static byte[] EncodeDepthPfm(MNNImageDepthResult depth)
        {
            if (depth == null) throw new System.ArgumentNullException(nameof(depth));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("Pf\n" + depth.GridWidth.ToString(CultureInfo.InvariantCulture) + " " + depth.GridHeight.ToString(CultureInfo.InvariantCulture) + "\n-1.0\n"));
                var values = depth.RelativeInverseDepth;
                for (int y = depth.GridHeight - 1; y >= 0; --y)
                    for (int x = 0; x < depth.GridWidth; ++x)
                        writer.Write(values[y * depth.GridWidth + x]);
                return stream.ToArray();
            }
        }
    }
}
