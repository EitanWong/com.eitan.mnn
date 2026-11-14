using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    /// <summary>Managed factory/lifecycle integration only; no native ABI, device or model qualification.</summary>
    public sealed class RealtimeFallbackRuntimeTests
    {
        [Test]
        public void TextReplyFallbackRetainsDisabledSpeechThroughEverySelection()
        {
            Run(async () =>
            {
                var factory = new Factory();
                factory.Create = (report, output, token) =>
                {
                    Assert.IsFalse(report.ProducesSpeech);
                    Assert.IsFalse(report.RequiresMeasuredSpeech);
                    Assert.IsNull(report.TtsContract);
                    if (report.Route == MNNRealtimeRoute.AudioTextToSpeech)
                        return Task.FromException<IMNNRealtimeSessionRuntime>(Unavailable(report.Route));
                    Assert.AreEqual(MNNRealtimeRoute.CascadedSpeech, report.Route);
                    return Ready(new Runtime("text", factory.Trace));
                };
                var model = Caps(MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn |
                    MNNRealtimeFeature.TextInput | MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.ContextRecovery |
                    MNNRealtimeFeature.BoundedCancellation);
                var asr = Caps(MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn |
                    MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.SpeechRecognition | MNNRealtimeFeature.BoundedCancellation);
                var vad = Caps(MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn |
                    MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection | MNNRealtimeFeature.BoundedScheduling);
                var runtime = new MNNRealtimeFallbackRuntime(factory,
                    new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160), model,
                    Caps(MNNRealtimeFeature.DuplexDevice), asr: asr, vad: vad, speechOutput: false);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                await session.StartAsync();
                Assert.AreEqual(2, factory.CreateCount);
                Assert.AreEqual(MNNRealtimeRoute.CascadedSpeech, runtime.CurrentReport.Route);
                await session.StopAsync();
            });
        }

        [TestCase("factory")]
        [TestCase("prepare")]
        [TestCase("listen")]
        public void ClassifiedStartupFailureReleasesOldOwnerBeforeNextFactory(string phase)
        {
            Run(async () =>
            {
                var factory = new Factory();
                var first = new Runtime("first", factory.Trace);
                var second = new Runtime("second", factory.Trace);
                var error = Unavailable(MNNRealtimeRoute.NativeDuplexSpeech);
                if (phase == "prepare") first.Prepare = token => Fail(error);
                if (phase == "listen") first.Listen = token => Fail(error);
                factory.Create = (report, output, token) => report.Route == MNNRealtimeRoute.NativeDuplexSpeech
                    ? (phase == "factory" ? Task.FromException<IMNNRealtimeSessionRuntime>(error) : Ready(first))
                    : Ready(second);
                var runtime = NewFallback(factory);
                var initial = runtime.Selection;
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                await session.StartAsync();
                Assert.AreEqual(MNNRealtimeRoute.NativeDuplexTextToSpeech, runtime.CurrentReport.Route);
                Assert.AreEqual(MNNRealtimeRoute.NativeDuplexSpeech, initial.Selected.Route);
                Assert.AreSame(error, runtime.Failures.Single());
                Assert.AreEqual(MNNRealtimeSessionState.Running, session.State);
                var trace = factory.Trace.ToArray();
                if (phase != "factory")
                {
                    Assert.Less(Array.IndexOf(trace, "first.stop"), Array.IndexOf(trace, "first.join"));
                    Assert.Less(Array.IndexOf(trace, "first.join"), Array.IndexOf(trace, "first.dispose"));
                    Assert.Less(Array.IndexOf(trace, "first.dispose"), Array.IndexOf(trace, "create.NativeDuplexTextToSpeech"));
                }
                Assert.AreEqual(1, trace.Count(value => value == "second.prepare"));
                Assert.AreEqual(1, trace.Count(value => value == "second.listen"));
                await session.StopAsync();
                Assert.AreEqual(1, second.DisposeCount);
            });
        }

        [Test]
        public void EveryRejectedRouteIsAttemptedOnceAndExhaustionRetainsReasons()
        {
            Run(async () =>
            {
                var factory = new Factory();
                factory.Create = (report, output, token) =>
                    Task.FromException<IMNNRealtimeSessionRuntime>(Unavailable(report.Route));
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                var error = await Capture(session.StartAsync());
                Assert.IsInstanceOf<NotSupportedException>(error);
                Assert.AreEqual(5, runtime.Failures.Count);
                Assert.AreEqual(5, factory.Trace.Count);
                Assert.False(runtime.Selection.CanStart);
                Assert.Null(runtime.CurrentReport);
                Assert.AreEqual(5, runtime.Selection.ExcludedRoutes.Count);
                foreach (MNNRealtimeRoute route in Enum.GetValues(typeof(MNNRealtimeRoute)))
                    if (route != MNNRealtimeRoute.TextChat) StringAssert.Contains(route.ToString(), error.Message);
                Assert.Throws<NotSupportedException>(() =>
                    ((IList<MNNRealtimeRouteUnavailableException>)runtime.Failures).Clear());
                await session.StopAsync();
            });
        }

        [TestCase("permission")]
        [TestCase("cancellation")]
        [TestCase("abi")]
        [TestCase("unknown")]
        [TestCase("wrong-route")]
        public void UnclassifiedOrMismatchedErrorsNeverTryAnotherRoute(string kind)
        {
            Run(async () =>
            {
                Exception error = kind == "permission" ? new UnauthorizedAccessException("Microphone denied") :
                    kind == "cancellation" ? new OperationCanceledException() :
                    kind == "abi" ? new PlatformNotSupportedException("No ABI") :
                    kind == "wrong-route" ? (Exception)Unavailable(MNNRealtimeRoute.CascadedSpeech) :
                    new InvalidOperationException("Unknown failure");
                var factory = new Factory();
                var first = new Runtime("first", factory.Trace) { Prepare = token => Fail(error) };
                factory.Create = (report, output, token) => Ready(first);
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                Assert.AreSame(error, await Capture(session.StartAsync()));
                Assert.AreEqual(1, factory.CreateCount);
                Assert.AreEqual(0, runtime.Failures.Count);
                Assert.AreEqual(1, first.DisposeCount);
                await session.StopAsync();
            });
        }

        [Test]
        public void FactoryCompletionRacingCancellationStillTransfersOwnershipForCleanup()
        {
            Run(async () =>
            {
                var cancellation = new CancellationTokenSource();
                var factory = new Factory();
                var first = new Runtime("first", factory.Trace);
                factory.Create = (report, output, token) =>
                {
                    cancellation.Cancel();
                    return Ready(first);
                };
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                Assert.IsInstanceOf<OperationCanceledException>(await Capture(session.StartAsync(cancellation.Token)));
                Assert.False(factory.Trace.Contains("first.prepare"));
                Assert.AreEqual(1, first.DisposeCount);
                Assert.AreEqual(1, factory.CreateCount);
                await session.StopAsync();
                cancellation.Dispose();
            });
        }

        [TestCase("stop")]
        [TestCase("join")]
        [TestCase("dispose")]
        public void ReleaseFailureBlocksFallbackAndRetainsOwnerUntilSafeCleanup(string phase)
        {
            Run(async () =>
            {
                var factory = new Factory();
                var first = new Runtime("first", factory.Trace)
                {
                    Prepare = token => Fail(Unavailable(MNNRealtimeRoute.NativeDuplexSpeech))
                };
                var failure = new InvalidOperationException("Cleanup failed");
                // Keep failing through Session's startup cleanup, then allow explicit Stop to retry.
                if (phase == "stop") first.Stop = () => Fail(failure);
                if (phase == "join") first.Join = () => Fail(failure);
                if (phase == "dispose") first.Dispose = () => Fail(failure);
                factory.Create = (report, output, token) => Ready(first);
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                Assert.IsInstanceOf<AggregateException>(await Capture(session.StartAsync()));
                Assert.AreEqual(1, factory.CreateCount);
                if (phase == "join") Assert.AreEqual(0, first.DisposeCount);
                if (phase == "stop") Assert.AreEqual(1, first.DisposeCount);
                first.Stop = first.Join = first.Dispose = () => Task.CompletedTask;
                await session.StopAsync();
                Assert.AreEqual(1, factory.CreateCount);
                Assert.AreEqual(phase == "dispose" ? 3 : 1, first.DisposeCount);
            });
        }

        [Test]
        public void FallbackWaitsForGlobalQuiescenceAndCancellationPreventsNextFactory()
        {
            Run(async () =>
            {
                var joined = Signal();
                var entered = Signal();
                var cancellation = new CancellationTokenSource();
                var factory = new Factory();
                var first = new Runtime("first", factory.Trace)
                {
                    Prepare = token => Fail(Unavailable(MNNRealtimeRoute.NativeDuplexSpeech)),
                    Join = () => { entered.TrySetResult(true); return joined.Task; }
                };
                factory.Create = (report, output, token) => Ready(first);
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                var start = session.StartAsync(cancellation.Token);
                await Within(entered.Task);
                Assert.AreEqual(1, factory.CreateCount);
                Assert.AreEqual(0, first.DisposeCount);
                Assert.False(start.IsCompleted);
                cancellation.Cancel();
                joined.TrySetResult(true);
                Assert.IsInstanceOf<OperationCanceledException>(await Capture(start));
                Assert.AreEqual(1, factory.CreateCount);
                Assert.AreEqual(1, first.DisposeCount);
                await session.StopAsync();
                cancellation.Dispose();
            });
        }

        [Test]
        public void RecoveryFallbackKeepsOutputLedgerAndDoesNotReplayInterruptedResponse()
        {
            Run(async () =>
            {
                var join = Signal();
                var entered = Signal();
                var factory = new Factory();
                var first = new Runtime("first", factory.Trace)
                {
                    Restore = () => Fail(Unavailable(MNNRealtimeRoute.NativeDuplexSpeech)),
                    Join = () => { entered.TrySetResult(true); return join.Task; }
                };
                var second = new Runtime("second", factory.Trace);
                MNNRealtimeOutputPipeline reused = null;
                factory.Create = (report, output, token) =>
                {
                    if (report.Route == MNNRealtimeRoute.NativeDuplexSpeech) return Ready(first);
                    reused = output;
                    Assert.True(output.GetSegment(0).IsPresented);
                    Assert.False(output.GetSegment(1).IsPresented);
                    return Ready(second);
                };
                var runtime = NewFallback(factory);
                var outputPipeline = runtime.Output;
                var session = new MNNRealtimeSession(runtime, outputPipeline);
                await session.StartAsync();
                var response = session.BeginResponse();
                var pcm = Enumerable.Repeat(.5f, 80).ToArray();
                Assert.True(outputPipeline.TryRegisterSegment(response, 0, "Already heard."));
                Assert.True(outputPipeline.TryEnqueue(pcm, 0, pcm.Length, new MNNSpeechChunkInfo(response, 0, 0, true)));
                var rendered = new float[80];
                outputPipeline.Render(rendered, 0, 80);
                outputPipeline.DrainReceipts(8);
                outputPipeline.AcknowledgePresentation(80, false);
                Assert.True(outputPipeline.TryRegisterSegment(response, 1, "Not heard."));
                Assert.True(outputPipeline.TryEnqueue(pcm, 0, pcm.Length, new MNNSpeechChunkInfo(response, 1, 0, true)));

                var recovering = session.InterruptAsync();
                outputPipeline.Render(rendered, 0, 80);
                Assert.That(rendered, Is.All.EqualTo(0f));
                await Within(entered.Task);
                Assert.AreEqual(1, factory.CreateCount);
                Assert.AreEqual(0, first.DisposeCount);
                Assert.AreEqual(MNNRealtimeSessionState.Recovering, session.State);
                join.TrySetResult(true);
                await Within(recovering);
                Assert.AreSame(outputPipeline, reused);
                Assert.AreEqual(MNNRealtimeSessionState.Running, session.State);
                Assert.False(outputPipeline.Accepts(response));
                Assert.AreEqual(2, outputPipeline.SegmentCount, "No generated response was replayed.");
                Assert.AreNotEqual(response, session.BeginResponse());
                var trace = factory.Trace.ToArray();
                Assert.Less(Array.IndexOf(trace, "first.cancel"), Array.IndexOf(trace, "first.inference-join"));
                Assert.Less(Array.IndexOf(trace, "first.inference-join"), Array.IndexOf(trace, "first.restore"));
                Assert.Less(Array.IndexOf(trace, "first.dispose"), Array.IndexOf(trace, "create.NativeDuplexTextToSpeech"));
                await session.StopAsync();
            });
        }

        [Test]
        public void OrdinaryInterruptionUsesSameRuntimeAndKeepsFailureSnapshotEmpty()
        {
            Run(async () =>
            {
                var factory = new Factory();
                var first = new Runtime("first", factory.Trace);
                factory.Create = (report, output, token) => Ready(first);
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                await session.StartAsync();
                session.BeginResponse();
                await session.InterruptAsync();
                Assert.AreEqual(1, factory.CreateCount);
                Assert.AreEqual(0, runtime.Failures.Count);
                Assert.True(factory.Trace.Contains("first.restore"));
                await session.StopAsync();
            });
        }

        [Test]
        public void SuccessiveRecoveriesNeverRetryRejectedRoutesOrReplayResponses()
        {
            Run(async () =>
            {
                var factory = new Factory();
                var instances = new List<Runtime>();
                factory.Create = (report, output, token) =>
                {
                    var route = report.Route;
                    var backend = new Runtime(route.ToString(), factory.Trace)
                    {
                        Restore = () => Fail(Unavailable(route))
                    };
                    instances.Add(backend);
                    return Ready(backend);
                };
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                await session.StartAsync();
                for (int i = 0; i < 4; ++i)
                {
                    session.BeginResponse();
                    await Within(session.InterruptAsync());
                    Assert.AreEqual(i + 2, factory.CreateCount);
                    Assert.AreEqual(i + 1, runtime.Failures.Count);
                    Assert.AreEqual(1, instances[i].DisposeCount);
                    Assert.AreEqual(0, instances[i + 1].DisposeCount);
                }
                var failures = runtime.Failures;
                Assert.AreEqual(MNNRealtimeRoute.CascadedSpeech, runtime.CurrentReport.Route);
                session.BeginResponse();
                Assert.IsInstanceOf<NotSupportedException>(await Capture(session.InterruptAsync()));
                Assert.AreEqual(5, factory.CreateCount);
                Assert.AreEqual(4, failures.Count, "Existing diagnostic snapshots must not mutate.");
                Assert.AreEqual(5, runtime.Failures.Count);
                Assert.AreEqual(0, runtime.Output.SegmentCount);
                Assert.Null(runtime.CurrentReport);
                Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
                await session.StopAsync();
                Assert.That(instances.Select(instance => instance.DisposeCount), Is.All.EqualTo(1));
            });
        }

        [Test]
        public void SharedOutputFaultPreventsAnyFactoryAcquisition()
        {
            Run(async () =>
            {
                var factory = new Factory();
                var outputPipeline = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 80,
                    blockCapacity: 2, receiptCapacity: 1);
                var runtime = NewFallback(factory, outputPipeline);
                var session = new MNNRealtimeSession(runtime, outputPipeline);
                var response = outputPipeline.BeginResponse();
                var audio = new float[160];
                Assert.True(outputPipeline.TryRegisterSegment(response, 0, "Receipt overflow."));
                Assert.True(outputPipeline.TryEnqueue(audio, 0, 80, new MNNSpeechChunkInfo(response, 0, 0, false)));
                Assert.True(outputPipeline.TryEnqueue(audio, 80, 80, new MNNSpeechChunkInfo(response, 0, 1, true)));
                outputPipeline.Render(audio, 0, 80);
                outputPipeline.Render(audio, 80, 80);
                Assert.True(outputPipeline.IsFaulted);
                Assert.IsInstanceOf<InvalidOperationException>(await Capture(session.StartAsync()));
                Assert.AreEqual(0, factory.CreateCount);
                Assert.AreEqual(0, runtime.Failures.Count);
                await session.StopAsync();
            });
        }

        [Test]
        public void PrecancelledStartupAcquiresNoFactoryResources()
        {
            Run(async () =>
            {
                var factory = new Factory();
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                Assert.IsInstanceOf<OperationCanceledException>(await Capture(session.StartAsync(new CancellationToken(true))));
                Assert.AreEqual(0, factory.CreateCount);
                await session.StopAsync();
            });
        }

        [TestCase("cancel")]
        [TestCase("inference-join")]
        public void ResponseCancellationOrQuiescenceFailureCannotTriggerFallback(string phase)
        {
            Run(async () =>
            {
                var factory = new Factory();
                var error = Unavailable(MNNRealtimeRoute.NativeDuplexSpeech);
                var first = new Runtime("first", factory.Trace);
                if (phase == "cancel") first.Cancel = () => Fail(error);
                else first.InferenceJoin = () => Fail(error);
                factory.Create = (report, output, token) => Ready(first);
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                await session.StartAsync();
                session.BeginResponse();
                Assert.AreSame(error, await Capture(session.InterruptAsync()));
                Assert.AreEqual(1, factory.CreateCount);
                Assert.AreEqual(0, runtime.Failures.Count);
                Assert.AreEqual(MNNRealtimeSessionState.Faulted, session.State);
                await session.StopAsync();
            });
        }

        [Test]
        public void StopCancelsRecoveryPreparationAndNeverStartsReplacementCapture()
        {
            Run(async () =>
            {
                var preparing = Signal();
                var factory = new Factory();
                var first = new Runtime("first", factory.Trace)
                {
                    Restore = () => Fail(Unavailable(MNNRealtimeRoute.NativeDuplexSpeech))
                };
                var second = new Runtime("second", factory.Trace)
                {
                    Prepare = async token =>
                    {
                        preparing.TrySetResult(true);
                        await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                    }
                };
                factory.Create = (report, output, token) =>
                    Ready(report.Route == MNNRealtimeRoute.NativeDuplexSpeech ? first : second);
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                await session.StartAsync();
                session.BeginResponse();
                var recovery = session.InterruptAsync();
                await Within(preparing.Task);
                await Within(session.StopAsync());
                Assert.IsInstanceOf<OperationCanceledException>(await Capture(recovery));
                Assert.False(factory.Trace.Contains("second.listen"));
                Assert.AreEqual(1, first.DisposeCount);
                Assert.AreEqual(1, second.DisposeCount);
                Assert.AreEqual(MNNRealtimeSessionState.Stopped, session.State);
            });
        }

        [TestCase("null")]
        [TestCase("reuse")]
        public void InvalidFactoryOwnershipIsFatalAndDoesNotLoop(string mode)
        {
            Run(async () =>
            {
                var factory = new Factory();
                var first = new Runtime("first", factory.Trace)
                {
                    Prepare = token => Fail(Unavailable(MNNRealtimeRoute.NativeDuplexSpeech))
                };
                factory.Create = (report, output, token) => Ready(mode == "null" ? null : first);
                var runtime = NewFallback(factory);
                var session = new MNNRealtimeSession(runtime, runtime.Output);
                Assert.IsInstanceOf<InvalidOperationException>(await Capture(session.StartAsync()));
                Assert.AreEqual(mode == "null" ? 1 : 2, factory.CreateCount);
                Assert.AreEqual(mode == "null" ? 0 : 1, first.DisposeCount);
                await session.StopAsync();
            });
        }

        [Test]
        public void ComponentTracksReplacementReportThroughExistingSessionOwner()
        {
            var factory = new Factory();
            var first = new Runtime("first", factory.Trace)
            {
                Prepare = token => Fail(Unavailable(MNNRealtimeRoute.NativeDuplexSpeech))
            };
            var second = new Runtime("second", factory.Trace);
            factory.Create = (report, output, token) =>
                Ready(report.Route == MNNRealtimeRoute.NativeDuplexSpeech ? first : second);
            var runtime = NewFallback(factory);
            var gameObject = new UnityEngine.GameObject("Fallback component test");
            try
            {
                var component = gameObject.AddComponent<MNNRealtimeSessionComponent>();
                Assert.Throws<ArgumentException>(() => component.Configure(runtime, NewOutput(), runtime.CurrentReport));
                Assert.False(component.IsConfigured);
                component.Configure(runtime);
                var session = component.Session;
                component.StartSessionAsync().GetAwaiter().GetResult();
                Assert.AreSame(session, component.Session);
                Assert.AreSame(runtime.Output, component.Output);
                Assert.AreSame(runtime.CurrentReport, component.Preflight);
                Assert.AreEqual(MNNRealtimeRoute.NativeDuplexTextToSpeech, component.Preflight.Route);
                component.StopSessionAsync().GetAwaiter().GetResult();
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void ComponentDoesNotExposeStaleSupportedReportAfterExhaustion()
        {
            var factory = new Factory
            {
                Create = (report, output, token) =>
                    Task.FromException<IMNNRealtimeSessionRuntime>(Unavailable(report.Route))
            };
            var runtime = NewFallback(factory);
            var gameObject = new UnityEngine.GameObject("Exhausted fallback component test");
            try
            {
                var component = gameObject.AddComponent<MNNRealtimeSessionComponent>();
                component.Configure(runtime);
                Assert.Throws<NotSupportedException>(() => component.StartSessionAsync().GetAwaiter().GetResult());
                Assert.Null(component.Preflight);
                Assert.AreEqual(MNNRealtimeSessionState.Faulted, component.State);
                component.StopSessionAsync().GetAwaiter().GetResult();
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void UnsupportedCapabilitiesFailBeforeFactoryAcquiresAnything()
        {
            var factory = new Factory();
            var unsupported = new MNNRealtimeCapabilities(MNNRealtimeFeature.None, "unsupported/v1", null);
            Assert.Throws<NotSupportedException>(() =>
                new MNNRealtimeFallbackRuntime(factory, NewOutput(), unsupported, unsupported));
            Assert.AreEqual(0, factory.CreateCount);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MNNRealtimeRouteUnavailableException((MNNRealtimeRoute)99, "invalid"));
            Assert.Throws<ArgumentException>(() =>
                new MNNRealtimeRouteUnavailableException(MNNRealtimeRoute.NativeDuplexSpeech, " "));
        }

        private static MNNRealtimeFallbackRuntime NewFallback(Factory factory, MNNRealtimeOutputPipeline output = null)
        {
            const MNNRealtimeFeature binding = MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn;
            var model = Caps(binding | MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.TextInput |
                MNNRealtimeFeature.ContinuousUnderstanding | MNNRealtimeFeature.BoundedScheduling |
                MNNRealtimeFeature.BoundedCancellation | MNNRealtimeFeature.ContextRecovery |
                MNNRealtimeFeature.StreamingSpeech | MNNRealtimeFeature.StableSpokenText, .6);
            var device = Caps(MNNRealtimeFeature.DuplexDevice | MNNRealtimeFeature.EchoControl | MNNRealtimeFeature.PresentationClock);
            var tts = Caps(binding | MNNRealtimeFeature.IndependentTts | MNNRealtimeFeature.StreamingSpeech |
                MNNRealtimeFeature.BoundedCancellation, .7);
            var asr = Caps(binding | MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.SpeechRecognition |
                MNNRealtimeFeature.BoundedCancellation);
            var vad = Caps(binding | MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection |
                MNNRealtimeFeature.BoundedScheduling);
            return new MNNRealtimeFallbackRuntime(factory, output ?? NewOutput(), model, device, tts, asr, vad);
        }

        private static MNNRealtimeCapabilities Caps(MNNRealtimeFeature flags, double? rtf = null) =>
            new MNNRealtimeCapabilities(flags, "synthetic/v1", "managed-tests-only", sustainedRtf: rtf,
                sustainedRtfWindowAudioSeconds: rtf.HasValue ? 2 : 0);
        private static MNNRealtimeOutputPipeline NewOutput() => new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 80);
        private static MNNRealtimeRouteUnavailableException Unavailable(MNNRealtimeRoute route) =>
            new MNNRealtimeRouteUnavailableException(route, "Route unavailable: " + route);
        private static Task Fail(Exception error) => Task.FromException(error);
        private static Task<IMNNRealtimeSessionRuntime> Ready(IMNNRealtimeSessionRuntime runtime) => Task.FromResult(runtime);
        private static TaskCompletionSource<bool> Signal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Unity 2021 ships NUnit without Task-returning test support. Keep async work off its main context.
        private static void Run(Func<Task> action) => Task.Run(action).GetAwaiter().GetResult();

        private static async Task Within(Task task)
        {
            Assert.AreSame(task, await Task.WhenAny(task, Task.Delay(3000)).ConfigureAwait(false), "Lifecycle operation timed out.");
            await task.ConfigureAwait(false);
        }

        private static async Task<Exception> Capture(Task task)
        {
            try { await Within(task).ConfigureAwait(false); }
            catch (Exception error) { return error; }
            Assert.Fail("Expected lifecycle failure.");
            return null;
        }

        private sealed class Factory : IMNNRealtimeRuntimeFactory
        {
            public readonly ConcurrentQueue<string> Trace = new ConcurrentQueue<string>();
            public Func<MNNRealtimeReport, MNNRealtimeOutputPipeline, CancellationToken, Task<IMNNRealtimeSessionRuntime>> Create;
            public int CreateCount;
            public Task<IMNNRealtimeSessionRuntime> CreateAsync(MNNRealtimeReport report,
                MNNRealtimeOutputPipeline output, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref CreateCount);
                Trace.Enqueue("create." + report.Route);
                return Create(report, output, cancellationToken);
            }
        }

        private sealed class Runtime : IMNNRealtimeSessionRuntime
        {
            private readonly string _name;
            private readonly ConcurrentQueue<string> _trace;
            public Func<CancellationToken, Task> Prepare = token => Task.CompletedTask;
            public Func<CancellationToken, Task> Listen = token => Task.CompletedTask;
            public Func<Task> Cancel = () => Task.CompletedTask;
            public Func<Task> InferenceJoin = () => Task.CompletedTask;
            public Func<Task> Restore = () => Task.CompletedTask;
            public Func<Task> Stop = () => Task.CompletedTask;
            public Func<Task> Join = () => Task.CompletedTask;
            public Func<Task> Dispose = () => Task.CompletedTask;
            public int DisposeCount;
            public Runtime(string name, ConcurrentQueue<string> trace) { _name = name; _trace = trace; }
            private Task Call(string operation, Func<Task> action) { _trace.Enqueue(_name + "." + operation); return action(); }
            public Task PrepareAsync(CancellationToken token) => Call("prepare", () => Prepare(token));
            public Task StartListeningAsync(CancellationToken token) => Call("listen", () => Listen(token));
            public Task RequestCancellationAsync(MNNResponseId response) => Call("cancel", Cancel);
            public Task WaitForInferenceQuiescenceAsync(MNNResponseId response) => Call("inference-join", InferenceJoin);
            public Task RestoreContextAsync(MNNResponseId response) => Call("restore", Restore);
            public Task StopListeningAsync() => Call("stop", Stop);
            public Task WaitForQuiescenceAsync() => Call("join", Join);
            public Task DisposeAsync() { ++DisposeCount; return Call("dispose", Dispose); }
        }
    }
}
