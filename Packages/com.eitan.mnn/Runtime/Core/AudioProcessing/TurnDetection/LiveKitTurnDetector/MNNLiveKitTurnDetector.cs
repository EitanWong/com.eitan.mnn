using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>LiveKit transcript-semantic end-of-turn detector.</summary>
    public sealed class MNNLiveKitTurnDetector : IDisposable
    {
        private const int MaxHistoryTokens = 128, MaxHistoryTurns = 6;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private readonly MNNLiveKitTokenizer _tokenizer;
        private readonly bool _fixedContext;
        private bool _disposed;

        private MNNLiveKitTurnDetector(string directory, int threads, MNNBackendType backend)
        {
            backend = MNNAcceleration.Resolve(MNNAcceleration.ForDynamicGraph(backend));
            MNNLiveKitTurnDetectorModel.Verify(directory);
            _tokenizer = new MNNLiveKitTokenizer(directory);
            string graph = MNNLiveKitTurnDetectorModel.PreferredGraph(backend);
            _fixedContext = graph == MNNLiveKitTurnDetectorModel.GraphFp16;
            _graph = new MNNGenerationGraph(Path.Combine(directory, graph), threads, backendType: backend,
                precisionMode: MNNPrecisionMode.High);
        }

        public static MNNLiveKitTurnDetector Load(string directory, int threadCount = 2, MNNBackendType backendType = MNNBackendType.CPU)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNLiveKitTurnDetector(Path.GetFullPath(directory), threadCount, backendType);
        }

        public MNNBackendType Backend => _graph.Backend;

        public float Predict(string currentText, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            return Predict(new[]{new MNNLiveKitTurnMessage("user", currentText ?? string.Empty)}, threshold, cancellationToken);
        }

        public float Predict(IReadOnlyList<MNNLiveKitTurnMessage> messages, float threshold = .5f, CancellationToken cancellationToken = default)
        {
            if (messages == null || messages.Count == 0) throw new ArgumentException("Supply at least one transcript message.", nameof(messages));
            if (threshold < 0 || threshold > 1 || float.IsNaN(threshold)) throw new ArgumentOutOfRangeException(nameof(threshold));
            lock (_gate)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                string prompt = FormatChatContext(messages);
                int[] ids = _tokenizer.Encode(prompt);
                if (ids.Length == 0) throw new InvalidDataException("LiveKit tokenizer returned an empty prompt.");
                if (ids.Length > MaxHistoryTokens)
                    ids = ids.Skip(ids.Length - MaxHistoryTokens).ToArray();
                float[] output;
                if (_fixedContext)
                {
                    var padded = new int[MaxHistoryTokens];
                    Array.Copy(ids, padded, ids.Length);
                    output = _graph.Run("prob", new MNNGenerationGraph.Input("input_ids", padded, 1, MaxHistoryTokens),
                        new MNNGenerationGraph.Input("last_token_index", new[]{ids.Length - 1}, 1));
                }
                else
                    output = _graph.Run("prob", new MNNGenerationGraph.Input("input_ids", ids, 1, ids.Length));
                if (output.Length == 0 || output[0] < 0 || output[0] > 1 || float.IsNaN(output[0]))
                    throw new InvalidDataException("LiveKit turn detector returned an invalid probability.");
                return output[0];
            }
        }

        public bool IsEndOfTurn(string currentText, float threshold = .5f, CancellationToken cancellationToken = default) =>
            Predict(currentText, threshold, cancellationToken) >= threshold;

        internal static string FormatChatContext(IReadOnlyList<MNNLiveKitTurnMessage> messages)
        {
            var normalized = new List<MNNLiveKitTurnMessage>();
            for (int i = Math.Max(0, messages.Count - MaxHistoryTurns); i < messages.Count; ++i)
            {
                var message = messages[i];
                if (message == null || string.IsNullOrEmpty(message.Content)) continue;
                string role = string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                string content = Normalize(message.Content);
                if (normalized.Count > 0 && normalized[normalized.Count - 1].Role == role)
                    normalized[normalized.Count - 1] = new MNNLiveKitTurnMessage(role, normalized[normalized.Count - 1].Content + " " + content);
                else
                    normalized.Add(new MNNLiveKitTurnMessage(role, content));
            }
            var builder = new StringBuilder();
            foreach (var message in normalized)
                builder.Append("<|im_start|><|").Append(message.Role).Append("|>").Append(message.Content).Append("<|im_end|>");
            int end = builder.ToString().LastIndexOf("<|im_end|>", StringComparison.Ordinal);
            return end >= 0 ? builder.ToString(0, end) : builder.ToString();
        }

        private static string Normalize(string value)
        {
            value = Lowercase(value).Normalize(NormalizationForm.FormKC);
            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsPunctuation(c) && c != '\'' && c != '-') continue;
                builder.Append(c);
            }
            return System.Text.RegularExpressions.Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
        }

        private static string Lowercase(string text)
        {
            // Python uses Unicode full lowercasing. Invariant .NET lowercasing
            // omits the dotted-I expansion and the contextual final sigma.
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; ++i)
            {
                char c = text[i];
                if (c == '\u0130') result.Append("i\u0307");
                else if (c == '\u212a') result.Append('k');
                else if (c == '\u212b') result.Append('\u00e5');
                else if (c == '\u2126') result.Append('\u03c9');
                else if (c == '\u03a3')
                    result.Append(HasCasedLetter(text, i, -1) && !HasCasedLetter(text, i, 1) ? '\u03c2' : '\u03c3');
                else result.Append(c);
            }
            return result.ToString().ToLowerInvariant();
        }

        private static bool HasCasedLetter(string text, int start, int step)
        {
            for (int i = start + step; i >= 0 && i < text.Length; i += step)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(text, i);
                // Unicode Case_Ignorable characters do not end a word for sigma.
                if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.EnclosingMark ||
                    category == UnicodeCategory.Format || category == UnicodeCategory.ModifierLetter ||
                    category == UnicodeCategory.ModifierSymbol || text[i] == '\'' || text[i] == '\u2019') continue;
                return char.ToLowerInvariant(text[i]) != char.ToUpperInvariant(text[i]);
            }
            return false;
        }

        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(MNNLiveKitTurnDetector)); }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _graph.Dispose(); } }
    }
}
