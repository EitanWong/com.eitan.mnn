using System;
using System.Threading;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// Unity audio callback adapter for the realtime PCM ports. Capture is pushed by one device adapter;
    /// processing and model workers pull it independently. This component does not provide capture or AEC.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [AddComponentMenu("MNN/Realtime/Audio Device")]
    public sealed class MNNRealtimeAudioDevice : MonoBehaviour, IMNNAudioCaptureSource
    {
        private static long s_nextStreamId;

        private AudioSource _source;
        private AudioClip _createdClip;
        private AudioClip _previousClip;
        private bool _previousLoop;
        private int _previousPriority;
        private float[] _renderScratch;
        private Exception _outputFault;
        public Exception Fault => Volatile.Read(ref _outputFault);
        private MNNRealtimeOutputPipeline _output;
        private MNNAudioCaptureBuffer _capture;
        private MNNPcmFormat _captureFormat;
        private int _mainThreadId;
        private int _outputRunning;
        private long _streamId;
        private long _formatEpoch;
        private long _captureSequence;
        private long _captureStartFrame;
        private int _captureGap;
        private int _captureEnabled = 1;
        private long _renderedOutputFrames;

        public bool IsOutputRunning => Volatile.Read(ref _outputRunning) != 0;
        /// <summary>Rendered source frames, not speaker presentation. Reset by Bind before callbacks start.</summary>
        public long RenderedOutputFrames => Interlocked.Read(ref _renderedOutputFrames);
        /// <summary>Gate before startup/stop without moving the SPSC reader. Reopening marks a capture gap.</summary>
        public bool CaptureEnabled
        {
            get => Volatile.Read(ref _captureEnabled) != 0;
            set => Volatile.Write(ref _captureEnabled, value ? 1 : 0);
        }
        public MNNPcmFormat CaptureFormat => _captureFormat;
        MNNPcmFormat IMNNAudioCaptureSource.CaptureFormat => _captureFormat;
        public int MaximumCaptureSamples => Volatile.Read(ref _capture)?.MaximumSamples ?? 0;
        public long DroppedCaptureBlocks => Volatile.Read(ref _capture)?.DroppedBlocks ?? 0;
        public bool CanSubmitCapture => Volatile.Read(ref _capture)?.CanWrite ?? false;
        public bool HasPendingCaptureFrames => Volatile.Read(ref _capture)?.HasPendingFrames ?? false;

        private void Awake()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _source = GetComponent<AudioSource>();
        }

        /// <summary>Control thread only. Allocates bounded capture storage; call before starting capture callbacks.</summary>
        public void Bind(MNNRealtimeOutputPipeline output, MNNPcmFormat captureFormat,
            int captureQueueBlocks = 16, int maximumCaptureSamplesPerBlock = 4096)
        {
            EnsureMainThread();
            if (IsOutputRunning)
                throw new InvalidOperationException("Stop output before rebinding the audio device.");
            if (output == null)
                throw new ArgumentNullException(nameof(output));
            captureFormat.RequireValid();
            if (maximumCaptureSamplesPerBlock % captureFormat.Channels != 0)
                throw new ArgumentException("Capture capacity must contain complete interleaved frames.", nameof(maximumCaptureSamplesPerBlock));

            var nextCapture = new MNNAudioCaptureBuffer(captureQueueBlocks, maximumCaptureSamplesPerBlock);
            long nextFormatEpoch = checked(_formatEpoch + 1);
            _output?.Interrupt();
            Volatile.Write(ref _capture, null);
            Volatile.Write(ref _output, output);
            _captureFormat = captureFormat;
            _streamId = Interlocked.Increment(ref s_nextStreamId);
            _formatEpoch = nextFormatEpoch;
            _captureSequence = 0;
            _captureStartFrame = 0;
            Interlocked.Exchange(ref _renderedOutputFrames, 0);
            Volatile.Write(ref _captureGap, 0);
            Volatile.Write(ref _capture, nextCapture);
        }

        /// <summary>Control thread only. Unity pulls PCM at its actual DSP filter callback, with no streaming-clip pre-read.</summary>
        public void StartOutput()
        {
            EnsureMainThread();
            if (!isActiveAndEnabled)
                throw new InvalidOperationException("The audio device component must be active before output starts.");
            if (_output == null)
                throw new InvalidOperationException("Bind an output pipeline before starting the audio device.");
            if (IsOutputRunning)
                return;
            if (_source == null)
                _source = GetComponent<AudioSource>();
            if (_source.isPlaying)
                throw new InvalidOperationException("The required AudioSource must be dedicated to realtime output and stopped before binding.");

            AudioSettings.GetDSPBufferSize(out int dspFrames, out _);
            _renderScratch = new float[checked(dspFrames * 2 * _output.Format.Channels)];
            Volatile.Write(ref _outputFault, null);
            // Keep a looping silent source alive. Pull speech at the actual DSP filter,
            // avoiding the streaming clip's additional read-ahead ring and false render receipts.
            _createdClip = AudioClip.Create("MNN Realtime Output", dspFrames, _output.Format.Channels,
                _output.Format.SampleRate, false);
            if (_createdClip == null)
                throw new InvalidOperationException("Unity failed to create the streaming output clip.");
            _createdClip.hideFlags = HideFlags.DontSave;
            _previousClip = _source.clip;
            _previousLoop = _source.loop;
            _previousPriority = _source.priority;
            _source.clip = _createdClip;
            _source.loop = true;
            _source.priority = 0;
            Volatile.Write(ref _outputRunning, 1);
            _source.Play();
        }

        /// <summary>Control thread only. Interrupts queued output and restores the AudioSource configuration.</summary>
        public void StopOutput()
        {
            EnsureMainThread();
            Volatile.Write(ref _outputRunning, 0);
            _output?.Interrupt();
            if (_source != null && _createdClip != null)
            {
                _source.Stop();
                if (_source.clip == _createdClip)
                {
                    _source.clip = _previousClip;
                    _source.loop = _previousLoop;
                    _source.priority = _previousPriority;
                }
            }
            if (_createdClip != null)
            {
                if (Application.isPlaying)
                    Destroy(_createdClip);
                else
                    DestroyImmediate(_createdClip);
                _createdClip = null;
            }
            _previousClip = null;
        }

        /// <summary>
        /// Capture adapter callback. Timestamp the first sample frame in current-process Stopwatch ticks,
        /// using the same monotonic clock as output observations, rather than the enqueue time.
        /// One producer only; this method copies into preallocated storage and never waits for the consumer.
        /// </summary>
        public bool TrySubmitCapture(float[] samples, int offset, int count, long monotonicTicks)
        {
            if (Volatile.Read(ref _captureEnabled) == 0)
            {
                Volatile.Write(ref _captureGap, 1);
                return false;
            }
            var capture = Volatile.Read(ref _capture);
            if (capture == null || samples == null || offset < 0 || count < 1 || offset > samples.Length - (long)count ||
                count > capture.MaximumSamples || count % _captureFormat.Channels != 0 || monotonicTicks < 0)
            {
                Volatile.Write(ref _captureGap, 1);
                return false;
            }

            long frames = count / _captureFormat.Channels;
            bool discontinuity = Interlocked.Exchange(ref _captureGap, 0) != 0;
            var info = new MNNAudioFrameInfo(_streamId, _formatEpoch, _captureSequence++, _captureStartFrame,
                monotonicTicks, _captureFormat, discontinuity);
            _captureStartFrame += frames;
            bool accepted = capture.TryWrite(samples, offset, count, info);
            if (!accepted)
                Volatile.Write(ref _captureGap, 1);
            return accepted;
        }

        /// <summary>Capture producer only. Marks a recording-ring overrun or source discontinuity.</summary>
        public void MarkCaptureDiscontinuity() => Volatile.Write(ref _captureGap, 1);

        /// <summary>Single processing consumer only. The caller reuses destination storage.</summary>
        public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count)
        {
            var capture = Volatile.Read(ref _capture);
            if (capture == null)
            {
                info = default;
                count = 0;
                return false;
            }
            return capture.TryRead(destination, offset, out info, out count);
        }

        /// <summary>Called by Unity's audio thread. Fills every requested sample without locks or allocation.</summary>
        public void RenderOutputPcm(float[] destination)
        {
            if (destination == null)
                return;
            var output = Volatile.Read(ref _output);
            if (Volatile.Read(ref _outputRunning) == 0 || output == null)
            {
                Array.Clear(destination, 0, destination.Length);
                return;
            }
            output.Render(destination, 0, destination.Length);
            Interlocked.Add(ref _renderedOutputFrames, destination.Length / output.Format.Channels);
        }

        /// <summary>Approximate Unity DSP buffering only; hardware and operating-system tail are not included.</summary>
        public double GetUnityDspBufferMilliseconds()
        {
            EnsureMainThread();
            if (_output == null)
                throw new InvalidOperationException("Bind an output pipeline before reading its DSP budget.");
            AudioSettings.GetDSPBufferSize(out int framesPerBuffer, out int bufferCount);
            return (double)framesPerBuffer * bufferCount * 1000.0 / AudioSettings.outputSampleRate;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            var output = Volatile.Read(ref _output);
            if (!IsOutputRunning || output == null) { Array.Clear(data, 0, data.Length); return; }
            int sourceChannels = output.Format.Channels;
            var scratch = _renderScratch;
            if (channels < 1 || data.Length % channels != 0 || scratch == null || scratch.Length < sourceChannels ||
                sourceChannels != 1 && sourceChannels != channels)
            {
                Array.Clear(data, 0, data.Length);
                Volatile.Write(ref _outputRunning, 0);
                Interlocked.CompareExchange(ref _outputFault,
                    new InvalidOperationException("Unity output DSP channel format does not match the negotiated output."), null);
                return;
            }
            int frames = data.Length / channels;
            // Route changes may increase the callback size. Render through the existing
            // bounded scratch in pieces, without allocating or restarting the PCM stream.
            for (int offset = 0; offset < frames;)
            {
                int count = Math.Min(scratch.Length / sourceChannels, frames - offset);
                output.Render(scratch, 0, count * sourceChannels);
                for (int frame = 0; frame < count; ++frame)
                    for (int channel = 0; channel < channels; ++channel)
                        data[(offset + frame) * channels + channel] = scratch[frame * sourceChannels + (sourceChannels == 1 ? 0 : channel)];
                offset += count;
            }
            Interlocked.Add(ref _renderedOutputFrames, frames);
        }

        private void OnDisable()
        {
            if (_mainThreadId != 0)
                StopOutput();
        }

        private void OnDestroy()
        {
            Volatile.Write(ref _capture, null);
            Volatile.Write(ref _output, null);
        }

        private void EnsureMainThread()
        {
            if (_mainThreadId == 0)
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("Unity audio device lifecycle methods must run on the component's main thread.");
        }
    }
}
