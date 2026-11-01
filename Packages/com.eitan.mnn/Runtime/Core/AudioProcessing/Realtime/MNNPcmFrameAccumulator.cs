using System;

namespace MNN.Unity
{
    /// <summary>
    /// Packs arbitrary sample chunks into fixed-size frames with one reusable partial-frame buffer.
    /// A single processing worker owns each instance; device callbacks should only copy into a bounded queue.
    /// </summary>
    public sealed class MNNPcmFrameAccumulator
    {
        private readonly int _frameSamples;
        private readonly float[] _pending;
        private int _pendingSamples;
        private bool _completed;

        public MNNPcmFrameAccumulator(int frameSamples)
        {
            if (frameSamples < 1 || frameSamples > 65536)
                throw new ArgumentOutOfRangeException(nameof(frameSamples));
            _frameSamples = frameSamples;
            _pending = new float[frameSamples];
        }

        public int FrameSamples => _frameSamples;
        public int PendingSamples => _pendingSamples;
        public bool IsCompleted => _completed;

        /// <summary>
        /// Appends samples and writes complete frames into caller-owned storage. Unconsumed input remains caller-owned.
        /// The output array must have room for outputCapacityFrames * FrameSamples samples.
        /// </summary>
        public void Append(float[] input, int inputOffset, int inputSamples,
            float[] output, int outputOffset, int outputCapacityFrames,
            out int consumedSamples, out int producedFrames)
        {
            if (_completed)
                throw new InvalidOperationException("The stream is complete; call Reset before appending samples.");
            if (outputCapacityFrames < 0)
                throw new ArgumentOutOfRangeException(nameof(outputCapacityFrames));
            ValidateRange(input, inputOffset, inputSamples, nameof(input));
            int outputSamples = checked(outputCapacityFrames * _frameSamples);
            ValidateRange(output, outputOffset, outputSamples, nameof(output));
            if (ReferenceEquals(input, output) && RangesOverlap(inputOffset, inputSamples, outputOffset, outputSamples))
                throw new ArgumentException("Input and output ranges must not overlap.", nameof(output));

            consumedSamples = 0;
            producedFrames = 0;
            while (consumedSamples < inputSamples && producedFrames < outputCapacityFrames)
            {
                int remaining = inputSamples - consumedSamples;
                if (_pendingSamples == 0 && remaining >= _frameSamples)
                {
                    Array.Copy(input, inputOffset + consumedSamples, output,
                        outputOffset + producedFrames * _frameSamples, _frameSamples);
                    consumedSamples += _frameSamples;
                    ++producedFrames;
                    continue;
                }

                int copyCount = Math.Min(_frameSamples - _pendingSamples, remaining);
                Array.Copy(input, inputOffset + consumedSamples, _pending, _pendingSamples, copyCount);
                _pendingSamples += copyCount;
                consumedSamples += copyCount;
                if (_pendingSamples == _frameSamples)
                {
                    Array.Copy(_pending, 0, output, outputOffset + producedFrames * _frameSamples, _frameSamples);
                    _pendingSamples = 0;
                    ++producedFrames;
                }
            }
        }

        /// <summary>Emits one zero-padded final frame when partial samples remain. Repeated calls return false.</summary>
        public bool TryComplete(float[] output, int outputOffset, out int validSamples)
        {
            ValidateRange(output, outputOffset, _frameSamples, nameof(output));
            if (_completed)
            {
                validSamples = 0;
                return false;
            }

            _completed = true;
            validSamples = _pendingSamples;
            if (_pendingSamples == 0)
                return false;

            Array.Copy(_pending, 0, output, outputOffset, _pendingSamples);
            Array.Clear(output, outputOffset + _pendingSamples, _frameSamples - _pendingSamples);
            Array.Clear(_pending, 0, _pendingSamples);
            _pendingSamples = 0;
            return true;
        }

        public void Reset()
        {
            Array.Clear(_pending, 0, _pending.Length);
            _pendingSamples = 0;
            _completed = false;
        }

        private void ValidateRange(float[] samples, int offset, int count, string name)
        {
            if (samples == null)
                throw new ArgumentNullException(name);
            if (offset < 0 || count < 0 || offset > samples.Length - (long)count)
                throw new ArgumentOutOfRangeException(nameof(offset));
        }

        private static bool RangesOverlap(int firstOffset, int firstCount, int secondOffset, int secondCount) =>
            firstCount > 0 && secondCount > 0 && firstOffset < (long)secondOffset + secondCount &&
            secondOffset < (long)firstOffset + firstCount;
    }
}
