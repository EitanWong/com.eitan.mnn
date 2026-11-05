using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Reusable acoustic session: enhancement, continuous wake and utterance speaker selection.
    /// Stop capture/recognition workers before release. The session owns models and retains failed releases for retry.</summary>
    public sealed class MNNAcousticFrontend : IDisposable
    {
        private readonly MNNAcousticSettings _settings;
        private readonly SemaphoreSlim _utteranceGate = new SemaphoreSlim(1, 1);
        private MNNAcousticNoiseReduction _noise;
        private MNNWakeDetector _wake;
        private MNNAcousticSpeakerSelection _selection;
        private int _closed;
        private bool _loaded;
        public MNNWakeDetector WakeDetector => _wake;
        public Func<bool> WakeAllowed { get => _wake.Allowed; set => _wake.Allowed = value; }
        public bool WakeEnabled => _wake?.Enabled == true;
        public bool AutomaticSpeaker => _settings.profile == MNNAcousticProfile.AutomaticSpeaker;
        public TimeSpan MaximumCaptureAge => TimeSpan.FromMilliseconds(_settings.maximumCaptureAgeMilliseconds);
        public int RecognitionDelaySamples => _settings.denoise && _settings.enhanceRecognition ? _noise?.DelaySamples ?? 0 : 0;
        public string Diagnostics => (_noise?.Diagnostics ?? "Denoising: off") + "; " + (_selection?.Diagnostics ?? "speaker selections: 0; No speaker selection yet.");
        public MNNAcousticFrontend(MNNAcousticSettings settings, IMNNAcousticModelFactory factory = null)
        {
            _settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Snapshot();
            _settings.Validate();
            factory = factory ?? new MNNAcousticMnnModelFactory();
            _noise = new MNNAcousticNoiseReduction(factory);
            _wake = new MNNWakeDetector(_settings, factory);
            if (AutomaticSpeaker) _selection = new MNNAcousticSpeakerSelection(_settings, factory);
        }
        public bool CanReloadWake(MNNAcousticSettings settings) => MNNWakeWordSettings.OnlyWakeChanged(_settings, settings);
        public Task ReloadWakeAsync(string root, MNNAcousticSettings settings, CancellationToken token) => _wake.ReloadAsync(root, settings, token);
        public void Load(string root, CancellationToken token)
        {
            if (Volatile.Read(ref _closed) != 0 || _loaded) throw new InvalidOperationException("Load a fresh acoustic session once.");
            try
            {
                token.ThrowIfCancellationRequested();
                if (_settings.denoise) _noise.Load(root, _settings.denoiserDirectory);
                _wake.SetEnhancementDelaySamples(_noise.DelaySamples);
                token.ThrowIfCancellationRequested();
                if (_settings.wakeWord) _wake.Load(root);
                token.ThrowIfCancellationRequested();
                _selection?.Load(root, token);
                token.ThrowIfCancellationRequested();
                _loaded = true;
            }
            catch (Exception error)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException("Acoustic loading and cleanup failed.", error, cleanup); }
                throw;
            }
        }
        public IMNNRealtimeVoiceInputProcessor CreateInput(MNNPcmFormat format, int capacity, IMNNRealtimeVoiceActivity vad)
        {
            if (Volatile.Read(ref _closed) != 0) throw new ObjectDisposedException(nameof(MNNAcousticFrontend));
            return new MNNAcousticInputProcessor(format, capacity, vad, _noise?.Enabled == true ? (Action<float[], int>)_noise.Enhance : null,
                ResetCapture, ObserveFrame, _settings.enhanceRecognition, _settings.enhanceVoiceActivity);
        }
        private void ObserveFrame(float[] original, int offset, float[] enhanced)
        {
            _noise?.Observe(enhanced.Length);
            _wake?.Observe(original, offset, enhanced);
        }
        private void ResetCapture() { _noise?.Reset(); _wake?.Reset(); }
        public bool TakeWakeRequest() => _wake?.TakeRequest() == true;
        public float[] TakeWakeAudio() => _wake?.TakeAudio();
        public MNNConversationWakeHandoff TakeWakeHandoff() => _wake?.TakeHandoff();
        public long CaptureRevision => _wake?.CaptureRevision ?? 0;
        public void DiscardWake() => _wake?.Discard();
        public void ResetIdentity() => _selection?.ResetIdentity();
        public async Task<float[]> PrepareVoiceAsync(float[] samples, CancellationToken token)
        {
            await _utteranceGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref _closed) != 0) throw new ObjectDisposedException(nameof(MNNAcousticFrontend));
                if (_settings.denoise && _settings.enhanceRecognition)
                    samples = await Task.Run(() => _noise.EnhanceUtterance(samples, token), token).ConfigureAwait(false);
                return _selection == null ? samples : await _selection.PrepareVoiceAsync(samples, token).ConfigureAwait(false);
            }
            finally { _utteranceGate.Release(); }
        }
        /// <summary>Waits off the calling thread for utterance work, then releases model owners.
        /// Capture producers and workers must already have stopped.</summary>
        public async Task ReleaseAsync()
        {
            Interlocked.Exchange(ref _closed, 1);
            await _utteranceGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_wake != null) await _wake.QuiesceAsync().ConfigureAwait(false);
                await Task.Run(ReleaseCore).ConfigureAwait(false);
            }
            finally { _utteranceGate.Release(); }
        }
        public void Dispose()
        {
            if (!_utteranceGate.Wait(0)) throw new InvalidOperationException("Await ReleaseAsync while an utterance is running.");
            try { ReleaseCore(); }
            finally { _utteranceGate.Release(); }
        }
        private void ReleaseCore()
        {
            Interlocked.Exchange(ref _closed, 1);
            List<Exception> errors = null;
            MNNAcousticModelResources.Release(ref _noise, ref errors);
            MNNAcousticModelResources.Release(ref _wake, ref errors);
            MNNAcousticModelResources.Release(ref _selection, ref errors);
            MNNAcousticModelResources.ThrowIfFailed(errors);
        }
        public static float[] Normalize(float[] samples) => MNNAcousticSpeakerSelection.Normalize(samples);
    }
}
