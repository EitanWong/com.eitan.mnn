using System;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        [SerializeField] private float _layoutScoreThreshold = .5f;
        private int _layoutRegionIndex;

        private void DrawDocumentLayoutTool()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label("Document layout", MNNStudioUI.Heading);
                    GUILayout.Label("Locate headings, paragraphs, figures and tables in a page image. Inspect regions in predicted reading order.", MNNStudioUI.Body);
                    GUILayout.Space(16);
                    using (new EditorGUI.DisabledScope(Busy))
                    {
                        DrawTaskAttachment(true);
                        _layoutScoreThreshold = EditorGUILayout.Slider("Minimum confidence", _layoutScoreThreshold, .01f, 1);
                        string reason = GetTaskRunBlockedReason();
                        if (reason != null) EditorGUILayout.HelpBox(reason, MessageType.Info);
                        using (new EditorGUI.DisabledScope(reason != null))
                            if (MNNStudioUI.Button("Analyze page", 140, primary: true)) RunTask();
                    }
                    if (Busy && MNNStudioUI.Button("Stop", 68)) StopReply();
                    if (_retrieval?.DocumentLayout != null)
                    {
                        var layout = _retrieval.DocumentLayout;
                        var regions = layout.Regions;
                        GUILayout.Space(16);
                        GUILayout.Label(regions.Length + " regions · " + layout.Width + " × " + layout.Height + " · " + _retrieval.Seconds.ToString("F2") + " s", MNNStudioUI.Caption);
                        GUILayout.Label(_retrieval.BackendDescription ?? "", MNNStudioUI.Caption);
                        DrawLayoutPreview(layout);
                        if (regions.Length > 0)
                        {
                            var choices = new string[regions.Length];
                            for (int i = 0; i < choices.Length; ++i)
                                choices[i] = regions[i].ReadingOrder + " · " + regions[i].Detection.Label + " · " + regions[i].Detection.Score.ToString("P1");
                            _layoutRegionIndex = EditorGUILayout.Popup("Region", Mathf.Clamp(_layoutRegionIndex, 0, regions.Length - 1), choices);
                            var box = regions[_layoutRegionIndex].Detection;
                            GUILayout.Label(string.Format("Source pixels · x {0:F1}, y {1:F1}, width {2:F1}, height {3:F1}", box.X, box.Y, box.Width, box.Height), MNNStudioUI.Caption);
                        }
                        if (MNNStudioUI.Button("Copy region list", 140)) EditorGUIUtility.systemCopyBuffer = _retrieval.Text ?? "";
                    }
                    GUILayout.Space(12);
                    EditorGUILayout.HelpBox("Layout analysis identifies page regions. Use OCR to read text; table cells are not decoded by this model.", MessageType.Info);
                    GUILayout.Space(24);
                }
            }
        }

        private void DrawLayoutPreview(MNNDocumentLayoutResult layout)
        {
            if (_retrieval.GeneratedImage == null) return;
            if (_previewResult != _retrieval.GeneratedImage)
            {
                ClearGeneratedImage();
                _generatedPreview = MNNStudioImage.CreateTexture(_retrieval.GeneratedImage);
                _previewResult = _retrieval.GeneratedImage;
            }
            float width = Math.Min(ContentWidth, 700);
            Rect rect = GUILayoutUtility.GetRect(width, width * layout.Height / layout.Width, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, _generatedPreview, ScaleMode.StretchToFill);
            var regions = layout.Regions;
            for (int i = 0; i < regions.Length; ++i)
            {
                var box = regions[i].Detection;
                var target = new Rect(rect.x + box.X / layout.Width * rect.width, rect.y + box.Y / layout.Height * rect.height,
                    box.Width / layout.Width * rect.width, box.Height / layout.Height * rect.height);
                if (i == _layoutRegionIndex && Event.current.type == EventType.Repaint)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawAAPolyLine(3, new Vector3(target.x, target.y), new Vector3(target.xMax, target.y),
                        new Vector3(target.xMax, target.yMax), new Vector3(target.x, target.yMax), new Vector3(target.x, target.y));
                }
                GUI.Label(new Rect(target.x, target.y, Math.Max(90, target.width), 18), regions[i].ReadingOrder + " " + box.Label, EditorStyles.helpBox);
                if (GUI.Button(target, GUIContent.none, GUIStyle.none)) _layoutRegionIndex = i;
            }
        }
    }
}
