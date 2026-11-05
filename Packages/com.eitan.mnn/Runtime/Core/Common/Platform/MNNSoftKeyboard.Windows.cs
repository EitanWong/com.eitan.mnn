#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using static MNN.Unity.Interop.MNNInterop;

namespace MNN.Unity
{
    public sealed partial class MNNSoftKeyboard
    {
        private IFrameworkInputPane _windowsPane;
        private bool _windowsUnavailable, _windowsComInitialized;

        private bool ReadWindows(out Rect area)
        {
            area = Rect.zero;
            if (_windowsUnavailable) return false;
            try
            {
                if (_windowsPane == null)
                {
                    _windowsComInitialized = CoInitializeEx(IntPtr.Zero, 2) >= 0;
                    _windowsPane = (IFrameworkInputPane)new FrameworkInputPane();
                }
                var window = GetActiveWindow();
                if (window == IntPtr.Zero)
                {
                    window = GetForegroundWindow();
                    GetWindowThreadProcessId(window, out uint owner);
                    if (owner != GetCurrentProcessId()) return false;
                }
                var origin = new NativePoint();
                if (window == IntPtr.Zero || !GetClientRect(window, out var client) || !ClientToScreen(window, ref origin) ||
                    _windowsPane.Location(out var keyboard) < 0) return false;
                area = MapOcclusion(new Rect(keyboard.left, keyboard.top, keyboard.right - keyboard.left, keyboard.bottom - keyboard.top),
                    new Rect(origin.x, origin.y, client.right - client.left, client.bottom - client.top), new Vector2(Screen.width, Screen.height));
                return area.width > 0f && area.height > 0f;
            }
            catch (Exception error) when (error is COMException || error is InvalidCastException || error is PlatformNotSupportedException ||
                error is DllNotFoundException || error is EntryPointNotFoundException)
            {
                _windowsUnavailable = true;
                DisposeWindows();
                return false;
            }
        }

        private void DisposeWindows()
        {
            if (_windowsPane != null) Marshal.ReleaseComObject(_windowsPane);
            _windowsPane = null;
            if (_windowsComInitialized) CoUninitialize();
            _windowsComInitialized = false;
        }
    }
}
#endif
