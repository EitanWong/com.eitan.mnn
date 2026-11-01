using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>
    /// Composes streaming resampling, fixed-frame assembly and recurrent Silero VAD for one mono input stream.
    /// Process on a dedicated audio-processing worker, never from a device callback.
    /// </summary>
    public sealed class MNNRealtimeVoiceInputProcessor : IMNNRealtimeVoiceInputProcessor
    {
        private readonly IMNNRealtimeVoiceActivity _voiceActivity;
        private readonly MNNPcmFormat _inputFormat;
        private readonly MNNStreamingResampler _resampler;
        private readonly MNNPcmFrameAccumulator _frames;
        private readonly float[] _resampled;
        private readonly float[] _vadFrames;
        private readonly float[] _finalFrame;
        private int _processing;
        private bool _completed;
        private bool _faulted;

        public MNNRealtimeVoiceInputProcessor(MNNPcmFormat inputFormat,
            MNNRealtimeVoiceActivity voiceActivity, int maximumInputBlockFrames)
            : this(inputFormat, (IMNNRealtimeVoiceActivity)voiceActivity, maximumInputBlockFrames) { }

        public MNNRealtimeVoiceInputProcessor(MNNPcmFormat inputFormat,
            IMNNRealtimeVoiceActivity voiceActivity, int maximumInputBlockFrames)
        {
            inputFormat.RequireValid();
            if (inputFormat.Channels != 1)
                throw new ArgumentException("Supply mono PCM after explicit channel mixing.", nameof(inputFormat));
            if (voiceActivity == null)
                throw new ArgumentNullException(nameof(voiceActivity));
            if (maximumInputBlockFrames < 1 || maximumInputBlockFrames > inputFormat.SampleRate * 2)
                throw new ArgumentOutOfRangeException(nameof(maximumInputBlockFrames));

            _voiceActivity = voiceActivity;
            _inputFormat = inputFormat;
            _resampler = new MNNStreamingResampler(inputFormat,
                new MNNPcmFormat(MNNRealtimeVoiceActivity.SampleRate, 1));
            _frames = new MNNPcmFrameAccumulator(MNNRealtimeVoiceActivity.SamplesPerInference);
            int outputFrames = checked((int)Math.Ceiling((double)maximumInputBlockFrames *
                MNNRealtimeVoiceActivity.SampleRate / inputFormat.SampleRate) + 1);
            _resampled = new float[outputFrames];
            _vadFrames = new float[checked(((outputFrames + _frames.FrameSamples - 1) / _frames.FrameSamples) *
                _frames.FrameSamples)];
            _finalFrame = new float[_frames.FrameSamples];
            MaximumInputBlockFrames = maximumInputBlockFrames;
        }

        public int MaximumInputBlockFrames { get; }
        public MNNPcmFormat InputFormat => _inputFormat;
        public int PendingVadSamples => _frames.PendingSamples;
        public bool IsCompleted => _completed;
        public bool IsFaulted => _faulted;

        /// <summary>Consumes one bounded device block and emits zero or more complete VAD updates.</summary>
        public void Process(float[] monoPcm, int offset, int inputFrames, Action<MNNRealtimeVadUpdate> onUpdate,
            bool discontinuity = false, Action<float[], int, MNNRealtimeVadUpdate> onFrame = null)
        {
            Enter();
            bool stateMayHaveAdvanced = false;
            try
            {
                ThrowIfFaulted();
                if (_completed)
                    throw new InvalidOperationException("The stream is complete; call Reset before processing input.");
                if (discontinuity)
                    ResetCore();
                if (inputFrames > MaximumInputBlockFrames)
                    throw new ArgumentOutOfRangeException(nameof(inputFrames), "Split oversized input into bounded blocks.");
                if (onUpdate == null)
                    throw new ArgumentNullException(nameof(onUpdate));
                ValidateInput(monoPcm, offset, inputFrames);

                stateMayHaveAdvanced = true;
                _resampler.Process(monoPcm, offset, inputFrames, _resampled, 0, _resampled.Length,
                    out int consumed, out int produced);
                if (consumed != inputFrames)
                    throw new InvalidOperationException("Configured resampler output storage was insufficient for this input block.");
                ConsumeResampled(produced, onUpdate, onFrame);
            }
            catch
            {
                if (stateMayHaveAdvanced)
                    _faulted = true;
                throw;
            }
            finally
            {
                Exit();
            }
        }

        /// <summary>Flushes filter output and emits one zero-padded VAD frame when a partial frame remains.</summary>
        public bool Complete(Action<MNNRealtimeVadUpdate> onUpdate,
            Action<float[], int, MNNRealtimeVadUpdate> onFrame = null)
        {
            Enter();
            bool stateMayHaveAdvanced = false;
            try
            {
                ThrowIfFaulted();
                if (_completed)
                    return false;
                if (onUpdate == null)
                    throw new ArgumentNullException(nameof(onUpdate));

                stateMayHaveAdvanced = true;
                int produced;
                do
                {
                    _resampler.Complete(_resampled, 0, _resampled.Length, out produced);
                    ConsumeResampled(produced, onUpdate, onFrame);
                } while (produced == _resampled.Length);

                _completed = true;
                if (!_frames.TryComplete(_finalFrame, 0, out int validSamples))
                    return false;
                var update = _voiceActivity.ProcessFrame(_finalFrame, 0);
                onFrame?.Invoke(_finalFrame, 0, update);
                onUpdate(update);
                return validSamples > 0;
            }
            catch
            {
                if (stateMayHaveAdvanced)
                    _faulted = true;
                throw;
            }
            finally
            {
                Exit();
            }
        }

        /// <summary>Resets filter, frame and recurrent VAD state after a discontinuity or for a new stream.</summary>
        public void Reset()
        {
            Enter();
            try
            {
                ResetCore();
            }
            finally
            {
                Exit();
            }
        }

        private void ResetCore()
        {
            _resampler.Reset();
            _frames.Reset();
            _voiceActivity.Reset();
            _completed = false;
            _faulted = false;
        }

        private void ThrowIfFaulted()
        {
            if (_faulted)
                throw new InvalidOperationException("Input processing failed after stream state advanced; reset before reuse.");
        }

        private void ConsumeResampled(int samples, Action<MNNRealtimeVadUpdate> onUpdate,
            Action<float[], int, MNNRealtimeVadUpdate> onFrame)
        {
            int offset = 0;
            while (offset < samples)
            {
                _frames.Append(_resampled, offset, samples - offset, _vadFrames, 0,
                    _vadFrames.Length / _frames.FrameSamples, out int consumed, out int produced);
                offset += consumed;
                for (int frame = 0; frame < produced; ++frame)
                {
                    int frameOffset = frame * _frames.FrameSamples;
                    var update = _voiceActivity.ProcessFrame(_vadFrames, frameOffset);
                    onFrame?.Invoke(_vadFrames, frameOffset, update);
                    onUpdate(update);
                }
                if (consumed == 0 && produced == 0)
                    throw new InvalidOperationException("VAD frame storage cannot make progress.");
            }
        }

        private void ValidateInput(float[] samples, int offset, int frames)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (offset < 0 || frames < 0 || offset > samples.Length - (long)frames)
                throw new ArgumentOutOfRangeException(nameof(offset));
            for (int i = offset; i < offset + frames; ++i)
            {
                float sample = samples[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample) || sample < -1f || sample > 1f)
                    throw new ArgumentException("Input must contain finite normalized mono PCM.", nameof(samples));
            }
        }

        private void Enter()
        {
            if (Interlocked.Exchange(ref _processing, 1) != 0)
                throw new InvalidOperationException("Input processor calls must not overlap or reenter.");
        }

        private void Exit() => Volatile.Write(ref _processing, 0);
    }
}
