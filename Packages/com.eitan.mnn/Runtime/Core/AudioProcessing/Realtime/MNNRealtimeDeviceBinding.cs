using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Primitive device operations. Session sequencing, supervision and ownership belong to MNNRealtimeDeviceSession.
    /// Control operations run on the application's control thread; Capture is one-producer/one-consumer.</summary>
    public abstract class MNNRealtimeDeviceBinding
    {
        public abstract IMNNAudioCaptureSource Capture { get; }
        public abstract MNNPcmFormat OutputFormat { get; }
        public virtual bool OpensPlaybackFirst => false;
        public virtual int ReferenceBlockFrames => 0;
        public virtual MNNPlaybackWaveform Waveform => null;
        public virtual bool SpeechMayBeAudible => false;
        public virtual Exception Fault => null;
        public virtual float? SpeechVolume => null;
        public abstract MNNRealtimeDeviceSnapshot ReadState();
        public abstract void ValidateFresh();
        public virtual Task RequestPermissionAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public virtual void OpenPlayback() { }
        /// <summary>Control-thread backend diagnostics only. Never called by PCM callbacks.</summary>
        public virtual void Supervise() { }
        public abstract void StartCapture(Action<MNNPcmFormat, int> negotiated);
        public abstract void BindOutput(MNNRealtimeOutputPipeline output, MNNPcmFormat captureFormat, int capacity);
        public abstract void StartPlayback();
        public abstract void SetCaptureEnabled(bool enabled);
        public virtual Task FlushCaptureAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public virtual IDisposable BindEchoReference(MNNEchoCancellationWorker worker) => throw new NotSupportedException("This device binding does not provide a final render reference.");
        public virtual void SetPresentationTail(double milliseconds) { }
        public virtual Task FadeOutAsync() => Task.CompletedTask;
        public virtual void SetSpeechVolume(float volume) => throw new NotSupportedException("Speech gain is unavailable.");
        public abstract void StopCapture();
        public abstract void StopPlayback();
        public abstract void ResetForRetry();
    }

    /// <summary>Observed device facts. Bindings gather public API values; the shared session applies policy.</summary>
    public readonly struct MNNRealtimeDeviceSnapshot
    {
        public readonly bool Active, InputActive, Capturing, CaptureStarting, OutputActive, OutputRunning;
        public readonly bool Playing, Audible, RouteMatches, CanRestartPlayback, OutputPaused;
        public readonly MNNPcmFormat OutputFormat;
        public readonly long RenderedFrames;
        public readonly double PresentationTailMilliseconds;
        public readonly float Volume;
        public readonly Exception Fault;
        public MNNRealtimeDeviceSnapshot(bool active, bool inputActive, bool capturing, bool captureStarting,
            bool outputActive, bool outputRunning, bool playing, bool audible, bool routeMatches,
            MNNPcmFormat outputFormat, long renderedFrames = 0, double presentationTailMilliseconds = 0,
            float volume = 1, bool canRestartPlayback = false, bool outputPaused = false, Exception fault = null)
        {
            Active = active; InputActive = inputActive; Capturing = capturing; CaptureStarting = captureStarting;
            OutputActive = outputActive; OutputRunning = outputRunning; Playing = playing; Audible = audible;
            RouteMatches = routeMatches; OutputFormat = outputFormat; RenderedFrames = renderedFrames;
            PresentationTailMilliseconds = presentationTailMilliseconds; Volume = volume;
            CanRestartPlayback = canRestartPlayback; OutputPaused = outputPaused; Fault = fault;
        }
    }

    public interface IMNNRealtimeDeviceClock
    {
        double Seconds { get; }
        Task DelayAsync(int milliseconds, CancellationToken token);
    }

    internal sealed class MNNRealtimeDeviceClock : IMNNRealtimeDeviceClock
    {
        public double Seconds => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        public Task DelayAsync(int milliseconds, CancellationToken token) => Task.Delay(milliseconds, token);
    }

    [Serializable]
    public sealed class MNNRealtimeDeviceSessionOptions
    {
        public double CaptureWarmupSeconds = 10;
        public double OutputWarmupSeconds = 3;
        public bool IncludeMeasuredEchoDelayInPresentation = true;
        public bool EchoEnabled;
        public string EchoModelDirectory;
        public MNNEchoCancellationOptions Echo = new MNNEchoCancellationOptions();
        public int OutputBlockCapacity = 256;
        public int OutputSegmentCapacity = 16384;
        internal MNNRealtimeDeviceSessionOptions Snapshot()
        {
            var result = (MNNRealtimeDeviceSessionOptions)MemberwiseClone();
            result.Echo = Echo == null ? null : new MNNEchoCancellationOptions
            { automaticDelay = Echo.automaticDelay, referenceDelayMilliseconds = Echo.referenceDelayMilliseconds };
            return result;
        }
        internal void Validate()
        {
            if (double.IsNaN(CaptureWarmupSeconds) || CaptureWarmupSeconds <= 0 || CaptureWarmupSeconds > 120 ||
                double.IsNaN(OutputWarmupSeconds) || OutputWarmupSeconds <= 0 || OutputWarmupSeconds > 120 ||
                OutputBlockCapacity < 2 || OutputBlockCapacity > 4096 || OutputSegmentCapacity < 1 || OutputSegmentCapacity > 1048576)
                throw new ArgumentException("Choose finite bounded device startup timeouts and PCM capacities.");
            if (EchoEnabled) (Echo ?? throw new ArgumentException("Echo options are required.")).Validate();
        }
    }
}
