using System;
using System.Collections;
using System.Threading;
using MNN.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.AudioAcceptance
{
    /// <summary>Opt-in physical capture and Unity DSP acceptance. Import into an isolated test project.
    /// Does not qualify recognition, perceptual speech quality, acoustic AEC or native duplex inference.</summary>
    public sealed class MNNUnityAudioAcceptanceTests
    {
        private GameObject _input, _speaker, _listener;
        private MNNRealtimeDeviceSession _session;

        [UnityTest, Timeout(60000)]
        public IEnumerator ActualUnityDevicesNegotiateCaptureRenderAndCloseTwice()
        {
            Assert.Greater(Microphone.devices.Length, 0, "Real microphone required.");
            _listener = new GameObject("Acceptance listener", typeof(AudioListener));
            _input = new GameObject("Acceptance microphone");
            var microphone = _input.AddComponent<MNNUnityMicrophoneCapture>();
            _speaker = new GameObject("Acceptance speaker");
            var source = _speaker.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.pitch = 1;
            source.volume = .1f;
            var device = _speaker.AddComponent<MNNRealtimeAudioDevice>();
            var binding = new MNNUnityAudioDeviceBinding(microphone, device);
            for (int cycle = 0; cycle < 2; ++cycle)
            {
                _session = new MNNRealtimeDeviceSession(binding);
                var opening = _session.OpenAsync(CancellationToken.None);
                float deadline = Time.realtimeSinceStartup + 15;
                while (!opening.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(opening.IsCompleted, "Audio startup deadline.");
                var output = opening.GetAwaiter().GetResult();
                Assert.IsTrue(_session.IsReady);
                _session.SetCaptureEnabled(true);
                var response = output.BeginResponse();
                Assert.IsTrue(output.TryRegisterSegment(response, 1, "Physical DSP acceptance"));
                int frames = output.Format.SampleRate / 100;
                var pcm = new float[frames];
                int through = 0;
                for (int block = 0; block < 10; ++block)
                {
                    for (int i = 0; i < frames; ++i)
                        pcm[i] = .02f * (float)Math.Sin((block * frames + i) * 2 * Math.PI * 440 / output.Format.SampleRate);
                    Assert.IsTrue(output.TryEnqueue(pcm, 0, frames, new MNNSpeechChunkInfo(response, 1, block, block == 9)));
                    through += frames;
                }
                Assert.IsTrue(output.CompleteResponse(response));
                int captured = 0;
                var buffer = new float[binding.Capture.MaximumCaptureSamples];
                deadline = Time.realtimeSinceStartup + 8;
                while (Time.realtimeSinceStartup < deadline)
                {
                    _session.Tick();
                    while (binding.Capture.TryReadCapture(buffer, 0, out _, out int count))
                    {
                        Assert.Greater(count, 0);
                        for (int i = 0; i < count; ++i)
                            Assert.IsFalse(float.IsNaN(buffer[i]) || float.IsInfinity(buffer[i]));
                        captured += count;
                    }
                    if (captured > 0 && output.CanBeginNextResponse(response)) break;
                    yield return null;
                }
                Assert.IsNull(_session.Fault, "Device supervision failed: " + _session.Fault);
                Assert.Greater(captured, 0, "Real Unity microphone delivered no PCM.");
                Assert.GreaterOrEqual(device.RenderedOutputFrames, through, "DSP did not consume output frames.");
                Assert.IsTrue(output.CanBeginNextResponse(response), "Output presentation receipts did not advance.");
                TestContext.WriteLine("cycle=" + cycle + "; captured_samples=" + captured +
                    "; rendered_frames=" + device.RenderedOutputFrames + "; capture_rate=" + binding.Capture.CaptureFormat.SampleRate +
                    "; output_rate=" + output.Format.SampleRate + "; dsp_tail_ms=" + _session.PresentationTailMilliseconds);
                var close = _session.CloseAsync();
                while (!close.IsCompleted) yield return null;
                close.GetAwaiter().GetResult();
                Assert.IsTrue(_session.IsClosed);
                Assert.IsFalse(microphone.IsCapturing || device.IsOutputRunning || source.isPlaying);
                _session = null;
                binding.ResetForRetry();
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_session != null)
            {
                var close = _session.CloseAsync();
                while (!close.IsCompleted) yield return null;
                close.GetAwaiter().GetResult();
                _session = null;
            }
            if (_input != null) UnityEngine.Object.Destroy(_input);
            if (_speaker != null) UnityEngine.Object.Destroy(_speaker);
            if (_listener != null) UnityEngine.Object.Destroy(_listener);
            yield return null;
        }
    }
}
