using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity
{
    // Reuses the existing, platform-gated official Module symbols and ABI contract.
    // Module supports control-flow subgraphs that Variable.load cannot execute.
    internal sealed class MNNSingleInputModule : IDisposable
    {
        private readonly MNNInterop.ExpressExecutor _executor;
        private readonly ExpressModuleHandle _owner;
        private bool _disposed;
        internal MNNBackendType Backend => _executor.Backend;

        internal MNNSingleInputModule(string path, string input, string output, int threads, MNNBackendType backend, byte[] preparedGraph = null, MNNPrecisionMode precision = MNNPrecisionMode.Normal)
        {
            var graph = preparedGraph ?? File.ReadAllBytes(path);
            _executor = new MNNInterop.ExpressExecutor(threads, backend, precision);
            try
            {
                using (_executor.Enter())
                    _owner = new ExpressModuleHandle(MNNInterop.LoadSingleInputModule(graph, input, output), _executor);
            }
            catch
            {
                _executor.Dispose();
                throw;
            }
        }

        internal float[] Run(int[] values, params int[] shape)
        {
            return RunCore(values, shape);
        }

        internal float[] Run(float[] values, params int[] shape)
        {
            return RunCore(values, shape);
        }

        private float[] RunCore(Array values, int[] shape)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNSingleInputModule));
            if (values == null || shape == null || shape.Length == 0 || shape.Any(n => n < 1) ||
                shape.Aggregate(1L, (a, b) => checked(a * b)) != values.Length)
                throw new ArgumentException("Mismatched Module input.");
            using (_executor.Enter())
            {
                var pin = GCHandle.Alloc(values, GCHandleType.Pinned);
                MNNInterop.CppShared input = default;
                try
                {
                    input = MNNInterop.Constant(pin.AddrOfPinnedObject(), shape, 2,
                        new MNNInterop.HalideType {Code = values is float[] ? (byte)2 : (byte)0, Bits = 32, Lanes = 1});
                    var result = MNNInterop.ForwardSingleInputModule(_owner.DangerousGetHandle(), input);
                    if (result.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                        throw new InvalidDataException("Non-finite MNN Module output.");
                    return result;
                }
                finally
                {
                    MNNInterop.DestroyVariable(ref input);
                    pin.Free();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _owner.Dispose();
        }
    }
}
