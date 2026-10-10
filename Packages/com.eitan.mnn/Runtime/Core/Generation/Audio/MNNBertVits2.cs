using System;
using System.IO;
using System.Linq;
using System.Threading;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Chinese Chenxi speech from MNN's Bert-VITS2 repository. Uses managed G2P and official MNN graph APIs.</summary>
    public sealed class MNNBertVits2 : IDisposable
    {
        private readonly object _gate = new object ();
        private readonly MNNBertVits2Text _text;
        private MNNGenerationGraph _bert;
        private byte[] _generatorGraph;
        private MNN.Unity.Interop.MNNInterop.ExpressExecutor _executor;
        private bool _disposed;
        private readonly int _threads;
        public int SampleRate { get; }

        /// <summary>Current stage backends; native unsupported operations may still run on CPU.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, MNNBackendType> StageBackends
        {
            get
            {
                lock (_gate)
                {
                    if (_disposed)
                        throw new ObjectDisposedException(nameof(MNNBertVits2));
                    return new System.Collections.Generic.Dictionary<string, MNNBackendType>{{"Bert", _bert.Backend}, {"Generator", _executor.Backend}, };
                }
            }
        }

        public static MNNBertVits2 Load(string directory, int threads = 1, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNBertVits2(Path.GetFullPath(directory), threads, backendType);
        }

        private MNNBertVits2(string directory, int threads, MNNBackendType backendType)
        {
            _threads = threads;
            var config = MNNModelData.Read(Path.Combine(directory, "config.json"));
            if ((string)MNNModelData.Get(config, "model_type") != "bertvits")
                throw new NotSupportedException("Expected MNN Bert-VITS2 config.");
            SampleRate = MNNModelData.Integer(MNNModelData.Get(config, "sample_rate"));
            if (SampleRate != 44100)
                throw new NotSupportedException("This Chenxi pipeline requires 44100 Hz.");
            string assets = Path.GetFullPath(Path.Combine(directory, (string)MNNModelData.Get(config, "asset_folder")));
            _text = new MNNBertVits2Text(Path.Combine(assets, "common", "text_processing_jsons"));
            try
            {
                _bert = new MNNGenerationGraph(Path.Combine(assets, "common", "mnn_models", "chinese_bert.mnn"), threads, backendType: backendType);
                _generatorGraph = File.ReadAllBytes(Path.Combine(directory, (string)MNNModelData.Get(config, "model_path")));
                // The bundled Chenxi control-flow generator returns no outputs on Metal.
                // Official Module::forward indexes output[0] without checking emptiness,
                // causing a native crash that managed retry cannot catch. Keep only this
                // generator on the validated CPU path; the BERT stage still tries Auto.
                var generatorBackend = MNNAcceleration.Resolve(backendType);
                if (generatorBackend != MNNBackendType.CPU)
                {
                    UnityEngine.Debug.Log("[MNN] Bert-VITS2 control-flow generator uses CPU for compatibility with the bundled MNN Module API.");
                    generatorBackend = MNNBackendType.CPU;
                }

                _executor = new MNN.Unity.Interop.MNNInterop.ExpressExecutor(threads, generatorBackend);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public MNNGeneratedAudio Synthesize(string text, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNBertVits2));
                cancellationToken.ThrowIfCancellationRequested();
                var data = _text.Encode(text);
                int n = data.Tokens.Length;
                var hidden = _bert.Run("hidden_states", new Input("input_ids", data.Tokens, 1, n), new Input("attention_mask", Enumerable.Repeat(1, n).ToArray(), 1, n), new Input("token_type_ids", new int[n], 1, n));
                if (hidden.Length != n * 1024)
                    throw new InvalidDataException("Unexpected Chinese BERT feature shape.");
                int p = data.Phones.Length;
                var features = new float[p * 1024];
                int cursor = 0;
                for (int token = 0; token < n; ++token)
                    for (int repeat = 0; repeat < data.Word2Phone[token]; ++repeat, ++cursor)
                        for (int dim = 0; dim < 1024; ++dim)
                            features[dim * p + cursor] = hidden[token * 1024 + dim];
                cancellationToken.ThrowIfCancellationRequested();
                var specialized = MNNGraphConstant.FreezeInputs(_generatorGraph, new Input("tone", data.Tones, 1, p), new Input("lang_id", new int[p], 1, p), new Input("cn_bert", features, 1, 1024, p), new Input("en_bert", new float[p * 1024], 1, 1024, p));
                float[] samples;
                try
                {
                    samples = RunGenerator(specialized, data.Phones);
                }
                catch (Exception error)when (_executor.Backend != MNNBackendType.CPU && MNNAcceleration.CanRetryOnCpu(error))
                {
                    UnityEngine.Debug.Log($"[MNN] Bert-VITS2 generator: {_executor.Backend} failed; retrying on CPU. {error.Message}");
                    _executor.Dispose();
                    _executor = new MNN.Unity.Interop.MNNInterop.ExpressExecutor(_threads, MNNBackendType.CPU);
                    cancellationToken.ThrowIfCancellationRequested();
                    samples = RunGenerator(specialized, data.Phones);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return new MNNGeneratedAudio(samples, SampleRate, text);
            }
        }

        private float[] RunGenerator(byte[] specialized, int[] phones)
        {
            int p = phones.Length;
            float[] samples;
            using (_executor.Enter())
            {
                var pin = System.Runtime.InteropServices.GCHandle.Alloc(phones, System.Runtime.InteropServices.GCHandleType.Pinned);
                MNN.Unity.Interop.MNNInterop.CppShared input = default;
                try
                {
                    input = MNN.Unity.Interop.MNNInterop.Constant(pin.AddrOfPinnedObject(), new[]{1, p}, 2, new MNN.Unity.Interop.MNNInterop.HalideType{Code = 0, Bits = 32, Lanes = 1});
                    samples = MNN.Unity.Interop.MNNInterop.RunSingleInputModule(specialized, "phone", "audio", input);
                }
                finally
                {
                    MNN.Unity.Interop.MNNInterop.ReleaseShared(ref input);
                    pin.Free();
                }
            }

            if (samples.Length < SampleRate / 10 || samples.Length > SampleRate * 60)
                throw new InvalidDataException("Invalid Bert-VITS2 waveform length.");
            for (int i = 0; i < samples.Length; ++i)
            {
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]))
                    throw new InvalidDataException("Non-finite Bert-VITS2 waveform.");
                samples[i] = Math.Max(-1, Math.Min(1, samples[i]));
            }

            return samples;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _executor?.Dispose();
                _executor = null;
                _generatorGraph = null;
                _bert?.Dispose();
                _bert = null;
            }
        }
    }
}
