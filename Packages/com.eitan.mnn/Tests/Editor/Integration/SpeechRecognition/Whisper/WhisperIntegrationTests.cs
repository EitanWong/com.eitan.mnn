using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [TestFixture, Category("Integration"), Category("Whisper")]
    public class WhisperIntegrationTests
    {
        private string Root => Environment.GetEnvironmentVariable("MNN_TEST_WHISPER_ROOT");
        private string Model => Path.Combine(Root, "developerabu--whisper-tiny-en-mnn");
        private string Wave => Path.Combine(Root, "..", "..", "Zipformer", "Audio", "en-0.wav");
        private const string Expected = "After early nightfall, the yellow lamps would light up here and there the squalid quarter of the brothels.";

        [Test]
        public void FrontendMatchesKaldiNativeFbankAndSherpaNormalization()
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_WHISPER_ROOT; no models are downloaded.");
            var samples = MNNWaveAudio.Read(Wave, out int rate);
            Assert.AreEqual(16000, rate);
            var features = MNNWhisperFeatures.Extract(samples, out int frames, out int padded, CancellationToken.None);
            var reference = File.ReadAllBytes(Path.Combine(Root, "mel.f32"));
            double total = 0, maximum = 0;
            for (int m = 0; m < 80; ++m)
                for (int f = 0; f < frames; ++f)
                {
                    double difference = Math.Abs(features[m * padded + f] - BitConverter.ToSingle(reference, (m * 3000 + f) * 4));
                    total += difference; maximum = Math.Max(maximum, difference);
                }
            TestContext.WriteLine("frames=" + frames + " padded=" + padded + " max=" + maximum + " mean=" + total / (frames * 80));
            Assert.Less(maximum, .0001);
            Assert.Less(total / (frames * 80), .00001);
            for (int m = 0; m < 80; ++m)
                for (int f = frames; f < padded; ++f) Assert.AreEqual(0, features[m * padded + f]);
        }

        [Test]
        public void CpuRecognizesReferenceResetsCancelsAndReportsNoAlignment()
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_WHISPER_ROOT.");
            using (var recognizer = MNNWhisperRecognizer.Load(Model, new MNNWhisperConfig()))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var first = recognizer.RecognizeWaveFile(Wave);
                TestContext.WriteLine(first.Text + " elapsed=" + timer.ElapsedMilliseconds);
                Assert.AreEqual(Expected, first.Text);
                string upstream = File.ReadAllText(Path.Combine(Root, "..", "..", "Zipformer", "Audio", "en-trans.txt"));
                string reference = upstream.Split(new[] {'\n', '\r'}, StringSplitOptions.RemoveEmptyEntries).First(line => line.StartsWith("0.wav ", StringComparison.Ordinal)).Substring(6);
                string lexical = System.Text.RegularExpressions.Regex.Replace(first.Text.ToUpperInvariant(), "[^A-Z ]", "");
                Assert.AreEqual(reference.Trim(), lexical, "Match the independent upstream word sequence.");
                Assert.AreEqual(MNNBackendType.CPU, recognizer.Backend);
                Assert.IsNotEmpty(first.TokenIds);
                Assert.IsEmpty(first.Timestamps, "No fabricated alignment for the no-timestamps profile.");
                Assert.AreEqual("", recognizer.Recognize(new float[16000], 16000).Text);
                Assert.Throws<OperationCanceledException>(() => recognizer.RecognizeWaveFile(Wave, new CancellationToken(true)));
                var second = recognizer.RecognizeWaveFile(Wave);
                Assert.AreEqual(first.Text, second.Text);
                CollectionAssert.AreEqual(first.TokenIds, second.TokenIds);
            }
        }

        [Test]
        public void PreparedProfileRunsInStudioAndExpressReturnsAllOutputShapes()
        {
            if (Root == null) Assert.Ignore("Set MNN_TEST_WHISPER_ROOT.");
            MNNWhisperModel.Prepare(Model);
            var model = MNNStudioModel.Read(Model);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.SpeechRecognition).CanRun(model));
            Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest {Prompt = "Transcribe", Audio = Wave});
                Assert.AreEqual(Expected, result.Text);
                Assert.That(result.BackendDescription, Does.Contain("Whisper tiny.en"));
            }
        }
    }
}
