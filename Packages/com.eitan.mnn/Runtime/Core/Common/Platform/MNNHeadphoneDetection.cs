using System;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Output-route evidence, not a sensor indicating whether a person is wearing a device.</summary>
    public readonly struct MNNHeadphoneStatus
    {
        public bool IsKnown { get; }
        public bool Connected { get; }
        public string Detail { get; }
        public MNNHeadphoneStatus(bool known, bool connected, string detail)
        { IsKnown = known; Connected = connected; Detail = detail; }
        public static MNNHeadphoneStatus Unknown => new MNNHeadphoneStatus(false, false,
            "The current output cannot be identified. Confirm headphone use manually.");
    }

    public static partial class MNNHeadphoneDetection
    {
        /// <summary>Main thread. Queries documented OS APIs; never opens an audio device or guesses from device names.</summary>
        public static MNNHeadphoneStatus Detect()
        {
            try
            {
#if UNITY_IOS && !UNITY_EDITOR
                return DetectIos();
#elif UNITY_ANDROID && !UNITY_EDITOR
                return DetectAndroid();
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                return DetectMac();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return DetectWindows();
#else
                return MNNHeadphoneStatus.Unknown;
#endif
            }
            catch (Exception) { return MNNHeadphoneStatus.Unknown; }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static MNNHeadphoneStatus DetectAndroid()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var audio = activity.Call<AndroidJavaObject>("getSystemService", "audio"))
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                // Android 12 exposes the selected communication route. A list of merely
                // connected A2DP devices does not prove where this app's audio is playing.
                int mode = audio.Call<int>("getMode");
                if (version.GetStatic<int>("SDK_INT") >= 31 && (mode == 2 || mode == 3))
                {
                    using (var device = audio.Call<AndroidJavaObject>("getCommunicationDevice"))
                    {
                        if (device == null) return MNNHeadphoneStatus.Unknown;
                        int type = device.Call<int>("getType");
                        if (type == 3 || type == 4 || type == 22) // Wired headset, headphones, USB headset.
                            return new MNNHeadphoneStatus(true, true, "Android reports an active wired/USB headset communication route.");
                        if (type == 1 || type == 2)
                            return new MNNHeadphoneStatus(true, false, "Android reports an active built-in speaker/receiver route.");
                    }
                }
                return new MNNHeadphoneStatus(false, false,
                    "Android cannot verify this playback route. Bluetooth may be headphones, a car or speakers; confirm manually.");
            }
        }
#endif
    }
}
