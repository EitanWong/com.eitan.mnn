using System;
using System.IO;

namespace MNN.Unity
{
    // CPU retains the source's sensitive normalization/reduction order.
    // The optional split graph runs recurrent/decoder work on the selected GPU.
    internal sealed class MNNDtlnAecStage : IDisposable
    {
        private readonly MNNInterpreter _coreOwner, _frontOwner;
        private readonly MNNSession _core, _front;
        private readonly MNNTensor _first, _second, _stateInput, _stateOutput;
        private readonly MNNTensor[] _frontOutputs, _coreFeatures;
        private readonly float[][] _features;
        private bool _cachePending;
        internal MNNTensor Signal { get; }
        internal MNNBackendType Backend => _core.ActualBackend;

        internal MNNDtlnAecStage(string directory, int units, int stage, int threads, MNNBackendType backend)
        {
            bool split = backend != MNNBackendType.CPU;
            string path = Path.Combine(directory, split ? MNNDtlnAecModel.GpuCore(units, stage) : MNNDtlnAecModel.Graph(units, stage));
            _coreOwner = MNNInterpreter.CreateFromFile(path);
            try
            {
                if (split)
                {
                    _coreOwner.SetCacheFile(Path.ChangeExtension(MNNInferenceCache.PathFor(path, backend, MNNPrecisionMode.High), "None.cache"));
                    _cachePending = true;
                }
                _core = _coreOwner.CreateSession(new MNNSessionConfig { BackendType = backend, ThreadCount = threads, PrecisionMode = MNNPrecisionMode.High, GpuTuningMode = MNNGpuTuningMode.None });
                if (split)
                {
                    _frontOwner = MNNInterpreter.CreateFromFile(Path.Combine(directory, MNNDtlnAecModel.GpuFront(units, stage)));
                    _front = _frontOwner.CreateSession(new MNNSessionConfig { BackendType = MNNBackendType.CPU, ThreadCount = threads, PrecisionMode = MNNPrecisionMode.High });
                    int count = stage == 1 ? 1 : 2;
                    _frontOutputs = new MNNTensor[count]; _coreFeatures = new MNNTensor[count]; _features = new float[count][];
                    for (int i = 0; i < count; ++i)
                    {
                        int width = stage == 1 ? 514 : (i == 0 ? 512 : 1024);
                        _frontOutputs[i] = _front.GetOutput("front_" + i);
                        _coreFeatures[i] = _core.GetInput("front_" + i);
                        CheckTensor(_frontOutputs[i], new[] { 1, 1, width });
                        CheckTensor(_coreFeatures[i], new[] { 1, 1, width });
                        _features[i] = new float[width];
                    }
                }
                var audioSession = _front ?? _core;
                _first = audioSession.GetInput("input_" + (stage == 1 ? 3 : 6));
                _second = audioSession.GetInput("input_" + (stage == 1 ? 4 : 7));
                _stateInput = _core.GetInput("input_" + (stage == 1 ? 5 : 8));
                Signal = _core.GetOutput("Identity");
                _stateOutput = _core.GetOutput("Identity_1");
                int signalWidth = stage == 1 ? 257 : 512;
                CheckTensor(_first, new[] { 1, 1, signalWidth });
                CheckTensor(_second, new[] { 1, 1, signalWidth });
                CheckTensor(Signal, new[] { 1, 1, signalWidth });
                CheckTensor(_stateInput, new[] { 1, 2, units, 2 });
                CheckTensor(_stateOutput, new[] { 1, 2, units, 2 });
            }
            catch { Dispose(); throw; }
        }

        internal void Run(float[] first, float[] second, float[] state)
        {
            _first.CopyFromArray(first); _second.CopyFromArray(second);
            if (_front != null)
            {
                _front.Run();
                for (int i = 0; i < _features.Length; ++i)
                {
                    Copy(_frontOutputs[i], _features[i]);
                    _coreFeatures[i].CopyFromArray(_features[i]);
                }
            }
            _stateInput.CopyFromArray(state);
            _core.Run();
            Copy(_stateOutput, state);
            if (!_cachePending) return;
            _cachePending = false;
            try { _core.UpdateCache(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is MNNException)
            { UnityEngine.Debug.Log("[MNN] DTLN AEC cache update unavailable: " + error.Message); }
        }
        private static void Copy(MNNTensor source, float[] target)
        {
            var mapped = source.MapForRead<float>();
            try { mapped.CopyTo(target); }
            finally { source.Unmap(); }
        }
        private static void CheckTensor(MNNTensor tensor, int[] shape)
        {
            if (tensor.DataType != MNNDataType.Float || tensor.Shape.Length != shape.Length)
                throw new InvalidDataException("Unexpected DTLN AEC tensor interface.");
            for (int i = 0; i < shape.Length; ++i)
                if (tensor.Shape[i] != shape[i]) throw new InvalidDataException("Unexpected DTLN AEC tensor shape.");
        }
        public void Dispose()
        {
            _frontOwner?.Dispose();
            _coreOwner?.Dispose();
        }
    }
}
