using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class AcousticFrontendTests
    {
        [Test]
        public void DefaultFactoryRejectsUninstalledDirectoriesBeforeAnyNativeLoad()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "MissingAcousticModel-" + Guid.NewGuid().ToString("N")));
            var factory = new MNNAcousticMnnModelFactory();
            Assert.Throws<DirectoryNotFoundException>(() => factory.CreateEnhancer(directory));
            Assert.Throws<DirectoryNotFoundException>(() => factory.CreateKeyword(directory));
            Assert.Throws<DirectoryNotFoundException>(() => factory.CreateSeparator(directory));
            Assert.Throws<DirectoryNotFoundException>(() => factory.CreateSpeaker(directory));
        }

        [Test]
        public void WakeHandoffPreservesFramesCursorAndRevision()
        {
            var buffer = new MNNWakeAudioBuffer();
            var pcm = new float[2048];
            for (int i = 0; i < pcm.Length; ++i) pcm[i] = i / 4096f;
            buffer.Append(pcm);
            buffer.Mark(600.0 / 16000);
            buffer.Mark(1800.0 / 16000);
            var packet = buffer.TakeHandoff();
            Assert.False(packet.LostPrefix);
            Assert.AreEqual(2048, packet.EndSample);
            CollectionAssert.AreEqual(new ArraySegment<float>(pcm, 512, 1536), packet.Frames);
            buffer.Reset();
            Assert.AreNotEqual(packet.CaptureRevision, buffer.Revision);
            buffer.Mark(0);
            buffer.Append(new float[512 * 70]);
            packet = buffer.TakeHandoff();
            Assert.True(packet.LostPrefix);
            Assert.IsEmpty(packet.Frames);
        }

        [Test]
        public void FrontendSnapshotsConfigurationAndProcessesBothWakeWaveforms()
        {
            var factory = new Factory();
            var settings = Settings();
            using (var frontend = new MNNAcousticFrontend(settings, factory))
            {
                settings.wakeWord = false;
                frontend.WakeAllowed = () => true;
                frontend.Load("unused", CancellationToken.None);
                var input = frontend.CreateInput(new MNNPcmFormat(16000, 1), 512,
                    MNNRealtimeFrameVoiceActivity.Energy(.01f));
                var pcm = new float[512];
                Array.Fill(pcm, .25f);
                float recognized = -1;
                input.Process(pcm, 0, pcm.Length, _ => { }, onFrame: (audio, offset, vad) => recognized = audio[offset]);
                Assert.AreEqual(.25f, recognized);
                Assert.AreEqual(.25f, factory.Keyword.LastFirstSample);
                Assert.True(frontend.TakeWakeRequest());
                var handoff = frontend.TakeWakeHandoff();
                Assert.AreEqual(512, handoff.EndSample);
                Assert.AreEqual(512, handoff.Frames.Length);
            }
            Assert.AreEqual(1, factory.Enhancer.Disposals);
            Assert.AreEqual(1, factory.Keyword.Disposals);
        }

        [Test]
        public void FailedDisposalRetainsOnlyTheFailedOwnerForRetry()
        {
            var factory = new Factory();
            var frontend = new MNNAcousticFrontend(Settings(), factory);
            frontend.Load("unused", CancellationToken.None);
            factory.Enhancer.FailDispose = true;
            Assert.Throws<AggregateException>(() => frontend.Dispose());
            Assert.AreEqual(1, factory.Keyword.Disposals, "Other owners must still be released.");
            factory.Enhancer.FailDispose = false;
            frontend.Dispose();
            Assert.AreEqual(2, factory.Enhancer.Disposals);
            Assert.AreEqual(1, factory.Keyword.Disposals, "A successful release must not be repeated.");
        }

        [Test]
        public void LoadingFailureReleasesPreviouslyLoadedModelsAndCanRetryCleanup()
        {
            var factory = new Factory { FailKeyword = true };
            factory.Enhancer.FailDispose = true;
            var frontend = new MNNAcousticFrontend(Settings(), factory);
            Assert.Throws<AggregateException>(() => frontend.Load("unused", CancellationToken.None));
            factory.Enhancer.FailDispose = false;
            frontend.Dispose();
            Assert.AreEqual(2, factory.Enhancer.Disposals);
        }

        [Test]
        public void SettingsRejectInvalidWakeTimestampWithoutChangingBuffer()
        {
            var buffer = new MNNWakeAudioBuffer();
            buffer.Append(new float[512]);
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Mark(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Append(new float[2], 0, 3));
            Assert.AreEqual(512, buffer.Position);
            Assert.IsNull(buffer.TakeHandoff());
        }

        [Test]
        public void ReleaseWaitsForActiveUtteranceAndRejectsNewCaptureWork()
        {
            var factory = new Factory();
            factory.Enhancer.BlockUtterance = true;
            var settings = Settings(); settings.enhanceRecognition = true;
            var frontend = new MNNAcousticFrontend(settings, factory);
            frontend.Load("unused", CancellationToken.None);
            var utterance = frontend.PrepareVoiceAsync(new float[512], CancellationToken.None);
            Assert.True(factory.Enhancer.Entered.Wait(2000));
            var release = frontend.ReleaseAsync();
            Assert.False(release.IsCompleted);
            Assert.Zero(factory.Enhancer.Disposals);
            Assert.Throws<ObjectDisposedException>(() => frontend.CreateInput(new MNNPcmFormat(16000, 1), 512,
                MNNRealtimeFrameVoiceActivity.Energy(.01f)));
            factory.Enhancer.Proceed.Set();
            utterance.GetAwaiter().GetResult(); release.GetAwaiter().GetResult();
            Assert.AreEqual(1, factory.Enhancer.Disposals);
        }

        [Test]
        public void WakeReloadPreservesCaptureClockAndUsesEnhancerLatency()
        {
            var factory = new Factory();
            factory.Enhancer.Latency = 128;
            factory.Keyword.End = 640.0 / 16000;
            var settings = Settings(); settings.enhanceKeywords = true;
            using (var frontend = new MNNAcousticFrontend(settings, factory))
            {
                frontend.Load("unused", CancellationToken.None);
                frontend.WakeAllowed = () => true;
                var input = frontend.CreateInput(new MNNPcmFormat(16000, 1), 1024,
                    MNNRealtimeFrameVoiceActivity.Energy(.01f));
                var pcm = new float[1024];
                input.Process(pcm, 0, pcm.Length, _ => { });
                Assert.AreEqual(512, frontend.TakeWakeHandoff().Frames.Length,
                    "Wake-to-recognition alignment must use the actual injectable enhancer delay.");
                long revision = frontend.CaptureRevision;
                var next = new Keyword { End = 0 };
                factory.Replacements.Enqueue(next);
                settings.keywords = "computer";
                frontend.ReloadWakeAsync("unused", settings, CancellationToken.None).GetAwaiter().GetResult();
                input.Process(pcm, 0, 512, _ => { });
                var handoff = frontend.TakeWakeHandoff();
                Assert.AreEqual(1536, handoff.EndSample);
                Assert.AreEqual(revision, handoff.CaptureRevision);
                Assert.AreEqual(1, factory.Keyword.Disposals);
            }
        }

        [Test]
        public void ReleaseWaitsForPendingWakeModelCreationAndCleansItsUnpublishedOwner()
        {
            var factory = new Factory();
            var frontend = new MNNAcousticFrontend(Settings(), factory);
            frontend.Load("unused", CancellationToken.None);
            var next = new Keyword(); factory.Replacements.Enqueue(next);
            factory.BlockReload = true;
            var settings = Settings(); settings.keywords = "computer";
            var reload = frontend.ReloadWakeAsync("unused", settings, CancellationToken.None);
            Assert.True(factory.ReloadEntered.Wait(2000));
            var release = frontend.ReleaseAsync();
            try
            {
                Assert.False(release.Wait(100), "Release cannot finish while an acoustic model factory still runs.");
                Assert.Zero(next.Disposals);
            }
            finally { factory.ReloadProceed.Set(); }
            reload.GetAwaiter().GetResult(); release.GetAwaiter().GetResult();
            Assert.AreEqual(1, next.Disposals);
        }

        private static MNNAcousticSettings Settings() => new MNNAcousticSettings
        {
            enabled = true, denoise = true, wakeWord = true, enhanceRecognition = false,
            keywords = "hello", keywordLanguage = MNNKeywordLanguage.English
        };

        private sealed class Factory : IMNNAcousticModelFactory
        {
            internal readonly Enhancer Enhancer = new Enhancer();
            internal readonly Keyword Keyword = new Keyword();
            internal readonly Queue<Keyword> Replacements = new Queue<Keyword>();
            private int _keywordCalls;
            internal bool BlockReload;
            internal readonly ManualResetEventSlim ReloadEntered = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim ReloadProceed = new ManualResetEventSlim();
            internal bool FailKeyword;
            public IMNNAcousticEnhancer CreateEnhancer(string path) => Enhancer;
            public IMNNAcousticKeywordModel CreateKeyword(string path)
            {
                if (FailKeyword) throw new InvalidOperationException("load failure");
                if (_keywordCalls++ == 0) return Keyword;
                if (BlockReload) { ReloadEntered.Set(); ReloadProceed.Wait(); }
                return Replacements.Dequeue();
            }
            public IMNNAcousticSeparator CreateSeparator(string path) => throw new NotSupportedException();
            public IMNNAcousticSpeakerEncoder CreateSpeaker(string path) => throw new NotSupportedException();
        }
        private sealed class Enhancer : IMNNAcousticEnhancer
        {
            internal bool FailDispose;
            internal int Disposals;
            internal int Latency = 384;
            internal bool BlockUtterance;
            internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim Proceed = new ManualResetEventSlim();
            public bool Streaming => true;
            public int DelaySamples => Latency;
            public string Diagnostics => "test enhancer";
            public void EnhanceFrame(float[] samples, int offset) => Array.Clear(samples, offset, 512);
            public float[] EnhanceUtterance(float[] samples, CancellationToken token)
            { if (BlockUtterance) { Entered.Set(); Proceed.Wait(token); } return samples; }
            public void Reset() { }
            public void Dispose() { ++Disposals; if (FailDispose) throw new InvalidOperationException("dispose failure"); }
        }
        private sealed class Keyword : IMNNAcousticKeywordModel
        {
            internal int Disposals;
            internal float LastFirstSample;
            internal double End;
            public MNNKeywordLanguage Language => MNNKeywordLanguage.English;
            public void Reset(string words, float threshold) { }
            public double AcceptWaveform(float[] samples) { LastFirstSample = samples[0]; return End; }
            public void Dispose() => ++Disposals;
        }
    }
}
