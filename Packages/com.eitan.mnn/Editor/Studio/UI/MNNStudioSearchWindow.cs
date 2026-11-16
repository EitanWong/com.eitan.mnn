using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private bool _pendingSearch, _reduceMotion;
        private double _searchAt, _nextRepaint;
        private MNNStudioSearch.Operation _operation;
        private const float RowHeight = 72;
        internal static MNNStudioSearchWindow Open(Rect parent, MNNStudioHistory history, Action<MNNStudioSearchHit> choose)
        {
            var window = CreateInstance<MNNStudioSearchWindow>();
            window.titleContent = new GUIContent("Search chats");
            window._history = history;
            window._choose = choose;
            window._reduceMotion = EditorPrefs.GetBool("MNN.Studio.ReduceMotion", false);
            window._hits = MNNStudioSearch.Find(history, "");
            var size = new Vector2(Mathf.Min(620, parent.width - 64), Mathf.Min(500, parent.height - 64));
            window.position = new Rect(parent.center - size / 2, size);
            window.ShowPopup();
            window.Focus();
            return window;
        }

        private void OnEnable() => EditorApplication.update += Tick;
        private void OnDisable() => EditorApplication.update -= Tick;

        private void QueueSearch()
        {
            _operation = null;
            _hits = new List<MNNStudioSearchHit>();
            _selected = 0;
            _scroll = Vector2.zero;
            _searchAt = EditorApplication.timeSinceStartup + .15;
            _pendingSearch = true;
        }

        private void Tick()
        {
            if (!_pendingSearch || _history == null)
                return;
            double now = EditorApplication.timeSinceStartup;
            if (now < _searchAt)
                return;
            if (_operation == null)
                _operation = new MNNStudioSearch.Operation(_history, _query);
            var clock = Stopwatch.StartNew();
            do
            {
                _pendingSearch = !_operation.Step(8);
            } while (_pendingSearch && clock.Elapsed.TotalMilliseconds < 2);
            _hits = _operation.Hits;
            if (!_pendingSearch || now >= _nextRepaint)
            {
                _nextRepaint = now + .05;
                Repaint();
            }
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
                        QueueSearch();
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
                        GUILayout.Label(_pendingSearch ? "Searching conversations…" : "No matching conversations", MNNStudioUI.CenteredCaption);
                    }

                    var area = GUILayoutUtility.GetRect(0, _hits.Count * RowHeight, GUILayout.ExpandWidth(true));
                    int first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / RowHeight) - 1);
                    int last = Mathf.Min(_hits.Count, first + Mathf.CeilToInt(position.height / RowHeight) + 2);
                    for (int i = first; i < last; ++i)
                    {
                        var hit = _hits[i];
                        var row = new Rect(area.x, area.y + i * RowHeight, area.width, RowHeight);
                        if (i == _selected)
                        {
                            EditorGUI.DrawRect(row, MNNStudioUI.Sidebar);
                            EditorGUI.DrawRect(new Rect(row.x, row.y + 10, 3, row.height - 20), MNNStudioUI.Accent);
                        }
                        if (GUI.Button(row, GUIContent.none, GUIStyle.none))
                        {
                            Choose(i);
                            return;
                        }

                        GUI.Label(new Rect(row.x + 20, row.y + 8, row.width - 40, 22), hit.Title ?? "New chat", MNNStudioUI.Body);
                        string prefix = hit.MessageIndex < 0 ? "" : hit.IsUser ? "You · " : "Assistant · ";
                        GUI.Label(new Rect(row.x + 20, row.y + 32, row.width - 40, 30), prefix + hit.Preview, MNNStudioUI.SingleLineCaption);
                    }
                }

                GUILayout.Space(6);
                var footer = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
                if (_pendingSearch)
                    MNNStudioUI.Activity(new Rect(footer.x + 20, footer.y, 24, footer.height), !_reduceMotion);
                GUI.Label(new Rect(footer.x + (_pendingSearch ? 52 : 20), footer.y, Mathf.Max(0, footer.width - 64), footer.height),
                    _pendingSearch ? "Searching…" : _hits.Count >= 100 ? "First 100 matches · Refine your search" : "↑ ↓ Navigate   ·   Return Open   ·   Esc Close", MNNStudioUI.SingleLineCaption);
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
