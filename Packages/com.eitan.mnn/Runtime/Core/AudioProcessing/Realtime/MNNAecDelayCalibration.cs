using System;

namespace MNN.Unity
{
    /// <summary>Qualifies a stable host buffer-delay window, never the duration of AEC convergence.
    /// One worker owns this state. Add accepts fresh device snapshots and monotonic seconds without allocating.</summary>
    public sealed class MNNAecDelayCalibration
    {
        private readonly int[] _samples = new int[32], _sorted = new int[32];
        private int _count, _generation;
        private long _lastFrame = -1;
        private double _started;

        public void Reset() { _count = 0; _lastFrame = -1; }

        public bool Add(int milliseconds, long captureFrame, int generation, double now, out int delay)
        {
            delay = 0;
            if (milliseconds < 0 || milliseconds > 500 || captureFrame < 0 || double.IsNaN(now) || double.IsInfinity(now))
            { Reset(); return false; }
            if (_count > 0 && (generation != _generation || captureFrame < _lastFrame || now < _started)) Reset();
            if (captureFrame == _lastFrame) return false;
            if (_count > 0 && Math.Abs(milliseconds - _samples[0]) > 5) Reset();
            if (_count == 0) { _started = now; _generation = generation; }
            _lastFrame = captureFrame;
            _samples[Math.Min(_count, _samples.Length - 1)] = milliseconds;
            _count = Math.Min(_count + 1, _samples.Length);
            if (_count < 12 || now - _started < .55) return false;
            SortWindow();
            if (_sorted[_count - 1] - _sorted[0] > 5) { Reset(); return false; }
            delay = _sorted[_count / 2];
            return true;
        }

        private void SortWindow()
        {
            // The fixed window is tiny. Insertion sort avoids Mono's per-call comparison delegate allocation.
            for (int i = 0; i < _count; ++i)
            {
                int value = _samples[i], position = i;
                while (position > 0 && _sorted[position - 1] > value)
                {
                    _sorted[position] = _sorted[position - 1];
                    --position;
                }
                _sorted[position] = value;
            }
        }
    }
}
