using System;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Streaming mono synthesis → output-rate/channel conversion, with optional segment-tail trimming.</summary>
    public sealed partial class MNNStreamingSpeechConverter
    {
        private readonly MNNStreamingResampler _resampler;
        private readonly float[] _resampled;
        private readonly float[] _interleaved;
        private readonly float[] _lastFrame;
        private readonly int _channels;
        private readonly Func<float[], int, int, bool, Task> _publish;
        private bool _hasLast;
        private bool _completed;
        public MNNStreamingSpeechConverter(int inputSampleRate, MNNPcmFormat output,
            Func<float[], int, int, bool, Task> publish)
            : this(inputSampleRate, output, publish, false)
        {
        }

        /// <summary>
        /// When enabled, holds candidate silence until speech resumes or the segment ends. Only long
        /// quiet tails are shortened; internal pauses and short natural endings retain their PCM.
        /// Use a new converter for each independently synthesized text segment.
        /// </summary>
        public MNNStreamingSpeechConverter(int inputSampleRate, MNNPcmFormat output,
            Func<float[], int, int, bool, Task> publish, bool trimTrailingSilence)
        {
            output.RequireValid();
            _resampler = new MNNStreamingResampler(new MNNPcmFormat(inputSampleRate, 1), new MNNPcmFormat(output.SampleRate, 1));
            _channels = output.Channels;
            _resampled = new float[Math.Max(64, output.SampleRate / 50)];
            _interleaved = new float[_resampled.Length * _channels];
            _lastFrame = new float[_channels];
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
            if (trimTrailingSilence) InitializeSilenceTrimming(output.SampleRate);
        }
        public async Task AppendAsync(float[] mono, int count)
        {
            if (_completed) throw new InvalidOperationException("Speech converter is complete.");
            if (mono == null) throw new ArgumentNullException(nameof(mono));
            if (count < 0 || count > mono.Length) throw new ArgumentOutOfRangeException(nameof(count));
            int offset = 0;
            while (offset < count)
            {
                _resampler.Process(mono, offset, count - offset, _resampled, 0, _resampled.Length, out int consumed, out int produced);
                if (consumed == 0 && produced == 0) throw new InvalidOperationException("Speech conversion cannot progress.");
                offset += consumed;
                await ProcessConvertedAsync(produced).ConfigureAwait(false);
            }
        }
        public async Task CompleteAsync()
        {
            if (_completed) throw new InvalidOperationException("Speech converter is complete.");
            _completed = true;
            int produced;
            do
            {
                _resampler.Complete(_resampled, 0, _resampled.Length, out produced);
                await ProcessConvertedAsync(produced).ConfigureAwait(false);
            } while (produced == _resampled.Length);
            if (_silenceWindow != null) await CompleteSilenceTrimmingAsync().ConfigureAwait(false);
            if (!_hasLast) throw new InvalidOperationException("Synthesizer produced no audio.");
            await _publish(_lastFrame, 0, _channels, true).ConfigureAwait(false);
        }
        private async Task PublishAsync(float[] mono, int offset, int frames)
        {
            if (frames == 0) return;
            if (_hasLast) await _publish(_lastFrame, 0, _channels, false).ConfigureAwait(false);
            // Retain only one frame to mark EOS without adding a whole-block latency.
            for (int i = 0; i < frames - 1; ++i)
                for (int c = 0; c < _channels; ++c) _interleaved[i * _channels + c] = Math.Max(-1, Math.Min(1, mono[offset + i]));
            if (frames > 1) await _publish(_interleaved, 0, (frames - 1) * _channels, false).ConfigureAwait(false);
            for (int c = 0; c < _channels; ++c) _lastFrame[c] = Math.Max(-1, Math.Min(1, mono[offset + frames - 1]));
            _hasLast = true;
        }
    }
}
