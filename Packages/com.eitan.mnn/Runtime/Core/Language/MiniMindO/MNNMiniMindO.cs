using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>MiniMind-o Thinker/Talker through the package's official MNN C++ Interpreter/Tensor bindings.
    /// Accepts completed turns, streams text and speech, and supports cancellation. The released model is not native full duplex.</summary>
    public sealed partial class MNNMiniMindO : IDisposable
    {
        public const string Repository = "EitanWang/MiniMindO-113M-MNN";
        public const string Revision = "a3f5375a295b201697da0f0dd86eb8e94be2dd29";
        public const string InstallationName = "MiniMindO-113M-MNN";
        public const string ConfigFileName = "mnn_minimind_o.json";
        private readonly object _gate = new object();
        private readonly string _directory;
        private readonly int _threads;
        private readonly MNNMiniMindOTokenizer _tokenizer;
        private MNNGenerationGraph _decode, _mimi;
        private bool _disposed, _generating;
        public bool SupportsAudioInput => HasGraph(_directory, "audio_encoder") && HasGraph(_directory, "multimodal_prefill");
        public bool SupportsImageInput => HasGraph(_directory, "vision_encoder") && HasGraph(_directory, "multimodal_prefill");
        public bool SupportsSpeechOutput => HasGraph(_directory, "mimi_decoder");

        private MNNMiniMindO(string directory, int threads)
        {
            _directory = directory; _threads = threads;
            _tokenizer = new MNNMiniMindOTokenizer(Path.Combine(directory, "tokenizer.json"));
        }

        /// <summary>Load an installed MiniMind-o package. CPU/Low precision/Low memory preserves the measured quantization recipe.</summary>
        public static MNNMiniMindO Load(string directory, int threads = 4)
        {
            if (directory == null) throw new ArgumentNullException(nameof(directory));
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            directory = Path.GetFullPath(directory);
            if (!File.Exists(Path.Combine(directory, ConfigFileName)) || !HasGraph(directory, "prefill") ||
                !HasGraph(directory, "decode") || !File.Exists(Path.Combine(directory, "tokenizer.json")))
                throw new FileNotFoundException("MiniMind-o requires its install manifest, tokenizer, prefill/decode graphs and external weights.");
            var config = MNNModelData.Read(Path.Combine(directory, ConfigFileName));
            if (MNNModelData.Integer(MNNModelData.Get(config, "schema_version")) != 1 ||
                !Equals(MNNModelData.Get(config, "model_type"), "minimind-o"))
                throw new InvalidDataException("Unsupported MiniMind-o export schema.");
            MNNPlatformSupport.RequireSupported();
            return new MNNMiniMindO(directory, threads);
        }

        /// <summary>Check all graphs required for text, image/audio input and speech output without loading native libraries.</summary>
        public static bool IsComplete(string directory) => directory != null &&
            File.Exists(Path.Combine(directory, ConfigFileName)) && File.Exists(Path.Combine(directory, "tokenizer.json")) &&
            new[] { "prefill", "decode", "multimodal_prefill", "audio_encoder", "vision_encoder", "mimi_decoder" }.All(name => HasGraph(directory, name));

        private static bool HasGraph(string directory, string name) => File.Exists(Path.Combine(directory, name + ".mnn")) &&
            File.Exists(Path.Combine(directory, name + ".mnn.weight"));

        private MNNGenerationGraph Graph(string name) => new MNNGenerationGraph(Path.Combine(_directory, name + ".mnn"), _threads,
            backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.Low, memoryMode: MNNMemoryMode.Low,
            externalWeightsPath: Path.Combine(_directory, name + ".mnn.weight"));

        /// <summary>Generate a text turn with the released temperature 0.7/top-p 0.85 and default voice.</summary>
        public MNNMiniMindOResult Generate(string prompt, int maxNewTokens = 128, int seed = 42,
            Action<MNNMiniMindOUpdate> onUpdate = null, CancellationToken cancellationToken = default) =>
            Generate(new MNNMiniMindORequest { Text = prompt }, new MNNMiniMindOOptions { MaxNewTokens = maxNewTokens, Seed = seed }, onUpdate, cancellationToken);

        /// <summary>Run one completed turn. At most 512 prompt plus generation tokens are allowed by this bounded-memory adapter.</summary>
        public MNNMiniMindOResult Generate(MNNMiniMindORequest request, MNNMiniMindOOptions options = null,
            Action<MNNMiniMindOUpdate> onUpdate = null, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var input = SnapshotRequest(request);
            var settings = (options ?? new MNNMiniMindOOptions()).Snapshot();
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNMiniMindO));
                if (_generating) throw new InvalidOperationException("MiniMind-o callbacks cannot reenter generation.");
                cancellationToken.ThrowIfCancellationRequested();
                _generating = true;
                try { return GenerateCore(input, settings, onUpdate, cancellationToken); }
                finally { _generating = false; }
            }
        }

        /// <summary>Apply the released SigLIP2 processor: Pillow-compatible bilinear resize and RGB normalization.</summary>
        public static float[] PrepareImage(MNNRgbImage image, CancellationToken cancellationToken = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            var resized = MNNImageSampling.ResizeBilinear(image, 256, 256, cancellationToken);
            return MNNImageSampling.Tensor(resized, 256, 256, 127.5f, 127.5f, token: cancellationToken);
        }

        private static MNNMiniMindORequest SnapshotRequest(MNNMiniMindORequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            return new MNNMiniMindORequest { Text = request.Text ?? throw new ArgumentNullException(nameof(request.Text)),
                TrimHistoryToFit = request.TrimHistoryToFit,
                History = request.History == null ? null : (MNNChatMessage[])request.History.Clone(),
                Audio16Khz = request.Audio16Khz == null ? null : (float[])request.Audio16Khz.Clone(),
                ImagePixels = request.ImagePixels == null ? null : (float[])request.ImagePixels.Clone() };
        }

        /// <summary>Run synchronous native calls on a worker; callbacks also run on that worker.</summary>
        public Task<MNNMiniMindOResult> GenerateAsync(MNNMiniMindORequest request, MNNMiniMindOOptions options = null,
            Action<MNNMiniMindOUpdate> onUpdate = null, CancellationToken cancellationToken = default)
        {
            var input = SnapshotRequest(request);
            var settings = (options ?? new MNNMiniMindOOptions()).Snapshot();
            return Task.Run(() => Generate(input, settings, onUpdate, cancellationToken), cancellationToken);
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                if (_generating) throw new InvalidOperationException("Dispose MiniMind-o after its callback returns.");
                _disposed = true;
                _decode?.Dispose(); _mimi?.Dispose();
                _decode = null; _mimi = null;
            }
        }
    }
}
