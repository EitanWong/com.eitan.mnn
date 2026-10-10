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

    // MNN::ScheduleConfig, including its empty STL saveTensors / Path vectors.
    [StructLayout(LayoutKind.Sequential)]
    internal struct ScheduleConfig
    {
        internal MNNInterop.CppVector saveTensors;
        public MNNBackendType type;
        public int numThread;
        internal MNNInterop.CppVector inputs, outputs;
        internal int pathMode;
        public MNNBackendType backupType;
        public IntPtr backendConfig;
    }
}
