using System;
using System.IO;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Sortformer")]
    public sealed class SortformerSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/Sortformer-4spk-v2.1-MNN");
        [Test]
        public void PublicApiHasWaveformAndFileAnalysis()
        {
            var type = typeof(MNNInterpreter).Assembly.GetType("MNN.Unity.MNNSortformerDiarization");
            Assert.NotNull(type, "Missing Sortformer waveform pipeline");
            Assert.NotNull(type.GetMethod("Analyze"));
            Assert.NotNull(type.GetMethod("AnalyzeWaveFile"));
        }
        [Test]
        public void StudioDiscoversCompleteDiarizationPipeline() => Assert.True(MNNStudioModel.Read(Directory).HasAudioProcessingPipeline);
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_SORTFORMER_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_SORTFORMER_REFERENCE to the original NeMo oracle.");
        private static readonly string[] Cases = { "speech", "quiet", "gain2", "silence", "noise", "tone", "minimum", "short", "boundary_before", "boundary_at", "boundary_after", "long", "maximum" };
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        private static double Check(float[] expected, float[] actual, double budget, string name)
        {
            Assert.AreEqual(expected.Length, actual.Length, name);
            double maximum = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                maximum = Math.Max(maximum, Math.Abs((double)actual[i] - expected[i]));
            }
            TestContext.WriteLine(name + "; maximum_absolute_error=" + maximum);
            Assert.Less(maximum, budget, name);
            return maximum;
        }
        [Test]
        public void EvalFrontendMatchesOriginalCheckpointBuffersAndFeatureLengths()
        {
            var config = MNNSortformerConfig.Read(Directory);
            var frontend = new MNNNeMoMelFeatures(config.analysis_window, config.mel_filters, 128, true);
            foreach (string name in Cases)
            {
                var pcm = Read(name + "-input");
                var values = frontend.Extract(pcm, 16, out int frames, out int valid, default);
                Assert.AreEqual(pcm.Length / 160, valid);
                Check(Read(name + "-features-source"), values, .0001, name);
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void CompleteWaveformAndCachePipelineMatchesOriginalNeMo(MNNBackendType backend)
        {
            using (var model = MNNSortformerDiarization.Load(Directory, 4, backend))
            {
                foreach (string name in System.Linq.Enumerable.Concat(Cases, new[] { "speech" }))
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var result = model.Analyze(Read(name + "-input"));
                        Check(Read(name + "-predictions-source"), result.Probabilities, .001, name + ";repeat=" + repeat);
                        Assert.AreEqual(result.Probabilities.Length / 4, result.FrameCount);
                    }
                var expected = backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend;
                Assert.AreEqual(expected, model.PreencodeBackend);
                Assert.AreEqual(expected, model.CoreBackend);
            }
        }
        [Test]
        public void StudioExecutesActualWaveFileAndCancellation()
        {
            var model = MNNStudioModel.Read(Directory);
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = Path.Combine(Reference, "speech.wav") });
                StringAssert.Contains("Sortformer speaker activity", result.Text);
                StringAssert.Contains("speaker 0:", result.Text);
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest
                {
                    Audio = Path.Combine(Reference, "speech.wav"), Cancellation = new System.Threading.CancellationToken(true)
                }));
            }
        }
        [Test]
        public void InputBoundariesCancellationAndDisposalAreExplicit()
        {
            var model = MNNSortformerDiarization.Load(Directory, 1, MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(() => model.Analyze(new float[159]));
                Assert.Throws<ArgumentException>(() => model.Analyze(new float[960001]));
                var invalid = new float[160]; invalid[50] = float.NaN;
                Assert.Throws<ArgumentException>(() => model.Analyze(invalid));
                Assert.Throws<OperationCanceledException>(() => model.Analyze(new float[160], cancellationToken: new System.Threading.CancellationToken(true)));
                Assert.Throws<NotSupportedException>(() => MNNSortformerDiarization.Load(Directory, backendType: MNNBackendType.NN));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Analyze(new float[160]));
        }
        [Test]
        public void MeasureWarmWholeCallAndAlternatingDurations()
        {
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4 } : new[] { 1 })
            using (var model = MNNSortformerDiarization.Load(Directory, threads, backend))
            {
                var pcm = Read("speech-input"); model.Analyze(pcm);
                var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var actual = model.Analyze(pcm); timer.Stop();
                    timings[i] = timer.Elapsed.TotalMilliseconds;
                    Check(Read("speech-predictions-source"), actual.Probabilities, .001, "timing");
                }
                Array.Sort(timings);
                TestContext.WriteLine("backend=" + backend + ";threads=" + threads + ";warm_ms=" + timings[2]);
                foreach (string name in new[] { "speech", "minimum", "long", "maximum", "speech" })
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var actual = model.Analyze(Read(name + "-input")); timer.Stop();
                    Check(Read(name + "-predictions-source"), actual.Probabilities, .001, name);
                    TestContext.WriteLine("alternating_backend=" + backend + ";threads=" + threads + ";case=" + name + ";ms=" + timer.Elapsed.TotalMilliseconds);
                }
            }
        }
        [Test]
        public void CompareMetalCoreTuningAndSessionReuse()
        {
            string[] names = { "minimum", "short", "boundary_before", "boundary_at", "boundary_after", "chunk", "partial_cache", "full_cache", "long", "minimum" };
            int[] lengths = { 1, 1, 2, 2, 3, 5, 19, 233, 258, 1 };
            foreach (bool recreate in new[] { false, true })
            foreach (var tuning in new[] { MNNGpuTuningMode.None, MNNGpuTuningMode.Fast, MNNGpuTuningMode.Wide })
            using (var graph = new MNNGenerationGraph(Path.Combine(Directory, MNNSortformerModel.Core), 1,
                backendType: MNNBackendType.Metal, precisionMode: MNNPrecisionMode.High, gpuTuningMode: tuning,
                recreateGpuSessionOnShapeChange: recreate))
            {
                bool accepted = true; double maximum = 0;
                for (int i = 0; i < names.Length; ++i)
                for (int repeat = 0; repeat < 2; ++repeat)
                {
                    var actual = graph.RunOutputs(new[] { "predictions" },
                        new MNNGenerationGraph.Input("embeddings", Read(names[i] + "-embeddings"), 1, lengths[i], 512),
                        new MNNGenerationGraph.Input("lengths", new[] { lengths[i] }, 1))[0].Values;
                    var expected = Read(names[i] + "-predictions");
                    Assert.AreEqual(expected.Length, actual.Length);
                    for (int j = 0; j < actual.Length; ++j)
                    {
                        double error = Math.Abs((double)actual[j] - expected[j]);
                        maximum = Math.Max(maximum, error);
                        accepted &= !double.IsNaN(error) && !double.IsInfinity(error) && error < .001;
                    }
                }
                var embedding = Read("chunk-embeddings"); var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    graph.RunOutputs(new[] { "predictions" }, new MNNGenerationGraph.Input("embeddings", embedding, 1, 5, 512),
                        new MNNGenerationGraph.Input("lengths", new[] { 5 }, 1));
                    timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds;
                }
                Array.Sort(timings);
                TestContext.WriteLine("tuning=" + tuning + ";recreate=" + recreate + ";accepted=" + accepted + ";maximum_error=" + maximum + ";core_ms=" + timings[2]);
                if (tuning == MNNGpuTuningMode.None && recreate) Assert.True(accepted);
            }
        }
        [Test]
        public void MeasureSelectedMetalWholeCalls()
        {
            using (var model = MNNSortformerDiarization.Load(Directory, 1, MNNBackendType.Metal))
            {
                foreach (string name in new[] { "speech", "minimum", "long", "maximum", "speech" })
                {
                    var pcm = Read(name + "-input"); model.Analyze(pcm);
                    var timings = new double[5];
                    for (int i = 0; i < timings.Length; ++i)
                    {
                        var timer = System.Diagnostics.Stopwatch.StartNew(); var actual = model.Analyze(pcm); timer.Stop();
                        timings[i] = timer.Elapsed.TotalMilliseconds;
                        Check(Read(name + "-predictions-source"), actual.Probabilities, .001, name);
                    }
                    Array.Sort(timings);
                    TestContext.WriteLine("selected_metal_case=" + name + ";median_ms=" + timings[2]);
                }
            }
        }
    }
}
