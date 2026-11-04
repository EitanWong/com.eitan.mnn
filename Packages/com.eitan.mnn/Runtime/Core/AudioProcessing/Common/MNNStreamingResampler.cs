using System;

namespace MNN.Unity
{
    /// <summary>
    /// Stateful fixed-memory windowed-sinc resampler. Each instance accepts one producer and is not thread-safe.
    /// Process on the audio worker, never from a device callback. Reset between independent streams.
    /// </summary>
    public sealed class MNNStreamingResampler
    {
        private const int MinimumHalfTaps = 16;
        private const int PhaseCount = 1024;
        private readonly int _inputRate;
        private readonly int _outputRate;
        private readonly int _channels;
        private readonly bool _identity;
        private readonly double _step;
        private readonly int _tapCount;
        private readonly int _lookAhead;
        private readonly int _historyFrames;
        private readonly float[] _history;
        private readonly float[] _coefficients;
        private long _inputFrames;
        private long _outputFrames;
        private bool _completed;

        public MNNStreamingResampler(MNNPcmFormat input, MNNPcmFormat output)
        {
            input.RequireValid();
            output.RequireValid();
            if (input.Channels != output.Channels)
                throw new ArgumentException("Resampling preserves channels; use an explicit channel mixer first.", nameof(output));
            _inputRate = input.SampleRate;
            _outputRate = output.SampleRate;
            _channels = input.Channels;
            _identity = _inputRate == _outputRate;
            _step = (double)_inputRate / _outputRate;
            // Keep filter support relative to the lower Nyquist frequency. A fixed input-sample
            // count widens the transition band and aliases microphone noise as the input rate rises.
            _lookAhead = (int)Math.Ceiling(MinimumHalfTaps * Math.Max(1.0, _step));
            _tapCount = _lookAhead * 2;
            _historyFrames = _tapCount + 32;
            _history = _identity ? Array.Empty<float>() : new float[_historyFrames * _channels];
            _coefficients = _identity ? Array.Empty<float>() : BuildCoefficients(_inputRate, _outputRate, _tapCount);
        }

        public long InputFrames => _inputFrames;
        public double NextOutputPosition => _outputFrames * _step;
        public bool IsCompleted => _completed;

        /// <summary>
        /// Consumes as many complete interleaved frames as fit the output buffer. Unconsumed input remains caller-owned.
        /// Returns immediately if outputCapacityFrames is zero. No allocations occur after construction.
        /// </summary>
        public void Process(float[] input, int inputOffsetSamples, int inputFrames,
            float[] output, int outputOffsetSamples, int outputCapacityFrames,
            out int consumedFrames, out int producedFrames)
        {
            if (_completed)
                throw new InvalidOperationException("The stream is complete; call Reset before processing another stream.");
            ValidateRange(input, inputOffsetSamples, inputFrames, _channels, nameof(input));
            ValidateRange(output, outputOffsetSamples, outputCapacityFrames, _channels, nameof(output));
            int inputSamples = checked(inputFrames * _channels);
            int outputSamples = checked(outputCapacityFrames * _channels);
            if (ReferenceEquals(input, output) && RangesOverlap(inputOffsetSamples, inputSamples, outputOffsetSamples, outputSamples))
                throw new ArgumentException("Input and output ranges must not overlap.", nameof(output));
            consumedFrames = 0;
            producedFrames = 0;
            if (outputCapacityFrames == 0)
                return;

            if (_identity)
            {
                int frames = Math.Min(inputFrames, outputCapacityFrames);
                int samples = frames * _channels;
                for (int i = 0; i < samples; ++i)
                {
                    float value = input[inputOffsetSamples + i];
                    if (float.IsNaN(value) || float.IsInfinity(value) || value < -1f || value > 1f)
                        throw new ArgumentException("Input must contain finite normalized PCM.", nameof(input));
                }
                Array.Copy(input, inputOffsetSamples, output, outputOffsetSamples, samples);
                _inputFrames += frames;
                _outputFrames += frames;
                consumedFrames = frames;
                producedFrames = frames;
                return;
            }

            while (true)
            {
                while (CanProduceLive())
                {
                    if (producedFrames == outputCapacityFrames)
                        return;
                    WriteOutput(output, outputOffsetSamples + producedFrames * _channels);
                    ++producedFrames;
                }
                if (consumedFrames == inputFrames)
                    return;

                int sourceOffset = inputOffsetSamples + consumedFrames * _channels;
                for (int channel = 0; channel < _channels; ++channel)
                {
                    float value = input[sourceOffset + channel];
                    if (float.IsNaN(value) || float.IsInfinity(value) || value < -1f || value > 1f)
                        throw new ArgumentException("Input must contain finite normalized PCM.", nameof(input));
                }
                long frame = _inputFrames++;
                int historyOffset = (int)(frame % _historyFrames) * _channels;
                Array.Copy(input, sourceOffset, _history, historyOffset, _channels);
                ++consumedFrames;
            }
        }

