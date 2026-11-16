using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Reusable acoustic model lifetime for any audio transport/application.
    /// Uses the task model factory rather than concrete model names. Stop and join capture before release.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MNN/Realtime/Acoustic Processing")]
    public sealed class MNNAcousticProcessing : MonoBehaviour
    {
        private bool _preparing;
        public MNNAcousticFrontend Frontend { get; private set; }
        public Func<bool> WakeAllowed { get; set; }

        public async Task PrepareAsync(MNNAcousticSettings settings, string modelsDirectory,
            CancellationToken token, IMNNAcousticModelFactory factory = null)
        {
            if (_preparing || Frontend != null) throw new InvalidOperationException("Release the previous acoustic session before preparing again.");
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var snapshot = settings.Snapshot();
            snapshot.Validate();
            token.ThrowIfCancellationRequested();
            if (!snapshot.enabled) return;
            _preparing = true;
            var frontend = new MNNAcousticFrontend(snapshot, factory);
            try
            {
                await Task.Run(() => frontend.Load(modelsDirectory, token), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                frontend.WakeAllowed = () => WakeAllowed?.Invoke() == true;
                Frontend = frontend;
            }
            catch { frontend.Dispose(); throw; }
            finally { _preparing = false; }
        }

        public IMNNRealtimeVoiceInputProcessor CreateVoiceInputProcessor(MNNPcmFormat format, int capacity, IMNNRealtimeVoiceActivity vad) =>
            Frontend?.CreateInput(format, capacity, vad) ?? new MNNRealtimeVoiceInputProcessor(format, vad, capacity);
        public Task<float[]> PrepareVoiceAsync(float[] samples, CancellationToken token) =>
            Frontend?.PrepareVoiceAsync(samples, token) ?? Task.FromResult(samples);

        public void Release()
        {
            if (_preparing) throw new InvalidOperationException("Join acoustic preparation before release.");
            Frontend?.Dispose();
            Frontend = null; WakeAllowed = null;
        }

        public async Task ReleaseAsync()
        {
            if (_preparing) throw new InvalidOperationException("Join acoustic preparation before release.");
            var frontend = Frontend;
            if (frontend != null) await frontend.ReleaseAsync().ConfigureAwait(false);
            if (ReferenceEquals(Frontend, frontend)) { Frontend = null; WakeAllowed = null; }
        }
    }
}
