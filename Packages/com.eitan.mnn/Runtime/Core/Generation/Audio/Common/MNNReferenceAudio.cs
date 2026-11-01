using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MNN.Unity
{
    /// <summary>A mono reference recording. Samples are copied and must be finite normalized PCM.</summary>
    public sealed class MNNReferenceAudio
    {
        internal readonly float[] Samples;
        public int SampleRate { get; }
        public double Duration => (double)Samples.Length / SampleRate;

        public MNNReferenceAudio(float[] samples, int sampleRate)
        {
            if (sampleRate < 8000 || sampleRate > 96000)
                throw new ArgumentOutOfRangeException(nameof(sampleRate), "Reference rate must be 8–96 kHz.");
            if (samples == null || (long)samples.Length * 2 < sampleRate || samples.Length > sampleRate * 15)
                throw new ArgumentException("Reference audio must contain 0.5–15 seconds of mono speech.", nameof(samples));
            if (samples.Any(x => float.IsNaN(x) || float.IsInfinity(x) || Math.Abs(x) > 1))
                throw new ArgumentException("Reference audio must contain finite PCM in [-1, 1].", nameof(samples));
            if (samples.Sum(x => (double)x * x) / samples.Length < 1e-8)
                throw new ArgumentException("Reference audio is silent.", nameof(samples));
            Samples = (float[])samples.Clone();
            SampleRate = sampleRate;
        }

        /// <summary>Read RIFF WAV: PCM16/24/32 or IEEE float32, mono or stereo (mixed to mono).</summary>
        public static MNNReferenceAudio FromWaveFile(string path)
        {
            var samples = MNNWaveAudio.Read(path, out int sampleRate);
            return new MNNReferenceAudio(samples, sampleRate);
        }
    }
}
