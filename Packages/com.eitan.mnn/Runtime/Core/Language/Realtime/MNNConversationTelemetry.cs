using System;
using System.Diagnostics;
using System.Threading;

namespace MNN.Unity
{
    public class MNNConversationTelemetry
    {
        private const int Capacity = 64;
        private readonly object _gate = new object();
        private readonly MNNConversationTiming[] _history = new MNNConversationTiming[Capacity];
        private readonly double[] _percentileScratch = new double[Capacity];
        private int _next, _count;
        private long _completedCount;
        private string _summary, _bottleneck;

        public class Trace
        {
            public readonly long AcceptedAt = Stopwatch.GetTimestamp();
            public long ProcessingStartedAt, ModelStartedAt, FirstTokenAt, ModelFinishedAt, SpeechStartedAt;
            public long FirstAudioAt, SpeechFinishedAt, PresentedAt;
            public double AsrMs, VoiceIntentMs, SpeechWorkMs, TtsTokenMs, TtsFlowMs, TtsVocoderMs;
            public string Route;
            public bool Failed;
            public int Recorded;
        }

        public readonly struct MNNConversationTiming
        {
            public readonly double QueueMs, AsrMs, VoiceIntentMs, FirstTokenMs, ModelMs, FirstAudioMs, SpeechMs, SpeechElapsedMs, PresentationMs, TotalMs;
            public readonly double TtsTokenMs, TtsFlowMs, TtsVocoderMs;
            public readonly string Route;
            public readonly bool Failed;

            public MNNConversationTiming(Trace trace, long completedAt)
            {
                double ms = 1000.0 / Stopwatch.Frequency;
                QueueMs = Elapsed(trace.AcceptedAt, trace.ProcessingStartedAt, ms);
                AsrMs = trace.AsrMs;
                VoiceIntentMs = trace.VoiceIntentMs;
                FirstTokenMs = Elapsed(trace.AcceptedAt, trace.FirstTokenAt, ms);
                ModelMs = Elapsed(trace.ModelStartedAt, trace.ModelFinishedAt, ms);
                FirstAudioMs = Elapsed(trace.AcceptedAt, trace.FirstAudioAt, ms);
                SpeechMs = trace.SpeechWorkMs;
                TtsTokenMs = trace.TtsTokenMs;
                TtsFlowMs = trace.TtsFlowMs;
                TtsVocoderMs = trace.TtsVocoderMs;
                SpeechElapsedMs = Elapsed(trace.SpeechStartedAt, trace.SpeechFinishedAt, ms);
                PresentationMs = Elapsed(trace.SpeechFinishedAt, trace.PresentedAt, ms);
                TotalMs = Elapsed(trace.AcceptedAt, completedAt, ms);
                Route = trace.Route ?? "Unknown";
                Failed = trace.Failed;
            }

            private static double Elapsed(long start, long end, double scale) => start > 0 && end >= start ? (end - start) * scale : -1;
        }

        public MNNConversationTiming Latest
        {
            get { lock (_gate) return _count == 0 ? default : _history[(_next - 1 + Capacity) % Capacity]; }
        }

        /// <summary>Lifetime count of distinct completed traces, independent of the bounded timing history.</summary>
        public long CompletedCount
        {
            get { lock (_gate) return _completedCount; }
        }

        public string Summary
        {
            get
            {
                lock (_gate)
                {
                    if (_count == 0) return "No completed turns yet";
                    if (_summary != null) return _summary;
                    var latest = _history[(_next - 1 + Capacity) % Capacity];
                    string turnLabel = _count == 1 ? "turn" : "turns";
                    _summary = $"Latest {latest.TotalMs:F0} ms | p50/p95 {Percentile(0.50):F0}/{Percentile(0.95):F0} ms ({_count} {turnLabel})";
                    return _summary;
                }
            }
        }

        public string Bottleneck
        {
            get
            {
                lock (_gate)
                {
                    if (_count == 0) return "Waiting for first response";
                    if (_bottleneck != null) return _bottleneck;
                    var item = _history[(_next - 1 + Capacity) % Capacity];
                    string name = "End-to-end processing";
                    double value = -1;
                    Choose("Queue", item.QueueMs, ref name, ref value);
                    Choose("ASR", item.AsrMs, ref name, ref value);
                    Choose("Voice intent", item.VoiceIntentMs, ref name, ref value);
                    double parallelMs = Math.Max(item.ModelMs, item.SpeechElapsedMs);
                    double previous = value;
                    Choose("Model/TTS parallel stage", parallelMs, ref name, ref value);
                    if (value > previous && item.SpeechElapsedMs > item.ModelMs)
                        name = "TTS pipeline (parallel with model)";
                    else if (value > previous && item.ModelMs >= item.SpeechElapsedMs)
                        name = "Model generation (parallel with TTS)";
                    Choose("Playback presentation", item.PresentationMs, ref name, ref value);
                    if (value < 0) value = item.TotalMs;
                    _bottleneck = $"Main bottleneck: {name} {value:F0} ms" + (item.Failed ? " (turn incomplete)" : "");
                    return _bottleneck;
                }
            }
        }

        public void Complete(Trace trace, long timestamp = 0)
        {
            if (trace == null) return;
            var item = new MNNConversationTiming(trace, timestamp > 0 ? timestamp : Stopwatch.GetTimestamp());
            lock (_gate)
            {
                if (Interlocked.CompareExchange(ref trace.Recorded, 1, 0) != 0) return;
                _history[_next] = item; _next = (_next + 1) % Capacity; _count = Math.Min(Capacity, _count + 1);
                ++_completedCount;
                _summary = _bottleneck = null;
            }
        }

        private double Percentile(double p)
        {
            int start = (_next - _count + Capacity) % Capacity;
            int used = 0;
            for (int i = 0; i < _count; ++i)
            {
                double value = _history[(start + i) % Capacity].TotalMs;
                if (value >= 0) _percentileScratch[used++] = value;
            }
            if (used == 0) return 0;
            Array.Sort(_percentileScratch, 0, used);
            return _percentileScratch[(int)Math.Ceiling(p * used) - 1];
        }

        private static void Choose(string candidate, double duration, ref string name, ref double value)
        {
            if (duration > value) { name = candidate; value = duration; }
        }
    }
}
