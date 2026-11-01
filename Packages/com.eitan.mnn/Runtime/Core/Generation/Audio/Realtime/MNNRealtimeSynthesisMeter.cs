using System;
using System.Diagnostics;

namespace MNN.Unity
{
    /// <summary>Fixed-memory rolling and lifetime RTF accounting for one TTS producer.</summary>
    public sealed class MNNRealtimeSynthesisMeter
    {
        private const int WindowCapacity = 128;
        private readonly object _gate = new object();
        private readonly long[] _computeTicks = new long[WindowCapacity];
        private readonly long[] _audioTicks = new long[WindowCapacity];
        private readonly double _minimumWindowAudioTicks;
        private int _next;
        private int _count;
        private double _windowComputeTicks;
        private double _windowAudioTicks;
        private double _totalComputeTicks;
        private double _totalAudioTicks;

        public MNNRealtimeSynthesisMeter(double minimumWindowAudioSeconds = 1.0)
        {
            if (double.IsNaN(minimumWindowAudioSeconds) || double.IsInfinity(minimumWindowAudioSeconds) || minimumWindowAudioSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(minimumWindowAudioSeconds));
            if (minimumWindowAudioSeconds > long.MaxValue / (double)Stopwatch.Frequency)
                throw new ArgumentOutOfRangeException(nameof(minimumWindowAudioSeconds));
            _minimumWindowAudioTicks = minimumWindowAudioSeconds * Stopwatch.Frequency;
        }

        public double WindowRtf
        {
            get { lock (_gate) return _windowAudioTicks == 0 ? double.PositiveInfinity : (double)_windowComputeTicks / _windowAudioTicks; }
        }
        public double LifetimeRtf
        {
            get { lock (_gate) return _totalAudioTicks == 0 ? double.PositiveInfinity : (double)_totalComputeTicks / _totalAudioTicks; }
        }
        public double TotalComputeMilliseconds
        {
            get { lock (_gate) return _totalComputeTicks * 1000.0 / Stopwatch.Frequency; }
        }
        public double WindowAudioSeconds
        {
            get { lock (_gate) return (double)_windowAudioTicks / Stopwatch.Frequency; }
        }
        public bool HasRealtimeHeadroom
        {
            get { lock (_gate) return _windowAudioTicks >= _minimumWindowAudioTicks && _windowComputeTicks < _windowAudioTicks; }
        }
        public int WindowChunkCount
        {
            get { lock (_gate) return _count; }
        }

        /// <summary>Record completed synthesis work once per generated chunk, including chunks awaiting queue retry.</summary>
        public void Record(long computeTicks, int generatedFrames, int sampleRate)
        {
            if (computeTicks < 0) throw new ArgumentOutOfRangeException(nameof(computeTicks));
            if (generatedFrames < 1) throw new ArgumentOutOfRangeException(nameof(generatedFrames));
            if (sampleRate < 1) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double measuredAudioTicks = (double)generatedFrames * Stopwatch.Frequency / sampleRate;
            if (measuredAudioTicks > long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(generatedFrames), "Generated audio duration exceeds the supported stopwatch range.");
            long audioTicks = (long)measuredAudioTicks;
            if (audioTicks < 1) audioTicks = 1;

            lock (_gate)
            {
                if (_count == WindowCapacity)
                {
                    _windowComputeTicks -= _computeTicks[_next];
                    _windowAudioTicks -= _audioTicks[_next];
                }
                else
                {
                    ++_count;
                }

                _computeTicks[_next] = computeTicks;
                _audioTicks[_next] = audioTicks;
                _windowComputeTicks += computeTicks;
                _windowAudioTicks += audioTicks;
                _totalComputeTicks += computeTicks;
                _totalAudioTicks += audioTicks;
                _next = (_next + 1) % WindowCapacity;
            }
        }
    }
}
