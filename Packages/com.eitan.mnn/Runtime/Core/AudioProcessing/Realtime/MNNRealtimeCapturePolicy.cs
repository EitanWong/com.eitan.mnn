using System;
using System.Diagnostics;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Worker-safe half-duplex/cue policy. Explicit input ownership alone controls publication;
    /// loss of echo health must never splice holes into an active user recording.</summary>
    public sealed class MNNRealtimeCapturePolicy
    {
        private int _requested, _protect = 1;
        private long _cueUntil;
        public bool CaptureRequested => Volatile.Read(ref _requested) != 0;
        public void RequestCapture(bool enabled) => Volatile.Write(ref _requested, enabled ? 1 : 0);
        public void ProtectCaptureFromSounds(bool enabled) => Volatile.Write(ref _protect, enabled ? 1 : 0);
        public bool ShouldStopCue(bool echoQualified) => CaptureRequested && Volatile.Read(ref _protect) != 0 && !echoQualified;
        public bool SpeakerAudible(bool renderedSpeechAudible, long queuedSpeechFrames) => renderedSpeechAudible || queuedSpeechFrames > 0 || Stopwatch.GetTimestamp() < Interlocked.Read(ref _cueUntil);
        public bool TryBeginCue(bool echoQualified, int tailMilliseconds)
        { if (ShouldStopCue(echoQualified)) return false; MarkCueAudible(tailMilliseconds); return true; }
        public void MarkCueAudible(int tailMilliseconds)
        {
            if (tailMilliseconds < 0 || tailMilliseconds > 60000) throw new ArgumentOutOfRangeException(nameof(tailMilliseconds));
            Interlocked.Exchange(ref _cueUntil, Stopwatch.GetTimestamp() + (long)tailMilliseconds * Stopwatch.Frequency / 1000);
        }
        public void Reset() { RequestCapture(false); ProtectCaptureFromSounds(true); }
    }
}
