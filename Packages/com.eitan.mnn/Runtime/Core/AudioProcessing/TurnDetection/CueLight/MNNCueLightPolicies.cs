namespace MNN.Unity
{
    internal sealed class MNNCueLightOnset
    {
        private int _silence, _run;
        private bool _armed;
        internal bool Update(float speech)
        {
            if (speech < .5f) { ++_silence; _run = 0; return false; }
            if (_run == 0) { _armed = _silence >= 100; _silence = 0; }
            ++_run;
            if (!_armed || _run != 3) return false;
            _armed = false; return true;
        }
    }
    internal sealed class MNNCueLightEndpoint
    {
        private readonly double _threshold, _minimum, _timeout;
        private int _silence;
        private bool _fired = true;
        internal MNNCueLightEndpoint(double threshold, double minimum, double timeout)
        { _threshold = threshold; _minimum = minimum; _timeout = timeout; }
        internal bool Update(float speech, float end)
        {
            if (speech >= .5f) { _silence = 0; _fired = false; return false; }
            double silence = ++_silence * .02;
            if (_fired || !((silence >= _minimum && end >= _threshold) || silence >= _timeout)) return false;
            _fired = true; return true;
        }
    }
}
