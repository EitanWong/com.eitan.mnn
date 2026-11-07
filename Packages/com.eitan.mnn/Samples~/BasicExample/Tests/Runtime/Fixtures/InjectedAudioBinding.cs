using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Examples.Tests
{
    /// <summary>Injected PCM capture and rendered output; no microphone, speaker or physical presentation proof.</summary>
    internal sealed class InjectedAudioBinding : MNNRealtimeDeviceBinding, IMNNAudioCaptureSource
    {
        private readonly ConcurrentQueue<float[]> _capture = new ConcurrentQueue<float[]>();
        private readonly float[] _render = new float[160];
        private long _sequence, _capturePosition, _rendered;
        private readonly double[] _renderMilliseconds = new double[65536];
        private int _durationCount, _durationIndex;
        private MNNRealtimeOutputPipeline _output;
        internal bool Capturing, Playing, CaptureEnabled, BlockPermission, FailInputStop;
        internal int InputStarts, OutputStarts, InputStops, OutputStops, NonzeroRenderedSamples;
        internal long FirstAudioRenderedAt { get; private set; }
        internal long TotalRenderCalls { get; private set; }
        internal readonly TaskCompletionSource<bool> PermissionCleanup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public override IMNNAudioCaptureSource Capture => this;
        public override MNNPcmFormat OutputFormat => CaptureFormat;
        public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
        public int MaximumCaptureSamples => 512;
        public long DroppedCaptureBlocks => 0;
        public bool HasPendingCaptureFrames => !_capture.IsEmpty;
        public override MNNRealtimeDeviceSnapshot ReadState() => new MNNRealtimeDeviceSnapshot(
            true, true, Capturing, false, true, Playing, Playing, true, true, OutputFormat,
            renderedFrames: _rendered, presentationTailMilliseconds: 25);
        public override void ValidateFresh()
        {
            if (Capturing || Playing) throw new InvalidOperationException("Endpoints already owned.");
        }
        public override async Task RequestPermissionAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!BlockPermission) return;
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { await PermissionCleanup.Task; }
        }
        public override void StartCapture(Action<MNNPcmFormat, int> negotiated)
        { ++InputStarts; Capturing = true; negotiated(CaptureFormat, MaximumCaptureSamples); }
        public override void BindOutput(MNNRealtimeOutputPipeline output, MNNPcmFormat format, int capacity) => _output = output;
        public override void StartPlayback() { ++OutputStarts; Playing = true; }
        public override void SetCaptureEnabled(bool enabled) => CaptureEnabled = enabled;
        public override void StopCapture()
        {
            ++InputStops;
            if (FailInputStop) throw new InvalidOperationException("stop input");
            Capturing = false;
        }
        public override void StopPlayback() { ++OutputStops; Playing = false; }
        public override void ResetForRetry() { }
        internal void InjectSpeech(int blocks)
        {
            if (!Capturing || !CaptureEnabled) throw new InvalidOperationException("Begin recording before injecting capture.");
            for (int block = 0; block < blocks; ++block)
            {
                var samples = new float[512];
                for (int i = 0; i < samples.Length; ++i) samples[i] = (i & 1) == 0 ? .2f : -.2f;
                _capture.Enqueue(samples);
            }
        }
        public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count)
        {
            if (!_capture.TryDequeue(out var samples)) { info = default; count = 0; return false; }
            count = samples.Length; Array.Copy(samples, 0, destination, offset, count);
            info = new MNNAudioFrameInfo(1, 1, _sequence++, _capturePosition, Stopwatch.GetTimestamp(), CaptureFormat);
            _capturePosition += count; return true;
        }
        internal void Render(int blocks)
        {
            for (int block = 0; block < blocks; ++block)
            {
                long started = Stopwatch.GetTimestamp();
                _output.Render(_render, 0, _render.Length);
                double duration = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                _renderMilliseconds[_durationIndex] = duration;
                _durationIndex = (_durationIndex + 1) % _renderMilliseconds.Length;
                _durationCount = Math.Min(_durationCount + 1, _renderMilliseconds.Length);
                ++TotalRenderCalls; _rendered += _render.Length;
                foreach (float sample in _render)
                    if (sample != 0)
                    {
                        if (FirstAudioRenderedAt == 0) FirstAudioRenderedAt = Stopwatch.GetTimestamp();
                        ++NonzeroRenderedSamples;
                    }
            }
        }
        internal double RenderPercentile(double percentile)
        {
            if (_durationCount == 0) return -1;
            var copy = new double[_durationCount]; Array.Copy(_renderMilliseconds, copy, copy.Length); Array.Sort(copy);
            return copy[Math.Max(0, (int)Math.Ceiling(copy.Length * percentile) - 1)];
        }
        internal void BeginTurnMetrics() => FirstAudioRenderedAt = 0;
    }
}
