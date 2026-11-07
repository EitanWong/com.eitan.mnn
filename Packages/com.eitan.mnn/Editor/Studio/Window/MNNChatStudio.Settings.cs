using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private void OpenSettings(Rect anchor)
        {
            _settingsPopup = new MNNStudioPopover(new Vector2(380, Busy ? 510 : 482), DrawSettings);
            PopupWindow.Show(anchor, _settingsPopup);
        }

        private void DrawSettings()
        {
            GUILayout.Label("Model settings", MNNStudioUI.Heading);
            GUILayout.Space(16);
            float oldLabelWidth = EditorGUIUtility.labelWidth;
            try
            {
                EditorGUIUtility.labelWidth = 110;
                using (new EditorGUI.DisabledScope(Busy))
                {
                    int threads = EditorGUILayout.IntSlider("CPU threads", _threads, 1, Math.Min(16, Math.Max(1, SystemInfo.processorCount)));
                    GUILayout.Space(8);
                    bool thinking = EditorGUILayout.Toggle("Thinking", _thinking);
                    if (threads != _threads || thinking != _thinking)
                    {
                        StopVoiceLoop();
                        ReleaseSession();
                        _threads = threads;
                        _thinking = thinking;
                    }
                }

                GUILayout.Space(8);
                _contextTokens = Mathf.Clamp(EditorGUILayout.IntField("Context tokens", _contextTokens), 2048, 32768);
                _tokenLimit = Mathf.Clamp(EditorGUILayout.IntSlider("Response tokens", _tokenLimit, 16, 2048), 16, 2048);
            }
            finally
            {
                EditorGUIUtility.labelWidth = oldLabelWidth;
            }

            GUILayout.Space(12);
            MNNStudioUI.Separator();
            GUILayout.Space(12);
            GUILayout.Label("Additional model folder", MNNStudioUI.Heading);
            GUILayout.Space(8);
            var pathStyle = new GUIStyle(MNNStudioUI.Caption)
            {wordWrap = false, clipping = TextClipping.Clip};
            GUILayout.Label(new GUIContent(string.IsNullOrEmpty(_customRoot) ? "No additional folder selected" : _customRoot, _customRoot), pathStyle, GUILayout.Height(20));
            using (new EditorGUI.DisabledScope(Busy))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (MNNStudioUI.Button("Choose folder…", 124))
                    ShowDialogAfterGUI(() =>
                    {
                        string root = EditorUtility.OpenFolderPanel("Additional MNN model folder", _customRoot, "");
                        if (!string.IsNullOrEmpty(root))
                        {
                            _customRoot = root;
                            EditorPrefs.SetString("MNN.Studio.ModelRoot", root);
                            RefreshKeepingConversation();
                        }
                    });
                if (!string.IsNullOrEmpty(_customRoot) && MNNStudioUI.Button("Remove", 76, quiet: true))
                {
                    _customRoot = "";
                    EditorPrefs.DeleteKey("MNN.Studio.ModelRoot");
                    RefreshKeepingConversation();
                }
            }

            GUILayout.Space(12);
            GUILayout.Label("Bundled and runtime model folders are included automatically. Settings apply to the next request.", MNNStudioUI.Caption);
            GUILayout.Space(8);
            bool reduce = EditorGUILayout.Toggle("Reduce motion", _reduceMotion);
            if (reduce != _reduceMotion)
            {
                _reduceMotion = reduce;
                EditorPrefs.SetBool("MNN.Studio.ReduceMotion", reduce);
            }

            GUILayout.Label("Voice models", MNNStudioUI.Heading);
            using (new EditorGUI.DisabledScope(Busy))
            {
                if (_recognizerNames.Length > 0)
                    _voiceRecognizer = EditorGUILayout.Popup("Recognition", _voiceRecognizer, _recognizerNames);
                if (_synthesizerNames.Length > 0)
                    _voiceSynthesizer = EditorGUILayout.Popup("Speech", _voiceSynthesizer, _synthesizerNames);
            }
            if (_recognizerNames.Length == 0 || _synthesizerNames.Length == 0)
                GUILayout.Label("Install recognition and synthesis models in Model Manager, then Refresh.", MNNStudioUI.Caption);
            _autoTitle = EditorGUILayout.Toggle("Automatic chat titles", _autoTitle);
            if (Busy)
            {
                GUILayout.Space(8);
                GUILayout.Label("Model options can be changed when the current request finishes.", MNNStudioUI.Caption);
            }
        }
    }
}
