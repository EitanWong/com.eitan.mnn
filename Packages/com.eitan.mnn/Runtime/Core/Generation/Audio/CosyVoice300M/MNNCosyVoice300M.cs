using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>First-generation CosyVoice-300M, built-in reference speaker, 22050 Hz mono.
    /// Uses official MNN APIs through the package's pinned platform ABI. No Python process is required.</summary>
    public sealed partial class MNNCosyVoice300M : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<int, MNNGenerationGraph> _metalEncoders = new Dictionary<int, MNNGenerationGraph>();
        private readonly Dictionary<int, MNNGenerationGraph> _metalVocoderCores = new Dictionary<int, MNNGenerationGraph>();
        private MNNGenerationGraph _text, _flowEncoder, _regulator, _flow, _f0, _hift;
        private MNNCosyVoice300MDecoder _decoder;
        private MNNCosyVoice300MTokenizer _tokenizer;
        private float[] _specialEmbedding, _speechEmbedding, _speakerEmbedding, _flowSpeaker, _sourceWeight, _sourceBias, _promptMel;
        private int[] _promptText, _promptSpeech;
        private string _flowPath, _modelRoot;
        private int _threads, _flowLength, _vocoderLength, _metalShapeCacheCapacity;
        private bool _disposed;
        public const int OutputSampleRate = 22050;
        public int SampleRate => OutputSampleRate;
        public MNNBackendType DecoderBackend => MNNBackendType.CPU;
        public MNNBackendType FlowBackend => _flow.Backend;
        public MNNBackendType VocoderBackend => _hift.Backend;
        public bool ExperimentalQuantizedDecoder { get; private set; }
        public MNNCosyVoice300MDecoderMode DecoderMode { get; private set; }
        public bool UsesCompactDecoder => _decoder != null && _decoder.UsesCompactGraph;
        public bool LastAcousticCpuRetry { get; private set; }
        public double LastTokenSeconds { get; private set; }
        public double LastFlowSeconds { get; private set; }
        public double LastVocoderSeconds { get; private set; }
        public double LastSynthesisSeconds { get; private set; }
        public double LastRealTimeFactor { get; private set; }
        public int LastSpeechTokens { get; private set; }

        /// <summary>Full prompt, FP16 weights and High computation by default.
        /// Quantized decode is explicit research opt-in; Python timings do not describe this managed runner.</summary>
        public static MNNCosyVoice300M Load(string directory, int threads = 4,
            MNNBackendType acousticBackend = MNNBackendType.Auto, bool experimentalQuantizedDecoder = false,
            MNNCosyVoice300MDecoderMode decoderMode = MNNCosyVoice300MDecoderMode.Float16, int metalShapeCacheCapacity = 4)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            if (metalShapeCacheCapacity < 1 || metalShapeCacheCapacity > 4) throw new ArgumentOutOfRangeException(nameof(metalShapeCacheCapacity));
            if (!Enum.IsDefined(typeof(MNNCosyVoice300MDecoderMode), decoderMode)) throw new ArgumentOutOfRangeException(nameof(decoderMode));
            if (experimentalQuantizedDecoder && decoderMode != MNNCosyVoice300MDecoderMode.Float16)
                throw new ArgumentException("Select one experimental decoder configuration.", nameof(decoderMode));
            MNNAcceleration.Validate(acousticBackend);
            if (!MNNCosyVoice300MModel.IsComplete(directory)) throw new InvalidDataException("Incomplete CosyVoice-300M export. Prepare it with Tools~/Models/CosyVoice300M/prepare_unity.py.");
            MNNPlatformSupport.RequireSupported();
            var model = new MNNCosyVoice300M {ExperimentalQuantizedDecoder = experimentalQuantizedDecoder, DecoderMode = decoderMode,
                _metalShapeCacheCapacity = metalShapeCacheCapacity};
            try { model.LoadCore(Path.GetFullPath(directory), threads, acousticBackend); return model; }
            catch { model.Dispose(); throw; }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                ClearMetalShapeCaches();
                _hift?.Dispose(); _f0?.Dispose(); _flow?.Dispose(); _regulator?.Dispose(); _flowEncoder?.Dispose(); _text?.Dispose(); _decoder?.Dispose();
                _decoder = null; _tokenizer = null; _promptText = _promptSpeech = null;
                _specialEmbedding = _speechEmbedding = _speakerEmbedding = _flowSpeaker = _sourceWeight = _sourceBias = _promptMel = null;
            }
        }
    }
}
