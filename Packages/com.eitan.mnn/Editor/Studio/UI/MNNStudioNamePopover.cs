using System;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor.UI
{
    internal sealed class MNNStudioNamePopover : PopupWindowContent
    {
        private string _name;
        private readonly Action<string> _save;
        private bool _focus = true;
        internal MNNStudioNamePopover(string name, Action<string> save)
        {
            _name = name ?? "";
            _save = save;
        }

        public override Vector2 GetWindowSize() => new Vector2(280, 126);
        public override void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, MNNStudioUI.Background);
            bool enter = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
            if (enter)
                Event.current.Use();
            using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
            {
                GUILayout.Label("Name", MNNStudioUI.Heading);
                GUILayout.Space(8);
                GUI.SetNextControlName("MNNStudioName");
                _name = EditorGUILayout.TextField(_name);
                if (_focus)
                {
                    GUI.FocusControl("MNNStudioName");
                    _focus = false;
                }

                GUILayout.Space(8);
                using (new EditorGUI.DisabledScope(MNNStudioHistory.CleanName(_name).Length == 0))
                    if (MNNStudioUI.Button("Save", 80, primary: true) || enter && MNNStudioHistory.CleanName(_name).Length > 0)
                    {
                        _save(_name);
                        editorWindow.Close();
                        GUIUtility.ExitGUI();
                    }
            }
        }
    }
}
