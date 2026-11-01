using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || (UNITY_IOS && !UNITY_EDITOR)
#if UNITY_IOS && !UNITY_EDITOR
        private const string Security = "__Internal";
        private const string CoreFoundation = "__Internal";
        private const string LibSystem = "__Internal";
#else
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string LibSystem = "/usr/lib/libSystem.B.dylib";
#endif
        internal static IntPtr AppleConstant(string name)
        {
            string framework = name.StartsWith("kCF", StringComparison.Ordinal) ? CoreFoundation : Security;
            IntPtr library = AppleDlopen(framework == "__Internal" ? null : framework, 1);
            if (library == IntPtr.Zero) throw new PlatformNotSupportedException("Apple Security framework unavailable.");
            try
            {
                IntPtr symbol = AppleDlsym(library, name);
                if (symbol == IntPtr.Zero) throw new PlatformNotSupportedException("Apple Keychain constant unavailable: " + name);
                return Marshal.ReadIntPtr(symbol);
            }
            finally { AppleDlclose(library); }
        }
        internal static int AppleSecItemCopyMatching(IntPtr query, out IntPtr result) => SecItemCopyMatching(query, out result);
        internal static int AppleSecItemUpdate(IntPtr query, IntPtr attributes) => SecItemUpdate(query, attributes);
        internal static int AppleSecItemAdd(IntPtr attributes, IntPtr result) => SecItemAdd(attributes, result);
        internal static int AppleSecItemDelete(IntPtr query) => SecItemDelete(query);
        internal static IntPtr AppleCFDictionaryCreateMutable(IntPtr allocator, IntPtr capacity, IntPtr keyCallbacks, IntPtr valueCallbacks) =>
            CFDictionaryCreateMutable(allocator, capacity, keyCallbacks, valueCallbacks);
        internal static void AppleCFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value) => CFDictionarySetValue(dictionary, key, value);
        internal static IntPtr AppleCFStringCreateWithCString(IntPtr allocator, string value, uint encoding) =>
            CFStringCreateWithCString(allocator, value, encoding);
        internal static IntPtr AppleCFDataCreate(IntPtr allocator, byte[] bytes, IntPtr length) => CFDataCreate(allocator, bytes, length);
        internal static IntPtr AppleCFDataGetLength(IntPtr data) => CFDataGetLength(data);
        internal static IntPtr AppleCFDataGetBytePtr(IntPtr data) => CFDataGetBytePtr(data);
        internal static void AppleCFRelease(IntPtr value) => CFRelease(value);

        [DllImport(Security, CallingConvention = CallingConvention.Cdecl)] private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);
        [DllImport(Security, CallingConvention = CallingConvention.Cdecl)] private static extern int SecItemUpdate(IntPtr query, IntPtr attributes);
        [DllImport(Security, CallingConvention = CallingConvention.Cdecl)] private static extern int SecItemAdd(IntPtr attributes, IntPtr result);
        [DllImport(Security, CallingConvention = CallingConvention.Cdecl)] private static extern int SecItemDelete(IntPtr query);
        [DllImport(CoreFoundation, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr CFDictionaryCreateMutable(IntPtr allocator, IntPtr capacity, IntPtr keyCallbacks, IntPtr valueCallbacks);
        [DllImport(CoreFoundation, CallingConvention = CallingConvention.Cdecl)] private static extern void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);
        [DllImport(CoreFoundation, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, [MarshalAs(UnmanagedType.LPUTF8Str)] string value, uint encoding);
        [DllImport(CoreFoundation, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, IntPtr length);
        [DllImport(CoreFoundation, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr CFDataGetLength(IntPtr data);
        [DllImport(CoreFoundation, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr CFDataGetBytePtr(IntPtr data);
        [DllImport(CoreFoundation, CallingConvention = CallingConvention.Cdecl)] private static extern void CFRelease(IntPtr value);
        [DllImport(LibSystem, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dlopen")] private static extern IntPtr AppleDlopen(string path, int flags);
        [DllImport(LibSystem, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dlsym")] private static extern IntPtr AppleDlsym(IntPtr handle, string name);
        [DllImport(LibSystem, CallingConvention = CallingConvention.Cdecl, EntryPoint = "dlclose")] private static extern int AppleDlclose(IntPtr handle);
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [StructLayout(LayoutKind.Sequential)] internal struct DataBlob { internal int Length; internal IntPtr Data; }
        internal static bool WindowsCryptProtectData(ref DataBlob input, out DataBlob output) =>
            CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
        internal static bool WindowsCryptUnprotectData(ref DataBlob input, out DataBlob output) =>
            CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
        internal static IntPtr WindowsLocalFree(IntPtr memory) => LocalFree(memory);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref DataBlob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
        [DllImport("crypt32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out DataBlob output);
        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)] private static extern IntPtr LocalFree(IntPtr memory);
#endif
    }
}
