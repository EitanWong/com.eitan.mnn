using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        internal static IReadOnlyList<MNNNeuralDevice> ReadNeuralDevices()
        {
            var devices = new List<MNNNeuralDevice>();
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (NNDeviceCount(out uint count) != 0) return devices.AsReadOnly();
                for (uint index = 0; index < Math.Min(count, 64u); index++)
                {
                    if (NNGetDevice(index, out IntPtr device) != 0 || device == IntPtr.Zero) continue;
                    if (NNDeviceName(device, out IntPtr name) != 0 || NNDeviceType(device, out int type) != 0) continue;
                    NNDeviceFeatureLevel(device, out long level);
                    devices.Add(new MNNNeuralDevice(Marshal.PtrToStringUTF8(name), type, level));
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { } // API < 29.
#endif
            return devices.AsReadOnly();
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        // Android NDK NeuralNetworks.h, API 29; no custom native entry points.
        [DllImport("neuralnetworks", CallingConvention = Conv, EntryPoint = "ANeuralNetworks_getDeviceCount")]
        private static extern int NNDeviceCount(out uint count);
        [DllImport("neuralnetworks", CallingConvention = Conv, EntryPoint = "ANeuralNetworks_getDevice")]
        private static extern int NNGetDevice(uint index, out IntPtr device);
        [DllImport("neuralnetworks", CallingConvention = Conv, EntryPoint = "ANeuralNetworksDevice_getName")]
        private static extern int NNDeviceName(IntPtr device, out IntPtr name);
        [DllImport("neuralnetworks", CallingConvention = Conv, EntryPoint = "ANeuralNetworksDevice_getType")]
        private static extern int NNDeviceType(IntPtr device, out int type);
        [DllImport("neuralnetworks", CallingConvention = Conv, EntryPoint = "ANeuralNetworksDevice_getFeatureLevel")]
        private static extern int NNDeviceFeatureLevel(IntPtr device, out long level);
#endif
    }
}
