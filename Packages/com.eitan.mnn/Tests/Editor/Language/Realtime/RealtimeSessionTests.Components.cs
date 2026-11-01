using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class RealtimeSessionTests
    {
        [Test]
        public void UnityComponentDisableSilencesBeforeAsynchronousRuntimeShutdown()
        {
            var gameObject = new UnityEngine.GameObject("Realtime session component test");
            var stopListening = PendingTask();
            try
            {
                var component = gameObject.AddComponent<MNNRealtimeSessionComponent>();
                var runtime = new FakeRuntime{StopListening = stopListening.Task};
                var output = NewOutput();
                var preflight = CreateSupportedPreflight();
                component.Configure(runtime, output, preflight);
                Assert.AreSame(preflight, component.Preflight);
                Exception workerError = Task.Run(() =>
                {
                    try
                    {
                        component.InterruptAsync();
                    }
                    catch (Exception error)
                    {
                        return error;
                    }

                    return null;
                }).GetAwaiter().GetResult();
                Assert.IsInstanceOf<InvalidOperationException>(workerError);
                component.StartSessionAsync().GetAwaiter().GetResult();
                var response = output.BeginResponse();
                Assert.True(output.TryRegisterSegment(response, 0, "Speaking"));
                var audio = new float[80];
                Array.Fill(audio, .5f);
                Assert.True(output.TryEnqueue(audio, 0, audio.Length, new MNNSpeechChunkInfo(response, 0, 0, true)));
                typeof(MNNRealtimeSessionComponent).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
                output.Render(audio, 0, audio.Length);
                Assert.That(audio, Is.All.EqualTo(0f));
                AwaitCount(ref runtime.StopCount);
                Assert.AreEqual(1, runtime.StopCount);
                Assert.AreEqual(0, runtime.DisposeCount);
                var stop = component.StopSessionAsync();
                Assert.False(stop.IsCompleted);
                stopListening.TrySetResult(true);
                stop.GetAwaiter().GetResult();
                Assert.AreEqual(1, runtime.DisposeCount);
                Assert.AreEqual(MNNRealtimeSessionState.Stopped, component.State);
            }
            finally
            {
                stopListening.TrySetResult(true);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void UnityComponentRejectsMissingOrUnsupportedPreflightBeforeOwningRuntime()
        {
            var gameObject = new UnityEngine.GameObject("Realtime session preflight test");
            try
            {
                var component = gameObject.AddComponent<MNNRealtimeSessionComponent>();
                var runtime = new FakeRuntime();
                Assert.Throws<ArgumentNullException>(() => component.Configure(runtime, NewOutput(), null));
                var unsupported = MNNRealtimePreflight.CheckBundledBackend(MNNRealtimeMode.NativeDuplex, MNNSpeechRoute.OmniSpeech);
                Assert.Throws<NotSupportedException>(() => component.Configure(runtime, NewOutput(), unsupported));
                Assert.False(component.IsConfigured);
                Assert.AreEqual(0, runtime.Calls.Count);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void UnityComponentAcceptsSelectedCascadeThroughTheExistingSessionOwnershipPath()
        {
            var gameObject = new UnityEngine.GameObject("Cascade selection session test");
            var runtime = new FakeRuntime();
            try
            {
                var component = gameObject.AddComponent<MNNRealtimeSessionComponent>();
                const MNNRealtimeFeature binding = MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn;
                var model = new MNNRealtimeCapabilities(binding | MNNRealtimeFeature.TextInput | MNNRealtimeFeature.StableSpokenText | MNNRealtimeFeature.BoundedCancellation | MNNRealtimeFeature.ContextRecovery, "text-model/v1", "synthetic-session-test");
                var device = new MNNRealtimeCapabilities(MNNRealtimeFeature.DuplexDevice | MNNRealtimeFeature.EchoControl | MNNRealtimeFeature.PresentationClock, "device/v1", "synthetic-session-test");
                var tts = new MNNRealtimeCapabilities(binding | MNNRealtimeFeature.IndependentTts | MNNRealtimeFeature.StreamingSpeech | MNNRealtimeFeature.BoundedCancellation, "tts/v1", "synthetic-session-test", sustainedRtf: .7, sustainedRtfWindowAudioSeconds: 2);
                var asr = new MNNRealtimeCapabilities(binding | MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.SpeechRecognition | MNNRealtimeFeature.BoundedCancellation, "asr/v1", "synthetic-session-test");
                var vad = new MNNRealtimeCapabilities(binding | MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection | MNNRealtimeFeature.BoundedScheduling, "vad/v1", "synthetic-session-test");
                var unsupported = MNNRealtimeRouteSelector.SelectBest(model, device, tts);
                Assert.Throws<NotSupportedException>(() => component.Configure(runtime, NewOutput(), unsupported.RequireSelected()));
                Assert.False(component.IsConfigured);
                Assert.AreEqual(0, runtime.Calls.Count);
                var selection = MNNRealtimeRouteSelector.SelectBest(model, device, tts, asr, vad);
                component.Configure(runtime, NewOutput(), selection.RequireSelected());
                Assert.AreEqual(MNNRealtimeRoute.CascadedSpeech, component.Preflight.Route);
                Assert.AreSame(selection.Selected, component.Preflight);
                component.StartSessionAsync().GetAwaiter().GetResult();
                component.StopSessionAsync().GetAwaiter().GetResult();
                Assert.That(runtime.Calls, Is.EqualTo(new[]{"prepare", "listen", "stop", "join", "dispose"}));
                Assert.AreEqual(1, runtime.DisposeCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static MNNRealtimeReport CreateSupportedPreflight()
        {
            var model = new MNNRealtimeCapabilities(MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn | MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.BoundedCancellation | MNNRealtimeFeature.ContextRecovery | MNNRealtimeFeature.StreamingSpeech | MNNRealtimeFeature.SustainedRtfBelowOne, "test-model/v1", "deterministic-session-test", sustainedRtf: .8, sustainedRtfWindowAudioSeconds: 5);
            var device = new MNNRealtimeCapabilities(MNNRealtimeFeature.DuplexDevice | MNNRealtimeFeature.PresentationClock | MNNRealtimeFeature.EchoControl, "test-device/v1", "deterministic-session-test");
            return MNNRealtimePreflight.Check(MNNRealtimeMode.InterruptibleConversation, MNNSpeechRoute.OmniSpeech, model, device);
        }
    }
}
