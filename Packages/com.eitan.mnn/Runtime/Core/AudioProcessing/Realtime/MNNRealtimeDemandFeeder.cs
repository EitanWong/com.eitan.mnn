using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public interface IMNNRealtimeDemandSink
    {
        MNNPcmFormat Format { get; }
        int QueuedSamples { get; }
        void Enqueue(ReadOnlySpan<float> samples);
    }
    /// <summary>Bounded zero demand ahead of a pull-based playback route. Speech is rendered later by MNNRealtimeOutputPull.
    /// Stop before disposing the sink. No per-iteration allocation or Unity API access.</summary>
    public sealed class MNNRealtimeDemandFeeder : IDisposable
    {
        private readonly ManualResetEventSlim _stop = new ManualResetEventSlim();
        private readonly IMNNRealtimeDemandSink _sink;
        private readonly float[] _silence;
        private readonly Thread _thread;
        private Exception _fault;
        public Exception Fault => Volatile.Read(ref _fault);
        public MNNRealtimeDemandFeeder(IMNNRealtimeDemandSink sink, int milliseconds)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink)); sink.Format.RequireValid();
            if (milliseconds < 1 || milliseconds > 1000) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            _silence = new float[Math.Max(sink.Format.Channels, sink.Format.SampleRate * milliseconds / 1000 * sink.Format.Channels)];
            _thread = new Thread(Run) { IsBackground = true, Name = "MNN playback demand" }; _thread.Start();
        }
        private void Run()
        {
            try
            {
                int channels = _sink.Format.Channels;
                while (!_stop.IsSet)
                {
                    int missing = Math.Max(0, _silence.Length - _sink.QueuedSamples); missing -= missing % channels;
                    if (missing > 0) _sink.Enqueue(_silence.AsSpan(0, Math.Min(missing, _silence.Length)));
                    _stop.Wait(2);
                }
            }
            catch (Exception error) { Interlocked.CompareExchange(ref _fault, error, null); }
        }
        public Task StopAsync() { _stop.Set(); return Task.Run((Action)_thread.Join); }
        public void Dispose() { _stop.Set(); _thread.Join(); _stop.Dispose(); }
    }
}
