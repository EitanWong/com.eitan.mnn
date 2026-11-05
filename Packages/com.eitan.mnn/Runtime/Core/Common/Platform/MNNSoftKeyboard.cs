using System;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Keyboard occlusion in Unity screen pixels; never guesses a fixed keyboard height.</summary>
    public sealed partial class MNNSoftKeyboard : IDisposable
    {
        private float _nextPoll;
        private Rect _platformArea;

        public bool Read(out Rect? area)
        {
            bool visible = TouchScreenKeyboard.visible;
            Rect unityArea = TouchScreenKeyboard.area;
            if (visible && unityArea.width > 0f && unityArea.height > 0f)
            {
                area = unityArea;
                return true;
            }
            if (Time.realtimeSinceStartup >= _nextPoll)
            {
                _nextPoll = Time.realtimeSinceStartup + .05f;
                _platformArea = Rect.zero;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                ReadWindows(out _platformArea);
#elif UNITY_ANDROID && !UNITY_EDITOR
                if (visible) ReadAndroid(out _platformArea);
#endif
            }
            area = _platformArea.width > 0f && _platformArea.height > 0f ? (Rect?)_platformArea : null;
            return visible || area.HasValue;
        }

        /// <summary>Map top-left desktop/Android coordinates into a bottom-left Unity client viewport.</summary>
        public static Rect MapOcclusion(Rect keyboard, Rect client, Vector2 viewport)
        {
            if (client.width <= 0 || client.height <= 0 || viewport.x <= 0 || viewport.y <= 0) return Rect.zero;
            float left = Mathf.Max(client.xMin, keyboard.xMin), right = Mathf.Min(client.xMax, keyboard.xMax);
            float top = Mathf.Max(client.yMin, keyboard.yMin), bottom = Mathf.Min(client.yMax, keyboard.yMax);
            if (right <= left || bottom <= top) return Rect.zero;
            return new Rect((left - client.xMin) * viewport.x / client.width,
                (client.yMax - bottom) * viewport.y / client.height,
                (right - left) * viewport.x / client.width, (bottom - top) * viewport.y / client.height);
        }

        public void Dispose()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            DisposeWindows();
#endif
        }
    }
}
