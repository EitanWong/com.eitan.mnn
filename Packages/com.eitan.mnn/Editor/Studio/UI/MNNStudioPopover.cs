using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor.UI
{
    /// <summary>Reusable anchored settings surface; native popup dismissal and Escape behavior.</summary>
    internal sealed class MNNStudioPopover : PopupWindowContent
    {
        private readonly Action _draw;
        private readonly Vector2 _size;
        internal MNNStudioPopover(Vector2 size, Action draw)
        {
            _size = size;
            _draw = draw;
        }

        public override Vector2 GetWindowSize() => _size;
        public override void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, MNNStudioUI.Background);
            using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
                _draw();
        }
    }
}
