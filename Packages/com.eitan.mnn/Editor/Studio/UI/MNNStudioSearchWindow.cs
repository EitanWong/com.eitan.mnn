using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor.UI
{
    internal sealed class MNNStudioSearchWindow : EditorWindow
    {
        private MNNStudioHistory _history;
        private Action<MNNStudioSearchHit> _choose;
        private string _query = "";
        private List<MNNStudioSearchHit> _hits = new List<MNNStudioSearchHit>();
        private Vector2 _scroll;
        private int _selected;
        private bool _focus = true;
        private const float RowHeight = 72;
        internal static MNNStudioSearchWindow Open(Rect parent, MNNStudioHistory history, Action<MNNStudioSearchHit> choose)
        {
            var window = CreateInstance<MNNStudioSearchWindow>();
            window.titleContent = new GUIContent("Search chats");
            window._history = history;
            window._choose = choose;
            window._hits = MNNStudioSearch.Find(history, "");
            var size = new Vector2(Mathf.Min(620, parent.width - 64), Mathf.Min(500, parent.height - 64));
            window.position = new Rect(parent.center - size / 2, size);
            window.ShowPopup();
            window.Focus();
            return window;
        }

        private void OnLostFocus()
        {
            Close();
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(Vector2.zero, position.size), MNNStudioUI.Background);
            var evt = Event.current;
            if (evt.type == EventType.KeyDown)
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    Close();
                    evt.Use();
                    return;
                }

                if (evt.keyCode == KeyCode.Return && _hits.Count > 0 && string.IsNullOrEmpty(Input.compositionString))
                {
                    evt.Use();
                    Choose(_selected);
                    return;
                }

                if (evt.keyCode == KeyCode.DownArrow || evt.keyCode == KeyCode.UpArrow)
                {
                    _selected = Mathf.Clamp(_selected + (evt.keyCode == KeyCode.DownArrow ? 1 : -1), 0, Math.Max(0, _hits.Count - 1));
                    float top = _selected * RowHeight, visible = position.height - 110;
                    if (top < _scroll.y)
                        _scroll.y = top;
                    else if (top + RowHeight > _scroll.y + visible)
                        _scroll.y = top + RowHeight - visible;
                    evt.Use();
                    Repaint();
                }
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandHeight(true)))
            {
                GUILayout.Space(16);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(20);
                    GUILayout.Label("Search chats", MNNStudioUI.Heading);
                    GUILayout.FlexibleSpace();
                    if (MNNStudioUI.Button("×", 28, quiet: true, tooltip: "Close (Escape)"))
                        Close();
                    GUILayout.Space(12);
                }

                GUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(20);
                    GUI.SetNextControlName("MNNStudioSearchQuery");
                    EditorGUI.BeginChangeCheck();
                    _query = EditorGUILayout.TextField(_query, MNNStudioUI.Input, GUILayout.Height(30));
                    if (EditorGUI.EndChangeCheck())
                    {
                        _hits = MNNStudioSearch.Find(_history, _query);
                        _selected = 0;
                        _scroll = Vector2.zero;
                    }

                    if (_focus)
                    {
                        GUI.FocusControl("MNNStudioSearchQuery");
                        _focus = false;
                    }

                    if (_query.Length == 0)
                        GUI.Label(GUILayoutUtility.GetLastRect(), "Search titles and messages…", MNNStudioUI.Placeholder);
                    GUILayout.Space(20);
                }

                GUILayout.Space(12);
                MNNStudioUI.Separator();
                using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.ExpandHeight(true)))
                {
                    _scroll = scroll.scrollPosition;
                    if (_hits.Count == 0)
                    {
                        GUILayout.Space(32);
                        GUILayout.Label("No matching conversations", new GUIStyle(MNNStudioUI.Caption)
                        {alignment = TextAnchor.MiddleCenter});
                    }

                    for (int i = 0; i < _hits.Count; ++i)
                    {
                        var hit = _hits[i];
                        var row = GUILayoutUtility.GetRect(0, RowHeight, GUILayout.ExpandWidth(true));
                        if (i == _selected)
                            EditorGUI.DrawRect(row, MNNStudioUI.Sidebar);
                        if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                        {
                            Choose(i);
                            return;
                        }

                        GUI.Label(new Rect(row.x + 20, row.y + 8, row.width - 40, 22), hit.Title ?? "New chat", MNNStudioUI.Body);
                        string prefix = hit.MessageIndex < 0 ? "" : hit.IsUser ? "You · " : "Assistant · ";
                        GUI.Label(new Rect(row.x + 20, row.y + 32, row.width - 40, 30), prefix + hit.Preview, new GUIStyle(MNNStudioUI.Caption)
                        {wordWrap = false, clipping = TextClipping.Clip});
                    }
                }

                GUILayout.Space(6);
                GUILayout.Label(_hits.Count >= 100 ? "Showing first 100 matches · Refine your search" : "   ↑ ↓ Navigate   ·   Return Open   ·   Esc Close", MNNStudioUI.Caption, GUILayout.Height(22));
            }
        }

        private void Choose(int index)
        {
            if (index < 0 || index >= _hits.Count)
                return;
            var hit = _hits[index];
            Close();
            _choose?.Invoke(hit);
        }
    }
}
