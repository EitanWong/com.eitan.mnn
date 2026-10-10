using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        // libc++ ABI v1 std::function<bool(const float*, size_t, bool)>.
        // Layout verified from clang record layouts of official omni.cpp:
        // arm64 uses 24-byte storage / pointer at 24; x86_64's 16-byte
        // max_align_t rounds that storage to 32 bytes / pointer at 32.
        // The functor is managed-owned; native clones/deletes dispatch through
        // these rooted delegates. No C++ shim or executable trampoline is built.
        private sealed class WaveCollector
        {
            internal readonly List<float> Samples = new List<float>();
            internal Exception Error;
        }

        [UnmanagedFunctionPointer(Conv)]
        private delegate void FunctorAction(IntPtr self);
        [UnmanagedFunctionPointer(Conv)]
        private delegate IntPtr FunctorClone(IntPtr self);
        [UnmanagedFunctionPointer(Conv)]
        private delegate void FunctorCopy(IntPtr self, IntPtr destination);
        [UnmanagedFunctionPointer(Conv)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool FunctorInvoke(IntPtr self, IntPtr samplesRef, IntPtr countRef, IntPtr lastRef);
        [UnmanagedFunctionPointer(Conv)]
        private delegate void SetWaveFunction(IntPtr model, IntPtr function);
        [UnmanagedFunctionPointer(Conv)]
        private delegate void GenerateWaveFunction(IntPtr model);
        // Do not initialize native callback pointers when a caller merely asks
        // whether its platform is supported. In particular IL2CPP must be able
        // to execute the guard without touching the Mono callback ABI.
        private static class WaveFunctorAbi
        {
            private static readonly FunctorAction WaveDestroy = DestroyWaveFunctor;
            private static readonly FunctorAction WaveDelete = DeleteWaveFunctor;
            private static readonly FunctorClone WaveClone = CloneWaveFunctor;
            private static readonly FunctorCopy WaveCopy = CopyWaveFunctor;
            private static readonly FunctorInvoke WaveInvoke = InvokeWaveFunctor;
            internal static readonly IntPtr Vtable = Create();
            private static IntPtr Create()
            {
                IntPtr table = Marshal.AllocHGlobal(9 * 8);
                Marshal.WriteIntPtr(table, IntPtr.Zero);
                Marshal.WriteIntPtr(table, 8, IntPtr.Zero);
                Delegate[] methods = {WaveDestroy, WaveDelete, WaveClone, WaveCopy, WaveDestroy, WaveDelete, WaveInvoke};
                for (int i = 0; i < methods.Length; ++i)
                    Marshal.WriteIntPtr(table, (i + 2) * 8, Marshal.GetFunctionPointerForDelegate(methods[i]));
                return IntPtr.Add(table, 16); // Process lifetime, like a native static vtable.
            }
        }

        private static WaveCollector GetCollector(IntPtr self) => (WaveCollector)GCHandle.FromIntPtr(Marshal.ReadIntPtr(self, 8)).Target;
        private static void InitWaveFunctor(IntPtr self, WaveCollector collector)
        {
            Marshal.WriteIntPtr(self, WaveFunctorAbi.Vtable);
            Marshal.WriteIntPtr(self, 8, GCHandle.ToIntPtr(GCHandle.Alloc(collector)));
        }

        private static void DestroyWaveFunctor(IntPtr self)
        {
            IntPtr handle = Marshal.ReadIntPtr(self, 8);
            if (handle != IntPtr.Zero)
            {
                GCHandle.FromIntPtr(handle).Free();
                Marshal.WriteIntPtr(self, 8, IntPtr.Zero);
            }
        }

        private static void DeleteWaveFunctor(IntPtr self)
        {
            DestroyWaveFunctor(self);
            Marshal.FreeHGlobal(self);
        }

        private static IntPtr CloneWaveFunctor(IntPtr self)
        {
            IntPtr copy = Marshal.AllocHGlobal(16);
            InitWaveFunctor(copy, GetCollector(self));
            return copy;
        }

        private static void CopyWaveFunctor(IntPtr self, IntPtr destination) => InitWaveFunctor(destination, GetCollector(self));
        private static bool InvokeWaveFunctor(IntPtr self, IntPtr samplesRef, IntPtr countRef, IntPtr lastRef)
        {
            var collector = GetCollector(self);
            try
            {
                int count = checked((int)unchecked((ulong)Marshal.ReadInt64(countRef)));
                if (count < 0 || count > 24000 * 600)
                    throw new InvalidOperationException("Invalid native waveform size.");
                IntPtr samples = Marshal.ReadIntPtr(samplesRef);
                if (count > 0 && samples != IntPtr.Zero)
                {
                    var part = new float[count];
                    Marshal.Copy(samples, part, 0, count);
                    lock (collector.Samples)
                        collector.Samples.AddRange(part);
                }

                return true;
            }
            catch (Exception error)
            {
                collector.Error = error;
                return false;
            }
        }

        private sealed class WaveCallback : IDisposable
        {
            internal readonly WaveCollector Collector = new WaveCollector();
            private readonly IntPtr _model;
            internal WaveCallback(IntPtr model)
            {
                _model = model;
                Set(Collector);
            }

            private void Set(WaveCollector collector)
            {
                bool x64 = RuntimeInformation.ProcessArchitecture == Architecture.X64;
                int storageSize = x64 ? 32 : 24;
                int functionSize = storageSize + IntPtr.Size;
                IntPtr function = Marshal.AllocHGlobal(functionSize), functor = IntPtr.Zero;
                for (int i = 0; i < functionSize; i += IntPtr.Size)
                    Marshal.WriteIntPtr(function, i, IntPtr.Zero);
                try
                {
                    if (collector != null)
                    {
                        functor = Marshal.AllocHGlobal(16);
                        InitWaveFunctor(functor, collector);
                        Marshal.WriteIntPtr(function, storageSize, functor);
                    }

                    Virtual<SetWaveFunction>(_model, 12)(_model, function);
                }
                finally
                {
                    // By-value nontrivial argument: caller destroys the moved-from
                    // argument, which the implementation may have emptied.
                    IntPtr remaining = Marshal.ReadIntPtr(function, storageSize);
                    if (remaining != IntPtr.Zero)
                        DeleteWaveFunctor(remaining);
                    Marshal.FreeHGlobal(function);
                }
            }

            public void Dispose() => Set(null);
        }
    }
}
