using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Namo")]
    public sealed class NamoSourceParityTests
    {
        [Serializable] private sealed class Row
        {
            public string name, text;
            public bool has_text;
            public int[] tokens, attention_mask;
            public float probability;
        }
        [Serializable] private sealed class Cases { public Row[] rows; }
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/NAMO-Turn-Detector-v1-Multilingual-MNN");
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_NAMO_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_NAMO_REFERENCE to the frozen official ONNX oracle.");
        private static Row[] Read() => JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "unity-reference.json"))).rows;

        [Test]
        public void PublicApiProvidesTranscriptProbability()
        {
            Type type = typeof(MNNInterpreter).Assembly.GetType("MNN.Unity.MNNNamoTurnDetector");
            Assert.NotNull(type, "NAMO requires a complete transcript turn detection API.");
            Assert.NotNull(type.GetMethod("Predict"));
            Assert.NotNull(type.GetMethod("IsEndOfTurn"));
        }

        [Test]
        public void StudioDiscoversCompleteTranscriptPipeline() => Assert.True(MNNStudioModel.Read(Directory).HasAudioProcessingPipeline);

        [TestCase(MNNBackendType.CPU, 8192), TestCase(MNNBackendType.Metal, 512)]
        [Explicit("Runs the complete candidate graph against the independently published FP32 ONNX.")]
        public void CandidateGraphMatchesSource(MNNBackendType backend, int maximumTokens)
            => VerifyCandidateGraph(backend, maximumTokens, false);

        private static void VerifyCandidateGraph(MNNBackendType backend, int maximumTokens, bool recreate)
        {
            using (var graph = new MNNGenerationGraph(Path.Combine(Reference, "namo_tiled_fp16.mnn"), 4, backendType: backend,
                precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None, recreateGpuSessionOnShapeChange: recreate))
            {
                Row[] rows = Read().Where(row => row.tokens.Length <= maximumTokens).ToArray();
                foreach (Row row in rows.Concat(new[] { rows[0] }))
                {
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        int length = backend == MNNBackendType.Metal ? 512 : row.tokens.Length;
                        var tokens = new int[length]; var mask = new int[length];
                        Array.Copy(row.tokens, tokens, row.tokens.Length); Array.Copy(row.attention_mask, mask, row.attention_mask.Length);
                        float[] logits = graph.Run("logits", new MNNGenerationGraph.Input("input_ids", tokens, 1, length),
                            new MNNGenerationGraph.Input("attention_mask", mask, 1, length));
                        Assert.AreEqual(2, logits.Length, row.name);
                        float probability = (float)(1 / (1 + Math.Exp((double)logits[0] - logits[1])));
                        double error = Math.Abs(probability - row.probability);
                        TestContext.WriteLine("candidate=" + backend + ";case=" + row.name + ";probability_error=" + error);
                        Debug.Log("NAMO candidate=" + backend + ";case=" + row.name + ";repeat=" + repeat + ";error=" + error);
                        Assert.Less(error, .001, row.name);
                    }
                }
                Assert.AreEqual(backend, graph.Backend, "Requested acceleration must actually remain active.");
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Auto)]
        public void CompleteTranscriptApiMatchesOriginalOnnx(MNNBackendType backend)
            => VerifyTranscriptApi(backend);

        [Test]
        public void MetalProfilesMatchOriginalOnnx() => VerifyTranscriptApi(MNNBackendType.Metal);

        private static void VerifyTranscriptApi(MNNBackendType backend)
        {
            using (var model = MNNNamoTurnDetector.Load(Directory, 4, backend))
            {
                foreach (Row row in Read().Where(row => row.has_text))
                {
                    float probability = model.Predict(row.text);
                    double error = Math.Abs(probability - row.probability);
                    TestContext.WriteLine("api=" + backend + ";case=" + row.name + ";probability_error=" + error);
                    Assert.Less(error, .001, row.name);
                    Assert.AreEqual(row.probability > .5f, probability > .5f, row.name);
                    Assert.AreEqual(backend == MNNBackendType.Metal && row.tokens.Length <= MNNNamoTurnDetector.MetalMaximumTokens
                        ? MNNBackendType.Metal : MNNBackendType.CPU, model.Backend, row.name + " actual backend");
                }
                Row first = Read()[0];
                Assert.Less(Math.Abs(model.Predict(first.text) - first.probability), .001, "return to first shape");
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend, model.Backend);
            }
        }

        [Test]
        public void StudioExecutesTranscriptAndCancellation()
        {
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(Directory), 4, false))
            {
                foreach (Row row in Read().Where(row => row.has_text).Take(6))
                {
                    var result = backend.Run(new MNNStudioRequest { Prompt = row.text });
                    Assert.NotNull(result.TurnDetection);
                    Assert.Less(Math.Abs(result.TurnDetection.CompletionProbability - row.probability), .001, row.name);
                    Assert.AreEqual(row.probability > .5f, result.TurnDetection.IsComplete);
                }
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest
                {
                    Prompt = "hello", Cancellation = new System.Threading.CancellationToken(true)
                }));
            }
        }

        [Test]
        public void CancellationBoundsAndDisposalAreExplicit()
        {
            var model = MNNNamoTurnDetector.Load(Directory, 1, MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentNullException>(() => model.Predict(null));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.IsEndOfTurn("hello", float.NaN));
                Assert.Throws<OperationCanceledException>(() => model.Predict("hello", new System.Threading.CancellationToken(true)));
                Assert.That(model.Predict(string.Empty), Is.InRange(0f, 1f));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Predict("hello"));
        }

        [Test]
        public void MeasureWholeCalls()
        {
            foreach (MNNBackendType backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4 } : new[] { 4 })
            using (var model = MNNNamoTurnDetector.Load(Directory, threads, backend))
            {
                foreach (Row row in Read().Where(row => row.name == "text0" || row.name == "length128" || row.name == "length512"))
                {
                    model.Predict(row.text);
                    var times = new double[5];
                    for (int repeat = 0; repeat < times.Length; ++repeat)
                    {
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        float probability = model.Predict(row.text); timer.Stop();
                        Assert.Less(Math.Abs(probability - row.probability), .001);
                        times[repeat] = timer.Elapsed.TotalMilliseconds;
                    }
                    Array.Sort(times);
                    TestContext.WriteLine("timing_backend=" + backend + ";threads=" + threads + ";case=" + row.name + ";warm_ms=" + times[2]);
                }
            }
        }
    }
}
