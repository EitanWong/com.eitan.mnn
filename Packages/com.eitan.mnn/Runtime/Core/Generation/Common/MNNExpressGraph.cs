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
    // STL ownership follows the bundled MNN 3.6.2 compiler-specific ABI contract.
    internal sealed class MNNExpressGraph : IDisposable
    {
        private readonly Dictionary<string, MNNInterop.CppShared> _byName = new Dictionary<string, MNNInterop.CppShared>(StringComparer.Ordinal);
        private readonly MNNInterop.ExpressExecutor _executor;
        private ExpressVariablesHandle _owner;
        private bool _disposed;
        internal MNNBackendType Backend => _executor.Backend;
        internal MNNExpressGraph(string path, int threads, MNNBackendType backendType = MNNBackendType.Auto, byte[] preparedGraph = null, MNNPrecisionMode precision = MNNPrecisionMode.Normal)
        {
            _executor = new MNNInterop.ExpressExecutor(threads, backendType, precision);
            try
            {
                using (_executor.Enter())
                {
                    var variables = preparedGraph == null ? MNNInterop.LoadVariables(path)
                        : MNNInterop.LoadVariables(preparedGraph, (UIntPtr)(uint)preparedGraph.Length);
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
            return RunOutputs(new[] {output}, inputs)[0].Values;
        }

        internal MNNGenerationGraph.Output[] RunOutputs(string[] outputs, MNNGenerationGraph.Input[] inputs)
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
                        MNNInterop.DestroyVariable(ref source);
                    }
                }

                var values = new MNNInterop.CppShared[outputs.Length];
                for (int i = 0; i < outputs.Length; ++i)
                    if (!_byName.TryGetValue(outputs[i], out values[i]))
                        throw new InvalidDataException("Missing Express output: " + outputs[i]);
                var pin = GCHandle.Alloc(values, GCHandleType.Pinned);
                try
                {
                    var address = pin.AddrOfPinnedObject();
                    MNNInterop.Compute(new MNNInterop.CppVector{Begin = address, End = IntPtr.Add(address, values.Length * 16), Capacity = IntPtr.Add(address, values.Length * 16)});
                }
                finally
                {
                    pin.Free();
                }

                var results = new MNNGenerationGraph.Output[values.Length];
                for (int i = 0; i < values.Length; ++i)
                {
                    var data = MNNInterop.ReadFloat(values[i], out var shape);
                    results[i] = new MNNGenerationGraph.Output(data, shape);
                }
                return results;
            }
        }

        internal string[] GetVariableNames()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNExpressGraph));
            return _byName.Keys.ToArray();
        }

        internal bool ContainsVariable(string name)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNExpressGraph));
            return _byName.ContainsKey(name);
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
