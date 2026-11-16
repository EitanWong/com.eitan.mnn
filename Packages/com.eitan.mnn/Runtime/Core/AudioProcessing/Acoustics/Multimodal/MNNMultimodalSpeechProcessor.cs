using System;
using System.Threading;

namespace MNN.Unity
{
    public enum MNNMultimodalSpeechRoute { Rejected, SingleSpeaker, AudioVisualTarget, ReferenceTarget, SeparatedTarget }

    /// <summary>Owns the accepted waveform. A rejected block contains no audio and must not reach ASR.</summary>
    public sealed class MNNMultimodalSpeechResult
    {
        public MNNMultimodalSpeechRoute Route { get; }
        public float[] Audio { get; }
        public int SampleRate { get; }
        public string Reason { get; }
        internal MNNMultimodalSpeechResult(MNNMultimodalSpeechRoute route, float[] audio, int rate, string reason)
        { Route = route; Audio = audio; SampleRate = rate; Reason = reason; }
    }

    /// <summary>Worker-owned target routing before acoustic enhancement, VAD and ASR.
    /// Inject already-loaded providers; this class owns no models and adds no native ABI.
    /// Model exceptions propagate. No failure silently falls back to unseparated mixture.</summary>
    public sealed class MNNMultimodalSpeechProcessor
    {
        private readonly Func<MNNMultimodalSpeechInput, CancellationToken, float[]> _visual, _reference;
        private readonly Func<MNNMultimodalSpeechInput, CancellationToken, MNNSeparatedAudio> _separate;
        private readonly Func<MNNSeparatedAudio, string, int> _select;
        private readonly bool _requirePresence;
        private readonly Func<MNNMultimodalSpeechInput, bool> _visualSupports, _referenceSupports;
        private readonly double _freshness, _syncTolerance;
        private int _processing;

        public MNNMultimodalSpeechProcessor(
            Func<MNNMultimodalSpeechInput, CancellationToken, float[]> audioVisual = null,
            Func<MNNMultimodalSpeechInput, CancellationToken, float[]> referenceTarget = null,
            Func<MNNMultimodalSpeechInput, CancellationToken, MNNSeparatedAudio> separate = null,
            Func<MNNSeparatedAudio, string, int> selectSpeaker = null,
            bool requirePresence = true, double maximumAgeSeconds = .75, double synchronizationToleranceSeconds = .08,
            Func<MNNMultimodalSpeechInput, bool> visualSupports = null, Func<MNNMultimodalSpeechInput, bool> referenceSupports = null)
        {
            MNNPresenceOptions.RequireTime(maximumAgeSeconds, nameof(maximumAgeSeconds));
            MNNPresenceOptions.RequireTime(synchronizationToleranceSeconds, nameof(synchronizationToleranceSeconds));
            if (maximumAgeSeconds <= 0 || synchronizationToleranceSeconds > maximumAgeSeconds)
                throw new ArgumentOutOfRangeException(nameof(maximumAgeSeconds));
            _visual = audioVisual; _reference = referenceTarget; _separate = separate; _select = selectSpeaker;
            _requirePresence = requirePresence; _freshness = maximumAgeSeconds; _syncTolerance = synchronizationToleranceSeconds;
            _visualSupports = visualSupports; _referenceSupports = referenceSupports;
        }

        public MNNMultimodalSpeechResult Process(MNNMultimodalSpeechInput input, MNNPresenceSnapshot presence,
            double now, CancellationToken cancellationToken = default)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            MNNPresenceOptions.RequireTime(now, nameof(now));
            if (now < input.EndedAt) throw new ArgumentOutOfRangeException(nameof(now), "Cannot process audio from the future.");
            if (Interlocked.Exchange(ref _processing, 1) != 0) throw new InvalidOperationException("Speech routing calls must not overlap or reenter.");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (now - input.EndedAt > _freshness) return Reject(input, "Audio expired.");
                if (_requirePresence && (!presence.CanStartInteraction || presence.TrackId != input.TrackId ||
                    input.StartedAt < presence.ConfirmedAt || presence.EvaluatedAt > now || now - presence.EvaluatedAt > _freshness))
                    return Reject(input, "No fresh confirmed target presence.");
                return Route(input, cancellationToken);
            }
            finally { Volatile.Write(ref _processing, 0); }
        }

        private MNNMultimodalSpeechResult Route(MNNMultimodalSpeechInput input, CancellationToken token)
        {
            var visual = input.Visual;
            if (_visual != null && visual != null && (_visualSupports == null || _visualSupports(input)) && visual.TrackId == input.TrackId &&
                Math.Abs(visual.StartedAt - input.StartedAt) <= _syncTolerance &&
                Math.Abs(visual.EndedAt - input.EndedAt) <= _syncTolerance)
                return Accept(input, _visual(input, token), MNNMultimodalSpeechRoute.AudioVisualTarget, token);
            if (_reference != null && (_referenceSupports == null || _referenceSupports(input)))
                return Accept(input, _reference(input, token), MNNMultimodalSpeechRoute.ReferenceTarget, token);
            if (input.SpeakerCount == 1)
                return Accept(input, input.CopyAudio(), MNNMultimodalSpeechRoute.SingleSpeaker, token);
            if (input.SpeakerCount != 2 || _separate == null || _select == null)
                return Reject(input, "Target evidence or a supported separation provider is required.");
            var sources = _separate(input, token);
            if (sources == null || sources.SampleRate != input.SampleRate || sources.Speaker0.Length != input.SampleCount)
                throw new InvalidOperationException("Separator changed sample rate or duration.");
            token.ThrowIfCancellationRequested();
            int slot = _select(sources, input.TrackId);
            if (slot != 0 && slot != 1) return Reject(input, "Separated speaker identity is ambiguous.");
            return Accept(input, slot == 0 ? sources.Speaker0 : sources.Speaker1, MNNMultimodalSpeechRoute.SeparatedTarget, token);
        }

        private static MNNMultimodalSpeechResult Accept(MNNMultimodalSpeechInput input, float[] audio,
            MNNMultimodalSpeechRoute route, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (audio == null || audio.Length != input.SampleCount) throw new InvalidOperationException("Provider changed audio duration.");
            MNNMultimodalSpeechInput.ValidatePcm(audio);
            return new MNNMultimodalSpeechResult(route, (float[])audio.Clone(), input.SampleRate, null);
        }

        private static MNNMultimodalSpeechResult Reject(MNNMultimodalSpeechInput input, string reason) =>
            new MNNMultimodalSpeechResult(MNNMultimodalSpeechRoute.Rejected, Array.Empty<float>(), input.SampleRate, reason);
    }
}
