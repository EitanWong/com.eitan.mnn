using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Pull speech at render time, before mixing/AEC reference. Reuses bounded scratch storage.</summary>
    public sealed class MNNRealtimeOutputPull : IDisposable
    {
        private readonly MNNRealtimeOutputPipeline _output;
        private readonly float[] _scratch;
        private long _renderedFrames;
        private int _enabled = 1;
        private Exception _fault;
        private readonly MNNSpeechOutputProcessor _presentation;
        public bool MayHaveAudibleSpeech => _presentation.MayBeAudible;
        public long RenderedFrames => Interlocked.Read(ref _renderedFrames);
        public Exception Fault => Volatile.Read(ref _fault);
        public MNNSpeechFade Fade => _presentation.Fade;
        public MNNPlaybackWaveform Waveform => _presentation.Waveform;

        public MNNRealtimeOutputPull(MNNRealtimeOutputPipeline output, int captureTailMilliseconds = 500)
        {
            _output = output ?? throw new ArgumentNullException(nameof(output));
            _scratch = new float[output.MaximumSamplesPerChunk];
            _presentation = new MNNSpeechOutputProcessor(output, output.Format.SampleRate, Math.Max(500, captureTailMilliseconds));
        }
        public void Initialize(MNNPcmFormat format)
        {
            if (format.Channels != _output.Format.Channels || format.SampleRate != _output.Format.SampleRate)
                throw new NotSupportedException("Source format must match the realtime output pipeline.");
        }
        public void Render(Span<float> buffer, MNNPcmFormat format)
        {
            buffer.Clear();
            if (Volatile.Read(ref _enabled) == 0)
                return;
            try
            {
                if (format.Channels != _output.Format.Channels || format.SampleRate != _output.Format.SampleRate ||
                    buffer.Length % format.Channels != 0)
                    throw new InvalidOperationException("Playback format changed during a live session.");
                for (int offset = 0; offset < buffer.Length;)
                {
                    int count = Math.Min(_scratch.Length, buffer.Length - offset);
                    var response = _output.CurrentResponse;
                    _output.Render(_scratch, 0, count);
                    _presentation.Process(_scratch, count, format.Channels, response);
                    _scratch.AsSpan(0, count).CopyTo(buffer.Slice(offset, count));
                    Interlocked.Add(ref _renderedFrames, count / format.Channels);
                    offset += count;
                }
            }
            catch (Exception error)
            {
                buffer.Clear();
                Interlocked.CompareExchange(ref _fault, error, null);
                Disable();
            }
        }
        public void Disable() => Volatile.Write(ref _enabled, 0);
        public void Dispose() => Disable();
    }
}
