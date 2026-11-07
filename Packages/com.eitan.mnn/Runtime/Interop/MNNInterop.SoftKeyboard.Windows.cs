#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // Windows 8+ desktop COM contract, as declared by the Windows SDK's ShObjIdl_core.h.
        // https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-iframeworkinputpane
        [ComImport, Guid("D5120AA3-46BA-44C5-822D-CA8092C1FC72")]
        internal class FrameworkInputPane { }
        [ComImport, Guid("5752238B-24F0-495A-82F1-2FD593056796"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IFrameworkInputPane
        {
            [PreserveSig] int Advise(IntPtr window, IntPtr handler, out uint cookie);
            [PreserveSig] int AdviseWithHWND(IntPtr window, IntPtr handler, out uint cookie);
            [PreserveSig] int Unadvise(uint cookie);
            [PreserveSig] int Location(out NativeRect location);
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct NativeRect { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct NativePoint { public int x, y; }
        [DllImport("user32.dll", ExactSpelling = true)] internal static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", ExactSpelling = true)] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", ExactSpelling = true)] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("kernel32.dll", ExactSpelling = true)] internal static extern uint GetCurrentProcessId();
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetClientRect(IntPtr window, out NativeRect rect);
        [DllImport("user32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
    }
}
#endif
