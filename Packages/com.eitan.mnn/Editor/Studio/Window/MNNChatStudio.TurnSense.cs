using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private string BuildTurnSensePrompt() => _query ?? string.Empty;

        private void DrawTurnSenseTool()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label("TurnSense · transcript completion", MNNStudioUI.Heading);
                    GUILayout.Space(8);
                    GUILayout.Label("Enter the current ASR transcript. English was used for training; punctuation affects the decision. Whitespace is preserved. Maximum 256 tokens including the user prefix.", MNNStudioUI.Body);
                    GUILayout.Space(16);
                    using (new EditorGUI.DisabledScope(Busy))
                    {
                        _query = EditorGUILayout.TextArea(_query ?? string.Empty, MNNStudioUI.Input, GUILayout.Height(120));
                        if (MNNStudioUI.Button("Use example", 112, quiet: true)) _query = "I am finished.";
                    }
                    GUILayout.Space(16);
                    using (new EditorGUI.DisabledScope(!CanRunTask()))
                        if (MNNStudioUI.Button("Evaluate transcript", 170)) RunTask();
                    if (Busy && MNNStudioUI.Button("Stop", 68)) StopReply();
                    GUILayout.Space(16);
                    if (_retrieval != null)
                    {
                        if (_taskResultMessage == null || _taskResultMessage.Result != _retrieval)
                            _taskResultMessage = new MNNStudioMessage(false, _retrieval.Text, result: _retrieval);
                        DrawMessage(_taskResultMessage);
                    }
                }
            }
        }
    }
}
