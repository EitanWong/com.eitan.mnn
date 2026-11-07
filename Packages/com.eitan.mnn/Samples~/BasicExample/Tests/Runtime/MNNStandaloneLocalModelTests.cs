using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace MNN.Examples.Tests
{
    /// <summary>Explicit installed-model tests. Real text/TTS inference with injected device rendering;
    /// these do not prove microphone capture, speaker audibility, ASR accuracy or native duplex support.</summary>
    [Category("RealModel"), Category("StandaloneConversation")]
    public sealed class MNNStandaloneLocalModelTests
    {
        private const int MaximumSoakSeconds = 86400;
        private const int FrameworkGraceSeconds = 1800;
        private Task _exercise;
        private CancellationTokenSource _exerciseCancellation;
        private bool _resultObserved;
        private Exception _cancellationFailure;

        [UnityTest, Timeout(FrameworkGraceSeconds * 1000), Explicit("Requires compatible installed language/ASR/TTS models and the supported native ABI.")]
        public IEnumerator LocalTextProducesSpeechAndPresentedReply() => RunOwned(token => ExerciseAsync(0, token), 1500);

        [UnityTest, Timeout((MaximumSoakSeconds + FrameworkGraceSeconds) * 1000), Explicit("Opt-in repeated-session soak. Defaults to 3600 seconds; configure MNN_STANDALONE_SOAK_SECONDS for smoke runs.")]
        public IEnumerator RepeatedSessionsMeasureLocalModelSoak()
        {
            int seconds = Integer("MNN_STANDALONE_SOAK_SECONDS", 3600, 1, MaximumSoakSeconds);
            return RunOwned(token => ExerciseAsync(seconds, token), seconds + 1200);
        }

        private static async Task ExerciseAsync(int seconds, CancellationToken cancellation)
        {
            var settings = Settings.Read(seconds);
            Directory.CreateDirectory(settings.Output);
            string turnsPath = Path.Combine(settings.Output, "turns.csv");
            var report = new Report
            {
                startedUtc = DateTime.UtcNow.ToString("o"), requestedSeconds = seconds,
                languageModel = settings.Model, recognitionModel = settings.Asr, speechModel = settings.Tts,
                backendRequested = settings.Backend.ToString(), maximumReplyTokens = settings.MaximumTokens,
                rssGrowthBudgetMiB = settings.RssGrowthMiB, managedGrowthBudgetMiB = settings.ManagedGrowthMiB,
                validationScope = "Real local text model and buffered TTS; ASR prepared but not exercised. Injected 16 kHz render receipts and estimated 25 ms presentation. No real microphone/speaker, ASR quality, native duplex, acoustic AEC or realtime throughput qualification.",
                renderScope = "10 ms output pipeline Render CPU duration p95/p99, last up to 65536 calls per session. These are simulated device hops, not native model inference hops.",
                nativeMemory = "Unavailable: Unity tracked allocation excludes some native/model allocations; RSS is whole Unity process and is not a native-only estimate.",
                memoryScope = "Sampled on control thread approximately every 250 ms; peaks are observed samples, not exhaustive allocator high-water marks. No forced GC.",
                status = "running"
            };
            var watch = Stopwatch.StartNew();
            using var memory = new MemoryWindow();
            Exception failure = null;
            using (var csv = new StreamWriter(turnsPath, false))
            {
                csv.WriteLine("session,turn,startup_ms,first_token_ms,first_pcm_ms,first_nonzero_render_ms,presentation_ms,total_ms,render_hop_p95_ms,render_hop_p99_ms,rss_bytes,managed_bytes,unity_tracked_bytes,reply");
                csv.Flush();
                try
                {
                    do
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var binding = new InjectedAudioBinding();
                        var app = Create(settings, binding);
                        ++report.sessions;
                        try
                        {
                            var startup = Stopwatch.StartNew();
                            await PumpAsync(app.StartAsync(), app, binding, memory, 600, cancellation);
                            double startupMs = startup.Elapsed.TotalMilliseconds;
                            report.maximumStartupMilliseconds = Math.Max(report.maximumStartupMilliseconds, startupMs);
                            for (int turn = 0; turn < settings.TurnsPerSession; ++turn)
                            {
                                cancellation.ThrowIfCancellationRequested();
                                binding.BeginTurnMetrics();
                                long acceptedAt = Stopwatch.GetTimestamp();
                                int before = app.Transcript.Count;
                                long completedBefore = app.Telemetry.CompletedCount;
                                await PumpAsync(app.Commands.SendTextAsync("Say hello in one short English sentence."), app, binding, memory, settings.MaximumTurnSeconds, cancellation);
                                var response = app.Devices.Output.CurrentResponse;
                                Assert.IsTrue(response.IsValid);
                                var deadline = Stopwatch.StartNew();
                                while (!app.Devices.Output.CanBeginNextResponse(response))
                                {
                                    Assert.Less(deadline.Elapsed.TotalSeconds, settings.MaximumTurnSeconds, "Speech did not reach simulated presentation.");
                                    Pump(app, binding, memory); await Task.Delay(10, cancellation);
                                }
                                // The shared input worker completes telemetry after observing presentation.
                                for (int attempt = 0; attempt < 100 && app.Telemetry.CompletedCount == completedBefore; ++attempt)
                                { Pump(app, binding, memory); await Task.Delay(10, cancellation); }
                                Assert.AreEqual(completedBefore + 1, app.Telemetry.CompletedCount, "Presentation must publish exactly one new turn timing.");
                                app.Tick();
                                Assert.AreEqual(before + 2, app.Transcript.Count);
                                var reply = app.Transcript[app.Transcript.Count - 1];
                                Assert.AreEqual(MNNChatRole.Assistant, reply.Role);
                                Assert.IsFalse(string.IsNullOrWhiteSpace(reply.Text), "Model generated no displayed reply.");
                                Assert.Greater(binding.FirstAudioRenderedAt, 0, "TTS produced no nonzero rendered samples.");
                                var timing = app.Telemetry.Latest;
                                Assert.Greater(timing.FirstTokenMs, 0);
                                Assert.Greater(timing.FirstAudioMs, 0);
                                ++report.turns;
                                report.maximumFirstTokenMilliseconds = Math.Max(report.maximumFirstTokenMilliseconds, timing.FirstTokenMs);
                                report.maximumFirstPcmMilliseconds = Math.Max(report.maximumFirstPcmMilliseconds, timing.FirstAudioMs);
                                report.maximumTurnMilliseconds = Math.Max(report.maximumTurnMilliseconds, timing.TotalMs);
                                report.renderCalls += binding.TotalRenderCalls - report.previousRenderCalls;
                                report.previousRenderCalls = binding.TotalRenderCalls;
                                double p95 = binding.RenderPercentile(.95), p99 = binding.RenderPercentile(.99);
                                report.maximumSessionRenderHopP95Milliseconds = Math.Max(report.maximumSessionRenderHopP95Milliseconds, p95);
                                report.maximumSessionRenderHopP99Milliseconds = Math.Max(report.maximumSessionRenderHopP99Milliseconds, p99);
                                csv.WriteLine(string.Join(",", report.sessions, report.turns, Number(startupMs), Number(timing.FirstTokenMs), Number(timing.FirstAudioMs),
                                    Number((binding.FirstAudioRenderedAt - acceptedAt) * 1000.0 / Stopwatch.Frequency), Number(timing.PresentationMs), Number(timing.TotalMs),
                                    Number(p95), Number(p99), memory.Rss, memory.Managed, memory.UnityTracked, Quote(reply.Text)));
                                csv.Flush();
                                report.diagnostics = Array.ConvertAll(app.Diagnostics.Read(), entry => new ProviderEvidence
                                {
                                    stage = entry.Stage, implementation = entry.Implementation, state = entry.State,
                                    backend = entry.Backend, contract = entry.Contract, limitation = entry.Limitation
                                });
                                memory.Sample();
                                if (seconds == 0 || watch.Elapsed.TotalSeconds >= seconds) break;
                            }
                        }
                        finally
                        {
                            var shutdown = Stopwatch.StartNew();
                            await app.StopAsync();
                            report.maximumShutdownMilliseconds = Math.Max(report.maximumShutdownMilliseconds, shutdown.Elapsed.TotalMilliseconds);
                            Assert.IsTrue(app.IsClosed);
                            Assert.IsFalse(binding.Capturing || binding.Playing || binding.CaptureEnabled);
                            memory.Sample();
                            report.previousRenderCalls = 0;
                        }
                        if (report.sessions == 1) memory.MarkWarmBaseline();
                        settings.CheckBudgets(memory);
                        WriteReport(settings, report, memory, watch.Elapsed.TotalSeconds);
                    } while (seconds > 0 && (watch.Elapsed.TotalSeconds < seconds || report.sessions < 2));
                    report.status = "passed";
                }
                catch (Exception error) { failure = error; report.status = error is OperationCanceledException && cancellation.IsCancellationRequested ? "cancelled" : "failed"; report.error = error.ToString(); }
                finally { WriteReport(settings, report, memory, watch.Elapsed.TotalSeconds); }
            }
            TestContext.WriteLine("Standalone local-model evidence: " + settings.Output + "; status=" + report.status + "; turns=" + report.turns);
            if (failure != null) throw failure;
            Assert.Greater(report.turns, 0);
        }

        private static MNNStandaloneConversation Create(Settings settings, InjectedAudioBinding binding)
        {
            var options = new MNNConversationLocalModelOptions
            {
                Backend = settings.Backend, MaximumReplyTokens = settings.MaximumTokens, Temperature = 0,
                Language = "en", ScratchDirectory = Path.Combine(settings.Output, "Scratch"),
                CacheRoot = Path.Combine(settings.Output, "Cache")
            };
            MNNConversationStageRegistration Local(MNNConversationModelAdapter adapter, string directory) =>
                new MNNConversationStageRegistration(MNNConversationLocalProviders.DescribeCapabilities(adapter),
                    () => MNNConversationLocalProviders.Create(adapter, directory, options));
            return new MNNStandaloneConversation(binding, new MNNRealtimeCapabilities(
                MNNRealtimeFeature.DuplexDevice | MNNRealtimeFeature.HalfDuplexDevice | MNNRealtimeFeature.PresentationClock,
                "Injected render acceptance v1", "Actual local model PCM rendered in bounded test buffer with estimated receipts.", execution: MNNRealtimeExecution.Managed),
                new MNNConversationRegistrations
                {
                    Model = Local(MNNConversationModelAdapter.Language, settings.Model),
                    Asr = Local(MNNConversationModelAdapter.SpeechRecognition, settings.Asr),
                    Tts = Local(MNNConversationModelAdapter.SpeechSynthesis, settings.Tts),
                    Vad = MNNConversationStageRegistration.Energy, AllowBufferedSpeech = true
                }, Path.Combine(settings.Output, "Transcript"),
                new MNNConversationOptions { endRealtimeOnExitIntent = false, instructions = "Reply in one short English sentence.", maximumHistoryMessages = 16 });
        }

        private static void Pump(MNNStandaloneConversation app, InjectedAudioBinding binding, MemoryWindow memory)
        {
            if (binding.Playing) binding.Render(1);
            app.Tick(); memory.Tick();
            if (app.Fault != null) throw new InvalidOperationException("Standalone application faulted.", app.Fault);
        }
        private static async Task PumpAsync(Task task, MNNStandaloneConversation app, InjectedAudioBinding binding, MemoryWindow memory, int seconds, CancellationToken cancellation)
        {
            var watch = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                Assert.Less(watch.Elapsed.TotalSeconds, seconds, "Standalone operation exceeded its configured deadline.");
                cancellation.ThrowIfCancellationRequested();
                Pump(app, binding, memory); await Task.Delay(10, cancellation);
            }
            await task; Pump(app, binding, memory);
        }

        internal IEnumerator RunOwned(Func<CancellationToken, Task> operation, double maximumSeconds = 1200)
        {
            if (_exercise != null) throw new InvalidOperationException("Join the previous test operation before starting another.");
            _exerciseCancellation = new CancellationTokenSource(); _resultObserved = false; _cancellationFailure = null;
            try
            {
                _exercise = operation(_exerciseCancellation.Token);
                if (_exercise == null) throw new InvalidOperationException("The test operation returned no task.");
                var elapsed = Stopwatch.StartNew();
                while (!_exercise.IsCompleted)
                {
                    if (elapsed.Elapsed.TotalSeconds >= maximumSeconds)
                    {
                        RequestExerciseStop();
                        throw new TimeoutException("Local-model acceptance exceeded its operation budget; teardown is joining cancellation.");
                    }
                    yield return null;
                }
                _resultObserved = true; _exercise.GetAwaiter().GetResult();
            }
            finally { if (_exercise?.IsCompleted != true) RequestExerciseStop(); }
        }

        private void RequestExerciseStop()
        {
            try { _exerciseCancellation?.Cancel(); }
            catch (Exception error) { _cancellationFailure = error; }
        }

        [UnityTearDown]
        public IEnumerator JoinStoppedExercise()
        {
            if (_exercise == null) { _exerciseCancellation?.Dispose(); _exerciseCancellation = null; yield break; }
            RequestExerciseStop();
            var join = Stopwatch.StartNew();
            while (!_exercise.IsCompleted && join.Elapsed.TotalSeconds < 60) yield return null;
            Assert.IsTrue(_exercise.IsCompleted, "Cancelled local-model test did not join within 60 seconds; its resources remain owned.");
            try
            {
                if (!_resultObserved && !_exercise.IsCanceled) _exercise.GetAwaiter().GetResult();
                if (_cancellationFailure != null) throw _cancellationFailure;
            }
            finally { _exerciseCancellation.Dispose(); _exerciseCancellation = null; _exercise = null; }
        }
        private static string Number(double value) => value.ToString("F4", CultureInfo.InvariantCulture);
        private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        private static int Integer(string name, int fallback, int minimum, int maximum)
        {
            string value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            if (!int.TryParse(value, out int parsed) || parsed < minimum || parsed > maximum)
                throw new ArgumentException(name + " must be within " + minimum + "–" + maximum + ".");
            return parsed;
        }
        private static void WriteReport(Settings settings, Report report, MemoryWindow memory, double elapsed)
        {
            report.elapsedSeconds = elapsed;
            report.peakRssBytes = memory.PeakRss; report.peakManagedBytes = memory.PeakManaged; report.peakUnityTrackedBytes = memory.PeakUnityTracked;
            report.finalRssBytes = memory.Rss; report.finalManagedBytes = memory.Managed;
            report.warmBaselineRssBytes = memory.WarmRss; report.warmBaselineManagedBytes = memory.WarmManaged;
            File.WriteAllText(Path.Combine(settings.Output, "summary.json"), JsonUtility.ToJson(report, true));
        }

        private sealed class Settings
        {
            internal string Model, Asr, Tts, Output;
            internal int TurnsPerSession, MaximumTokens, MaximumTurnSeconds, RssGrowthMiB, ManagedGrowthMiB;
            internal MNNBackendType Backend;
            internal static Settings Read(int seconds)
            {
                string models = Path.Combine(Application.streamingAssetsPath, "MNN", "Models");
                var result = new Settings
                {
                    Model = Environment.GetEnvironmentVariable("MNN_STANDALONE_MODEL") ?? Path.Combine(models, "SmolLM2-135M-Instruct-MNN"),
                    Asr = Environment.GetEnvironmentVariable("MNN_STANDALONE_ASR") ?? Path.Combine(models, "wo105090--SenseVoiceSmall-MNN"),
                    Tts = Environment.GetEnvironmentVariable("MNN_STANDALONE_TTS") ?? Path.Combine(models, "PiperLessac"),
                    Output = Path.GetFullPath(Environment.GetEnvironmentVariable("MNN_STANDALONE_ARTIFACTS") ?? "TestArtifacts~/SiriAICompletion/Standalone/LocalModels"),
                    TurnsPerSession = seconds == 0 ? 1 : Integer("MNN_STANDALONE_TURNS_PER_SESSION", 5, 1, 1000),
                    MaximumTokens = Integer("MNN_STANDALONE_MAX_REPLY_TOKENS", 32, 1, 8192),
                    MaximumTurnSeconds = Integer("MNN_STANDALONE_MAX_TURN_SECONDS", 300, 1, 1800),
                    RssGrowthMiB = Integer("MNN_STANDALONE_RSS_GROWTH_MIB", 0, 0, 65536),
                    ManagedGrowthMiB = Integer("MNN_STANDALONE_MANAGED_GROWTH_MIB", 0, 0, 65536)
                };
                string backend = Environment.GetEnvironmentVariable("MNN_STANDALONE_BACKEND") ?? "CPU";
                if (!Enum.TryParse(backend, true, out result.Backend) || !Enum.IsDefined(typeof(MNNBackendType), result.Backend))
                    throw new ArgumentException("MNN_STANDALONE_BACKEND must name a supported MNNBackendType.");
                foreach (string path in new[] { result.Model, result.Asr, result.Tts })
                    if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Installed model directory required: " + path);
                string artifactRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("MNN_STANDALONE_ARTIFACT_ROOT") ?? "TestArtifacts~") + Path.DirectorySeparatorChar;
                if (!result.Output.StartsWith(artifactRoot, StringComparison.Ordinal))
                    throw new ArgumentException("MNN_STANDALONE_ARTIFACTS must be inside this project's TestArtifacts~/.");
                return result;
            }
            internal void CheckBudgets(MemoryWindow memory)
            {
                if (RssGrowthMiB > 0) Assert.LessOrEqual(memory.Rss - memory.WarmRss, RssGrowthMiB * 1024L * 1024, "Warm-session RSS growth exceeded the supplied budget.");
                if (ManagedGrowthMiB > 0) Assert.LessOrEqual(memory.Managed - memory.WarmManaged, ManagedGrowthMiB * 1024L * 1024, "Warm-session managed growth exceeded the supplied budget.");
            }
        }
        private sealed class MemoryWindow : IDisposable
        {
            private readonly Process _process = Process.GetCurrentProcess();
            private long _next;
            internal long Rss, Managed, UnityTracked, PeakRss, PeakManaged, PeakUnityTracked, WarmRss, WarmManaged;
            internal void Tick() { if (Stopwatch.GetTimestamp() >= _next) Sample(); }
            internal void Sample()
            {
                _next = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 4;
                _process.Refresh(); Rss = _process.WorkingSet64; Managed = GC.GetTotalMemory(false); UnityTracked = Profiler.GetTotalAllocatedMemoryLong();
                PeakRss = Math.Max(PeakRss, Rss); PeakManaged = Math.Max(PeakManaged, Managed); PeakUnityTracked = Math.Max(PeakUnityTracked, UnityTracked);
            }
            internal void MarkWarmBaseline() { WarmRss = Rss; WarmManaged = Managed; }
            public void Dispose() => _process.Dispose();
        }
        [Serializable]
        private sealed class ProviderEvidence
        {
            public string stage, implementation, state, backend, contract, limitation;
        }
        [Serializable]
        private sealed class Report
        {
            public string startedUtc, status, error, languageModel, recognitionModel, speechModel, backendRequested;
            public string validationScope, renderScope, nativeMemory, memoryScope;
            public int requestedSeconds, sessions, turns, maximumReplyTokens, rssGrowthBudgetMiB, managedGrowthBudgetMiB;
            public double elapsedSeconds, maximumStartupMilliseconds, maximumShutdownMilliseconds, maximumFirstTokenMilliseconds, maximumFirstPcmMilliseconds, maximumTurnMilliseconds;
            public double maximumSessionRenderHopP95Milliseconds, maximumSessionRenderHopP99Milliseconds;
            public long peakRssBytes, peakManagedBytes, peakUnityTrackedBytes, finalRssBytes, finalManagedBytes, warmBaselineRssBytes, warmBaselineManagedBytes, renderCalls;
            [NonSerialized] public long previousRenderCalls;
            public ProviderEvidence[] diagnostics;
        }
    }
}
