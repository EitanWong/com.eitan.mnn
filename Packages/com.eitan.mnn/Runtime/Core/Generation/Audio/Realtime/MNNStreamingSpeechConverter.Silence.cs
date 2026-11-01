using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed partial class MNNStreamingSpeechConverter
    {
        // Conservative absolute gates: quiet windows need both RMS <= -60 dBFS and peak <= -50 dBFS.
        // Keep 80 ms after the last audible window; tails shorter than 160 ms remain untouched.
        private const double SilenceRmsSquared = 0.000001;
        private const float SilencePeak = 0.003162278f;
        private float[] _silenceWindow;
        private float[] _quietBuffer;
        private List<float> _pendingSilence;
        private int _silenceWindowCount;
        private int _minimumTailFrames;
        private int _retainedTailFrames;

        private void InitializeSilenceTrimming(int sampleRate)
        {
            _silenceWindow = new float[Math.Max(1, sampleRate / 100)];
            _quietBuffer = new float[_resampled.Length];
            _minimumTailFrames = Math.Max(1, (int)Math.Ceiling(sampleRate * 0.160));
            _retainedTailFrames = Math.Max(1, (int)Math.Ceiling(sampleRate * 0.080));
            _pendingSilence = new List<float>(_minimumTailFrames);
        }

        private async Task ProcessConvertedAsync(int frames)
        {
            if (_silenceWindow == null)
            {
                await PublishAsync(_resampled, 0, frames).ConfigureAwait(false);
                return;
            }

            int offset = 0;
            while (offset < frames)
            {
                int take = Math.Min(frames - offset, _silenceWindow.Length - _silenceWindowCount);
                Array.Copy(_resampled, offset, _silenceWindow, _silenceWindowCount, take);
                offset += take;
                _silenceWindowCount += take;
                if (_silenceWindowCount == _silenceWindow.Length)
                    await ProcessSilenceWindowAsync().ConfigureAwait(false);
            }
        }

        private async Task ProcessSilenceWindowAsync()
        {
            double energy = 0;
            float peak = 0;
            for (int i = 0; i < _silenceWindowCount; ++i)
            {
                float value = _silenceWindow[i];
                energy += (double)value * value;
                peak = Math.Max(peak, Math.Abs(value));
            }

            if (peak <= SilencePeak && energy <= SilenceRmsSquared * _silenceWindowCount)
            {
                // Own the candidate samples: provider and resampler buffers are borrowed and reused.
                // Retain only the quiet run, never accumulate the spoken waveform. If speech resumes,
                // replay it exactly so even long pauses within a sentence remain unchanged.
                for (int i = 0; i < _silenceWindowCount; ++i) _pendingSilence.Add(_silenceWindow[i]);
            }
            else
            {
                await PublishPendingSilenceAsync(0, _pendingSilence.Count).ConfigureAwait(false);
                await PublishAsync(_silenceWindow, 0, _silenceWindowCount).ConfigureAwait(false);
            }
            _silenceWindowCount = 0;
        }

        private async Task CompleteSilenceTrimmingAsync()
        {
            if (_silenceWindowCount > 0) await ProcessSilenceWindowAsync().ConfigureAwait(false);
            int retained = _pendingSilence.Count >= _minimumTailFrames ? _retainedTailFrames : _pendingSilence.Count;
            await PublishPendingSilenceAsync(_pendingSilence.Count - retained, retained).ConfigureAwait(false);
        }

        private async Task PublishPendingSilenceAsync(int offset, int frames)
        {
            int published = 0;
            while (published < frames)
            {
                // Neither the current window nor the live resampler buffer may be overwritten here.
                int take = Math.Min(frames - published, _quietBuffer.Length);
                _pendingSilence.CopyTo(offset, _quietBuffer, 0, take);
                await PublishAsync(_quietBuffer, 0, take).ConfigureAwait(false);
                offset += take;
                published += take;
            }
            _pendingSilence.Clear();
        }
    }
}
