using System.IO;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        [SerializeField] private string _speakerReferencePath;
        private void DrawSpeakerReferenceInput()
        {
            GUILayout.Space(12);
            GUILayout.Label("Target speaker recording", MNNStudioUI.Heading);
            GUILayout.Label("Choose a separate WAV of the speaker to extract. Mixture: up to 4 seconds. Target reference: 35 ms–8 seconds; a clear utterance works best. Playback is peak normalized.", MNNStudioUI.Caption);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MNNStudioUI.Button("Choose target WAV…", 160)) PickSpeakerReference();
                if (!string.IsNullOrWhiteSpace(_speakerReferencePath) && MNNStudioUI.Button("Clear", 68, quiet: true)) _speakerReferencePath = null;
            }
            if (!string.IsNullOrWhiteSpace(_speakerReferencePath))
                EditorGUILayout.SelectableLabel(_speakerReferencePath, MNNStudioUI.Caption, GUILayout.Height(36));
        }
        private void PickSpeakerReference()
        {
            string directory = Selected?.Directory;
            ShowDialogAfterGUI(() =>
            {
                if (Busy || Selected?.Directory != directory || Selected?.IsSpExPlus != true) return;
                string path = _attachmentPicker("Choose target speaker recording", "", "wav");
                if (this != null && !Busy && Selected?.Directory == directory && Selected?.IsSpExPlus == true && File.Exists(path))
                    _speakerReferencePath = Path.GetFullPath(path);
            });
        }
    }
}
