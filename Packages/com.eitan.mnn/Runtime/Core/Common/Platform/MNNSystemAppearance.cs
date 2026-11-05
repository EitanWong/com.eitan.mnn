using System;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || (UNITY_IOS && !UNITY_EDITOR)
using System.Runtime.InteropServices;
#endif
using UnityEngine;
using static MNN.Unity.Interop.MNNInterop;

namespace MNN.Unity
{
    /// <summary>Reads public OS appearance preferences. No native plugin or custom exports.</summary>
    public static class MNNSystemAppearance
    {
        public static bool IsDark(bool fallback = true)
        {
            try
            {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                IntPtr pool = AppearanceSend(AppearanceSend(Appearanceobjc_getClass("NSAutoreleasePool"), AppearanceSelector("alloc")), AppearanceSelector("init"));
                try
                {
                    IntPtr defaults = AppearanceSend(Appearanceobjc_getClass("NSUserDefaults"), AppearanceSelector("standardUserDefaults"));
                    IntPtr key = AppearanceSendString(Appearanceobjc_getClass("NSString"), AppearanceSelector("stringWithUTF8String:"), "AppleInterfaceStyle");
                    IntPtr value = AppearanceSendObject(defaults, AppearanceSelector("objectForKey:"), key);
                    return value != IntPtr.Zero && string.Equals(Marshal.PtrToStringAnsi(AppearanceSend(value, AppearanceSelector("UTF8String"))), "Dark", StringComparison.OrdinalIgnoreCase);
                }
                finally { if (pool != IntPtr.Zero) AppearanceSend(pool, AppearanceSelector("drain")); }
#elif UNITY_IOS && !UNITY_EDITOR
                IntPtr screen = AppearanceSend(Appearanceobjc_getClass("UIScreen"), AppearanceSelector("mainScreen"));
                IntPtr traits = AppearanceSend(screen, AppearanceSelector("traitCollection"));
                // userInterfaceStyle is available from iOS 12; older devices use light appearance.
                if (!AppearanceResponds(traits, AppearanceSelector("respondsToSelector:"), AppearanceSelector("userInterfaceStyle"))) return false;
                return AppearanceSendInteger(traits, AppearanceSelector("userInterfaceStyle")) == 2;
#elif UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resources = activity.Call<AndroidJavaObject>("getResources"))
                using (var configuration = resources.Call<AndroidJavaObject>("getConfiguration"))
                {
                    int night = configuration.Get<int>("uiMode") & 0x30;
                    return night == 0x20 || night == 0 && fallback;
                }
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                uint bytes = 4;
                int result = AppearanceRegistryValue(new IntPtr(unchecked((int)0x80000001)),
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme",
                    0x10, IntPtr.Zero, out uint light, ref bytes);
                return result == 0 && bytes == 4 ? light == 0 : fallback;
#elif UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
                string theme = Environment.GetEnvironmentVariable("GTK_THEME");
                return string.IsNullOrEmpty(theme) ? fallback : theme.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0;
#else
                return fallback;
#endif
            }
            catch (Exception) { return fallback; }
        }

    }
}
