using System.IO;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        [SerializeField] private string _gestureFeaturesPath;
        private void DrawGestureFeaturesInput()
        {
            GUILayout.Space(12);
            GUILayout.Label("Synchronized gesture features", MNNStudioUI.Heading);
            GUILayout.Label("Choose SEG gesture JSON: 30 features per frame at 15 FPS, starting with the WAV. Recordings can be up to 10 seconds. Prepare features using the YGD feature convention described in the model README.", MNNStudioUI.Caption);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MNNStudioUI.Button("Choose gesture JSON…", 180)) PickGestureFeatures();
                if (!string.IsNullOrWhiteSpace(_gestureFeaturesPath) && MNNStudioUI.Button("Clear", 68, quiet: true)) _gestureFeaturesPath = null;
            }
            if (!string.IsNullOrWhiteSpace(_gestureFeaturesPath))
                EditorGUILayout.SelectableLabel(_gestureFeaturesPath, MNNStudioUI.Caption, GUILayout.Height(36));
        }

        private void PickGestureFeatures()
        {
            string directory = Selected?.Directory;
            ShowDialogAfterGUI(() =>
            {
                if (Busy || Selected?.Directory != directory || Selected?.IsSeg != true) return;
                string path = _attachmentPicker("Choose synchronized gesture features", "", "json");
                if (this != null && !Busy && Selected?.Directory == directory && Selected?.IsSeg == true && File.Exists(path))
                    _gestureFeaturesPath = Path.GetFullPath(path);
            });
        }
    }
}
