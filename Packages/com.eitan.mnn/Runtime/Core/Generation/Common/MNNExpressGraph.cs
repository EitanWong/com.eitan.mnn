using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity
{
    // Public Express Variable APIs for data-dependent graphs without control-flow subgraphs.
    // STL ownership follows the existing MNN 3.6.1 / Apple libc++ 64-bit ABI contract.
    internal sealed class MNNExpressGraph : IDisposable
    {
        private readonly Dictionary<string, MNNInterop.CppShared> _byName = new Dictionary<string, MNNInterop.CppShared>(StringComparer.Ordinal);
        private readonly MNNInterop.ExpressExecutor _executor;
        private ExpressVariablesHandle _owner;
        private bool _disposed;
        internal MNNBackendType Backend => _executor.Backend;
        internal MNNExpressGraph(string path, int threads, MNNBackendType backendType = MNNBackendType.Auto)
        {
            _executor = new MNNInterop.ExpressExecutor(threads, backendType);
            try
            {
                using (_executor.Enter())
                {
                    var variables = MNNInterop.LoadVariables(path);
                    _owner = new ExpressVariablesHandle(variables, _executor);
                    int count = MNNInterop.VectorCount(variables, 16);
                    for (int i = 0; i < count; ++i)
                    {
                        var value = Marshal.PtrToStructure<MNNInterop.CppShared>(IntPtr.Add(variables.Begin, i * 16));
                        string name = MNNInterop.GetName(value.Object);
                        if (!string.IsNullOrEmpty(name))
                            _byName[name] = value;
                    }

                    if (_byName.Count == 0)
                        throw new InvalidDataException("Express graph has no named variables.");
                }
            }
            catch
            {
                if (_owner != null && !_owner.IsInvalid)
                    _owner.Dispose();
                else
                    _executor.Dispose();
                throw;
            }
        }

        internal float[] Run(string output, MNNGenerationGraph.Input[] inputs)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNExpressGraph));
            using (_executor.Enter())
            {
                foreach (var input in inputs)
                {
                    if (!_byName.TryGetValue(input.Name, out var variable))
                        throw new InvalidDataException("Missing Express input: " + input.Name);
                    var source = MakeConstant(input.Values, input.Shape);
                    try
                    {
                        if (!MNNInterop.Input(variable, source))
                            throw new MNNException(MNNErrorCode.NoExecution, "Express input rejected: " + input.Name);
                    }
                    finally
                    {
                        MNNInterop.ReleaseShared(ref source);
                    }
                }

                if (!_byName.TryGetValue(output, out var result))
                    throw new InvalidDataException("Missing Express output: " + output);
                var values = new[]{result};
                var pin = GCHandle.Alloc(values, GCHandleType.Pinned);
                try
                {
                    var address = pin.AddrOfPinnedObject();
                    MNNInterop.Compute(new MNNInterop.CppVector{Begin = address, End = IntPtr.Add(address, 16), Capacity = IntPtr.Add(address, 16)});
                }
                finally
                {
                    pin.Free();
                }

                return MNNInterop.ReadFloat(result);
            }
        }

        internal string[] GetVariableNames()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNExpressGraph));
            return _byName.Keys.ToArray();
        }

        private static MNNInterop.CppShared MakeConstant(Array values, int[] shape)
        {
            if (!(values is int[]) && !(values is float[]))
                throw new NotSupportedException("Express input requires int32 or float32 data.");
            var type = new MNNInterop.HalideType{Code = values is float[] ? 2 : 0, Bits = 32, Lanes = 1};
            var pin = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                return MNNInterop.Constant(pin.AddrOfPinnedObject(), shape, 2, type);
            }
            finally
            {
                pin.Free();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _owner.Dispose();
            _byName.Clear();
        }
    }
}
