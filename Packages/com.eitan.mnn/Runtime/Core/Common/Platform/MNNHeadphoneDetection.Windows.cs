#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using MNN.Unity.Interop;
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity
{
    public static partial class MNNHeadphoneDetection
    {
        private static MNNHeadphoneStatus DetectWindows()
        {
            if (IntPtr.Size != 8) return MNNHeadphoneStatus.Unknown;
            int initialized = MNNInterop.CoInitializeEx(IntPtr.Zero, 2);
            object enumerator = null;
            MNNInterop.IAudioDevice device = null;
            MNNInterop.IAudioProperties properties = null;
            var value = default(MNNInterop.PropertyValue);
            try
            {
                enumerator = new MNNInterop.DeviceEnumerator();
                // eRender / eMultimedia describes normal playback, rather than an unused
                // communications endpoint which can be a different physical device.
                if (((MNNInterop.IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(0, 1, out device) != 0 || device == null ||
                    device.OpenPropertyStore(0, out properties) != 0 || properties == null) return MNNHeadphoneStatus.Unknown;
                var key = new MNNInterop.PropertyKey { format = new Guid("1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E"), id = 0 };
                if (properties.GetValue(ref key, out value) != 0 || value.type != 19) return MNNHeadphoneStatus.Unknown; // VT_UI4
                if (value.number == 3 || value.number == 5) // Headphones / Headset
                    return new MNNHeadphoneStatus(true, true, "Windows reports a headphone/headset playback endpoint.");
                if (value.number == 1 || value.number == 8) // Speakers / SPDIF
                    return new MNNHeadphoneStatus(true, false, "Windows reports a speaker playback endpoint.");
                return MNNHeadphoneStatus.Unknown;
            }
            finally
            {
                MNNInterop.PropVariantClear(ref value);
                if (properties != null) Marshal.ReleaseComObject(properties);
                if (device != null) Marshal.ReleaseComObject(device);
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
                if (initialized >= 0) MNNInterop.CoUninitialize();
            }
        }
    }
}
#endif
