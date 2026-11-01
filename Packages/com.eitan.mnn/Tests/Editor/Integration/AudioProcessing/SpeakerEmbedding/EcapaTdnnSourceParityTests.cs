using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SpeakerEmbedding")]
    public sealed class EcapaTdnnSourceParityTests
    {
        private static readonly string[] Cases = { "speech", "second_speaker", "natural_speech", "quiet", "gain2", "silence", "noise", "tone", "short", "long", "minimum" };
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/ECAPA-TDNN-MNN");
        private static string Reference
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("MNN_TEST_ECAPA_REFERENCE");
                if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("Set MNN_TEST_ECAPA_REFERENCE to the original SpeechBrain oracle.");
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
        public void FrontendMatchesOriginalSpeechBrainFbank()
        {
            var config = UnityEngine.JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(Path.Combine(Directory, MNNSpeakerModelConfig.FileName)));
            var frontend = new MNNSpeechBrainFeatures(config);
            foreach (string name in Cases)
            {
                var actual = frontend.Extract(Read(name + "-input"), out int frames, CancellationToken.None);
                var expected = Read(name + "-features"); Assert.AreEqual(expected.Length, frames * 80);
                double maximum = actual.Select((value, i) => Math.Abs((double)value - expected[i])).Max();
                TestContext.WriteLine(name + ": feature_max_abs=" + maximum);
                Assert.That(maximum, Is.LessThan(.001), name);
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void EmbeddingsMatchOriginalSpeechBrainAcrossChangingShapes(MNNBackendType backend)
        {
            using (var model = MNNSpeakerEmbedding.Load(Directory, 4, backend))
            {
                Assert.AreEqual("ECAPA-TDNN", model.ModelName);
                foreach (string name in Cases.Concat(new[] { Cases[0] }).SelectMany(name => new[] { name, name }))
                    Check(Read(name + "-unit"), model.Encode(Read(name + "-input")), name);
                if (backend == MNNBackendType.Auto) Assert.AreEqual(MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU, model.Backend);
                else Assert.AreEqual(backend, model.Backend);
            }
        }
        [Test]
        public void StudioExecutesActualSpeakerRequestAndCancellation()
        {
            var studio = MNNStudioModel.Read(Directory);
            Assert.True(studio.HasSpeakerRecognitionPipeline);
            string input = Path.Combine(Reference, "speech.wav");
            using (var backend = MNNStudioNativeBackend.Load(studio, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = input });
                StringAssert.Contains("192 dimensions", result.Text);
                var vector = result.Text.Split('\n')[1].Split(',').Select(value => float.Parse(value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                Check(Read("speech-unit"), vector, "Studio");
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = input, Cancellation = new CancellationToken(true) }));
            }
        }
        [Test]
        public void MeasureWarmWholeCallThreadAndBackendChoices()
        {
            var pcm = Read("speech-input");
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4 } : new[] { 1 })
            using (var model = MNNSpeakerEmbedding.Load(Directory, threads, backend))
            {
                model.Encode(pcm);
                var timings = new double[5]; var timer = new System.Diagnostics.Stopwatch();
                for (int i = 0; i < timings.Length; ++i)
                {
                    timer.Restart(); var result = model.Encode(pcm); timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds;
                    Check(Read("speech-unit"), result, "timing");
                }
                Array.Sort(timings);
                TestContext.WriteLine("backend=" + backend + "; threads=" + threads + "; samples=" + pcm.Length + "; median_ms=" + timings[2]);
            }
        }
        [Test]
        public void CompareMetalTuningModesAgainstAllOriginalFeatureShapes()
        {
            foreach (var tuning in new[] { MNNGpuTuningMode.None, MNNGpuTuningMode.Fast, MNNGpuTuningMode.Wide })
            using (var graph = new MNNGenerationGraph(Path.Combine(Directory, "ecapa_fp32.mnn"), 1,
                backendType: MNNBackendType.Metal, precisionMode: MNNPrecisionMode.High, gpuTuningMode: tuning, recreateGpuSessionOnShapeChange: true))
            {
                bool accepted = true; double maximum = 0;
                foreach (string name in Cases.Concat(new[] { Cases[0] }).SelectMany(name => new[] { name, name }))
                {
                    var features = Read(name + "-features");
                    var actual = graph.RunOutputs(new[] { "embedding" }, new MNNGenerationGraph.Input("x", features, 1, features.Length / 80, 80))[0].Values;
                    Assert.AreEqual(192, actual.Length);
                    double norm = Math.Sqrt(actual.Sum(value => (double)value * value));
                    var expected = Read(name + "-unit");
                    double error = Math.Sqrt(actual.Select((value, i) => Math.Pow(value / norm - expected[i], 2)).Sum());
                    TestContext.WriteLine("tuning_case=" + tuning + "; case=" + name + "; unit_l2=" + error);
                    bool finite = !double.IsNaN(error) && !double.IsInfinity(error);
                    accepted &= finite && error < .001; maximum = Math.Max(maximum, error);
                }
                var input = Read("speech-features"); var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    graph.RunOutputs(new[] { "embedding" }, new MNNGenerationGraph.Input("x", input, 1, input.Length / 80, 80));
                    timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds;
                }
                Array.Sort(timings);
                TestContext.WriteLine("tuning=" + tuning + "; accepted=" + accepted + "; max_l2=" + maximum + "; core_median_ms=" + timings[2]);
                if (tuning == MNNGpuTuningMode.None) Assert.True(accepted, "Default tuning must preserve all source shapes and repeats.");
            }
        }
        [Test]
        public void MeasureAlternatingRecordingLengthsWithSourceIdentity()
        {
            var names = new[] { "speech", "second_speaker", "natural_speech", "short", "long", "minimum", "speech" };
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            using (var model = MNNSpeakerEmbedding.Load(Directory, 4, backend))
            {
                var timings = new double[names.Length];
                for (int i = 0; i < names.Length; ++i)
                {
                    var pcm = Read(names[i] + "-input");
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var actual = model.Encode(pcm); timer.Stop();
                    timings[i] = timer.Elapsed.TotalMilliseconds; Check(Read(names[i] + "-unit"), actual, names[i]);
                }
                TestContext.WriteLine("alternating_backend=" + backend + "; case_names=" + string.Join(",", names) + "; milliseconds=" + string.Join(",", timings));
            }
        }
        [Test]
        public void InvalidContextCancellationAndDisposalHaveExplicitContracts()
        {
            var model = MNNSpeakerEmbedding.Load(Directory, 1, MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(() => model.Encode(new float[639]));
                Assert.Throws<ArgumentException>(() => model.Encode(new[] { float.NaN }));
                Assert.Throws<ArgumentException>(() => model.Encode(new float[480001]));
                Assert.Throws<OperationCanceledException>(() => model.Encode(new float[16000], cancellationToken: new CancellationToken(true)));
                Assert.Throws<NotSupportedException>(() => MNNSpeakerEmbedding.Load(Directory, backendType: MNNBackendType.NN));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Encode(new float[16000]));
        }
    }
}
