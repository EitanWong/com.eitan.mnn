using System;

namespace MNN.Unity
{
    /// <summary>Worker-owned bounded mono history indexed by sample time. Gaps invalidate the old window.
    /// Storage is allocated once; one processing worker owns all calls.</summary>
    public sealed class MNNAudioReferenceTimeline
    {
        private readonly float[] _samples;
        private readonly int _mask;
        private long _start, _end;
        private bool _initialized;
        public long EndSample => _end;
        public int CapacitySamples => _samples.Length;

        public MNNAudioReferenceTimeline(int capacitySamples = 32768)
        {
            if (capacitySamples < 1 || (capacitySamples & (capacitySamples - 1)) != 0)
                throw new ArgumentOutOfRangeException(nameof(capacitySamples), "Capacity must be a positive power of two.");
            _samples = new float[capacitySamples];
            _mask = capacitySamples - 1;
        }

        public void Reset()
        {
            _initialized = false;
            _start = _end = 0;
        }

        /// <summary>Copies samples already validated by the capture/resampling boundary. Zero count is a no-op.</summary>
        public void Write(long firstSample, float[] samples, int count)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (count < 0 || count > samples.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (firstSample < 0 || firstSample > long.MaxValue - count)
                throw new ArgumentOutOfRangeException(nameof(firstSample));
            if (count == 0) return;
            if (!_initialized || firstSample != _end)
            {
                _start = _end = firstSample;
                _initialized = true;
            }
            // An oversized write retains only the suffix that can still be observed.
            int skip = Math.Max(0, count - _samples.Length);
            _end += skip;
            for (int i = skip; i < count; ++i) _samples[(int)(_end++ & _mask)] = samples[i];
            _start = Math.Max(_start, _end - _samples.Length);
        }

        /// <summary>Copies the complete requested window, or clears destination and returns false if unavailable.</summary>
        public bool Read(long firstSample, float[] destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (!_initialized || firstSample < _start || firstSample > _end || destination.Length > _end - firstSample)
            {
                Array.Clear(destination, 0, destination.Length);
                return false;
            }
            for (int i = 0; i < destination.Length; ++i)
                destination[i] = _samples[(int)((firstSample + i) & _mask)];
            return true;
        }
    }
}
