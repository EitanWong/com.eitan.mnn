using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private void DrawStyleTransferTool()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label("Candy style transfer", MNNStudioUI.Heading);
                    GUILayout.Label("Apply the Candy painting style to a photo. The image is resized to 224 × 224; the style is built into this model.", MNNStudioUI.Body);
                    GUILayout.Space(16);
                    using (new EditorGUI.DisabledScope(Busy))
                    {
                        DrawTaskAttachment(true);
                        string reason = GetTaskRunBlockedReason();
                        if (reason != null) EditorGUILayout.HelpBox(reason, MessageType.Info);
                        using (new EditorGUI.DisabledScope(reason != null))
                            if (MNNStudioUI.Button("Apply Candy style", 160, primary: true)) RunTask();
                    }
                    if (Busy && MNNStudioUI.Button("Stop", 68)) StopReply();
                    if (_retrieval?.ImageTask?.Image != null)
                    {
                        GUILayout.Space(16);
                        GUILayout.Label("Result · " + _retrieval.Seconds.ToString("F2") + " s · " + (_retrieval.BackendDescription ?? ""), MNNStudioUI.Caption);
                        DrawGeneratedImage(_retrieval.ImageTask.Image, showSeed: false);
                    }
                    GUILayout.Space(24);
                }
            }
        }
    }
}
