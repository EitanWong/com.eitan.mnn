using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SpeakerEmbedding")]
    public sealed class CampPlusSourceParityTests
    {
        private static readonly string[] Cases = { "speaker1_a_cn_16k", "speaker1_b_cn_16k", "speaker2_a_cn_16k", "quiet", "gain2", "silence", "noise", "short", "long" };
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/CAMPlus-MNN");
        private static string Reference
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("MNN_TEST_CAMPPLUS_REFERENCE");
                if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("Set MNN_TEST_CAMPPLUS_REFERENCE to the original PyTorch checkpoint oracle.");
                return path;
            }
        }
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        private static void Check(float[] expected, float[] actual, string name)
        {
            Assert.AreEqual(192, actual.Length);
            double error = Math.Sqrt(actual.Select((value, i) => Math.Pow((double)value - expected[i], 2)).Sum());
            TestContext.WriteLine(name + ": normalized_embedding_l2=" + error);
            Assert.That(error, Is.LessThan(.001), name);
            Assert.That(MNNSpeakerEmbedding.Similarity(actual, expected), Is.GreaterThan(.99999), name);
        }
        [Test]
        public void FrontendMatchesUnmodifiedOfficialTorchFbank()
        {
            foreach (string name in Cases)
            {
                var actual = MNNKaldiFeatures.Extract3dSpeaker(Read(name + "-input"), out int frames, CancellationToken.None);
                var expected = Read(name + "-features"); Assert.AreEqual(expected.Length, frames * 80);
                double maximum = actual.Select((value, i) => Math.Abs((double)value - expected[i])).Max();
                TestContext.WriteLine(name + ": feature_max_abs=" + maximum);
                Assert.That(maximum, Is.LessThan(.001), name);
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void EmbeddingsMatchOriginalPyTorchAcrossChangingShapes(MNNBackendType backend)
        {
            using (var model = MNNSpeakerEmbedding.Load(Directory, 2, backend))
            {
                foreach (string name in Cases.Concat(new[] { Cases[0] }).SelectMany(name => new[] { name, name }))
                    Check(Read(name + "-embedding"), model.Encode(Read(name + "-input")), name);
                if (backend == MNNBackendType.Auto) Assert.AreNotEqual(MNNBackendType.Auto, model.Backend);
                else Assert.AreEqual(backend, model.Backend);
            }
        }
        [Test]
        public void StudioExecutesActualSpeakerRequestAndCancellation()
        {
            var studio = MNNStudioModel.Read(Directory);
            Assert.True(studio.HasSpeakerRecognitionPipeline);
            string input = Path.GetFullPath(Path.Combine(Reference, "../source/examples/speaker1_a_cn_16k.wav"));
            using (var backend = MNNStudioNativeBackend.Load(studio, 2, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = input });
                StringAssert.Contains("192 dimensions", result.Text);
                var vector = result.Text.Split('\n')[1].Split(',').Select(value => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Check(Read(Cases[0] + "-embedding"), vector, "Studio");
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = input, Cancellation = new CancellationToken(true) }));
            }
        }
        [Test]
        public void MeasureWarmWholeCallThreadAndBackendChoices()
        {
            var pcm = Read(Cases[0] + "-input");
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4 } : new[] { 1 })
            using (var model = MNNSpeakerEmbedding.Load(Directory, threads, backend))
            {
                model.Encode(pcm);
                var timings = new double[5]; var timer = new System.Diagnostics.Stopwatch();
                for (int i = 0; i < timings.Length; ++i)
                {
                    timer.Restart(); var result = model.Encode(pcm); timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds;
                    Check(Read(Cases[0] + "-embedding"), result, "timing");
                }
                Array.Sort(timings);
                TestContext.WriteLine("backend=" + backend + "; threads=" + threads + "; samples=" + pcm.Length + "; median_ms=" + timings[2]);
            }
        }

        [Test]
        public void AlternatingRecordingLengthsKeepSourceIdentityAndMeasureLifecycleCost()
        {
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            using (var model = MNNSpeakerEmbedding.Load(Directory, 4, backend))
            {
                var timings = new double[6];
                for (int i = 0; i < timings.Length; ++i)
                {
                    string name = Cases[i % 2];
                    var pcm = Read(name + "-input");
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var result = model.Encode(pcm);
                    timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds;
                    Check(Read(name + "-embedding"), result, name);
                }
                Array.Sort(timings);
                TestContext.WriteLine("shape_switch_backend=" + backend + "; median_ms=" + timings[3]);
            }
        }

        [Test]
        public void InvalidContextCancellationAndDisposalHaveExplicitContracts()
        {
            var model = MNNSpeakerEmbedding.Load(Directory, 1, MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(() => model.Encode(new float[1679]));
                Assert.Throws<ArgumentException>(() => model.Encode(new[] { float.NaN }));
                Assert.Throws<OperationCanceledException>(() => model.Encode(new float[16000], cancellationToken: new CancellationToken(true)));
                Assert.Throws<NotSupportedException>(() => MNNSpeakerEmbedding.Load(Directory, backendType: MNNBackendType.NN));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Encode(new float[16000]));
        }
    }
}
