using System;
using System.IO;

namespace MNN.Unity
{
    // One runner owns one fixed native session. Both offline and real-time VAD
    // use the same context/state interface and backend/precision policy.
    internal sealed class MNNSileroFrameRunner : IDisposable
    {
        private readonly MNNSingleInputModule _legacy;
        private readonly MNNInterpreter _interpreter;
        private readonly MNNSession _session;
        private readonly MNNTensor _audioInput, _stateInput, _probabilityOutput, _stateOutput;
        private readonly float[] _audio = new float[576], _state = new float[256];
        private bool _cachePending;
        internal MNNBackendType Backend => _legacy?.Backend ?? _session.ActualBackend;

        internal MNNSileroFrameRunner(string directory, int threads, MNNBackendType backend)
        {
            var config = MNNSileroVadConfig.Read(directory);
            if (backend == MNNBackendType.NN) throw new NotSupportedException("Silero NNAPI graph/device compatibility has not been verified.");
            backend = backend == MNNBackendType.Auto ? MNNBackendType.CPU : MNNAcceleration.Resolve(backend);
            if (!MNNSileroModel.IsOfficialProfile(config))
            {
                if (!MNNSileroModel.IsRepository(config.repository)) throw new InvalidDataException("Unknown Silero VAD profile.");
                if (backend != MNNBackendType.CPU) throw new NotSupportedException("Legacy quantized Silero GPU execution failed source validation. Install the official FP32 profile.");
                MNNSileroModel.Verify(directory);
                string legacyPath = Path.Combine(directory, MNNSileroModel.Graph);
                _legacy = new MNNSingleInputModule(legacyPath, MNNSileroGraph.Input, MNNSileroGraph.Output,
                    threads, backend, MNNSileroGraph.Prepare(File.ReadAllBytes(legacyPath)));
                return;
            }
            MNNSileroModel.VerifyOfficial(directory, config);
            string path = Path.Combine(directory, MNNSileroModel.OfficialGraph);
            _interpreter = MNNInterpreter.CreateFromFile(path);
            try
            {
                if (backend != MNNBackendType.CPU)
                {
                    _interpreter.SetCacheFile(MNNInferenceCache.PathFor(path, backend, MNNPrecisionMode.High));
                    _cachePending = true;
                }
                _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = backend, ThreadCount = threads, PrecisionMode = MNNPrecisionMode.High});
                _audioInput = _session.GetInput("input");
                _stateInput = _session.GetInput("state");
                _probabilityOutput = _session.GetOutput("output");
                _stateOutput = _session.GetOutput("stateN");
                CheckTensor(_audioInput, new[]{1,576});
                CheckTensor(_stateInput, new[]{2,1,128});
                CheckTensor(_probabilityOutput, new[]{1,1});
                CheckTensor(_stateOutput, new[]{2,1,128});
            }
            catch
            {
                _interpreter.Dispose();
                throw;
            }
        }

        internal void Run(float[] packed, float[] output)
        {
            if (_legacy != null)
            {
                var result = _legacy.Run(packed, 832);
                if (result.Length != output.Length) throw new InvalidDataException("Unexpected legacy Silero output.");
                Array.Copy(result, output, result.Length);
                return;
            }
            Array.Copy(packed, 0, _audio, 0, _audio.Length);
            Array.Copy(packed, _audio.Length, _state, 0, _state.Length);
            _audioInput.CopyFromArray(_audio);
            _stateInput.CopyFromArray(_state);
            _session.Run();
            CopyOutput(_probabilityOutput, output, 0, 1);
            CopyOutput(_stateOutput, output, 1, 256);
            if (!_cachePending) return;
            _cachePending = false;
            try { _session.UpdateCache(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is MNNException)
            { UnityEngine.Debug.Log("[MNN] Silero cache update unavailable: " + error.Message); }
        }

        private static void CopyOutput(MNNTensor tensor, float[] output, int offset, int count)
        {
            var values = tensor.MapForRead<float>();
            try
            {
                if (values.Length != count) throw new InvalidDataException("Unexpected Silero output shape.");
                for (int i = 0; i < count; ++i)
                {
                    float value = values[i];
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Silero returned non-finite probability/state.");
                    output[offset + i] = value;
                }
            }
            finally { tensor.Unmap(); }
        }

        private static void CheckTensor(MNNTensor tensor, int[] shape)
        {
            if (tensor.DataType != MNNDataType.Float) throw new InvalidDataException("Unexpected Silero tensor type.");
            var actual = tensor.Shape;
            if (actual.Length != shape.Length) throw new InvalidDataException("Unexpected Silero tensor rank.");
            for (int i = 0; i < shape.Length; ++i)
                if (actual[i] != shape[i]) throw new InvalidDataException("Unexpected Silero tensor shape.");
        }

        public void Dispose()
        {
            _legacy?.Dispose();
            _interpreter?.Dispose();
        }
    }
}
