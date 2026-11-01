using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct BackendConfig
    {
        public MNNMemoryMode memory;
        public MNNPowerMode power;
        public MNNPrecisionMode precision;
        public IntPtr sharedContext;
    }

    // MNN::ScheduleConfig under the pinned compiler ABI, including its STL vectors.
    [StructLayout(LayoutKind.Sequential)]
    internal struct ScheduleConfig
    {
        internal MNNInterop.CppVector saveTensors;
        public MNNBackendType type;
        public int numThread;
        internal MNNInterop.CppVector inputs, outputs;
        internal int pathMode;
        // ScheduleConfig::Path is a nested 56-byte object (including 4 bytes
        // of tail padding). backupType follows the whole object at offset 88.
        private int pathPadding;
        public MNNBackendType backupType;
        public IntPtr backendConfig;
    }
    internal static partial class MNNInterop
    {
        // A borrowed vector<string> for the synchronous public createSession call.
        // String construction/destruction reuses official libc++ members. Sizes and
        // vector representation belong to the existing compiler-specific ABI contract.
        internal sealed class SavedTensorNames : IDisposable
        {
            private NativeString[] _strings;
            private GCHandle _pin;
            internal CppVector Value { get; private set; }
            internal SavedTensorNames(string[] names)
            {
                if (names == null || names.Length == 0) return;
                if (names.Length > 1024) throw new ArgumentException("Too many saved tensor names.", nameof(names));
                _strings = new NativeString[names.Length];
                try
                {
                    var values = new CppString[names.Length];
                    for (int i = 0; i < names.Length; ++i)
                    {
                        if (string.IsNullOrEmpty(names[i]) || names[i].IndexOf('\0') >= 0)
                            throw new ArgumentException("Saved tensor names must be nonempty and contain no NUL.", nameof(names));
                        _strings[i] = new NativeString(names[i]);
                        values[i] = _strings[i].Value;
                    }
                    _pin = GCHandle.Alloc(values, GCHandleType.Pinned);
                    var begin = _pin.AddrOfPinnedObject();
                    var end = IntPtr.Add(begin, checked(values.Length * Marshal.SizeOf<CppString>()));
                    Value = new CppVector {Begin = begin, End = end, Capacity = end};
                }
                catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                Value = default;
                if (_pin.IsAllocated) _pin.Free();
                if (_strings == null) return;
                foreach (var value in _strings) value?.Dispose();
                _strings = null;
            }
        }
    }

}
