using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class DtlnTests
    {
        private static string ModelDirectory => Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models", "DTLN-MNN"));
        [TestCase(1), TestCase(2), TestCase(4)]
        [Explicit("Measures CPU thread budgets on the existing DTLN waveform oracle.")]
        public void CpuThreadBudgetRetainsAccuracy(int threads)
        {
            var input = Read("input");
            var expected = Read("enhanced");
            using (var model = MNNDtln.Load(ModelDirectory, threads, MNNBackendType.CPU))
            {
                model.Enhance(input);
                var elapsed = new double[5];
                for (int i = 0; i < elapsed.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var actual = model.Enhance(input);
                    elapsed[i] = timer.Elapsed.TotalMilliseconds;
                    Assert.That(RelativeError(actual, expected), Is.LessThan(.0001));
                }
                Array.Sort(elapsed);
                string report = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"threads\":{0},\"median_ms\":{1:F4},\"samples\":{2}}}", threads, elapsed[2], input.Length);
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_DTLN_REFERENCE"), "cpu-threads-" + threads + ".json"), report + "\n");
                TestContext.WriteLine(report);
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires DTLN, the native MNN plugin and the independent ONNX streaming oracle.")]
        public void EnhancementMatchesOnnxAndMeasuresWarmLatency(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            var input = Read("input");
            var expected = Read("enhanced");
            using (var model = MNNDtln.Load(ModelDirectory, 1, backend))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var actual = model.Enhance(input);
                double first = timer.Elapsed.TotalMilliseconds;
                double relative = RelativeError(actual, expected);
                Assert.AreEqual(backend, model.Stage1Backend);
                Assert.AreEqual(backend, model.Stage2Backend);
                Assert.That(relative, Is.LessThan(backend == MNNBackendType.CPU ? .0001 : .002));
                double warm = 0;
                for (int i = 0; i < 3; ++i)
                {
                    timer.Restart();
                    var repeated = model.Enhance(input);
                    warm += timer.Elapsed.TotalMilliseconds;
                    Assert.That(actual.Select((value, index) => Math.Abs(value - repeated[index])).Max(), Is.LessThan(1e-6));
                }
                string report = "{" + string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "\"backend\":\"{0}\",\"samples\":{1},\"first_ms\":{2:F4},\"warm_mean_ms\":{3:F4},\"normalized_rmse\":{4:G9}",
                    backend, input.Length, first, warm / 3, relative) + "}";
                File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_DTLN_REFERENCE"), "unity-" + backend + ".json"), report + "\n");
                TestContext.WriteLine(report);
                Assert.Throws<ArgumentException>(() => model.Enhance(new[] {float.NaN}));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Enhance(input, 0));
                Assert.Throws<OperationCanceledException>(() => model.Enhance(input, cancellationToken: new System.Threading.CancellationToken(true)));
                Assert.That(RelativeError(model.Enhance(input), expected), Is.LessThan(.002), "Cancellation must not contaminate a later utterance.");
            }
        }
        [Test, Explicit("Requires DTLN and the original ONNX raw streaming waveform.")]
        public void StreamingHopsFlushTailAndResetDeterministically()
        {
            var input = Read("input");
            var expected = Read("raw");
            using (var model = MNNDtln.Load(ModelDirectory))
            {
                var hop = new float[128];
                var output = new float[128];
                var raw = new float[expected.Length];
                model.ProcessHop(hop, output);
                model.Reset();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int start = 0; start < raw.Length; start += 128)
                {
                    Array.Clear(hop, 0, hop.Length);
                    if (start < input.Length) Array.Copy(input, start, hop, 0, Math.Min(hop.Length, input.Length - start));
                    model.ProcessHop(hop, output);
                    Array.Copy(output, 0, raw, start, output.Length);
                }
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Assert.Zero(allocated, "Prepared streaming hops must not allocate managed memory.");
                Assert.That(RelativeError(raw, expected), Is.LessThan(.0001));
                model.Reset();
                Array.Copy(input, hop, hop.Length);
                model.ProcessHop(hop, output);
                var first = (float[])output.Clone();
                model.ProcessHop(hop, output);
                model.Reset();
                model.ProcessHop(hop, output);
                Assert.That(output.Select((value, i) => Math.Abs(value - first[i])).Max(), Is.LessThan(1e-7));
                Assert.Throws<ArgumentException>(() => model.ProcessHop(new float[127], output));
                Assert.Throws<ArgumentException>(() => model.ProcessHop(hop, new float[127]));
            }
        }
        [Test, Explicit("Requires the DTLN graph pair, MNN plugin and original ONNX waveform oracle.")]
        public void StudioDiscoversEnhancementAndPreservesWaveRateAndLength()
        {
            var model = MNNStudioModel.Read(ModelDirectory);
            Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
            Assert.True(model.HasAudioProcessingPipeline);
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "dtln-studio-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                var input = Read("input");
                string wave = Path.Combine(directory, "noisy.wav");
                File.WriteAllBytes(wave, MNNWaveAudio.EncodePcm16(input, 16000));
                using (var backend = MNNStudioNativeBackend.Load(model, 1, false))
                {
                    var result = backend.Run(new MNNStudioRequest{Audio = wave});
                    Assert.AreEqual(input.Length, result.Waveform.Length);
                    Assert.AreEqual(16000, result.SampleRate);
                    Assert.That(RelativeError(result.Waveform, Read("enhanced")), Is.LessThan(.003));
                    Assert.That(result.BackendDescription, Does.StartWith("DTLN"));
                    var nonAligned = new float[11063];
                    for (int i = 0; i < nonAligned.Length; ++i) nonAligned[i] = .1f * (float)Math.Sin(i * .173);
                    File.WriteAllBytes(wave, MNNWaveAudio.EncodePcm16(nonAligned, 22050));
                    var other = backend.Run(new MNNStudioRequest{Audio = wave});
                    Assert.AreEqual(nonAligned.Length, other.Waveform.Length);
                    Assert.AreEqual(22050, other.SampleRate);
                }
            }
            finally { Directory.Delete(directory, true); }
        }
        private static float[] Read(string name)
        {
            string path = Environment.GetEnvironmentVariable("MNN_TEST_DTLN_REFERENCE");
            if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("Set MNN_TEST_DTLN_REFERENCE to the DTLN ONNX oracle directory.");
            var bytes = File.ReadAllBytes(Path.Combine(path, name + ".f32"));
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        private static double RelativeError(float[] actual, float[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            return Math.Sqrt(expected.Select((value, i) => Math.Pow((double)value - actual[i], 2)).Sum() / expected.Sum(value => (double)value * value));
        }
    }
}
