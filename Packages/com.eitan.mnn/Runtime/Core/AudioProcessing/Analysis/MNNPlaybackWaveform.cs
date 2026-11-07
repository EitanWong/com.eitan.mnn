using System;
using System.Diagnostics;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>One render writer and one UI reader. Bounded, nonblocking snapshots; no callback allocations.</summary>
    public sealed class MNNPlaybackWaveform
    {
        public const int PointCount = 64;
        private readonly float[] _points = new float[PointCount];
        private int _revision;
        private long _timestamp;
        private MNNResponseId _response;
        private float _rms;
        private readonly MNNAudioAnalysis _analysis = new MNNAudioAnalysis();
        private MNNAudioFeatures _features;

        /// <summary>Perceptual loudness with a silence gate and bounded consonant-peak contribution.</summary>
        public static float SpeechEnvelope(float rms, float peak, float sensitivity)
            => MNNAudioEnvelope.Measure(rms, peak, sensitivity);

        public void Publish(float[] samples, int count, int channels, MNNResponseId response, int sampleRate = 48000)
        {
            // Analyze on the existing render writer, downstream of fade and before publication.
            if (!_response.Equals(response)) _analysis.Clear();
            var features = _analysis.Analyze(samples, count, channels, sampleRate);
            Interlocked.Increment(ref _revision);
            _features = features;
            _rms = Reduce(samples, count, channels, _points);
            _response = response;
            _timestamp = Stopwatch.GetTimestamp();
            Interlocked.Increment(ref _revision);
        }

        public bool TryRead(float[] destination, MNNResponseId response, out float rms) =>
            TryRead(destination, response, out rms, out _);

        public bool TryRead(float[] destination, MNNResponseId response, out float rms, out MNNAudioFeatures features)
        {
            RequireDestination(destination);
            return TrySnapshot(destination, response, out rms, out features);
        }

        public bool TryReadFeatures(MNNResponseId response, out MNNAudioFeatures features) =>
            TrySnapshot(null, response, out _, out features);

        private bool TrySnapshot(float[] destination, MNNResponseId response, out float rms, out MNNAudioFeatures features)
        {
            for (int attempt = 0; attempt < 2; ++attempt)
            {
                int before = Volatile.Read(ref _revision);
                if (before == 0 || (before & 1) != 0) continue;
                long timestamp = _timestamp;
                var recordedResponse = _response;
                float level = _rms;
                var snapshot = _features;
                if (destination != null) Array.Copy(_points, destination, PointCount);
                Thread.MemoryBarrier();
                if (before != Volatile.Read(ref _revision)) continue;
                if (!response.IsValid || !recordedResponse.Equals(response) ||
                    Stopwatch.GetTimestamp() - timestamp > Stopwatch.Frequency / 5) break;
                rms = level; features = snapshot;
                return true;
            }
            if (destination != null) Array.Clear(destination, 0, PointCount);
            rms = 0; features = default;
            return false;
        }

        /// <summary>Preserve signed peaks in each bin so consonants survive reduction; energy uses all samples.</summary>
        public static float Reduce(float[] samples, int count, int channels, float[] destination)
        {
            RequireDestination(destination);
            if (samples == null || count <= 0 || count > samples.Length || channels <= 0 || count % channels != 0)
                throw new ArgumentException("Supply complete interleaved PCM frames.");
            int frames = count / channels;
            double energy = 0;
            for (int point = 0; point < PointCount; ++point)
            {
                int start = point * frames / PointCount;
                int energyEnd = (point + 1) * frames / PointCount;
                int end = Math.Max(start + 1, energyEnd);
                float peak = 0;
                for (int frame = start; frame < end; ++frame)
                {
                    float mono = 0;
                    for (int channel = 0; channel < channels; ++channel)
                    {
                        float value = samples[frame * channels + channel];
                        if (!float.IsNaN(value) && !float.IsInfinity(value)) mono += value / channels;
                    }
                    if (Math.Abs(mono) > Math.Abs(peak)) peak = mono;
                    // When there are fewer than 64 frames, repeated points are visual only.
                    if (frame < energyEnd) energy += mono * mono;
                }
                destination[point] = Math.Max(-1f, Math.Min(1f, peak));
            }
            return (float)Math.Sqrt(energy / frames);
        }

        private static void RequireDestination(float[] destination)
        {
            if (destination == null || destination.Length != PointCount)
                throw new ArgumentException("A playback waveform requires exactly 64 points.", nameof(destination));
        }
    }
}
