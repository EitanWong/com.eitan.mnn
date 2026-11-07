using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Allocation-free rendered speech fade, response-bound waveform and audible-tail tracking.
    /// Configure on the control thread; call Process from one render producer.</summary>
    public sealed class MNNSpeechOutputProcessor
    {
        private readonly MNNRealtimeOutputPipeline _output;
        private readonly MNNSpeechFade _fade = new MNNSpeechFade();
        private readonly int _sampleRate;
        private long _audibleUntil, _tailTicks;
        public MNNPlaybackWaveform Waveform { get; } = new MNNPlaybackWaveform();
        public MNNSpeechFade Fade => _fade;
        public bool MayBeAudible => Stopwatch.GetTimestamp() < Interlocked.Read(ref _audibleUntil);
        public MNNSpeechOutputProcessor(MNNRealtimeOutputPipeline output, int sampleRate, double presentationTailMilliseconds = 0)
        {
            _output = output;
            new MNNPcmFormat(sampleRate, 1).RequireValid();
            _sampleRate = sampleRate;
            SetPresentationTail(presentationTailMilliseconds);
        }
        public void SetPresentationTail(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0 || milliseconds > 60000)
                throw new ArgumentOutOfRangeException(nameof(milliseconds));
            Interlocked.Exchange(ref _tailTicks, (long)(milliseconds * Stopwatch.Frequency / 1000));
        }
        public Task FadeOutAsync() => _fade.FadeOutAsync(_output?.CurrentResponse ?? default);
        public void Process(float[] samples, int channels) => Process(samples, samples?.Length ?? 0, channels);
        public void Process(float[] samples, int count, int channels)
            => Process(samples, count, channels, _output?.CurrentResponse ?? default);
        internal void Process(float[] samples, int count, int channels, MNNResponseId response)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (channels < 1 || count < 0 || count > samples.Length || count % channels != 0)
                throw new ArgumentException("Supply complete interleaved render frames.");
            if (_output != null)
            {
                _fade.Process(samples, count, channels, _sampleRate, response);
                Waveform.Publish(samples, count, channels, response, _sampleRate);
            }
            for (int i = 0; i < count; ++i)
                if (samples[i] > .00001f || samples[i] < -.00001f)
                {
                    Interlocked.Exchange(ref _audibleUntil, Stopwatch.GetTimestamp() + Interlocked.Read(ref _tailTicks));
                    break;
                }
        }
    }
}
