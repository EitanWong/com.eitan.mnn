using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Worker-owned resampling, 16 kHz/512-sample enhancement, observation and independent VAD/ASR routing.
    /// Callbacks borrow worker buffers for the duration of the call; observers must not mutate or retain them.
    /// The enhancer modifies its frame in place and may have at most 512 samples of signal delay.
    /// Process on a model worker, never a device callback. Reset after a callback fault.</summary>
    public sealed class MNNAcousticInputProcessor : IMNNRealtimeVoiceInputProcessor
    {
        private const int FrameSamples = MNNRealtimeVoiceActivity.SamplesPerInference;
        private readonly MNNRealtimeVoiceInputProcessor _input;
        private readonly IMNNRealtimeVoiceActivity _vad;
        private readonly Action<float[], int> _enhance;
        private readonly Action _reset;
        private readonly Action<float[], int, float[]> _observe;
        private readonly bool _enhanceRecognition, _enhanceVoiceActivity;
        private readonly float[] _frame = new float[FrameSamples], _silence = new float[FrameSamples];
        private readonly MNNRealtimeFrameVoiceActivity _activity;
        private bool _hasInput, _completed, _faulted;
        private int _processing;
        public MNNPcmFormat InputFormat => _input.InputFormat;
        public int MaximumInputBlockFrames => _input.MaximumInputBlockFrames;
        public bool IsFaulted => _faulted || _input.IsFaulted;
        public bool IsCompleted => _completed;

        public MNNAcousticInputProcessor(MNNPcmFormat format, int capacity, IMNNRealtimeVoiceActivity vad,
            Action<float[], int> enhance, Action reset, Action<float[], int, float[]> observe,
            bool enhanceRecognition = true, bool enhanceVoiceActivity = true)
        {
            _vad = vad ?? throw new ArgumentNullException(nameof(vad));
            _enhance = enhance;
            _reset = reset;
            _observe = observe;
            _enhanceRecognition = enhanceRecognition;
            _enhanceVoiceActivity = enhanceVoiceActivity;
            _activity = new MNNRealtimeFrameVoiceActivity(Detect, reset: ResetModels);
            _input = new MNNRealtimeVoiceInputProcessor(format, _activity, capacity);
        }

        private MNNRealtimeVadUpdate Detect(float[] samples, int offset)
        {
            Array.Copy(samples, offset, _frame, 0, FrameSamples);
            _enhance?.Invoke(_frame, 0);
            for (int i = 0; i < FrameSamples; ++i)
            {
                float value = _frame[i];
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new InvalidOperationException("Acoustic enhancement returned non-finite PCM.");
                _frame[i] = Math.Max(-1, Math.Min(1, value));
            }
            _observe?.Invoke(samples, offset, _frame);
            var update = _enhanceVoiceActivity ? _vad.ProcessFrame(_frame, 0) : _vad.ProcessFrame(samples, offset);
            if (_enhanceRecognition) Array.Copy(_frame, 0, samples, offset, FrameSamples);
            return update;
        }

        private void ResetModels()
        {
            _reset?.Invoke();
            _vad.Reset();
        }

        public void Process(float[] pcm, int offset, int frames, Action<MNNRealtimeVadUpdate> update,
            bool discontinuity = false, Action<float[], int, MNNRealtimeVadUpdate> onFrame = null)
        {
            Enter();
            try
            {
                ThrowIfFaulted();
                if (_completed) throw new InvalidOperationException("Reset a completed acoustic stream before reuse.");
                _input.Process(pcm, offset, frames, update, discontinuity, onFrame);
                _hasInput = discontinuity ? frames > 0 : _hasInput || frames > 0;
            }
            finally { Exit(); }
        }

        /// <summary>Drains resampling and partial input, followed by one enhancement frame for its delayed tail.
        /// Completion is idempotent on success; failure requires Reset.</summary>
        public bool Complete(Action<MNNRealtimeVadUpdate> update,
            Action<float[], int, MNNRealtimeVadUpdate> onFrame = null)
        {
            Enter();
            bool advanced = false;
            try
            {
                ThrowIfFaulted();
                if (_completed) return false;
                if (update == null) throw new ArgumentNullException(nameof(update));
                advanced = true;
                bool emitted = _input.Complete(update, onFrame);
                if (_hasInput && _enhance != null)
                {
                    Array.Clear(_silence, 0, _silence.Length);
                    var result = _activity.ProcessFrame(_silence, 0);
                    onFrame?.Invoke(_silence, 0, result);
                    update(result);
                    emitted = true;
                }
                _completed = true;
                return emitted;
            }
            catch
            {
                if (advanced) _faulted = true;
                throw;
            }
            finally { Exit(); }
        }

        public void Reset()
        {
            Enter();
            try
            {
                _input.Reset();
                _hasInput = _completed = _faulted = false;
            }
            catch
            {
                _faulted = true;
                throw;
            }
            finally { Exit(); }
        }

        private void ThrowIfFaulted()
        {
            if (IsFaulted) throw new InvalidOperationException("Acoustic processing failed; reset before reuse.");
        }

        private void Enter()
        {
            if (Interlocked.Exchange(ref _processing, 1) != 0)
                throw new InvalidOperationException("Acoustic processor calls must not overlap or reenter.");
        }

        private void Exit() => Volatile.Write(ref _processing, 0);
    }
}
