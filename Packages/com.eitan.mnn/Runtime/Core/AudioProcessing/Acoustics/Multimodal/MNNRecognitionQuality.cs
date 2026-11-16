using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Measured error counts for one exact pipeline configuration and pinned evaluation corpus.
    /// ConfigurationId must identify model revisions, preprocessing, toggles and thresholds together.
    /// Conversion tensor agreement is not recognition error-rate evidence.</summary>
    public sealed class MNNRecognitionQuality
    {
        public string ConfigurationId { get; }
        public string CorpusId { get; }
        public string DeviceId { get; }
        public string Language { get; }
        public string Metric { get; }
        public long Errors { get; }
        public long ReferenceUnits { get; }
        public double ErrorRate => (double)Errors / ReferenceUnits;
        public double P95LatencyMilliseconds { get; }
        public bool NativeInferenceVerified { get; }

        public MNNRecognitionQuality(string configurationId, string corpusId, string deviceId, string language,
            string metric, long errors, long referenceUnits, double p95LatencyMilliseconds, bool nativeInferenceVerified)
        {
            if (string.IsNullOrWhiteSpace(configurationId) || string.IsNullOrWhiteSpace(corpusId) ||
                string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(language) || metric != "CER" && metric != "WER")
                throw new ArgumentException("Supply exact configuration, corpus, device, language and CER/WER identifiers.");
            if (errors < 0 || referenceUnits <= 0) throw new ArgumentOutOfRangeException(nameof(referenceUnits));
            MNNPresenceOptions.RequireTime(p95LatencyMilliseconds, nameof(p95LatencyMilliseconds));
            ConfigurationId = configurationId; CorpusId = corpusId; DeviceId = deviceId; Language = language; Metric = metric;
            Errors = errors; ReferenceUnits = referenceUnits; P95LatencyMilliseconds = p95LatencyMilliseconds;
            NativeInferenceVerified = nativeInferenceVerified;
        }

        /// <summary>Returns the lowest measured error rate among comparable native runs within the latency budget.
        /// A tie prefers lower latency, then stable configuration ID. Null means no evidence-backed default exists.</summary>
        public static MNNRecognitionQuality Select(IEnumerable<MNNRecognitionQuality> results,
            string corpusId, string deviceId, string language, string metric, double maximumP95Milliseconds)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            MNNPresenceOptions.RequireTime(maximumP95Milliseconds, nameof(maximumP95Milliseconds));
            MNNRecognitionQuality best = null;
            foreach (var item in results)
            {
                if (item == null || !item.NativeInferenceVerified || item.CorpusId != corpusId || item.DeviceId != deviceId ||
                    item.Language != language || item.Metric != metric || item.P95LatencyMilliseconds > maximumP95Milliseconds) continue;
                if (best == null || item.ErrorRate < best.ErrorRate || item.ErrorRate == best.ErrorRate &&
                    (item.P95LatencyMilliseconds < best.P95LatencyMilliseconds || item.P95LatencyMilliseconds == best.P95LatencyMilliseconds &&
                    string.CompareOrdinal(item.ConfigurationId, best.ConfigurationId) < 0)) best = item;
            }
            return best;
        }
    }
}
