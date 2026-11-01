using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed partial class MNNDtlnAec
    {
        /// <summary>Process a complete pair of aligned mono recordings, at exactly 16 kHz and up to ten minutes.
        /// Resets before/after processing; keeps input length and the upstream 24 ms output delay.
        /// No resampling, reference-delay estimation, gain normalization or clipping is applied.</summary>
        public float[] Process(float[] microphone, float[] playbackReference, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            if (sampleRate != SampleRate) throw new ArgumentOutOfRangeException(nameof(sampleRate), "DTLN AEC requires 16 kHz; resample both synchronized inputs together before processing.");
            if (microphone == null || microphone.Length > SampleRate * 600) throw new ArgumentException("Supply up to ten minutes of audio.", nameof(microphone));
            cancellationToken.ThrowIfCancellationRequested();
            ValidateAudio(microphone, playbackReference, microphone.Length);
            lock (_gate)
            {
                ThrowIfDisposed();
                ResetCore();
                try
                {
                    var output = new float[microphone.Length];
                    var micHop = new float[128]; var refHop = new float[128]; var result = new float[128];
                    // Match upstream: pad 384 at each end, floor the frame count, crop 384.
                    int frames = (microphone.Length + OutputDelaySamples) / HopLength;
                    for (int frame = 0; frame < frames; ++frame)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int position = frame * HopLength - OutputDelaySamples;
                        Array.Clear(micHop, 0, 128); Array.Clear(refHop, 0, 128);
                        int start = Math.Max(0, position), end = Math.Min(microphone.Length, position + HopLength);
                        if (end > start)
                        {
                            Array.Copy(microphone, start, micHop, start - position, end - start);
                            Array.Copy(playbackReference, start, refHop, start - position, end - start);
                        }
                        ProcessHopCore(micHop, refHop, result);
                        if (end > start) Array.Copy(result, start - position, output, start, end - start);
                    }
                    return output;
                }
                finally { ResetCore(); }
            }
        }
        public float[] ProcessWaveFiles(string microphonePath, string playbackReferencePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var microphone = MNNWaveAudio.Read(microphonePath, out int micRate);
            var reference = MNNWaveAudio.Read(playbackReferencePath, out int refRate);
            if (micRate != refRate) throw new InvalidDataException("Microphone and playback reference sample rates must match.");
            return Process(microphone, reference, micRate, cancellationToken);
        }
        public Task<float[]> ProcessAsync(float[] microphone, float[] playbackReference, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            var micCopy = microphone == null ? null : (float[])microphone.Clone();
            var refCopy = playbackReference == null ? null : (float[])playbackReference.Clone();
            return Task.Run(() => Process(micCopy, refCopy, sampleRate, cancellationToken), cancellationToken);
        }
    }
}
