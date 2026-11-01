using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;
using Output = MNN.Unity.MNNGenerationGraph.Output;

namespace MNN.Unity
{
    /// <summary>English Pocket-TTS using MNN for reference encoding, generation and decoding. CPU, macOS Mono ABI.</summary>
    public sealed partial class MNNPocketTts : IDisposable
    {
        private readonly object _gate = new object ();
        private readonly string _root;
        private readonly int _threads;
        private readonly MNNPrecisionMode _mainPrecision;
        private readonly MNNPocketText _text;
        private readonly float[] _bos;
        private readonly List<object> _flowManifest, _mimiManifest;
        private readonly string[] _cacheNames = Enumerable.Range(0, 6).Select(i => "out_state_" + 3 * i).ToArray();
        private MNNExpressGraph _encoder, _priming, _main;
        private MNNGenerationGraph _conditioner, _flow, _decoder;
        private List<Input> _voiceState;
        private string _voiceKey;
        private int _voiceSteps;
        private bool _synthesizing;
        private bool _disposed;
        public int SampleRate => 24000;
        internal static bool IsComplete(string directory) => !string.IsNullOrWhiteSpace(directory) && MNNVerifiedTtsModel.PocketFiles().Where(file => file.Path != "README.md" && file.Path != "LICENSE" && file.Path != "reference_sample.wav").All(file => File.Exists(Path.Combine(directory, file.Path)) && new FileInfo(Path.Combine(directory, file.Path)).Length == file.Bytes);
        public static MNNPocketTts Load(string directory, int threads = 4) => Load(directory, threads, MNNPrecisionMode.Normal);
        /// <summary>
        /// Select Normal or opt-in Low precision for the autoregressive main graph only.
        /// Low may change generated speech; qualify content, voice quality and throughput on the target device.
        /// Other stages retain Normal precision. Neither option guarantees realtime delivery.
        /// </summary>
        public static MNNPocketTts Load(string directory, int threads, MNNPrecisionMode mainPrecision)
        {
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            if (mainPrecision != MNNPrecisionMode.Normal && mainPrecision != MNNPrecisionMode.Low)
                throw new ArgumentOutOfRangeException(nameof(mainPrecision), "Pocket main supports Normal or Low precision.");
            MNNPlatformSupport.RequireSupported();
            if (!IsComplete(directory))
                throw new InvalidDataException("Missing pinned Pocket-TTS MNN graphs or frontend files.");
            return new MNNPocketTts(Path.GetFullPath(directory), threads, mainPrecision);
        }

        private MNNPocketTts(string root, int threads, MNNPrecisionMode mainPrecision)
        {
            _root = root;
            _threads = threads;
            _mainPrecision = mainPrecision;
            // Graph adaptations are specific to this immutable export. Reject
            // changed graphs before preparing or executing any native stage.
            foreach (var file in MNNVerifiedTtsModel.PocketFiles().Where(file => file.Path != "README.md" && file.Path != "LICENSE" && file.Path != "reference_sample.wav"))
                using (var hash = SHA256.Create())
                using (var stream = File.OpenRead(Path.Combine(root, file.Path)))
                    if (BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != file.Hash)
                        throw new InvalidDataException("Pocket-TTS asset differs from the pinned export: " + file.Path);
            _text = new MNNPocketText(Path.Combine(root, "tokenizer.model"));
            var metadata = MNNModelData.Read(Path.Combine(root, "bundle.json"));
            _flowManifest = MNNModelData.Array(MNNModelData.Get(metadata, "flow_lm_state_manifest"));
            _mimiManifest = MNNModelData.Array(MNNModelData.Get(metadata, "mimi_state_manifest"));
            _bos = ReadBos(Path.Combine(root, "bos_before_voice.npy"));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_synthesizing)
                    throw new InvalidOperationException("Pocket-TTS cannot be disposed from its active synthesis callback.");
                if (_disposed)
                    return;
                _disposed = true;
                ReleaseGraphs();
                _voiceState = null;
                _voiceKey = null;
            }
        }
    }
}
