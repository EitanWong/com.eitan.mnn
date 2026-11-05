#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Documented Windows Core Audio COM interfaces, default render endpoint and
        // PKEY_AudioEndpoint_FormFactor. No parsing of localized device names.
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        internal class DeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IAudioDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioDevice
        {
            [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr activation, out IntPtr instance);
            [PreserveSig] int OpenPropertyStore(uint access, out IAudioProperties properties);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PropertyKey { public Guid format; public uint id; }

        // A PROPVARIANT occupies 24 bytes on Win64; the UInt32 payload for VT_UI4
        // starts at byte 8. Detection is restricted to Win64 below.
        [StructLayout(LayoutKind.Explicit, Size = 24)]
        internal struct PropertyValue
        {
            [FieldOffset(0)] public ushort type;
            [FieldOffset(8)] public uint number;
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IAudioProperties
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropertyValue value);
        }

        [DllImport("ole32.dll", ExactSpelling = true)]
        internal static extern int CoInitializeEx(IntPtr reserved, uint mode);
        [DllImport("ole32.dll", ExactSpelling = true)]
        internal static extern void CoUninitialize();
        [DllImport("ole32.dll", ExactSpelling = true)]
        internal static extern int PropVariantClear(ref PropertyValue value);

    }
}
#endif
