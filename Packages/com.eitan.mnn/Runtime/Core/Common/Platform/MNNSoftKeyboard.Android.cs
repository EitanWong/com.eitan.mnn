#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace MNN.Unity
{
    public sealed partial class MNNSoftKeyboard
    {
        private static void ReadAndroid(out Rect area)
        {
            area = Rect.zero;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var view = activity.Call<AndroidJavaObject>("findViewById", 16908290)) // android.R.id.content
                using (var visible = new AndroidJavaObject("android.graphics.Rect"))
                {
                    if (view == null) return;
                    view.Call("getWindowVisibleDisplayFrame", visible);
                    IntPtr coordinates = AndroidJNI.NewIntArray(2);
                    try
                    {
                        var method = AndroidJNIHelper.GetMethodID(view.GetRawClass(), "getLocationOnScreen", "([I)V");
                        AndroidJNI.CallVoidMethod(view.GetRawObject(), method, new[] { new jvalue { l = coordinates } });
                        int[] origin = AndroidJNI.FromIntArray(coordinates);
                        int width = view.Call<int>("getWidth"), height = view.Call<int>("getHeight");
                        int top = visible.Get<int>("bottom");
                        int occlusion = origin[1] + height - top;
                        // A resized content view has no overlap. Small system-bar insets are not an IME.
                        if (occlusion < Mathf.Max(80f, height * .15f)) return;
                        area = MapOcclusion(new Rect(origin[0], top, width, occlusion),
                            new Rect(origin[0], origin[1], width, height), new Vector2(Screen.width, Screen.height));
                    }
                    finally { AndroidJNI.DeleteLocalRef(coordinates); }
                }
            }
            catch (AndroidJavaException) { }
        }
    }
}
#endif
