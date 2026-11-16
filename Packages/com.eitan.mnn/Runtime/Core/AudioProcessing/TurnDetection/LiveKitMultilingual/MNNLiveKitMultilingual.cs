using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>LiveKit v0.4.1 multilingual transcript detector with published language thresholds.</summary>
    public sealed class MNNLiveKitMultilingual : IDisposable
    {
        public const string ConfigFileName = "mnn_livekit_multilingual.json";
        [Serializable] private sealed class Config
        {
            public string source_repository, source_revision, graph_sha256;
            public long graph_bytes;
        }
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNLiveKitTokenizer _tokenizer;
        private readonly Dictionary<string, float> _thresholds;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;
        private MNNLiveKitMultilingual(string directory, int threads)
        {
            Verify(directory);
            _tokenizer = new MNNLiveKitTokenizer(directory, qwen: true);
            _thresholds = MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "languages.json")))
                .ToDictionary(p => p.Key, p => Convert.ToSingle(MNNModelData.Get(p.Value, "threshold")), StringComparer.OrdinalIgnoreCase);
            _graph = new MNNGenerationGraph(Path.Combine(directory, "model.mnn"), threads,
                backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High);
        }
        public static MNNLiveKitMultilingual Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.CPU)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (backendType != MNNBackendType.CPU && backendType != MNNBackendType.Auto)
                throw new NotSupportedException("LiveKit multilingual currently qualifies CPU inference only.");
            return new MNNLiveKitMultilingual(Path.GetFullPath(directory), threadCount);
        }
        internal static void Verify(string directory)
        {
            var c = JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(directory, ConfigFileName)));
            string graph = Path.Combine(directory, "model.mnn");
            if (c == null || c.source_repository != "livekit/turn-detector" || c.source_revision != "87e35fcb1e60a569bea70346191c4886ea92e281" ||
                !File.Exists(graph) || new FileInfo(graph).Length != c.graph_bytes || MNNModelDeliveryManifest.ComputeSha256(graph) != c.graph_sha256 ||
                new[] { "vocab.json", "merges.txt", "added_tokens.json", "languages.json" }.Any(n => !File.Exists(Path.Combine(directory, n))))
                throw new InvalidDataException("LiveKit multilingual export failed verification.");
        }
        internal static bool IsComplete(string directory)
        {
            try { Verify(directory); return true; }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return false; }
        }
        public float Threshold(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) throw new ArgumentNullException(nameof(language));
            string key = language.Split('-', '_')[0];
            if (!_thresholds.TryGetValue(key, out float threshold)) throw new NotSupportedException("No published LiveKit threshold for " + language);
            return threshold;
        }
        public float Predict(string text, CancellationToken cancellationToken = default) =>
            Predict(new[] { new MNNLiveKitTurnMessage("user", text) }, cancellationToken);
        public float Predict(IReadOnlyList<MNNLiveKitTurnMessage> messages, CancellationToken cancellationToken = default)
        {
            if (messages == null || messages.Count == 0) throw new ArgumentException("Supply transcript messages.", nameof(messages));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNLiveKitMultilingual));
                cancellationToken.ThrowIfCancellationRequested();
                var ids = Tokenize(messages);
                var padded = new int[128]; Array.Copy(ids, padded, ids.Length);
                var output = _graph.Run("prob", new MNNGenerationGraph.Input("input_ids", padded, 1, 128),
                    new MNNGenerationGraph.Input("last_token_index", new[] { ids.Length - 1 }, 1));
                cancellationToken.ThrowIfCancellationRequested();
                if (output.Length != 1 || float.IsNaN(output[0]) || output[0] < 0 || output[0] > 1)
                    throw new InvalidDataException("Invalid LiveKit completion probability.");
                return output[0];
            }
        }
        internal int[] Tokenize(IReadOnlyList<MNNLiveKitTurnMessage> messages)
        {
            int[] ids = _tokenizer.Encode(MNNLiveKitTurnDetector.FormatChatContext(messages, qwen: true));
            if (ids.Length == 0) throw new ArgumentException("Transcript must contain text.", nameof(messages));
            return ids.Skip(Math.Max(0, ids.Length - 128)).ToArray();
        }
        public bool IsEndOfTurn(string text, string language = "en", CancellationToken cancellationToken = default)
        {
            float threshold = Threshold(language);
            return Predict(text, cancellationToken) >= threshold;
        }
        public void Dispose()
        {
            lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); }
        }
    }
}
