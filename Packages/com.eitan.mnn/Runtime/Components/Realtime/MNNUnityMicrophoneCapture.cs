using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// Unity-managed microphone capture. The recording ring is read directly on the main thread
    /// into bounded PCM blocks, independently of speaker mixing, muting or voice virtualization. It does not provide echo cancellation or device certification.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MNN/Realtime/Unity Microphone Capture")]
    public sealed class MNNUnityMicrophoneCapture : MonoBehaviour
    {
        internal class MicrophoneApi
        {
            internal virtual string[] Devices => Microphone.devices;
            internal virtual void GetDeviceCaps(string name, out int minimum, out int maximum) =>
                Microphone.GetDeviceCaps(name, out minimum, out maximum);
            internal virtual AudioClip Start(string name, int seconds, int rate) =>
                Microphone.Start(name, loop: true, lengthSec: seconds, frequency: rate);
            internal virtual void End(string name) => Microphone.End(name);
            internal virtual int GetPosition(string name) => Microphone.GetPosition(name);
            internal virtual bool IsRecording(string name) => Microphone.IsRecording(name);
        }

        internal MicrophoneApi Input { get; set; } = new MicrophoneApi();

        private MNNRealtimeAudioDevice _device;
        private AudioClip _microphoneClip;
        private string _deviceName;
        private int _mainThreadId;
        private int _pendingStart;
        private int _capturing;
        private int _captureChannels;
        private int _captureRate;
        private int _clipLengthSeconds;
        private int _minimumDelayFrames;
        private int _readPosition;
        private float[] _readSamples;
        private long _lastPollTicks;
        private int _lastWritePosition = -1;
        private int _unreadFrames;
        private long _overrunFrames;
        public long OverrunFrames => Interlocked.Read(ref _overrunFrames);
        private long _capturedFrames;
        private long _sampleClockOrigin;
        private long _sampleClockFrames;
        private float _lastBlockRms;
        public float LastBlockRms => Volatile.Read(ref _lastBlockRms);
        public long CapturedFrames => Interlocked.Read(ref _capturedFrames);
        private float _startDeadline;
        private long _lastCaptureLagFrames;
        private bool _microphoneStarted;
        private Exception _fault;

        public bool IsCapturing => Volatile.Read(ref _capturing) != 0;
        public bool IsWaitingForBuffer => Volatile.Read(ref _pendingStart) != 0;
        public long CaptureLagFrames => Interlocked.Read(ref _lastCaptureLagFrames);
        public double CaptureLagMilliseconds => _captureRate > 0
            ? CaptureLagFrames * 1000.0 / _captureRate
            : 0.0;
        public string DeviceName => _deviceName;
        public Exception Fault => Volatile.Read(ref _fault);

        private void Awake()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>
        /// Starts a Unity microphone ring buffer. Permission must already be granted by the owning application.
        /// No AudioSource is needed for recording.
        /// </summary>
        public void StartCapture(MNNRealtimeAudioDevice device, string deviceName = null,
            int requestedSampleRate = 48000, int clipLengthSeconds = 1, int minimumDelayMilliseconds = 30)
        {
            StartCaptureNegotiated(device, null, deviceName, requestedSampleRate, clipLengthSeconds, minimumDelayMilliseconds);
        }

        /// <summary>
        /// Negotiates the raw microphone clip format before polling starts. The callback must bind the supplied
        /// device to this format and at least the reported sample capacity; runs on Unity's main thread.
        /// </summary>
        public void StartCaptureNegotiated(MNNRealtimeAudioDevice device, Action<MNNPcmFormat, int> prepareCapture,
            string deviceName = null, int requestedSampleRate = 48000, int clipLengthSeconds = 1,
            int minimumDelayMilliseconds = 30)
        {
            EnsureMainThread();
            if (!isActiveAndEnabled)
                throw new InvalidOperationException("The microphone component must be active before capture starts.");
            if (IsCapturing || IsWaitingForBuffer || _microphoneStarted || _microphoneClip != null)
                throw new InvalidOperationException("Microphone capture is already starting or active.");
            if (device == null)
                throw new ArgumentNullException(nameof(device));
            if (requestedSampleRate < 8000 || requestedSampleRate > 96000)
                throw new ArgumentOutOfRangeException(nameof(requestedSampleRate));
            if (clipLengthSeconds < 1 || clipLengthSeconds > 10)
                throw new ArgumentOutOfRangeException(nameof(clipLengthSeconds));
            if (minimumDelayMilliseconds < 0 || minimumDelayMilliseconds > 250)
                throw new ArgumentOutOfRangeException(nameof(minimumDelayMilliseconds));
            var devices = Input.Devices;
            if (devices == null || devices.Length == 0)
                throw new InvalidOperationException("Unity reports no available microphone devices.");
            // Resolve Unity's default once; subsequent polls and End must target the input we opened.
            if (string.IsNullOrEmpty(deviceName)) deviceName = devices[0];
            if (Array.IndexOf(devices, deviceName) < 0)
                throw new ArgumentException("The requested microphone device is not available.", nameof(deviceName));

            Input.GetDeviceCaps(deviceName, out int minimumRate, out int maximumRate);
            int sampleRate = SelectSampleRate(requestedSampleRate, minimumRate, maximumRate);
            AudioClip clip;
            _deviceName = deviceName;
            _microphoneStarted = true;
            try
            {
                clip = Input.Start(deviceName, clipLengthSeconds, sampleRate);
            }
            catch (Exception startError)
            {
                Exception cleanupError = TryEndMicrophone(deviceName);
                if (cleanupError != null)
                {
                    Volatile.Write(ref _fault, cleanupError);
                    throw new AggregateException("Microphone startup failed and the partial recording could not be stopped.", startError, cleanupError);
                }
                throw;
            }
            if (clip == null)
            {
                var startError = new InvalidOperationException("Unity failed to start the microphone recording clip.");
                Exception cleanupError = TryEndMicrophone(deviceName);
                if (cleanupError != null)
                {
                    Volatile.Write(ref _fault, cleanupError);
                    throw new AggregateException("Unity returned no microphone clip and stopping the partial recording failed.", startError, cleanupError);
                }
                throw startError;
            }
            _microphoneClip = clip;

            try
            {
                int channels = clip.channels;
                var format = new MNNPcmFormat(clip.frequency, channels);
                // Raw microphone samples retain their actual rate/channel count.
                // No AudioSource or output-device conversion participates in capture.
                int blockFrames = Math.Max(1, clip.frequency / 100);
                int queueBlockSamples = checked(blockFrames * channels);
                prepareCapture?.Invoke(format, queueBlockSamples);
                if (!device.CaptureFormat.Equals(format) || device.MaximumCaptureSamples < queueBlockSamples)
                    throw new InvalidOperationException("Bind the realtime device to the microphone clip's actual rate, channels and block capacity.");

                _device = device;
                _captureRate = clip.frequency;
                _captureChannels = channels;
                _clipLengthSeconds = clipLengthSeconds;
                _readSamples = new float[queueBlockSamples];
                _minimumDelayFrames = Math.Max(blockFrames, clip.frequency * minimumDelayMilliseconds / 1000);
                _minimumDelayFrames = Math.Min(_minimumDelayFrames, clip.samples / 4);
                ResetRecordingRing();
                Interlocked.Exchange(ref _overrunFrames, 0);
                Interlocked.Exchange(ref _capturedFrames, 0);
                Volatile.Write(ref _fault, null);
                _startDeadline = Time.realtimeSinceStartup + 5f;
                Volatile.Write(ref _pendingStart, 1);
            }
            catch (Exception setupError)
            {
                Exception cleanupError = TryEndMicrophone(deviceName);
                if (!_microphoneStarted && TryDestroyClip(clip, ref cleanupError))
                    _microphoneClip = null;
                _device = null;
                _captureRate = 0;
                _captureChannels = 0;
                if (cleanupError != null)
                    throw new AggregateException("Microphone setup failed and cleanup reported additional errors.", setupError, cleanupError);
                throw;
            }
        }

        /// <summary>Stops microphone recording without changing the owner's AudioSource. Control thread only.</summary>
        public void StopCapture()
        {
            EnsureMainThread();
            Volatile.Write(ref _pendingStart, 0);
            Volatile.Write(ref _capturing, 0);
            Exception failure = null;
            if (_microphoneStarted)
            {
                failure = TryEndMicrophone(_deviceName);
            }
            if (_microphoneClip != null && !_microphoneStarted)
            {
                if (TryDestroyClip(_microphoneClip, ref failure))
                    _microphoneClip = null;
            }
            _device = null;
            if (!_microphoneStarted)
                _deviceName = null;
            _captureRate = 0;
            _captureChannels = 0;
            _readSamples = null;
            Volatile.Write(ref _lastBlockRms, 0);
            Interlocked.Exchange(ref _lastCaptureLagFrames, 0);
            Volatile.Write(ref _fault, failure);
            if (failure != null)
                throw new InvalidOperationException("Microphone capture stopped with cleanup errors.", failure);
        }

        private void Update() => PollCapture(Time.realtimeSinceStartup, Stopwatch.GetTimestamp());

        /// <summary>Poll capture on the Unity main thread when driven by an Editor host. Player Update polls automatically.</summary>
        public void PollCapture(float now, long nowTicks)
        {
            if (!IsWaitingForBuffer && !IsCapturing) return;
            try
            {
                int writePosition = Input.GetPosition(_deviceName);
                if (!Input.IsRecording(_deviceName) || writePosition < 0)
                {
                    if (!IsWaitingForBuffer)
                    {
                        RestartRecording(now);
                        return;
                    }
                    if (now <= _startDeadline) return;
                    throw new InvalidOperationException("Unity microphone recording did not recover. Check microphone permission and the selected input.");
                }
                if (IsWaitingForBuffer)
                {
                    if (writePosition < _minimumDelayFrames)
                    {
                        if (now > _startDeadline)
                            throw new TimeoutException("Unity microphone recording did not advance. Check microphone permission and the selected input.");
                        return;
                    }
                    // Publish only fresh input after startup or slow model preparation.
                    _readPosition = writePosition - _minimumDelayFrames;
                    Volatile.Write(ref _pendingStart, 0);
                    Volatile.Write(ref _capturing, 1);
                }
                PumpCapture(writePosition, nowTicks);
            }
            catch (Exception error)
            {
                StopFromUnityLifecycle();
                Volatile.Write(ref _fault, Combine(error, Fault));
            }
        }

        private void RestartRecording(float now)
        {
            string name = _deviceName;
            var devices = Input.Devices;
            if (devices == null || Array.IndexOf(devices, name) < 0)
                throw new InvalidOperationException("The selected microphone was disconnected: " + name);

            // Unity can stop recording during an audio route reconfiguration. Reopen the same
            // device once, then use the normal bounded warmup instead of faulting on that poll.
            _device.MarkCaptureDiscontinuity();
            Volatile.Write(ref _capturing, 0);
            Exception cleanupError = TryEndMicrophone(name);
            if (cleanupError != null)
                throw new InvalidOperationException("Could not stop the interrupted microphone recording.", cleanupError);
            if (!TryDestroyClip(_microphoneClip, ref cleanupError))
                throw new InvalidOperationException("Could not release the interrupted microphone clip.", cleanupError);
            _microphoneClip = null;
            _deviceName = name;
            _microphoneStarted = true;
            _microphoneClip = Input.Start(name, _clipLengthSeconds, _captureRate);
            if (_microphoneClip == null)
                throw new InvalidOperationException("Unity failed to restart the microphone recording clip.");
            if (_microphoneClip.frequency != _captureRate || _microphoneClip.channels != _captureChannels)
                throw new InvalidOperationException("The microphone format changed during recovery. Restart the conversation for the new input format.");
            ResetRecordingRing();
            _startDeadline = now + 5f;
            Volatile.Write(ref _pendingStart, 1);
        }

        private void ResetRecordingRing()
        {
            _readPosition = 0;
            _lastPollTicks = 0;
            _lastWritePosition = -1;
            _unreadFrames = 0;
            _sampleClockOrigin = 0;
            _sampleClockFrames = 0;
            Volatile.Write(ref _lastBlockRms, 0);
            Interlocked.Exchange(ref _lastCaptureLagFrames, 0);
        }

        // One main-thread producer. AudioClip.GetData wraps reads at the ring boundary.
        // Kept separate from device polling so the actual PCM path can be tested without hardware.
        internal void PumpCapture(int writePosition, long nowTicks) => ReadCapture(writePosition, nowTicks, false);

        /// <summary>Main thread. Publish all samples captured at this recording boundary,
        /// waiting for bounded queue backpressure without stopping physical recording.</summary>
        public async Task FlushCaptureAsync(CancellationToken token)
        {
            EnsureMainThread();
            if (!IsCapturing) return;
            AudioClip recording = _microphoneClip;
            long overruns = OverrunFrames;
            ReadCapture(Input.GetPosition(_deviceName), Stopwatch.GetTimestamp(), true);
            long through = _sampleClockFrames + _unreadFrames;
            while (_sampleClockFrames < through)
            {
                token.ThrowIfCancellationRequested();
                await Task.Delay(1, token);
                if (!IsCapturing || !ReferenceEquals(recording, _microphoneClip) || OverrunFrames != overruns)
                    throw new InvalidOperationException("Microphone recording stopped, restarted or its ring was overwritten while draining.", Fault);
                ReadCapture(Input.GetPosition(_deviceName), Stopwatch.GetTimestamp(), true);
            }
        }

        private void ReadCapture(int writePosition, long nowTicks, bool flush)
        {
            int ringFrames = _microphoneClip.samples;
            int blockFrames = _readSamples.Length / _captureChannels;
            if (writePosition < 0 || writePosition >= ringFrames)
                throw new InvalidOperationException("Unity microphone position is outside its recording ring.");
            int advanced = _lastWritePosition < 0 ? (writePosition - _readPosition + ringFrames) % ringFrames :
                (writePosition - _lastWritePosition + ringFrames) % ringFrames;
            _unreadFrames += advanced;
            _lastWritePosition = writePosition;
            if (_lastPollTicks != 0 && nowTicks - _lastPollTicks >= (long)ringFrames * Stopwatch.Frequency / _captureRate ||
                _unreadFrames >= ringFrames)
            {
                // Beyond one full recording ring, overwritten PCM is unrecoverable.
                // Keep the most recent ring and explicitly mark that real loss.
                Interlocked.Add(ref _overrunFrames, Math.Max(blockFrames, _unreadFrames - (ringFrames - blockFrames)));
                _unreadFrames = ringFrames - blockFrames;
                _readPosition = (writePosition - _unreadFrames + ringFrames) % ringFrames;
                _device.MarkCaptureDiscontinuity();
                _sampleClockOrigin = 0;
                _sampleClockFrames = 0;
            }
            int available = _unreadFrames;
            _lastPollTicks = nowTicks;
            if (_sampleClockOrigin == 0)
                _sampleClockOrigin = nowTicks - (long)available * Stopwatch.Frequency / _captureRate;
            while (available >= blockFrames || flush && available > 0)
            {
                // Leave unconsumed PCM in the recording ring when the processing queue is full.
                // This preserves exact input across short scheduler/VAD stalls without dropping or waiting.
                if (_device.CaptureEnabled && !_device.CanSubmitCapture) break;
                int countFrames = Math.Min(blockFrames, available);
                // A partial block is only needed on an explicit recording boundary.
                var samples = countFrames == blockFrames ? _readSamples : new float[countFrames * _captureChannels];
                if (!_microphoneClip.GetData(samples, _readPosition))
                    throw new InvalidOperationException("Unity could not read microphone PCM from its recording buffer.");
                double energy = 0;
                for (int i = 0; i < samples.Length; ++i) energy += samples[i] * samples[i];
                Volatile.Write(ref _lastBlockRms, (float)Math.Sqrt(energy / samples.Length));
                long ticks = _sampleClockOrigin + _sampleClockFrames * Stopwatch.Frequency / _captureRate;
                _device.TrySubmitCapture(samples, 0, samples.Length, Math.Max(0, ticks));
                _sampleClockFrames += countFrames;
                Interlocked.Add(ref _capturedFrames, countFrames);
                _readPosition = (_readPosition + countFrames) % ringFrames;
                available -= countFrames;
            }
            _unreadFrames = available;
            Interlocked.Exchange(ref _lastCaptureLagFrames, available);
        }

        private void DestroyClip(AudioClip clip)
        {
            if (Application.isPlaying)
                Destroy(clip);
            else
                DestroyImmediate(clip);
        }

        private Exception TryEndMicrophone(string deviceName)
        {
            try
            {
                Input.End(deviceName);
                _microphoneStarted = false;
                _deviceName = null;
                return null;
            }
            catch (Exception error)
            {
                Volatile.Write(ref _fault, error);
                return error;
            }
        }

        private bool TryDestroyClip(AudioClip clip, ref Exception failure)
        {
            try
            {
                DestroyClip(clip);
                return true;
            }
            catch (Exception error)
            {
                failure = Combine(failure, error);
                return false;
            }
        }

        private static Exception Combine(Exception first, Exception next)
        {
            if (first == null) return next;
            if (next == null) return first;
            return new AggregateException(first, next);
        }

        private void StopFromUnityLifecycle()
        {
            try { StopCapture(); }
            catch (Exception error) { Volatile.Write(ref _fault, error); }
        }

        private void OnDisable()
        {
            if (_mainThreadId != 0)
                StopFromUnityLifecycle();
        }

        private void OnDestroy()
        {
            if (_mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId)
                StopFromUnityLifecycle();
        }

        internal static int SelectSampleRate(int requested, int minimum, int maximum)
        {
            if (requested < 8000 || requested > 96000)
                throw new ArgumentOutOfRangeException(nameof(requested));
            if (minimum < 0 || maximum < 0)
                throw new ArgumentOutOfRangeException(nameof(minimum));
            if (minimum == 0 && maximum == 0)
                return requested;
            minimum = Math.Max(8000, minimum);
            maximum = Math.Min(96000, maximum);
            if (minimum > maximum)
                throw new InvalidOperationException("Microphone reports no supported sample rate in the Unity audio range.");
            return Math.Max(minimum, Math.Min(maximum, requested));
        }

        private void EnsureMainThread()
        {
            if (_mainThreadId == 0)
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("Microphone lifecycle methods must run on the component's main thread.");
        }
    }
}
