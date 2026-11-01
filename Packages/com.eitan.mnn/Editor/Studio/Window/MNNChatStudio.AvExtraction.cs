using System.IO;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        [SerializeField] private string _avFramesDirectory;
        private void DrawAvExtractionInput()
        {
            GUILayout.Space(12);
            GUILayout.Label("Target speaker face frames", MNNStudioUI.Heading);
            GUILayout.Label("Choose aligned 224×224 faces or prepared 112×112 crops at 25 FPS. Use zero-padded PNG/JPEG names in time order, starting with the WAV. " +
                (Selected?.RequiresExactVisualFrames == true ? "This model accepts 40 ms–6 seconds and requires exactly floor(duration × 25) frames." : "Maximum 30 seconds. Missing end frames repeat the last frame."), MNNStudioUI.Caption);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MNNStudioUI.Button("Choose frames…", 150)) PickAvFrames();
                if (!string.IsNullOrWhiteSpace(_avFramesDirectory) && MNNStudioUI.Button("Clear", 68, quiet: true)) _avFramesDirectory = null;
            }
            if (!string.IsNullOrWhiteSpace(_avFramesDirectory))
                EditorGUILayout.SelectableLabel(_avFramesDirectory, MNNStudioUI.Caption, GUILayout.Height(36));
        }
        private void PickAvFrames()
        {
            string modelDirectory = Selected?.Directory;
            ShowDialogAfterGUI(() =>
            {
                if (Busy || Selected?.Directory != modelDirectory || Selected?.RequiresVisualFrames != true) return;
                string selected = EditorUtility.OpenFolderPanel("Choose target face frames at 25 FPS", _avFramesDirectory ?? "", "");
                if (this != null && !Busy && Selected?.Directory == modelDirectory && Directory.Exists(selected))
                    _avFramesDirectory = Path.GetFullPath(selected);
            });
        }
    }
}
