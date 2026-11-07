using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        // Measured heights preserve the existing IMGUI layout, including media and actions.
        // Visibility is frozen at Layout so input/Repaint consume the same control tree.
        private readonly List<TranscriptRow> _transcriptRows = new List<TranscriptRow>();
        private float _transcriptWidth;
        private float _transcriptScale;
        private bool _transcriptDark;
        private int _transcriptHotControl;

        private sealed class TranscriptRow
        {
            internal MNNStudioMessage Message;
            internal float Height;
            internal bool Visible = true;
        }

        private void PrepareTranscript()
        {
            if (Event.current.type != EventType.Layout)
                return;
            float width = ContentWidth;
            bool dark = EditorGUIUtility.isProSkin;
            float scale = EditorGUIUtility.pixelsPerPoint;
            if (TranscriptLayoutChanged(width, dark, scale))
                _transcriptRows.Clear();
            _transcriptWidth = width;
            _transcriptDark = dark;
            _transcriptScale = scale;
            while (_transcriptRows.Count < _messages.Count)
                _transcriptRows.Add(new TranscriptRow { Message = _messages[_transcriptRows.Count] });
            float total = TranscriptHeight(out bool measured);
            float viewport = _conversationRect.height;
            float top = Mathf.Clamp(_scroll.y, 0, Mathf.Max(0, total - viewport));
            UpdateTranscriptVisibility(top, viewport, measured);
        }

        private bool TranscriptLayoutChanged(float width, bool dark, float scale) =>
            _transcriptWidth != width || _transcriptDark != dark || _transcriptScale != scale ||
            _transcriptRows.Count > _messages.Count ||
            _transcriptRows.Count > 0 && _transcriptRows[0].Message != _messages[0];

        private float TranscriptHeight(out bool measured)
        {
            float total = 48;
            measured = true;
            foreach (var row in _transcriptRows)
            {
                total += row.Height;
                measured &= row.Height > 0;
            }
            return total;
        }

        private void UpdateTranscriptVisibility(float top, float viewport, bool measured)
        {
            float y = 24;
            bool preserveVisibility = GUIUtility.hotControl != 0 && GUIUtility.hotControl == _transcriptHotControl;
            for (int i = 0; i < _transcriptRows.Count; ++i)
            {
                var row = _transcriptRows[i];
                // Keep active mouse selections/buttons alive until release.
                if (!preserveVisibility)
                    row.Visible = TranscriptRowVisible(row.Height, y, top, viewport);
                if (i == _jumpMessageIndex && measured)
                    _jumpScroll = Mathf.Max(0, y + (row.Message.IsUser ? 12 : 20) - 24);
                y += row.Height;
            }
        }

        private static bool TranscriptRowVisible(float height, float y, float top, float viewport) =>
            height <= 0 || viewport <= 0 || y + height >= top - 200 && y <= top + viewport + 200;

        private void DrawTranscript()
        {
            PrepareTranscript();
            // BeginScrollView has already acquired the scrollbar's hot control. Only
            // freeze visibility for controls acquired by the messages themselves.
            int previousHotControl = GUIUtility.hotControl;
            float skipped = 0;
            for (int index = 0; index < _transcriptRows.Count; ++index)
            {
                var row = _transcriptRows[index];
                if (!row.Visible)
                {
                    skipped += row.Height;
                    continue;
                }
                if (skipped > 0)
                {
                    GUILayout.Space(skipped);
                    skipped = 0;
                }
                DrawMessage(row.Message);
                if (Event.current.type != EventType.Repaint)
                    continue;
                var rect = GUILayoutUtility.GetLastRect();
                row.Height = rect.height + (row.Message.IsUser ? 12 : 20);
                if (index == _jumpMessageIndex)
                    _jumpScroll = Mathf.Max(0, rect.y - 24);
                if (index == _highlightMessageIndex && EditorApplication.timeSinceStartup < _highlightUntil)
                    EditorGUI.DrawRect(new Rect(rect.x - 8, rect.y, 3, rect.height), MNNStudioUI.Accent);
            }
            if (skipped > 0)
                GUILayout.Space(skipped);
            if (Event.current.rawType == EventType.MouseDown && GUIUtility.hotControl != previousHotControl)
                _transcriptHotControl = GUIUtility.hotControl;
        }
    }
}
