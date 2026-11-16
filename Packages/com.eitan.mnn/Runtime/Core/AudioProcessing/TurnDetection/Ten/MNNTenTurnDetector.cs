using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>TEN 7B transcript classification. Only finished ends a turn; wait never does.</summary>
    public sealed class MNNTenTurnDetector : IDisposable
    {
        public const string ConfigFileName = "mnn_ten_turn.json";
        [Serializable] private sealed class Config { public string source_repository, source_revision; }
        private readonly MNNLlm _model;
        public MNNBackendType Backend => _model.Backend;
        private MNNTenTurnDetector(string directory, int threads)
        {
            Verify(directory);
            _model = MNNLlm.Load(directory, threads, precisionMode: MNNPrecisionMode.High, backendType: MNNBackendType.CPU);
        }
        public static MNNTenTurnDetector Load(string directory, int threadCount = 2, MNNBackendType backendType = MNNBackendType.CPU)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (backendType != MNNBackendType.CPU && backendType != MNNBackendType.Auto)
                throw new NotSupportedException("TEN currently qualifies CPU inference only.");
            return new MNNTenTurnDetector(Path.GetFullPath(directory), threadCount);
        }
        internal static void Verify(string directory)
        {
            var c = JsonUtility.FromJson<Config>(File.ReadAllText(Path.Combine(directory, ConfigFileName)));
            if (c == null || c.source_repository != "TEN-framework/TEN_Turn_Detection" || c.source_revision != "41845390a898882c883330455ed1df57635fd33f")
                throw new InvalidDataException("Unsupported TEN turn export.");
            var files = MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "files.sha256.json")));
            if (new[] { "llm.mnn", "llm.mnn.weight", "embeddings_bf16.bin", "tokenizer.mtok", "llm_config.json", "config.json" }.Any(n => !files.ContainsKey(n)))
                throw new InvalidDataException("Incomplete TEN export manifest.");
            foreach (var pair in files)
            {
                if (Path.GetFileName(pair.Key) != pair.Key) throw new InvalidDataException("Expected sibling TEN assets.");
                string path = Path.Combine(directory, pair.Key);
                long bytes = Convert.ToInt64(MNNModelData.Get(pair.Value, "bytes"));
                if (!File.Exists(path) || new FileInfo(path).Length != bytes ||
                    MNNModelDeliveryManifest.ComputeSha256(path) != (string)MNNModelData.Get(pair.Value, "sha256"))
                    throw new InvalidDataException("TEN asset checksum mismatch: " + pair.Key);
            }
        }
        internal static bool IsComplete(string directory)
        {
            try { Verify(directory); return true; }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException) { return false; }
        }
        public string Predict(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Supply a transcript.", nameof(text));
            var result = _model.GenerateConversation(new[] { new MNNChatMessage(MNNChatRole.System, ""),
                new MNNChatMessage(MNNChatRole.User, text) }, maxNewTokens: 1, cancellationToken: cancellationToken);
            string label = result.Text.Trim();
            if (label != "finished" && label != "unfinished" && label != "wait")
                throw new InvalidDataException("TEN returned an unexpected class: " + label);
            return label;
        }
        public bool IsEndOfTurn(string text, CancellationToken cancellationToken = default) => Predict(text, cancellationToken) == "finished";
        public void Dispose() => _model.Dispose();
    }
}
