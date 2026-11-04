using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Bounded, optional recognition preview. Inference owns no capture/endpoint state.
    /// Loss invalidates the preview until the next turn; final recognition remains independent.</summary>
    public class MNNConversationRecognitionWorker : IDisposable
    {
        private const int Capacity = 64;
        private readonly object _sync = new object();
        private readonly IMNNConversationRecognitionStream _stream;
        private readonly float[][] _queue = new float[Capacity][], _preRoll;
        private readonly long[] _times = new long[Capacity];
        private readonly AutoResetEvent _ready = new AutoResetEvent(false);
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly Task _task;
        private readonly long _ageTicks;
        private int _read, _write, _count, _preCount, _preHead, _samples;
        private long _epoch, _dropped;
        private string _latest;
        private bool _started, _suspended, _stopping, _disposed;
        private Exception _fault;
        public Exception Fault { get { lock (_sync) return _fault; } }
        public long DroppedFrames { get { lock (_sync) return _dropped; } }

        public MNNConversationRecognitionWorker(IMNNConversationRecognitionStream stream, int preRollFrames = 8, int maximumAgeMilliseconds = 250)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (preRollFrames < 0 || preRollFrames > Capacity - 1 || maximumAgeMilliseconds < 1 || maximumAgeMilliseconds > 1000)
                throw new ArgumentOutOfRangeException(nameof(preRollFrames));
            _ageTicks = (long)(maximumAgeMilliseconds * .001 * Stopwatch.Frequency);
            _preRoll = new float[preRollFrames][];
            for (int i = 0; i < Capacity; ++i) _queue[i] = new float[512];
            for (int i = 0; i < _preRoll.Length; ++i) _preRoll[i] = new float[512];
            _task = Task.Factory.StartNew(Run, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public bool Append(float[] frame, bool speech)
        {
            if (frame == null || frame.Length != 512) throw new ArgumentException("Supply one 16 kHz model frame.");
            lock (_sync)
            {
                if (_stopping || _suspended || _fault != null) return false;
                if (!_started)
                {
                    if (!speech) { Remember(frame); return true; }
                    _started = true;
                    for (int i = 0; i < _preCount; ++i)
                        if (!Enqueue(_preRoll[(_preHead - _preCount + i + _preRoll.Length) % _preRoll.Length])) return false;
                    _preCount = 0;
                }
                if (_samples >= 960000) { Suspend(); return false; }
                if (!Enqueue(frame)) return false;
                _samples += 512;
                _ready.Set();
                return true;
            }
        }
        private void Remember(float[] frame)
        {
            if (_preRoll.Length == 0) return;
            Array.Copy(frame, _preRoll[_preHead], 512);
            _preHead = (_preHead + 1) % _preRoll.Length;
            _preCount = Math.Min(_preCount + 1, _preRoll.Length);
        }
        private bool Enqueue(float[] frame)
        {
            if (_count == Capacity) { Suspend(); return false; }
            Array.Copy(frame, _queue[_write], 512);
            _times[_write] = Stopwatch.GetTimestamp();
            _write = (_write + 1) % Capacity; ++_count;
            return true;
        }
        private void Suspend()
        {
            ++_dropped; ++_epoch; _count = _read = _write = 0;
            _latest = ""; _suspended = true;
        }
        public void Reset()
        {
            lock (_sync)
            {
                if (_stopping) return;
                ++_epoch; _count = _read = _write = _preCount = _preHead = _samples = 0;
                _started = _suspended = false; _latest = "";
                _ready.Set();
            }
        }
        public bool TryRead(out string text)
        {
            lock (_sync) { text = _latest; _latest = null; return text != null; }
        }
        private void Run()
        {
            var frame = new float[512];
            long seen = -1;
            string last = null;
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    long epoch;
                    lock (_sync)
                    {
                        if (_count == 0) { epoch = -1; }
                        else if (Stopwatch.GetTimestamp() - _times[_read] > _ageTicks) { Suspend(); epoch = -1; }
                        else
                        {
                            epoch = _epoch; Array.Copy(_queue[_read], frame, 512);
                            _read = (_read + 1) % Capacity; --_count;
                        }
                    }
                    if (epoch < 0) { _ready.WaitOne(10); continue; }
                    if (seen != epoch) { _stream.Reset(); seen = epoch; last = null; }
                    string text = _stream.AcceptWaveform(frame, 0, 512, _stop.Token);
                    lock (_sync)
                    {
                        if (!_stopping && !_suspended && epoch == _epoch && text != last)
                        { _latest = text ?? ""; last = text; }
                    }
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
            catch (Exception error) { lock (_sync) { _fault = error; _latest = ""; } }
            finally { _stream.Dispose(); }
        }
        public Task StopAsync()
        {
            lock (_sync) { _stopping = true; ++_epoch; _latest = null; _count = 0; }
            _stop.Cancel(); _ready.Set();
            return _task;
        }
        public void Dispose()
        {
            if (_disposed) return;
            StopAsync().GetAwaiter().GetResult();
            _ready.Dispose(); _stop.Dispose(); _disposed = true;
        }
    }
}
