using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using MNN.Examples;
using MNN.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.AudioAcceptance
{
    /// <summary>Opt-in acceptance of the imported standalone UI component with real local models and Unity audio.
    /// Configure MNN_STANDALONE_MODEL, MNN_STANDALONE_ASR and MNN_STANDALONE_TTS; no assets are downloaded.
    /// Recording content is uncontrolled: this verifies capture/ASR completion, not recognition accuracy or sound quality.</summary>
    public sealed class MNNStandaloneHardwareAcceptanceTests
    {
        private GameObject _root;
        private MNNStandaloneConversationExample _example;

        [UnityTest, Timeout(600000)]
        public IEnumerator StandaloneUiUsesLocalModelsPhysicalAudioAndRestart()
        {
            Assert.Greater(Microphone.devices.Length, 0, "A physical microphone is required.");
            _root = new GameObject("Standalone consumer acceptance");
            _example = _root.AddComponent<MNNStandaloneConversationExample>();
            Configure("modelDirectory", "MNN_STANDALONE_MODEL");
            Configure("recognitionDirectory", "MNN_STANDALONE_ASR");
            Configure("speechDirectory", "MNN_STANDALONE_TTS");
            string selected = Environment.GetEnvironmentVariable("MNN_STANDALONE_MICROPHONE");
            TestContext.WriteLine("Microphones: " + string.Join(" | ", Microphone.devices));
            if (!string.IsNullOrWhiteSpace(selected))
            {
                CollectionAssert.Contains(Microphone.devices, selected, "Configured microphone must be advertised by Unity.");
                SetInspectorField("microphoneDevice", selected);
                TestContext.WriteLine("Selected microphone: " + selected);
            }
            MNNConversationApplication previous = null;
            for (int cycle = 0; cycle < 2; ++cycle)
            {
                yield return Await(_example.StartConversationAsync(), 120);
                var app = _example.ApplicationOwner;
                Assert.AreNotSame(previous, app, "Restart must use a fresh application owner.");
                Assert.True(app.IsReady);
                var microphone = _root.GetComponentInChildren<MNNUnityMicrophoneCapture>();
                var device = _root.GetComponentInChildren<MNNRealtimeAudioDevice>();
                var source = device.GetComponent<AudioSource>();
                Assert.True(microphone.IsCapturing && device.IsOutputRunning && source.isPlaying);
                source.volume = .1f;

                var begin = _example.BeginRecordingAsync();
                yield return Await(begin, 30);
                Assert.True(begin.GetAwaiter().GetResult());
                Assert.True(app.Ports.Recording);
                long captureBefore = microphone.CapturedFrames;
                int halfSecondFrames = device.CaptureFormat.SampleRate / 2;
                float deadline = Time.realtimeSinceStartup + 5;
                while (microphone.CapturedFrames - captureBefore < halfSecondFrames && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.GreaterOrEqual(microphone.CapturedFrames - captureBefore, halfSecondFrames, "Physical microphone made no progress.");
                int transcriptBefore = app.Transcript.Count;
                var transcription = _example.FinishRecordingAsync(transcribeOnly: true);
                yield return Await(transcription, 120);
                Assert.NotNull(transcription.GetAwaiter().GetResult(), "Completed recognition returns a draft, which may be empty for silence.");
                Assert.False(app.Ports.Recording);
                Assert.AreEqual(transcriptBefore, app.Transcript.Count, "Transcribe-only must retain the visible conversation.");

                long completedBefore = app.Telemetry.CompletedCount;
                Task submission = _example.SendTextAsync("Say hello in one short English sentence.");
                bool nonzero = false, postFilterNonzero = false;
                var scratch = new float[1024];
                var points = new float[MNNPlaybackWaveform.PointCount];
                var filter = device.GetComponent<MNNSpeechOutputFilter>();
                Assert.NotNull(filter);
                deadline = Time.realtimeSinceStartup + 120;
                while (Time.realtimeSinceStartup < deadline && (!submission.IsCompleted || app.Telemetry.CompletedCount == completedBefore))
                {
                    if (submission.IsCompleted) submission.GetAwaiter().GetResult();
                    Assert.IsNull(app.Fault, app.Fault?.ToString());
                    // GetOutputData on the silent carrier AudioSource excludes this DSP injection.
                    // Observe both this response's post-filter waveform and the actual listener mix.
                    AudioListener.GetOutputData(scratch, 0);
                    foreach (float value in scratch)
                    {
                        Assert.False(float.IsNaN(value) || float.IsInfinity(value));
                        nonzero |= Math.Abs(value) > .000001f;
                    }
                    if (filter.Waveform.TryRead(points, app.Devices.Output.CurrentResponse, out float rms))
                    {
                        Assert.False(float.IsNaN(rms) || float.IsInfinity(rms));
                        postFilterNonzero |= rms > .000001f;
                    }
                    yield return null;
                }
                Assert.True(submission.IsCompleted, "Text submission deadline.");
                submission.GetAwaiter().GetResult();
                Assert.AreEqual(completedBefore + 1, app.Telemetry.CompletedCount, "Physical presentation did not finish the turn.");
                Assert.False(app.Telemetry.Latest.Failed);
                Assert.True(postFilterNonzero, "Local speech did not reach this response's post-filter waveform.");
                Assert.True(nonzero, "Local speech did not reach Unity's actual listener mix.");
                Assert.Greater(device.RenderedOutputFrames, 0);
                Assert.True(app.Devices.Output.CanBeginNextResponse(app.Devices.Output.CurrentResponse));
                Assert.AreEqual(transcriptBefore + 2, app.Transcript.Count);
                Assert.AreEqual(MNNChatRole.Assistant, app.Transcript[app.Transcript.Count - 1].Role);
                Assert.False(string.IsNullOrWhiteSpace(app.Transcript[app.Transcript.Count - 1].Text));
                TestContext.WriteLine("cycle=" + cycle + "; captured_frames=" + microphone.CapturedFrames +
                    "; rendered_frames=" + device.RenderedOutputFrames + "; completed_turns=" + app.Telemetry.CompletedCount +
                    "; presentation_tail_ms=" + app.Devices.PresentationTailMilliseconds);
                yield return Await(_example.StopConversationAsync(), 60);
                Assert.True(app.IsClosed);
                Assert.False(microphone.IsCapturing || device.IsOutputRunning || source.isPlaying);
                previous = app;
            }
        }

        private void Configure(string field, string variable)
        {
            string path = Environment.GetEnvironmentVariable(variable);
            Assert.True(!string.IsNullOrWhiteSpace(path) && Directory.Exists(path), "Set " + variable + " to an installed model.");
            SetInspectorField(field, path);
        }

        private void SetInspectorField(string field, string value)
        {
            typeof(MNNStandaloneConversationExample).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_example, value);
        }

        private static IEnumerator Await(Task operation, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!operation.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.True(operation.IsCompleted, "Standalone operation deadline.");
            operation.GetAwaiter().GetResult();
        }

        [UnityTearDown]
        public IEnumerator CloseOwnedApplication()
        {
            if (_example != null)
            {
                yield return Await(_example.StopConversationAsync(), 60);
                Assert.True(_example.ApplicationOwner == null || _example.ApplicationOwner.IsClosed);
            }
            if (_root != null) UnityEngine.Object.Destroy(_root);
            yield return null;
        }
    }
}
