using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace MNN.Unity
{
    // Uses the existing platform-gated official Interpreter/Tensor APIs.
    // No new STL layouts, private object offsets or virtual dispatch assumptions.
    internal sealed class MNNGenerationGraph : IDisposable
    {
        private readonly MNNInterpreter _interpreter;
        private MNNSession _session;
        private MNNExpressGraph _express;
        private readonly string _path;
        private readonly int _threads;
        private readonly MNNPrecisionMode _precisionMode;
        private readonly MNNGpuTuningMode _gpuTuningMode;
        private readonly bool _recreateGpuSessionOnShapeChange;
        private bool _sessionHasRun;
        private bool _disposed;
        private bool _cacheEnabled;
        private bool _cacheSaved;
        private readonly bool _prepared;
        private readonly int _cpuSessionCacheCapacity;
        private readonly Dictionary<string, MNNSession> _cpuSessions = new Dictionary<string, MNNSession>(StringComparer.Ordinal);
        internal MNNBackendType Backend => _express != null ? _express.Backend : _session.ActualBackend;
        internal MNNGenerationGraph(string path, int threads, byte[] preparedGraph = null, bool express = false, MNNBackendType backendType = MNNBackendType.Auto, int cpuSessionCacheCapacity = 1, MNNPrecisionMode precisionMode = MNNPrecisionMode.Normal, string externalWeightsPath = null, MNNGpuTuningMode gpuTuningMode = MNNGpuTuningMode.Wide, bool recreateGpuSessionOnShapeChange = false, int? cpuWinogradMemoryLevel = null)
        {
            backendType = MNNAcceleration.ForDynamicGraph(backendType);
            if (!Enum.IsDefined(typeof(MNNPrecisionMode), precisionMode) || express && precisionMode != MNNPrecisionMode.Normal)
                throw new ArgumentOutOfRangeException(nameof(precisionMode), "Explicit graph precision requires an Interpreter session.");
            _precisionMode = precisionMode;
            _gpuTuningMode = gpuTuningMode;
            _recreateGpuSessionOnShapeChange = recreateGpuSessionOnShapeChange;
            if (cpuWinogradMemoryLevel.HasValue && (express || backendType != MNNBackendType.CPU || cpuWinogradMemoryLevel.Value < 0 || cpuWinogradMemoryLevel.Value > 3))
                throw new ArgumentOutOfRangeException(nameof(cpuWinogradMemoryLevel), "Convolution hints require an explicit CPU Interpreter graph and a level from zero to three.");
            if (cpuSessionCacheCapacity < 1 || cpuSessionCacheCapacity > 16 || cpuSessionCacheCapacity > 1 && (express || backendType != MNNBackendType.CPU))
                throw new ArgumentOutOfRangeException(nameof(cpuSessionCacheCapacity), "Shape caching is limited to CPU Interpreter stages, at most sixteen sessions.");
            _cpuSessionCacheCapacity = cpuSessionCacheCapacity;
            if (!File.Exists(path) || new FileInfo(path).Length < 16)
                throw new FileNotFoundException("Missing MNN graph: " + path);
            if (externalWeightsPath != null && (express || !File.Exists(externalWeightsPath)))
                throw new ArgumentException("External weights require an existing file and an Interpreter graph.", nameof(externalWeightsPath));
            _path = path;
            _threads = threads;
            _prepared = preparedGraph != null;
            if (express)
            {
                try
                {
                    _express = new MNNExpressGraph(path, threads, backendType);
                }
                catch (Exception error)when (MNNAcceleration.Resolve(backendType) != MNNBackendType.CPU && MNNAcceleration.CanRetryOnCpu(error))
                {
                    UnityEngine.Debug.Log($"[MNN] {path}: accelerator graph loading failed; retrying on CPU. {error.Message}");
                    _express = new MNNExpressGraph(path, threads, MNNBackendType.CPU);
                }

                return;
            }

            using (MNN.Unity.Interop.MNNInterop.BeginNativeCall())
            {
                _interpreter = preparedGraph == null ? MNNInterpreter.CreateFromFile(path) : MNNInterpreter.CreateFromBuffer(preparedGraph);
                try
                {
                    if (preparedGraph != null || externalWeightsPath != null)
                        _interpreter.SetExternalFile(externalWeightsPath ?? path + ".weight");
                    if (cpuWinogradMemoryLevel.HasValue)
                        _interpreter.SetWinogradMemoryLevel(cpuWinogradMemoryLevel.Value);
                    ConfigureCache(MNNAcceleration.Resolve(backendType), precisionMode);
                    _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = backendType, ThreadCount = threads, PrecisionMode = precisionMode, GpuTuningMode = _gpuTuningMode});
                }
                catch
                {
                    _interpreter.Dispose();
                    throw;
                }
            }
        }

        internal float[] Run(string output, params Input[] inputs)
        {
            return Validate(RunOutputs(new[] { output }, inputs)[0].Values, output);
        }

        // Recurrent state may contain native integers or zero-length dimensions.
        // Preserve its type and shape from one execution through the next input.
        internal Output[] RunOutputs(string[] names, params Input[] inputs)
        {
            using (MNN.Unity.Interop.MNNInterop.BeginNativeCall())
                return RunOutputsWithNativeScope(names, inputs);
        }

        private Output[] RunOutputsWithNativeScope(string[] names, Input[] inputs)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNGenerationGraph));
            if (names == null || names.Length == 0 || names.Any(string.IsNullOrEmpty))
                throw new ArgumentException("At least one named graph output is required.", nameof(names));
            try
            {
                return RunOutputsCore(names, inputs);
            }
            catch (Exception error)when (Backend != MNNBackendType.CPU && MNNAcceleration.CanRetryOnCpu(error))
            {
                if (_express == null && (Backend == MNNBackendType.Metal || Backend == MNNBackendType.OpenCL || Backend == MNNBackendType.Vulkan) && error is InvalidDataException && _session.Config.PrecisionMode != MNNPrecisionMode.High)
                {
                    var gpu = Backend;
                    UnityEngine.Debug.Log($"[MNN] {_path}: {gpu} output invalid; retrying GPU with High (FP32) precision.");
                    try
                    {
                        _session.Dispose();
                        ConfigureCache(gpu, MNNPrecisionMode.High);
                        _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = gpu, ThreadCount = _threads, PrecisionMode = MNNPrecisionMode.High, GpuTuningMode = _gpuTuningMode});
                        return RunOutputsCore(names, inputs);
                    }
                    catch (Exception retryError)when (MNNAcceleration.CanRetryOnCpu(retryError))
                    {
                        UnityEngine.Debug.Log($"[MNN] {_path}: GPU FP32 retry failed: {retryError.Message}");
                    }
                }

                UnityEngine.Debug.Log($"[MNN] {_path}: {Backend} failed; retrying stage on CPU. {error.Message}");
                if (_express != null)
                {
                    _express.Dispose();
                    _express = new MNNExpressGraph(_path, _threads, MNNBackendType.CPU);
                }
                else
                {
                    _session.Dispose();
                    _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.CPU, ThreadCount = _threads, PrecisionMode = _precisionMode});
                }

                return RunOutputsCore(names, inputs);
            }
        }

        private void ConfigureCache(MNNBackendType backend, MNNPrecisionMode precision)
        {
            _cacheSaved = false;
            _cacheEnabled = false;
            if (_prepared || backend == MNNBackendType.CPU || _recreateGpuSessionOnShapeChange)
                return;
            try
            {
                string cache = MNNInferenceCache.PathFor(_path, backend, precision);
                if (_gpuTuningMode != MNNGpuTuningMode.Wide) cache = Path.ChangeExtension(cache, _gpuTuningMode + ".cache");
                _interpreter.SetCacheFile(cache);
                _cacheEnabled = true;
            }
            catch (Exception error)when (error is IOException || error is UnauthorizedAccessException || error is MNNException)
            {
                UnityEngine.Debug.Log("[MNN] GPU cache unavailable: " + error.Message);
            }
        }

        private static Output[] ValidateOutputs(Output[] outputs, string[] names)
        {
            for (int i = 0; i < outputs.Length; ++i)
                if (outputs[i].Data is float[] floats && floats.Length != 0)
                    Validate(floats, names[i]);
            return outputs;
        }

        internal sealed class Output
        {
            internal readonly Array Data;
            internal readonly int[] Shape;
            internal float[] Values => ToFloat(Data);

            internal Output(Array values, int[] shape)
            {
                if (values == null || shape == null || shape.Any(dimension => dimension < 0))
                    throw new ArgumentException("Invalid graph output shape or data.");
                if (!(values is float[]) && !(values is int[]) && !(values is long[]) && !(values is byte[]))
                    throw new NotSupportedException("Unsupported graph output data type.");
                int count = 1;
                foreach (int dimension in shape)
                    count = checked(count * dimension);
                if (count != values.Length)
                    throw new ArgumentException("Graph output shape/data mismatch.");
                Data = values;
                Shape = shape;
            }

            // Recurrent integer states must return to the graph without passing
            // through float32 (which cannot preserve arbitrary int32/int64 values).
            internal Input AsInput(string name) => new Input(name, Data, Shape);
        }

        private Output[] RunOutputsCore(string[] names, Input[] inputs)
        {
            if (_express != null)
                return ValidateOutputs(_express.RunOutputs(names, inputs), names);
            if (_cpuSessionCacheCapacity > 1)
            {
                string key = string.Join(";", inputs.OrderBy(input => input.Name, StringComparer.Ordinal)
                    .Select(input => input.Name + ":" + string.Join(",", input.Shape)));
                if (_cpuSessions.TryGetValue(key, out var cached))
                    _session = cached;
                else
                {
                    // One Interpreter owns shared model data. The first session is
                    // reused; subsequent entries keep each small, fixed KV shape.
                    if (_cpuSessions.Count > 0)
                    {
                        if (_cpuSessions.Count == _cpuSessionCacheCapacity)
                        {
                            string oldest = _cpuSessions.Keys.First();
                            _cpuSessions[oldest].Dispose(); _cpuSessions.Remove(oldest);
                        }
                        _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.CPU, ThreadCount = _threads, PrecisionMode = _precisionMode});
                    }
                    _cpuSessions.Add(key, _session);
                }
            }
            var shapes = inputs.Where(input => !_session.GetInput(input.Name).Shape.SequenceEqual(input.Shape)).ToDictionary(input => input.Name, input => input.Shape);
            if (shapes.Count > 0)
            {
                // CAM++ native Metal replay does not preserve source results after
                // repeated runs followed by a shape change. Keep the Interpreter's
                // model data, and give each new shape a fresh backend session.
                if (_recreateGpuSessionOnShapeChange && _sessionHasRun && Backend != MNNBackendType.CPU)
                {
                    var backend = Backend;
                    _session.Dispose();
                    _session = _interpreter.CreateSession(new MNNSessionConfig { BackendType = backend, ThreadCount = _threads,
                        PrecisionMode = _precisionMode, GpuTuningMode = _gpuTuningMode });
                    _sessionHasRun = false;
                    // The new session starts with model defaults for every input.
                    // Restore even inputs whose shape matched the previous session.
                    shapes = inputs.ToDictionary(input => input.Name, input => input.Shape);
                }
                _session.ResizeInputs(shapes, true);
                _cacheSaved = false;
            }

            foreach (var input in inputs)
            {
                var tensor = _session.GetInput(input.Name);
                if (input.ShapeOnly || input.Values.Length == 0)
                    continue;
                if (input.Values is int[] ints)
                {
                    if (tensor.DataType == MNNDataType.Int64)
                        tensor.CopyFromArray(ints.Select(value => (long)value).ToArray());
                    else
                        tensor.CopyFromArray(ints);
                }
                else if (input.Values is long[] longs)
                    tensor.CopyFromArray(longs);
                else if (input.Values is byte[] bytes)
                    tensor.CopyFromArray(bytes);
                else
                    tensor.CopyFromArray((float[])input.Values);
            }

            _session.Run();
            _sessionHasRun = true;
            if (_cacheEnabled && !_cacheSaved && Backend != MNNBackendType.CPU)
            {
                try
                {
                    _session.UpdateCache();
                    _cacheSaved = true;
                }
                catch (Exception error)when (error is MNNException || error is IOException || error is UnauthorizedAccessException)
                {
                    _cacheEnabled = false;
                    UnityEngine.Debug.Log("[MNN] GPU cache update unavailable: " + error.Message);
                }
            }

            var result = new Output[names.Length];
            for (int i = 0; i < names.Length; ++i)
            {
                var tensor = _session.GetOutput(names[i]);
                result[i] = new Output(ReadData(tensor), tensor.Shape);
            }
            return ValidateOutputs(result, names);
        }

        // Token IDs, lengths and boolean flags in exported speech stages are integer
        // tensors. Convert values explicitly, never reinterpret their bits as FP32.
        private static Array ReadData(MNNTensor tensor)
        {
            if (tensor.DataType == MNNDataType.Int32)
                return tensor.CopyToArray<int>();
            if (tensor.DataType == MNNDataType.Int64)
                return tensor.CopyToArray<long>();
            if (tensor.DataType == MNNDataType.UInt8)
                return tensor.CopyToArray<byte>();
            return tensor.CopyToArray<float>();
        }

        private static float[] ToFloat(Array values)
        {
            if (values is float[] floats)
                return floats;
            if (values is int[] ints)
                return ints.Select(value => Math.Abs((long)value) <= 16777216 ? (float)value : throw new InvalidDataException("Integer graph output exceeds exact FP32 range.")).ToArray();
            if (values is long[] longs)
                return longs.Select(value => value >= -16777216 && value <= 16777216 ? (float)value : throw new InvalidDataException("Integer graph output exceeds exact FP32 range.")).ToArray();
            if (values is byte[] bytes)
                return bytes.Select(value => (float)value).ToArray();
            throw new NotSupportedException("Unsupported graph output data type.");
        }

        internal static float[] Validate(float[] values, string output)
        {
            if (values.Length == 0)
                throw new InvalidDataException("Generation stage returned an empty or non-finite tensor: " + output);
            // Scan every element without LINQ's per-element delegate dispatch.
            // Ordered bounds reject both NaN and infinities, retaining finite extrema.
            for (int i = 0; i < values.Length; ++i)
            {
                float value = values[i];
                if (!(value >= -float.MaxValue && value <= float.MaxValue))
                    throw new InvalidDataException("Generation stage returned an empty or non-finite tensor: " + output);
            }
            return values;
        }

        internal sealed class Input
        {
            internal readonly string Name;
            internal readonly int[] Shape;
            internal readonly Array Values;
            internal readonly bool ShapeOnly;
            // Some exported inputs are used only by Shape operators and have no
            // native data allocation. Resize them without mapping a data buffer.
            internal Input(string name, int[] shape)
            {
                if (shape == null || shape.Length == 0 || shape.Any(dim => dim < 1))
                    throw new ArgumentException("Invalid shape-only input: " + name);
                Name = name; Shape = shape; Values = Array.Empty<float>(); ShapeOnly = true;
            }
            internal Input(string name, Array values, params int[] shape)
            {
                Name = name;
                Values = values;
                Shape = shape;
                // An empty shape is a rank-zero scalar with exactly one value.
                if (values == null || shape == null || shape.Any(dim => dim < 0))
                    throw new ArgumentException("Invalid graph input: " + name);
                int size = 1;
                foreach (int dim in shape)
                    size = checked(size * dim);
                if (size != values.Length)
                    throw new ArgumentException("Input shape/data mismatch: " + name);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _session?.Dispose();
            foreach (var cached in _cpuSessions.Values) cached.Dispose();
            _cpuSessions.Clear();
            _interpreter?.Dispose();
            _express?.Dispose();
        }
    }
}
