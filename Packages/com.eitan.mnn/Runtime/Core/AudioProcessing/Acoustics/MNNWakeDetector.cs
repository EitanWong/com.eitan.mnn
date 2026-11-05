using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Continuous language-banked keyword detection with a bounded command handoff and hot replacement.
    /// Models run on the capture worker. Activation gating never pauses their sample clock.</summary>
    public sealed class MNNWakeDetector : IDisposable
    {
        private MNNAcousticSettings _settings;
        private readonly IMNNAcousticModelFactory _factory;
        private readonly object _sync = new object();
        private readonly object _cleanupSync = new object();
        private readonly MNNWakeAudioBuffer _audio;
        private readonly float[] _frame = new float[512];
        private List<IMNNAcousticKeywordModel> _models = new List<IMNNAcousticKeywordModel>();
        private readonly List<IMNNAcousticKeywordModel> _retired = new List<IMNNAcousticKeywordModel>();
        private IReadOnlyDictionary<MNNKeywordLanguage, string> _keywords;
        private int _reloadRevision, _requested;
        private long _modelStartSample;
        private bool _disposed;
        private int _activeReloads;
        private TaskCompletionSource<bool> _reloadsFinished;
        private int _enhancementDelaySamples;
        public Func<bool> Allowed { get; set; }
        public bool Enabled => Volatile.Read(ref _settings).wakeWord;
        public long CaptureRevision => _audio.Revision;
        public MNNWakeAudioBuffer Audio => _audio;
        internal void SetEnhancementDelaySamples(int samples)
        {
            if (samples < 0 || samples > 512) throw new ArgumentOutOfRangeException(nameof(samples));
            lock (_sync) _enhancementDelaySamples = samples;
        }
        public MNNWakeDetector(MNNAcousticSettings settings, IMNNAcousticModelFactory factory = null, MNNWakeAudioBuffer audio = null)
        {
            _settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Snapshot();
            _settings.Validate();
            _factory = factory ?? new MNNAcousticMnnModelFactory();
            _audio = audio ?? new MNNWakeAudioBuffer();
        }
        public void Load(string root)
        {
            if (_disposed || _models.Count != 0) throw new InvalidOperationException("Load only a fresh wake detector.");
            _keywords = MNNWakeWordSettings.Group(_settings);
            try
            {
                foreach (var group in _keywords)
                {
                    var model = _factory.CreateKeyword(MNNAcousticModelResources.ResolvePath(root,
                        MNNWakeWordSettings.ModelDirectory(_settings, group.Key))) ?? throw new InvalidOperationException("Keyword factory returned no model.");
                    _models.Add(model);
                    if (model.Language != group.Key) throw new ArgumentException("The selected wake model does not match the phrase language.");
                    model.Reset(group.Value, _settings.keywordThreshold);
                }
            }
            catch (Exception error)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }
        public async Task ReloadAsync(string root, MNNAcousticSettings settings, CancellationToken token)
        {
            var snapshot = (settings ?? throw new ArgumentNullException(nameof(settings))).Snapshot();
            snapshot.Validate();
            int revision;
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNWakeDetector));
                revision = ++_reloadRevision;
                if (MNNWakeWordSettings.SameWake(_settings, snapshot)) return;
                if (_activeReloads++ == 0) _reloadsFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            var replacement = new MNNWakeDetector(snapshot, _factory);
            List<IMNNAcousticKeywordModel> previous = null;
            try
            {
                await Task.Run(() => { token.ThrowIfCancellationRequested(); if (snapshot.wakeWord) replacement.Load(root); token.ThrowIfCancellationRequested(); }, token).ConfigureAwait(false);
                lock (_sync)
                {
                    token.ThrowIfCancellationRequested();
                    if (_disposed || revision != _reloadRevision) return;
                    previous = _models;
                    _models = replacement._models;
                    replacement._models = new List<IMNNAcousticKeywordModel>();
                    _keywords = replacement._keywords;
                    Volatile.Write(ref _settings, snapshot);
                    _modelStartSample = _audio.Position;
                    Discard();
                }
            }
            finally
            {
                try
                {
                    lock (_cleanupSync)
                    {
                        if (previous != null) _retired.AddRange(previous);
                        _retired.AddRange(replacement._models);
                        replacement._models.Clear();
                        List<Exception> errors = null;
                        ReleaseModels(_retired, ref errors);
                        MNNAcousticModelResources.ThrowIfFailed(errors);
                    }
                }
                finally
                {
                    lock (_sync)
                        if (--_activeReloads == 0) _reloadsFinished.TrySetResult(true);
                }
            }
        }
        public void Observe(float[] original, int offset, float[] enhanced)
        {
            if (original == null || enhanced == null || enhanced.Length != 512 || offset < 0 || offset > original.Length - 512)
                throw new ArgumentException("Supply synchronized 512-sample raw and enhanced frames.");
            lock (_sync)
            {
                if (_disposed) return;
                _audio.Append(_settings.enhanceRecognition ? enhanced : original, _settings.enhanceRecognition ? 0 : offset, 512);
                if (_models.Count == 0) return;
                Array.Copy(_settings.enhanceKeywords ? enhanced : original, _settings.enhanceKeywords ? 0 : offset, _frame, 0, 512);
                double end = -1;
                foreach (var model in _models) end = Math.Max(end, model.AcceptWaveform(_frame));
                if (end < 0 || Allowed?.Invoke() != true) return;
                if (double.IsNaN(end) || double.IsInfinity(end)) throw new InvalidOperationException("Invalid keyword timestamp.");
                double delay = _settings.denoise ? _enhancementDelaySamples / 16000.0 : 0;
                double adjustment = (_settings.enhanceRecognition ? delay : 0) - (_settings.enhanceKeywords ? delay : 0);
                _audio.Mark(_modelStartSample / 16000.0 + Math.Max(0, end + adjustment));
                Interlocked.Exchange(ref _requested, 1);
            }
        }
        public bool TakeRequest() => Interlocked.Exchange(ref _requested, 0) != 0;
        public MNNConversationWakeHandoff TakeHandoff() => _audio.TakeHandoff();
        public float[] TakeAudio() => _audio.Take();
        public void Discard() { _audio.Discard(); Interlocked.Exchange(ref _requested, 0); }
        public void Reset()
        {
            lock (_sync)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNWakeDetector));
                foreach (var model in _models) model.Reset(_keywords[model.Language], _settings.keywordThreshold);
                _audio.Reset(); _modelStartSample = 0; Interlocked.Exchange(ref _requested, 0);
            }
        }
        private static void ReleaseModels(List<IMNNAcousticKeywordModel> models, ref List<Exception> errors)
        {
            for (int i = models.Count - 1; i >= 0; --i)
            {
                try { models[i].Dispose(); models.RemoveAt(i); }
                catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            }
        }
        public void Dispose()
        {
            List<IMNNAcousticKeywordModel> owned;
            lock (_sync)
            {
                if (_activeReloads != 0) throw new InvalidOperationException("Await ReleaseAsync while wake models are being replaced.");
                _disposed = true; ++_reloadRevision;
                owned = _models;
                _models = new List<IMNNAcousticKeywordModel>();
            }
            lock (_cleanupSync)
            {
                _retired.AddRange(owned);
                List<Exception> errors = null;
                ReleaseModels(_retired, ref errors);
                MNNAcousticModelResources.ThrowIfFailed(errors);
            }
        }
        internal Task QuiesceAsync()
        {
            lock (_sync)
            {
                _disposed = true; ++_reloadRevision;
                return _activeReloads == 0 ? Task.CompletedTask : _reloadsFinished.Task;
            }
        }
        public async Task ReleaseAsync()
        {
            await QuiesceAsync().ConfigureAwait(false);
            await Task.Run((Action)Dispose).ConfigureAwait(false);
        }
    }
}
