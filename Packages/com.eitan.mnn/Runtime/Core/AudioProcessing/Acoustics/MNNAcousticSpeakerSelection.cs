using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed class MNNAcousticSpeakerSelection : IDisposable
    {
        private readonly MNNAcousticSettings _settings;
        private readonly IMNNAcousticModelFactory _factory;
        private bool _closed;
        private readonly SemaphoreSlim _selectionGate = new SemaphoreSlim(1, 1);
        private readonly MNNAutomaticSpeakerSelector _selector;
        private IMNNAcousticSeparator _separation;
        private IMNNAcousticSpeakerEncoder _speaker;
        private float[] _identity;
        private int _identityRevision, _seenIdentityRevision;
        private long _selectionWorkTicks, _selections;
        private string _lastSpeakerEvidence = "No speaker selection yet.";
        public string Diagnostics => "speaker selections: " + Interlocked.Read(ref _selections) +
            "; selection compute ms: " + (Interlocked.Read(ref _selectionWorkTicks) * 1000.0 / Stopwatch.Frequency).ToString("F1") +
            "; " + Volatile.Read(ref _lastSpeakerEvidence);
        public MNNAcousticSpeakerSelection(MNNAcousticSettings settings, IMNNAcousticModelFactory factory = null)
        {
            _settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Snapshot();
            _settings.Validate();
            _factory = factory ?? new MNNAcousticMnnModelFactory();
            _selector = new MNNAutomaticSpeakerSelector(settings.speakerMinimum, settings.speakerMargin);
        }
        public void Load(string root, CancellationToken token)
        {
            if (_closed || _separation != null || _speaker != null) throw new InvalidOperationException("Load a fresh speaker selection once.");
            token.ThrowIfCancellationRequested();
            _separation = _factory.CreateSeparator(MNNAcousticModelResources.ResolvePath(root, _settings.separationDirectory)) ??
                throw new InvalidOperationException("Separation factory returned no model.");
            token.ThrowIfCancellationRequested();
            _speaker = _factory.CreateSpeaker(MNNAcousticModelResources.ResolvePath(root, _settings.speakerDirectory)) ??
                throw new InvalidOperationException("Speaker factory returned no model.");
        }
        public void ResetIdentity() => Interlocked.Increment(ref _identityRevision);
        public void Dispose()
        {
            if (!_selectionGate.Wait(0)) throw new InvalidOperationException("Await selection before disposing its models.");
            try
            {
                _closed = true;
                System.Collections.Generic.List<Exception> errors = null;
                MNNAcousticModelResources.Release(ref _separation, ref errors);
                MNNAcousticModelResources.Release(ref _speaker, ref errors);
                MNNAcousticModelResources.ThrowIfFailed(errors);
            }
            finally { _selectionGate.Release(); }
        }

        /// <summary>Noncausal separation belongs to the utterance worker, never the capture worker.
        /// Null means that no candidate was sufficiently clear and unambiguous.</summary>
        public async Task<float[]> PrepareVoiceAsync(float[] samples, CancellationToken token)
        {
            await _selectionGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (_closed) throw new ObjectDisposedException(nameof(MNNAcousticSpeakerSelection));
                if (_separation == null || _speaker == null) throw new InvalidOperationException("Load speaker selection before use.");
                return await Task.Run(() => SelectVoice(samples, token), token).ConfigureAwait(false);
            }
            finally { _selectionGate.Release(); }
        }

        private float[] SelectVoice(float[] samples, CancellationToken token)
        {
            long started = Stopwatch.GetTimestamp();
            try
            {
                // Speech activity was decided once by the capture VAD. Do not classify
                // the mixture or separated candidates again on the endpoint path.
                token.ThrowIfCancellationRequested();
                if (samples == null) throw new ArgumentNullException(nameof(samples));
                if (samples.Length < 4000 || Array.TrueForAll(samples, value => value == 0)) return null;
                int revision = Volatile.Read(ref _identityRevision);
                if (revision != _seenIdentityRevision)
                {
                    _selector.Reset(); _identity = null; _seenIdentityRevision = revision;
                }
                var separated = _separation.Separate(samples, token);
                if (separated == null || separated.Length != 2 || separated[0] == null || separated[1] == null ||
                    separated[0].Length != samples.Length || separated[1].Length != samples.Length)
                    throw new InvalidOperationException("Separator must return two candidates on the input sample clock.");
                var candidates = new[] { Normalize(separated[0]), Normalize(separated[1]) };
                if (!MNNAudioMixtureContribution.TryEstimate(samples, candidates[0], candidates[1], out float first, out float second)) return null;
                var contribution = new[] { first, second }; // Unexplained/duplicated outputs cannot identify a target.
                var similarity = new float[2];
                var identities = new float[2][];
                for (int i = 0; i < candidates.Length; ++i)
                {
                    token.ThrowIfCancellationRequested();
                    if (contribution[i] < _settings.speakerMinimum) continue;
                    // Bound identity inference to three seconds, independent of turn length.
                    var excerpt = new float[Math.Min(48000, candidates[i].Length)];
                    Array.Copy(candidates[i], excerpt, excerpt.Length);
                    identities[i] = _speaker.Encode(excerpt, token);
                    similarity[i] = _identity == null ? 0 : MNN.Unity.MNNSpeakerEmbedding.Similarity(_identity, identities[i]);
                }
                token.ThrowIfCancellationRequested();
                int selected = _selector.Select(contribution, similarity, Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
                Volatile.Write(ref _lastSpeakerEvidence, "mixture contribution=" + contribution[0].ToString("F3") + "/" + contribution[1].ToString("F3") +
                    "; selected=" + selected);
                if (selected < 0) return null;
                _identity = identities[selected];
                Interlocked.Increment(ref _selections);
                return candidates[selected];
            }
            finally { Interlocked.Add(ref _selectionWorkTicks, Stopwatch.GetTimestamp() - started); }
        }

        // MossFormer's public API matches both outputs to mixture RMS. Absolute output
        // loudness therefore contains no evidence about which original speaker was louder.
        // Estimate their contributions against the original mixture instead.
        // Apply only a uniform safety gain before ASR; never hard clip the candidates.
        public static float[] Normalize(float[] samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            float peak = 0;
            foreach (float value in samples)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("Separation returned non-finite PCM.");
                peak = Math.Max(peak, Math.Abs(value));
            }
            if (peak <= 1) return samples;
            float gain = .98f / peak;
            for (int i = 0; i < samples.Length; ++i) samples[i] *= gain;
            return samples;
        }

    }
}
