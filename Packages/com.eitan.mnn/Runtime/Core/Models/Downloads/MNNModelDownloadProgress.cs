using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MNN.Unity
{
    /// <summary>A thread-safe batch of repository transfers. Repository callbacks are cumulative across files.</summary>
    public class MNNModelDownloadProgress
    {
        public enum Phase { Queued, Checking, Waiting, Connecting, Downloading, Preparing, Reused, Complete, Loading, Cancelled, Failed }
        private sealed class Model
        {
            public string Key, Name, Source, File, Directory;
            public long Received, Total, Previous;
            public bool Estimated, Reused, Finished, Observed;
            public Phase Phase;
        }
        private readonly object _gate = new object();
        private readonly List<Model> _models = new List<Model>();
        private readonly Queue<KeyValuePair<double, long>> _samples = new Queue<KeyValuePair<double, long>>();
        private readonly Func<double> _clock;
        private readonly double _started;
        private string _current;
        private long _transferred;
        private double? _ended;

        public MNNModelDownloadProgress(Func<double> clock = null)
        {
            _clock = clock ?? (() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
            _started = _clock();
            _samples.Enqueue(new KeyValuePair<double, long>(_started, 0));
        }

        public void Add(string key, string name, string source, long estimatedBytes, string localDirectory = null)
        {
            lock (_gate)
            {
                if (_models.Any(model => model.Key == key)) return;
                bool reused = localDirectory != null;
                _models.Add(new Model
                {
                    Key = key,
                    Name = name,
                    Source = source,
                    Total = Math.Max(0, estimatedBytes),
                    Estimated = estimatedBytes > 0,
                    Directory = localDirectory,
                    Reused = reused,
                    Finished = reused,
                    Phase = reused ? Phase.Reused : Phase.Queued
                });
            }
        }

        public void SetPhase(string key, Phase phase, string directory = null)
        {
            lock (_gate)
            {
                var model = _models.Find(item => item.Key == key);
                if (model == null) return;
                _current = key;
                model.Phase = phase;
                _ended = null;
                if (directory != null) model.Directory = directory;
                if (phase == Phase.Reused) { model.Reused = model.Finished = true; model.Received = 0; }
                if (phase == Phase.Complete || phase == Phase.Loading)
                {
                    model.Finished = true;
                    if (!model.Reused && model.Total > 0) model.Received = model.Total;
                }
                if (_models.All(item => item.Phase == Phase.Reused || item.Phase == Phase.Complete ||
                    item.Phase == Phase.Cancelled || item.Phase == Phase.Failed)) _ended = _clock();
            }
        }

        public void Report(string key, long received, long total, string file)
        {
            lock (_gate)
            {
                var model = _models.Find(item => item.Key == key);
                if (model == null) return;
                _current = key;
                model.Reused = model.Finished = false;
                model.Phase = total > 0 && received >= total ? Phase.Preparing : Phase.Downloading;
                model.File = file;
                model.Received = Math.Max(0, received);
                model.Total = Math.Max(0, total);
                model.Estimated = false;
                // Initial resume/cached-file offsets and retry rollbacks are not transfer speed.
                if (model.Observed) _transferred += Math.Max(0, received - model.Previous);
                model.Observed = true;
                model.Previous = Math.Max(model.Previous, received);
                double now = _clock();
                // Limit sampling to 10 Hz regardless of file callback frequency.
                if (now - _samples.Last().Key >= .1) _samples.Enqueue(new KeyValuePair<double, long>(now, _transferred));
                while (_samples.Count > 1 && _samples.ElementAt(1).Key <= now - 3) _samples.Dequeue();
            }
        }

        public Snapshot Read()
        {
            lock (_gate)
            {
                double now = _clock();
                while (_samples.Count > 1 && _samples.ElementAt(1).Key <= now - 3) _samples.Dequeue();
                var baseline = _samples.Peek();
                double seconds = now - baseline.Key;
                double speed = seconds >= .25 && now - _samples.Last().Key < 3 ? (_transferred - baseline.Value) / seconds : 0;
                var current = _models.Find(model => model.Key == _current) ?? _models.FirstOrDefault(model => !model.Finished) ?? _models.LastOrDefault();
                var needed = _models.Where(model => !model.Reused).ToArray();
                bool known = needed.All(model => model.Total > 0);
                long received = needed.Sum(model => model.Received), total = needed.Sum(model => model.Total);
                int complete = _models.Count(model => model.Finished);
                float fraction = needed.Length == 0 || needed.All(model => model.Finished) ? 1f :
                    known && total > 0 ? (float)Math.Min(1, (double)received / total) : -1f;
                return new Snapshot(_models.Count, _models.Count(model => model.Reused), complete, needed.Count(model => model.Finished),
                    received, total, known, current?.Reused ?? false, needed.Any(model => model.Estimated), fraction, speed, (_ended ?? now) - _started,
                    current?.Name, current?.Source, current?.File, current?.Directory, current?.Phase ?? Phase.Queued,
                    current?.Received ?? 0, current?.Total ?? 0,
                    _models.Select(model => new KeyValuePair<string, Phase>(model.Name,
                        model.Reused && (model.Phase == Phase.Reused || model.Phase == Phase.Complete) ? Phase.Reused : model.Phase)).ToArray());
            }
        }

        public static string PhaseText(Phase phase)
        {
            switch (phase)
            {
                case Phase.Checking: return "Checking local files";
                case Phase.Waiting: return "Waiting for another transfer";
                case Phase.Connecting: return "Connecting / reading repository";
                case Phase.Downloading: return "Downloading";
                case Phase.Preparing: return "Verifying / preparing files";
                case Phase.Reused: return "Using local files";
                case Phase.Complete: return "Ready";
                case Phase.Loading: return "Loading model";
                case Phase.Cancelled: return "Cancelled · select again to resume";
                case Phase.Failed: return "Failed · select again to retry";
                default: return "Queued";
            }
        }

        public sealed class Snapshot
        {
            public readonly int TotalModels, ReusedModels, CompleteModels, DownloadedModels;
            public readonly long ReceivedBytes, TotalBytes, ModelReceivedBytes, ModelTotalBytes;
            public readonly bool TotalKnown, Estimated, ModelReused;
            public readonly float Fraction;
            public readonly double BytesPerSecond, ElapsedSeconds;
            public readonly string ModelName, Source, File, Directory;
            public readonly KeyValuePair<string, Phase>[] QueueItems;
            public string Queue => string.Join("\n", QueueItems.Select(item => item.Key + " — " + PhaseText(item.Value)));
            public readonly Phase State;
            public int DownloadModels => TotalModels - ReusedModels;
            public Snapshot(int models, int reused, int complete, int downloaded, long received, long total, bool known,
                bool modelReused, bool estimated, float fraction, double speed, double elapsed, string name, string source, string file,
                string directory, Phase state, long modelReceived, long modelTotal, KeyValuePair<string, Phase>[] queue)
            {
                TotalModels = models; ReusedModels = reused; CompleteModels = complete; DownloadedModels = downloaded;
                ReceivedBytes = received; TotalBytes = total; TotalKnown = known; Estimated = estimated; Fraction = fraction;
                ModelReused = modelReused; BytesPerSecond = speed; ElapsedSeconds = elapsed; ModelName = name; Source = source; File = file;
                Directory = directory; State = state; ModelReceivedBytes = modelReceived; ModelTotalBytes = modelTotal; QueueItems = queue;
            }
            public string Summary => TotalModels + " models · " + ReusedModels + " local · " + DownloadModels + " to download\n" +
                DownloadedModels + "/" + DownloadModels + " downloads complete · " + (Fraction < 0 ? "Overall size unknown" :
                    (Estimated ? "Approx. " : "Overall ") + (Fraction * 100).ToString("0", CultureInfo.InvariantCulture) + "%") + "\n" +
                Bytes(ReceivedBytes) + " / " + (TotalKnown ? (Estimated ? "~" : "") + Bytes(TotalBytes) : "Total size unknown") +
                (State == Phase.Downloading || State == Phase.Connecting ? " · " + Bytes((long)BytesPerSecond) + "/s" : "");
            public string Details
            {
                get
                {
                    var text = new StringBuilder();
                    text.Append(PhaseText(State)).Append(": ").Append(ModelName ?? "Waiting for a model");
                    if (!string.IsNullOrEmpty(Source)) text.Append('\n').Append(Source);
                    if (!string.IsNullOrEmpty(File)) text.Append("\nCurrent file: ").Append(File);
                    if (!ModelReused)
                        text.Append("\nModel: ").Append(Bytes(ModelReceivedBytes)).Append(" / ").Append(ModelTotalBytes > 0 ? Bytes(ModelTotalBytes) : "Size unknown")
                            .Append(ModelTotalBytes > 0 ? " · " + (Math.Min(1, (double)ModelReceivedBytes / ModelTotalBytes) * 100).ToString("0", CultureInfo.InvariantCulture) + "%" : "");
                    bool transferring = State == Phase.Downloading || State == Phase.Connecting || State == Phase.Waiting;
                    text.Append("\nSpeed: ").Append(transferring ? Bytes((long)BytesPerSecond) + "/s" : "—")
                        .Append(" · Elapsed: ").Append(Duration(ElapsedSeconds));
                    if (transferring && Fraction >= 0 && BytesPerSecond > 0 && TotalBytes > ReceivedBytes)
                        text.Append(" · Remaining: ").Append(Duration((TotalBytes - ReceivedBytes) / BytesPerSecond));
                    if (!string.IsNullOrEmpty(Directory)) text.Append("\nLocation: ").Append(Directory);
                    return text.ToString();
                }
            }
            public static string Bytes(long bytes) => bytes >= 1000000000L ? (bytes / 1000000000d).ToString("0.00", CultureInfo.InvariantCulture) + " GB" :
                bytes >= 1000000L ? (bytes / 1000000d).ToString("0.0", CultureInfo.InvariantCulture) + " MB" :
                bytes >= 1000L ? (bytes / 1000d).ToString("0.0", CultureInfo.InvariantCulture) + " KB" : Math.Max(0, bytes) + " B";
            public static string Duration(double seconds)
            {
                var duration = TimeSpan.FromSeconds(Math.Max(0, Math.Min(seconds, 86400 * 365)));
                return duration.TotalHours >= 1 ? ((int)duration.TotalHours).ToString(CultureInfo.InvariantCulture) + duration.ToString(@"\:mm\:ss") : duration.ToString(@"mm\:ss");
            }
        }
    }
}
