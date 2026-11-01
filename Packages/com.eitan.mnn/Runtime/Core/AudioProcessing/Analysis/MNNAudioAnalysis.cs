using System;

namespace MNN.Unity
{
    /// <summary>Model-free streaming PCM measurements for meters, visualization and audio-reactive controls.</summary>
    /// <remarks>One instance per audio stream and one writer. Copy returned snapshots to readers.
    /// Calls allocate no managed memory after construction. This is not a VAD or speech recognizer.</remarks>
    public sealed class MNNAudioAnalysis
    {
        private readonly float[] pitchWindow = new float[512];
        private readonly float[] correlation = new float[256];
        private int write, valid, rate, channelCount, decimation, decimated;
        private float low, mid, dc, decimatedSum, baseline, lowAlpha, midAlpha, dcAlpha;

        /// <summary>Measure interleaved normalized float PCM, downmixed to mono for analysis.</summary>
        public MNNAudioFeatures Analyze(float[] samples, int count, int channels, int sampleRate)
            => Analyze(samples, 0, count, channels, sampleRate);

        /// <summary>Measure a sample range. Offset/count are scalar samples, not channel frames.</summary>
        /// <remarks>NaN/Infinity become silence and finite samples are bounded to [-1,1].
        /// Call Clear after a capture gap, source change or seek. Format changes reset automatically.</remarks>
        public MNNAudioFeatures Analyze(float[] samples, int offset, int count, int channels, int sampleRate)
        {
            if (samples == null || offset < 0 || count <= 0 || offset > samples.Length - count ||
                channels <= 0 || offset % channels != 0 || count % channels != 0 || sampleRate < 8000 || sampleRate > 192000)
            {
                throw new ArgumentException("Supply complete PCM frames and a sample rate from 8000 through 192000 Hz.");
            }
            if (rate != sampleRate || channelCount != channels) Configure(sampleRate, channels);
            double energy = 0, lowEnergy = 0, highEnergy = 0;
            float peak = 0;
            int frames = count / channels;
            for (int frame = 0; frame < frames; ++frame)
            {
                float mono = 0;
                for (int c = 0; c < channels; ++c)
                {
                    float s = samples[offset + frame * channels + c];
                    if (!float.IsNaN(s) && !float.IsInfinity(s)) mono += Math.Max(-1, Math.Min(1, s)) / channels;
                }
                dc += dcAlpha * (mono - dc);
                float centered = mono - dc;
                low += lowAlpha * (centered - low);
                mid += midAlpha * (centered - mid);
                float high = centered - mid;
                energy += mono * mono; lowEnergy += low * low; highEnergy += high * high;
                peak = Math.Max(peak, Math.Abs(mono));
                decimatedSum += mid;
                if (++decimated < decimation) continue;
                pitchWindow[write] = decimatedSum / decimation;
                write = (write + 1) & 511; valid = Math.Min(512, valid + 1);
                decimated = 0; decimatedSum = 0;
            }
            float rms = (float)Math.Sqrt(energy / frames);
            float onset = Math.Max(0, Math.Min(1, (rms - baseline) / Math.Max(.025f, baseline) * .65f));
            baseline += (rms - baseline) * (1f - (float)Math.Exp(-(double)frames / rate / .12));
            if (rms <= .002f) return default;
            float bass = (float)Math.Min(1, lowEnergy / Math.Max(energy, 1e-12));
            float brightness = (float)Math.Min(1, highEnergy / Math.Max(energy, 1e-12));
            EstimatePitch(out float hz, out float confidence);
            if (brightness > .65f) hz = confidence = 0;
            return new MNNAudioFeatures(rms, peak, bass, brightness, hz, confidence, onset);
        }

        private void Configure(int sampleRate, int channels)
        {
            Clear(); rate = sampleRate; channelCount = channels; decimation = Math.Max(1, rate / 8000);
            lowAlpha = 1f - (float)Math.Exp(-2 * Math.PI * 250 / rate);
            midAlpha = 1f - (float)Math.Exp(-2 * Math.PI * 1600 / rate);
            dcAlpha = 1f - (float)Math.Exp(-2 * Math.PI * 30 / rate);
        }

        private void EstimatePitch(out float hz, out float confidence)
        {
            hz = confidence = 0;
            float effectiveRate = (float)rate / decimation;
            int first = Math.Max(2, (int)(effectiveRate / 450)), last = Math.Min(250, (int)(effectiveRate / 70));
            if (valid < 256 + last) return;
            float best = 0;
            int bestLag = first;
            for (int lag = first; lag <= last; ++lag)
            {
                double cross = 0, aa = 0, bb = 0;
                for (int n = 0; n < 256; ++n)
                {
                    float a = pitchWindow[(write - 1 - n) & 511], b = pitchWindow[(write - 1 - n - lag) & 511];
                    cross += a * b; aa += a * a; bb += b * b;
                }
                float score = (float)(cross / Math.Sqrt(Math.Max(aa * bb, 1e-16)));
                correlation[lag] = score;
                if (score > best) { best = score; bestLag = lag; }
            }
            if (best < .65f) return;
            // Prefer the first strong periodic peak over a subharmonic at a later multiple.
            for (int lag = first + 1; lag < last; ++lag)
                if (correlation[lag] >= best * .93f && correlation[lag] > correlation[lag - 1] && correlation[lag] >= correlation[lag + 1])
                { bestLag = lag; break; }
            float shift = 0;
            if (bestLag > first && bestLag < last)
            {
                float left = correlation[bestLag - 1], center = correlation[bestLag], right = correlation[bestLag + 1];
                float denominator = left - 2 * center + right;
                if (Math.Abs(denominator) > .0001f) shift = Math.Max(-.5f, Math.Min(.5f, .5f * (left - right) / denominator));
            }
            hz = effectiveRate / (bestLag + shift); confidence = Math.Max(0, correlation[bestLag]);
        }

        public void Clear()
        {
            Array.Clear(pitchWindow, 0, pitchWindow.Length);
            write = valid = decimated = rate = channelCount = 0;
            low = mid = dc = decimatedSum = baseline = 0;
        }
    }
}
