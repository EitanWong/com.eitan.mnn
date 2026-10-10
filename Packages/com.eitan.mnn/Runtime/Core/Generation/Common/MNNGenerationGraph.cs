using System;
using System.IO;
using System.Linq;

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
        private bool _disposed;
        private bool _cacheEnabled;
        private bool _cacheSaved;
        private readonly bool _prepared;
        internal MNNBackendType Backend => _express != null ? _express.Backend : _session.ActualBackend;
        internal MNNGenerationGraph(string path, int threads, byte[] preparedGraph = null, bool express = false, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < 16)
                throw new FileNotFoundException("Missing MNN graph: " + path);
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

            _interpreter = preparedGraph == null ? MNNInterpreter.CreateFromFile(path) : MNNInterpreter.CreateFromBuffer(preparedGraph);
            try
            {
                if (preparedGraph != null)
                    _interpreter.SetExternalFile(path + ".weight");
                ConfigureCache(MNNAcceleration.Resolve(backendType), MNNPrecisionMode.Normal);
                _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = backendType, ThreadCount = threads, PrecisionMode = MNNPrecisionMode.Normal});
            }
            catch
            {
                _interpreter.Dispose();
                throw;
            }
        }

        internal float[] Run(string output, params Input[] inputs)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNGenerationGraph));
            try
            {
                return RunCore(output, inputs);
            }
            catch (Exception error)when (Backend != MNNBackendType.CPU && MNNAcceleration.CanRetryOnCpu(error))
            {
                if (_express == null && Backend == MNNBackendType.Metal && error is InvalidDataException && _session.Config.PrecisionMode != MNNPrecisionMode.High)
                {
                    UnityEngine.Debug.Log($"[MNN] {_path}: Metal output invalid; retrying GPU with High (FP32) precision.");
                    try
                    {
                        _session.Dispose();
                        ConfigureCache(MNNBackendType.Metal, MNNPrecisionMode.High);
                        _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.Metal, ThreadCount = _threads, PrecisionMode = MNNPrecisionMode.High});
                        return RunCore(output, inputs);
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
                    _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.CPU, ThreadCount = _threads});
                }

                return RunCore(output, inputs);
            }
        }

        private void ConfigureCache(MNNBackendType backend, MNNPrecisionMode precision)
        {
            _cacheSaved = false;
            _cacheEnabled = false;
            if (_prepared || backend == MNNBackendType.CPU)
                return;
            try
            {
                _interpreter.SetCacheFile(MNNInferenceCache.PathFor(_path, backend, precision));
                _cacheEnabled = true;
            }
            catch (Exception error)when (error is IOException || error is UnauthorizedAccessException || error is MNNException)
            {
                UnityEngine.Debug.Log("[MNN] GPU cache unavailable: " + error.Message);
            }
        }

        private float[] RunCore(string output, Input[] inputs)
        {
            if (_express != null)
                return Validate(_express.Run(output, inputs), output);
            var shapes = inputs.Where(input => !_session.GetInput(input.Name).Shape.SequenceEqual(input.Shape)).ToDictionary(input => input.Name, input => input.Shape);
            if (shapes.Count > 0)
            {
                _session.ResizeInputs(shapes, true);
                _cacheSaved = false;
            }

            foreach (var input in inputs)
            {
                var tensor = _session.GetInput(input.Name);
                if (input.Values.Length == 0)
                    continue;
                if (input.Values is int[] ints)
                {
                    if (tensor.DataType == MNNDataType.Int64)
                        tensor.CopyFromArray(ints.Select(value => (long)value).ToArray());
                    else
                        tensor.CopyFromArray(ints);
                }
                else
                    tensor.CopyFromArray((float[])input.Values);
            }

            _session.Run();
            var values = _session.GetOutput(output).CopyToArray<float>();
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

            return Validate(values, output);
        }

        private static float[] Validate(float[] values, string output)
        {
            if (values.Length == 0 || values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidDataException("Generation stage returned an empty or non-finite tensor: " + output);
            return values;
        }

        internal sealed class Input
        {
            internal readonly string Name;
            internal readonly int[] Shape;
            internal readonly Array Values;
            internal Input(string name, Array values, params int[] shape)
            {
                Name = name;
                Values = values;
                Shape = shape;
                if (values == null || shape == null || shape.Length == 0 || shape.Any(dim => dim < 0))
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
            _interpreter?.Dispose();
            _express?.Dispose();
        }
    }
}
