using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Managed mono PCM resampling shared by applications and task frontends.</summary>
    public static class MNNAudioResampling
    {
        /// <summary>Return independently owned PCM at the target rate. tailSamples limits output to its last samples;
        /// zero keeps the entire signal. Rates must be 8–96 kHz; input must contain finite PCM.</summary>
        public static float[] Resample(float[] audio, int source, int target, CancellationToken token = default, int tailSamples = 0)
        {
            if (audio == null) throw new ArgumentNullException(nameof(audio));
            if (source < 8000 || source > 96000) throw new ArgumentOutOfRangeException(nameof(source));
            if (target < 8000 || target > 96000) throw new ArgumentOutOfRangeException(nameof(target));
            if (tailSamples < 0) throw new ArgumentOutOfRangeException(nameof(tailSamples));
            token.ThrowIfCancellationRequested();
            foreach (float sample in audio)
                if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new ArgumentException("Supply finite PCM.", nameof(audio));
            return MNNSpeechDsp.Resample(audio, source, target, token, tailSamples);
        }
    }
}
