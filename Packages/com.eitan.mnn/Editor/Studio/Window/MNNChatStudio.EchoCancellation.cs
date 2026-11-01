using System.IO;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        [SerializeField] private string _aecReferencePath;
        private void DrawEchoCancellationTool()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label("Echo cancellation", MNNStudioUI.Heading);
                    GUILayout.Label("Choose a microphone recording and the audio sent to the speaker. Both WAV files must be 16 kHz and the same length. Align playback/device delay before processing.", MNNStudioUI.Body);
                    GUILayout.Space(16);
                    using (new EditorGUI.DisabledScope(Busy))
                    {
                        GUILayout.Label("Microphone recording", MNNStudioUI.Heading);
                        DrawTaskAttachment(false);
                        GUILayout.Space(8);
                        GUILayout.Label("Playback reference", MNNStudioUI.Heading);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (MNNStudioUI.Button("Choose reference…", 160)) PickEchoReference();
                            if (!string.IsNullOrEmpty(_aecReferencePath) && MNNStudioUI.Button("Clear", 68, quiet: true)) _aecReferencePath = null;
                        }
                        if (!string.IsNullOrEmpty(_aecReferencePath))
                            EditorGUILayout.SelectableLabel(_aecReferencePath, MNNStudioUI.Caption, GUILayout.Height(36));
                        GUILayout.Space(12);
                        string reason = GetTaskRunBlockedReason();
                        if (reason != null) EditorGUILayout.HelpBox(reason, MessageType.Info);
                        using (new EditorGUI.DisabledScope(reason != null))
                            if (MNNStudioUI.Button("Cancel echo", 160, primary: true)) RunTask();
                    }
                    if (Busy && MNNStudioUI.Button("Stop", 68)) StopReply();
                    if (_retrieval != null)
                    {
                        GUILayout.Space(16);
                        if (_taskResultMessage == null || _taskResultMessage.Result != _retrieval)
                            _taskResultMessage = new MNNStudioMessage(false, _retrieval.Text, result: _retrieval);
                        DrawMessage(_taskResultMessage);
                    }
                    GUILayout.Space(24);
                }
            }
        }
        private void PickEchoReference()
        {
            string directory = Selected?.Directory;
            ShowDialogAfterGUI(() =>
            {
                if (Busy || Selected?.Directory != directory || CurrentTask.Task != MNNStudioTask.EchoCancellation) return;
                string path = _attachmentPicker("Choose synchronized playback reference", "", "wav");
                if (this != null && !Busy && Selected?.Directory == directory && CurrentTask.Task == MNNStudioTask.EchoCancellation && File.Exists(path))
                    _aecReferencePath = Path.GetFullPath(path);
            });
        }
    }
}
