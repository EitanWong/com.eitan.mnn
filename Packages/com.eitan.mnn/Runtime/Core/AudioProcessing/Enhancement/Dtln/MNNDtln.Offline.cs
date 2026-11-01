using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed partial class MNNDtln
    {
        /// <summary>Enhance up to ten minutes of mono PCM; resets before and after each call.
        /// Flushes three hops and removes the streaming delay, preserving input rate and length.</summary>
        public float[] Enhance(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples == null || samples.Length > (long)sampleRate * 600) throw new ArgumentException("Supply up to ten minutes of mono PCM.", nameof(samples));
            ValidateAudio(samples, samples.Length, cancellationToken);
            lock (_gate)
            {
                ThrowIfDisposed();
                ResetCore();
                try
                {
                    var pcm = MNNSpeechDsp.Resample(samples, sampleRate, SampleRate, cancellationToken);
                    var output = EnhanceAt16K(pcm, cancellationToken);
                    if (sampleRate == SampleRate) return output;
                    var resampled = MNNSpeechDsp.Resample(output, SampleRate, sampleRate, cancellationToken);
                    if (resampled.Length != samples.Length) Array.Resize(ref resampled, samples.Length);
                    return resampled;
                }
                finally { ResetCore(); }
            }
        }
        private float[] EnhanceAt16K(float[] input, CancellationToken token)
        {
            var output = new float[input.Length];
            var hop = new float[HopLength];
            var result = new float[HopLength];
            int count = (input.Length + HopLength - 1) / HopLength + OutputDelaySamples / HopLength;
            for (int frame = 0; frame < count; ++frame)
            {
                token.ThrowIfCancellationRequested();
                int position = frame * HopLength;
                Array.Clear(hop, 0, HopLength);
                if (position < input.Length) Array.Copy(input, position, hop, 0, Math.Min(HopLength, input.Length - position));
                ProcessHopCore(hop, result);
                int start = position - OutputDelaySamples;
                if (start >= 0 && start < output.Length) Array.Copy(result, 0, output, start, Math.Min(HopLength, output.Length - start));
            }
            return output;
        }
        public float[] EnhanceWaveFile(string path, out int sampleRate, CancellationToken cancellationToken = default)
        {
            var audio = MNNWaveAudio.Read(path, out sampleRate);
            return Enhance(audio, sampleRate, cancellationToken);
        }
        public Task<float[]> EnhanceAsync(float[] samples, int sampleRate = SampleRate, CancellationToken cancellationToken = default)
        {
            var copy = samples == null ? null : (float[])samples.Clone();
            return Task.Run(() => Enhance(copy, sampleRate, cancellationToken), cancellationToken);
        }
    }
}
