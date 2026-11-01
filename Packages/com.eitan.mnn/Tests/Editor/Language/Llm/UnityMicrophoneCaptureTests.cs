using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class UnityMicrophoneCaptureTests
    {
        private GameObject _gameObject;
        private AudioSource _source;
        private MNNUnityMicrophoneCapture _capture;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("Unity microphone capture test");
            _source = _gameObject.AddComponent<AudioSource>();
            _capture = _gameObject.AddComponent<MNNUnityMicrophoneCapture>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void DefaultInputStaysPinnedWhenTheSystemDefaultChanges()
        {
            var input = StartSyntheticCapture();
            Assert.AreEqual("Built-in", _capture.DeviceName);
            input.Available = new[] { "Headset", "Built-in" };
            Poll(0);
            Assert.AreEqual("Built-in", input.LastPolledDevice);
            _capture.StopCapture();
            Assert.AreEqual("Built-in", input.LastEndedDevice);
        }

        [TestCase(false), TestCase(true)]
        public void InterruptedRecordingRestartsAndPublishesFreshDiscontinuousPcm(bool negativePosition)
        {
            var input = StartSyntheticCapture();
            Poll(0);
            var device = _gameObject.GetComponent<MNNRealtimeAudioDevice>();
            var samples = new float[160];
            Assert.True(device.TryReadCapture(samples, 0, out var before, out int count));
            Assert.AreEqual(160, count);
            Assert.That(samples, Is.All.EqualTo(.25f));
            Assert.False(before.Discontinuity);

            input.Recording = negativePosition;
            if (negativePosition) input.Position = -1;
            Poll(1);
            Assert.IsNull(_capture.Fault);
            Assert.True(_capture.IsWaitingForBuffer);
            Assert.False(_capture.IsCapturing);
            Assert.AreEqual(2, input.Starts);
            Assert.AreEqual(1, input.Ends);
            Assert.AreEqual("Built-in", input.LastStartedDevice);
            Assert.AreEqual(16000, input.LastRate);
            Assert.AreEqual(1, input.LastSeconds);

            input.Position = 320;
            Poll(2);
            Assert.True(_capture.IsCapturing);
            Assert.False(_capture.IsWaitingForBuffer);
            Assert.True(device.TryReadCapture(samples, 0, out var after, out count));
            Assert.That(samples, Is.All.EqualTo(.75f), "Never replay PCM from the stopped ring.");
            Assert.True(after.Discontinuity, "Downstream VAD/AEC must reset across the recording gap.");
            Assert.Greater(after.MonotonicTicks, before.MonotonicTicks);
            Assert.AreEqual(320, _capture.CapturedFrames);
        }

        [Test]
        public void RestartThatNeverRecordsFaultsWithinTheOriginalRecoveryDeadline()
        {
            var input = StartSyntheticCapture();
            Poll(0);
            input.Recording = false;
            Poll(1);
            input.Recording = false;
            Poll(3);
            Assert.IsNull(_capture.Fault);
            Assert.True(_capture.IsWaitingForBuffer);
            Poll(7);
            Assert.IsInstanceOf<InvalidOperationException>(_capture.Fault);
            Assert.False(_capture.IsCapturing);
            Assert.False(_capture.IsWaitingForBuffer);
            Assert.AreEqual(2, input.Starts, "Do not restart every poll or extend the deadline forever.");
            Assert.AreEqual(2, input.Ends);
        }

        [Test]
        public void RestartThatRecordsButNeverAdvancesTimesOut()
        {
            var input = StartSyntheticCapture();
            Poll(0);
            input.Recording = false;
            Poll(1);
            Poll(7);
            Assert.IsInstanceOf<TimeoutException>(_capture.Fault);
            Assert.False(_capture.IsCapturing);
            Assert.AreEqual(2, input.Starts);
            Assert.AreEqual(2, input.Ends);
        }

        [Test]
        public void RemovedSelectedInputFaultsWithoutSwitchingToAnotherMicrophone()
        {
            var input = StartSyntheticCapture();
            Poll(0);
            input.Available = new[] { "Headset" };
            input.Recording = false;
            Poll(1);
            Assert.IsInstanceOf<InvalidOperationException>(_capture.Fault);
            Assert.False(_capture.IsCapturing);
            Assert.AreEqual(1, input.Starts);
            Assert.AreEqual("Built-in", input.LastEndedDevice);
        }

        [Test]
        public void RestartWithChangedFormatFaultsBeforePublishingMismatchedPcm()
        {
            var input = StartSyntheticCapture();
            Poll(0);
            input.Recording = false;
            input.NextRate = 48000;
            Poll(1);
            Assert.IsInstanceOf<InvalidOperationException>(_capture.Fault);
            StringAssert.Contains("format", _capture.Fault.Message);
            Assert.AreEqual(2, input.Starts);
            Assert.AreEqual(2, input.Ends);
            Assert.False(_capture.IsWaitingForBuffer);
        }

        [Test]
        public void StopDuringRecoveryCancelsPendingRecordingAndDoesNotRestart()
        {
            var input = StartSyntheticCapture();
            Poll(0);
            input.Recording = false;
            Poll(1);
            _capture.StopCapture();
            Poll(2);
            Assert.IsNull(_capture.Fault);
            Assert.False(_capture.IsCapturing);
            Assert.False(_capture.IsWaitingForBuffer);
            Assert.AreEqual(2, input.Starts);
            Assert.AreEqual(2, input.Ends);
        }

        [TestCase(false), TestCase(true)]
        public void FailedRestartCleansUpThePartialRecording(bool throws)
        {
            var input = StartSyntheticCapture();
            Poll(0);
            input.Recording = false;
            input.FailStart = true;
            input.ThrowOnStart = throws;
            Poll(1);
            Assert.IsInstanceOf<InvalidOperationException>(_capture.Fault);
            Assert.False(_capture.IsCapturing);
            Assert.False(_capture.IsWaitingForBuffer);
            Assert.AreEqual(2, input.Starts);
            Assert.AreEqual(2, input.Ends, "A partial failed restart must also be ended.");
        }

        [Test]
        public void InitialWarmupWaitsWithoutRestartingAndStillTimesOut()
        {
            var input = StartSyntheticCapture();
            input.Position = 0;
            input.Recording = false;
            Poll(1);
            Assert.True(_capture.IsWaitingForBuffer);
            Assert.IsNull(_capture.Fault);
            Assert.AreEqual(1, input.Starts);
            Poll(6);
            Assert.IsNotNull(_capture.Fault);
            Assert.AreEqual(1, input.Starts);
            Assert.AreEqual(1, input.Ends);
        }

        [UnityEngine.TestTools.UnityTest]
        public System.Collections.IEnumerator DrainRejectsAReplacementRecordingRing()
        {
            var input = StartSyntheticCapture();
            Poll(0); // Fill the one-block publication queue.
            input.Position = 480;
            using (var cancellation = new System.Threading.CancellationTokenSource())
            {
                Task drain = _capture.FlushCaptureAsync(cancellation.Token);
                try
                {
                    Assert.False(drain.IsCompleted, "The old ring is waiting for queue space.");
                    input.Recording = false;
                    Poll(1);
                    input.Position = 320;
                    Poll(2); // Recording recovers before the pending drain resumes.
                    float deadline = Time.realtimeSinceStartup + 1;
                    while (!drain.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.True(drain.IsFaulted, "A drain must reject the replaced ring, not wait for its old sample count.");
                    Assert.IsInstanceOf<InvalidOperationException>(drain.Exception.GetBaseException());
                }
                finally { cancellation.Cancel(); }
            }
        }

        private float _startedAt;
        private long _startedTicks;

        private SyntheticMicrophone StartSyntheticCapture()
        {
            var input = new SyntheticMicrophone();
            _capture.Input = input;
            var device = _gameObject.AddComponent<MNNRealtimeAudioDevice>();
            device.Bind(new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160),
                new MNNPcmFormat(16000, 1), captureQueueBlocks: 1, maximumCaptureSamplesPerBlock: 160);
            _capture.StartCapture(device, requestedSampleRate: 16000, minimumDelayMilliseconds: 10);
            _startedAt = Time.realtimeSinceStartup;
            _startedTicks = Stopwatch.GetTimestamp();
            input.Position = 320;
            return input;
        }

        private void Poll(int seconds) => _capture.PollCapture(_startedAt + seconds,
            _startedTicks + seconds * Stopwatch.Frequency);

        private sealed class SyntheticMicrophone : MNNUnityMicrophoneCapture.MicrophoneApi
        {
            internal string[] Available = { "Built-in", "Headset" };
            internal bool Recording;
            internal int Position;
            internal int Starts;
            internal int Ends;
            internal int LastRate;
            internal int LastSeconds;
            internal int NextRate;
            internal bool FailStart;
            internal bool ThrowOnStart;
            internal string LastStartedDevice;
            internal string LastPolledDevice;
            internal string LastEndedDevice;
            internal override string[] Devices => Available;
            internal override void GetDeviceCaps(string name, out int minimum, out int maximum) => minimum = maximum = 0;
            internal override AudioClip Start(string name, int seconds, int rate)
            {
                LastStartedDevice = name;
                LastRate = rate;
                LastSeconds = seconds;
                ++Starts;
                Recording = true;
                Position = 0;
                if (FailStart)
                {
                    if (ThrowOnStart) throw new InvalidOperationException("Synthetic restart failure.");
                    return null;
                }
                rate = NextRate == 0 ? rate : NextRate;
                var clip = AudioClip.Create("Synthetic microphone ring", rate * seconds, 1, rate, false);
                var pcm = new float[clip.samples];
                Array.Fill(pcm, Starts == 1 ? .25f : .75f);
                clip.SetData(pcm, 0);
                return clip;
            }
            internal override void End(string name) { ++Ends; LastEndedDevice = name; Recording = false; }
            internal override int GetPosition(string name) { LastPolledDevice = name; return Position; }
            internal override bool IsRecording(string name) => Recording;
        }

        [Test]
        public void StopWithoutStartingLeavesTheOwnersAudioSourceUntouched()
        {
            var clip = AudioClip.Create("Owner clip", 32, 1, 16000, false);
            _source.clip = clip;
            _source.loop = false;
            _source.playOnAwake = true;
            _source.spatialBlend = .5f;
            _source.volume = .37f;

            _capture.StopCapture();

            Assert.AreSame(clip, _source.clip);
            Assert.False(_source.loop);
            Assert.True(_source.playOnAwake);
            Assert.AreEqual(.5f, _source.spatialBlend);
            Assert.AreEqual(.37f, _source.volume, .0001f);
            UnityEngine.Object.DestroyImmediate(clip);
        }

        [Test]
        public void SampleRateSelectionHonorsUnspecifiedAndConstrainedDeviceCaps()
        {
            Assert.AreEqual(48000, MNNUnityMicrophoneCapture.SelectSampleRate(48000, 0, 0));
            Assert.AreEqual(32000, MNNUnityMicrophoneCapture.SelectSampleRate(48000, 16000, 32000));
            Assert.AreEqual(16000, MNNUnityMicrophoneCapture.SelectSampleRate(8000, 16000, 48000));
            Assert.AreEqual(96000, MNNUnityMicrophoneCapture.SelectSampleRate(96000, 0, 0));
            Assert.Throws<InvalidOperationException>(() => MNNUnityMicrophoneCapture.SelectSampleRate(48000, 96000, 16000));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNUnityMicrophoneCapture.SelectSampleRate(48000, -1, 48000));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNUnityMicrophoneCapture.SelectSampleRate(7000, 0, 0));
        }

        [Test]
        public void StartValidationFailsBeforePermissionOrDeviceEnumeration()
        {
            Assert.Throws<ArgumentNullException>(() => _capture.StartCapture(null));
            var device = _gameObject.AddComponent<MNNRealtimeAudioDevice>();
            Assert.Throws<ArgumentOutOfRangeException>(() => _capture.StartCapture(device, requestedSampleRate: 7000));
            Assert.Throws<ArgumentOutOfRangeException>(() => _capture.StartCapture(device, clipLengthSeconds: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _capture.StartCapture(device, minimumDelayMilliseconds: 251));
            Assert.False(_capture.IsWaitingForBuffer);
            Assert.False(_capture.IsCapturing);
        }

        [Test]
        public void UnityLifecycleCallsRejectWorkerThreadUseBeforeTouchingUnityObjects()
        {
            _capture.StopCapture();
            Exception observed = Task.Run(() =>
            {
                try { _capture.StopCapture(); }
                catch (Exception error) { return error; }
                return null;
            }).GetAwaiter().GetResult();

            Assert.IsInstanceOf<InvalidOperationException>(observed);
        }
    }
}
