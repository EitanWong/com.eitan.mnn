using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class DeepFilterNet3Tests
    {
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_DEEPFILTERNET_REFERENCE");
        private static float[] Read(string name)
        {
            if (string.IsNullOrEmpty(Reference)) Assert.Ignore("Set MNN_TEST_DEEPFILTERNET_REFERENCE to the libDF/ONNX oracle directory.");
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        [Test]
        public void AnalysisAndSynthesisRecoverNonAlignedInput()
        {
            var input = new float[4817];
            for (int i = 0; i < input.Length; ++i) input[i] = (float)(.15 * Math.Sin(i * .19));
            var dsp = new MNNDeepFilterNet3Dsp();
            var features = dsp.Analyze(input);
            Assert.AreEqual(input.Length / 480 + 2, features.Frames);
            var output = dsp.Synthesize(features.Spectrum, features.Frames, input.Length);
            Assert.AreEqual(input.Length, output.Length);
            for (int i = 0; i < input.Length; ++i) Assert.AreEqual(input[i], output[i], 1e-6, "Sample " + i);
        }
        [Test]
        public void FrontendAndComplexFilteringMatchOfficialLibDfOracle()
        {
            var dsp = new MNNDeepFilterNet3Dsp();
            var input = Read("input");
            var features = dsp.Analyze(input);
            AssertMaximumError(Read("spectrum"), features.Spectrum, 2e-6);
            AssertMaximumError(Read("feat_erb"), features.Erb, 2e-5);
            AssertMaximumError(Read("feat_spec"), features.Complex, 2e-5);
            var filtered = dsp.Filter(features, Read("mask"), Read("coefs"));
            var output = dsp.Synthesize(filtered, features.Frames, input.Length);
            AssertMaximumError(Read("enhanced"), output, 2e-5);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires the local three-graph model, MNN plugin and libDF/ONNX Runtime oracle.")]
        public void NativeEnhancementMatchesReferenceAndMeasuresWarmLatency(MNNBackendType backend)
        {
            if (backend == MNNBackendType.Metal && !MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore("Metal unavailable.");
            var input = Read("input");
            var expected = Read("enhanced");
            string directory = Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models", "DeepFilterNet3-MNN"));
            using (var model = MNNDeepFilterNet3.Load(directory, 2, backend))
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var output = model.Enhance(input);
                timer.Stop();
                double first = timer.Elapsed.TotalMilliseconds;
                Assert.AreEqual(backend, model.EncoderBackend);
                Assert.AreEqual(backend, model.ErbDecoderBackend);
                Assert.AreEqual(backend, model.DfDecoderBackend);
                AssertMaximumError(expected, output, backend == MNNBackendType.CPU ? 1e-5 : .003);
                double relative = Math.Sqrt(expected.Select((value, i) => Math.Pow(value - output[i], 2)).Sum() / expected.Sum(value => (double)value * value));
                Assert.That(relative, Is.LessThan(backend == MNNBackendType.CPU ? 2e-5 : .002), "Normalized waveform RMSE.");
                double warmMilliseconds = 0;
                for (int i = 0; i < 3; ++i)
                {
                    timer.Restart();
                    var repeated = model.Enhance(input);
                    timer.Stop();
                    warmMilliseconds += timer.Elapsed.TotalMilliseconds;
                    AssertMaximumError(output, repeated, 1e-6);
                }
                string report = "{" + string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "\"requested\":\"{0}\",\"encoder\":\"{1}\",\"erb\":\"{2}\",\"df\":\"{3}\",\"samples\":{4},\"first_ms\":{5:F4},\"warm_ms\":{6:F4},\"normalized_rmse\":{7:G9}",
                    backend, model.EncoderBackend, model.ErbDecoderBackend, model.DfDecoderBackend, input.Length, first, warmMilliseconds / 3, relative) + "}";
                File.WriteAllText(Path.Combine(Reference, "unity-" + backend + ".json"), report + "\n");
                TestContext.WriteLine(report);
                Assert.Throws<ArgumentException>(() => model.Enhance(new[] {float.NaN}));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Enhance(new float[1], 0));
                var cancelled = new System.Threading.CancellationToken(true);
                Assert.Throws<OperationCanceledException>(() => model.Enhance(input, cancellationToken: cancelled));
            }
        }
        private static void AssertMaximumError(float[] expected, float[] actual, double maximum)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            double error = expected.Select((value, i) => Math.Abs((double)value - actual[i])).Max();
            TestContext.WriteLine("Maximum absolute error: " + error);
            Assert.That(error, Is.LessThan(maximum));
        }
    }
}
