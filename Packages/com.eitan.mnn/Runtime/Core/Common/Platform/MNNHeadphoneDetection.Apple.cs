using MNN.Unity.Interop;
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity
{
    public static partial class MNNHeadphoneDetection
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private static uint[] ReadAudioProperty(uint target, uint selector, uint scope)
        {
            var address = new MNNInterop.AudioDevicePropertyAddress(selector, scope);
            if (MNNInterop.AudioObjectGetPropertyDataSize(target, ref address, 0, IntPtr.Zero, out uint size) != 0 || size == 0 || size > 1024 || size % 4 != 0)
                return Array.Empty<uint>();
            var values = new uint[size / 4];
            if (MNNInterop.AudioObjectGetPropertyData(target, ref address, 0, IntPtr.Zero, ref size, values) != 0) return Array.Empty<uint>();
            Array.Resize(ref values, (int)size / 4);
            return values;
        }

        private static MNNHeadphoneStatus DetectMac()
        {
            const uint global = 0x676c6f62, output = 0x6f757470; // glob, outp
            var devices = ReadAudioProperty(1, 0x644f7574, global); // dOut
            if (devices.Length != 1) return MNNHeadphoneStatus.Unknown;
            var streams = ReadAudioProperty(devices[0], 0x73746d23, output); // stm#
            if (streams.Length == 0) return MNNHeadphoneStatus.Unknown;
            bool? headphones = null;
            foreach (uint stream in streams)
            {
                var terminal = ReadAudioProperty(stream, 0x7465726d, global); // term
                if (terminal.Length != 1) return MNNHeadphoneStatus.Unknown;
                bool isHeadphone = terminal[0] == 0x68647068; // hdph
                bool isSpeaker = terminal[0] == 0x73706b72 || terminal[0] == 0x6c666573 || terminal[0] == 0x7273706b;
                if (!isHeadphone && !isSpeaker || headphones.HasValue && headphones.Value != isHeadphone)
                    return MNNHeadphoneStatus.Unknown;
                headphones = isHeadphone;
            }
            return new MNNHeadphoneStatus(true, headphones == true,
                headphones == true ? "macOS reports a headphone output terminal." : "macOS reports a speaker output terminal.");
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private static IntPtr GetObject(IntPtr receiver, string selector) => receiver == IntPtr.Zero ? IntPtr.Zero : MNNInterop.SendObject(receiver, MNNInterop.sel_registerName(selector));
        private static MNNHeadphoneStatus DetectIos()
        {
            IntPtr session = GetObject(MNNInterop.objc_getClass("AVAudioSession"), "sharedInstance");
            IntPtr outputs = GetObject(GetObject(session, "currentRoute"), "outputs");
            if (outputs == IntPtr.Zero) return MNNHeadphoneStatus.Unknown;
            ulong count = MNNInterop.SendCount(outputs, MNNInterop.sel_registerName("count"));
            if (count == 0 || count > 16) return MNNHeadphoneStatus.Unknown;
            bool? headphones = null;
            for (ulong i = 0; i < count; ++i)
            {
                IntPtr port = MNNInterop.SendIndex(outputs, MNNInterop.sel_registerName("objectAtIndex:"), i);
                string type = Marshal.PtrToStringAnsi(GetObject(GetObject(port, "portType"), "UTF8String"));
                bool isHeadphone = type == "Headphones";
                bool isSpeaker = type == "Speaker" || type == "Receiver";
                if (!isHeadphone && !isSpeaker || headphones.HasValue && headphones.Value != isHeadphone)
                    return new MNNHeadphoneStatus(false, false,
                        "iOS reports an external or Bluetooth route whose headphone type cannot be verified. Confirm manually.");
                headphones = isHeadphone;
            }
            return new MNNHeadphoneStatus(true, headphones == true,
                headphones == true ? "iOS reports a headphone output route." : "iOS reports a built-in speaker/receiver route.");
        }
#endif
    }
}