        /// <summary>Marks end-of-stream and emits the filter tail using zero padding.</summary>
        public void Complete(float[] output, int outputOffsetSamples, int outputCapacityFrames, out int producedFrames)
        {
            ValidateRange(output, outputOffsetSamples, outputCapacityFrames, _channels, nameof(output));
            _completed = true;
            producedFrames = 0;
            if (_identity)
                return;
            long lastInputFrame = _inputFrames - 1;
            while (NextOutputPosition <= lastInputFrame && producedFrames < outputCapacityFrames)
            {
                WriteOutput(output, outputOffsetSamples + producedFrames * _channels);
                ++producedFrames;
            }
        }

        /// <summary>Clears phase and filter history for a new recording.</summary>
        public void Reset()
        {
            Array.Clear(_history, 0, _history.Length);
            _inputFrames = 0;
            _outputFrames = 0;
            _completed = false;
        }

        private bool CanProduceLive() =>
            Math.Floor(NextOutputPosition) + _lookAhead < _inputFrames;

        private void WriteOutput(float[] destination, int destinationOffset)
        {
            double position = NextOutputPosition;
            long center = (long)Math.Floor(position);
            double fraction = position - center;
            int phase = Math.Min(PhaseCount - 1, (int)(fraction * PhaseCount));
            int coefficientOffset = phase * _tapCount;
            for (int channel = 0; channel < _channels; ++channel)
            {
                double sum = 0;
                for (int tap = 0; tap < _tapCount; ++tap)
                {
                    long frame = center + _lookAhead - tap;
                    if (frame < 0 || frame >= _inputFrames)
                        continue;
                    int historyOffset = (int)(frame % _historyFrames) * _channels + channel;
                    sum += _history[historyOffset] * _coefficients[coefficientOffset + tap];
                }
                destination[destinationOffset + channel] = (float)Math.Max(-1.0, Math.Min(1.0, sum));
            }
            ++_outputFrames;
        }

        private static float[] BuildCoefficients(int inputRate, int outputRate, int tapCount)
        {
            var result = new float[PhaseCount * tapCount];
            int halfTaps = tapCount / 2;
            double cutoff = Math.Min(1.0, (double)outputRate / inputRate) * .94;
            for (int phase = 0; phase < PhaseCount; ++phase)
            {
                double fraction = (double)phase / PhaseCount;
                double sum = 0;
                for (int tap = 0; tap < tapCount; ++tap)
                {
                    double distance = halfTaps - tap - fraction;
                    double windowPosition = distance / halfTaps;
                    double window = Math.Abs(windowPosition) >= 1
                        ? 0
                        : .42 + .5 * Math.Cos(Math.PI * windowPosition) + .08 * Math.Cos(2 * Math.PI * windowPosition);
                    double argument = cutoff * distance;
                    double sinc = argument == 0 ? 1 : Math.Sin(Math.PI * argument) / (Math.PI * argument);
                    double coefficient = cutoff * sinc * window;
                    result[phase * tapCount + tap] = (float)coefficient;
                    sum += coefficient;
                }
                int offset = phase * tapCount;
                for (int tap = 0; tap < tapCount; ++tap)
                    result[offset + tap] = (float)(result[offset + tap] / sum);
            }
            return result;
        }

        private static void ValidateRange(float[] samples, int offset, int frames, int channels, string name)
        {
            if (samples == null)
                throw new ArgumentNullException(name);
            if (offset < 0 || frames < 0 || offset > samples.Length - (long)frames * channels)
                throw new ArgumentOutOfRangeException(nameof(offset));
        }

        private static bool RangesOverlap(int firstOffset, int firstCount, int secondOffset, int secondCount) =>
            firstCount > 0 && secondCount > 0 && firstOffset < (long)secondOffset + secondCount &&
            secondOffset < (long)firstOffset + firstCount;
    }
}
