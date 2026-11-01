using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SpExPlus")]
    public sealed class SpExPlusSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/SpExPlus-WSJ02Mix-8K-MNN");
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_SPEXPLUS_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_SPEXPLUS_REFERENCE to the pinned author oracle.");
        private static readonly string[] Cases = { "minimum", "encoder_before", "encoder_at", "encoder_after", "pool_before", "pool_at", "pool_after", "long_reference", "maximum", "one_second", "odd", "real", "other_speaker", "quiet", "gain", "silence", "silent_reference", "dc", "inverted_reference", "reference_short", "impulse", "silence_both", "silence_short_reference", "silence_quiet_reference" };
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var result = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result;
        }
        private static void Check(string name, float[] actual, string suffix = "target")
        {
            var expected = Read(name + "-" + suffix); Assert.AreEqual(expected.Length, actual.Length, name);
            double energy = 0, error = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                energy += (double)expected[i] * expected[i]; double delta = actual[i] - expected[i]; error += delta * delta;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";" + suffix + ";nrmse=" + nrmse); Assert.Less(nrmse, .001, name);
        }
        [Test]
        public void CatalogueClassifiesTargetExtractionFamily()
        {
            Assert.AreEqual("AudioProcessing", MNNModelRepository.Classify("owner/SpExPlus-WSJ02Mix-8K-MNN"));
            Assert.AreEqual("AudioProcessing", MNNModelRepository.Classify("owner/SpEx-plus-MNN"));
        }
        [Test]
        public void PublicApiSupportsReusableReferenceEncoding()
        {
            Assert.NotNull(typeof(MNNSpExPlusExtraction).GetMethod("EncodeReference"));
            Assert.NotNull(typeof(MNNSpExPlusExtraction).GetMethod("Extract"));
        }
        [Test]
        public void StudioDiscoversSpeechReferencePipeline()
        {
            var model = MNNStudioModel.Read(Directory); Assert.True(model.HasAudioProcessingPipeline);
            Assert.True(model.IsSpExPlus); Assert.False(model.RequiresVisualFrames);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void CompleteWaveformsAndEmbeddingsMatchOriginalAuthor(MNNBackendType backend)
        {
            using (var model = MNNSpExPlusExtraction.Load(Directory, 4, backend))
            {
                foreach (string name in Cases.Concat(new[] { "minimum" }))
                {
                    var reference = model.EncodeReference(Read(name + "-reference")); Check(name, reference.ToArray(), "embedding");
                    for (int repeat = 0; repeat < 2; ++repeat) Check(name, model.Extract(Read(name + "-input"), reference));
                    Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.Backend, name);
                    Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.ReferenceBackend, name);
                }
            }
        }
        [Test]
        public void AutoUsesMetalForWholeAndCachedRecordings()
        {
            using (var model = MNNSpExPlusExtraction.Load(Directory))
            {
                Assert.AreEqual(MNNBackendType.Metal, model.Backend);
                Assert.AreEqual(MNNBackendType.Metal, model.ReferenceBackend);
            }
        }
        [Test]
        public void ReferenceIsImmutableAndReusableAcrossShapesAndInstances()
        {
            MNNSpExPlusReference reference;
            using (var first = MNNSpExPlusExtraction.Load(Directory, 1, MNNBackendType.CPU))
            {
                reference = first.EncodeReference(Read("real-reference")); Assert.AreEqual(256, reference.Size);
                var exposed = reference.ToArray(); exposed[0] = float.NaN;
                Check("real", first.Extract(Read("real-input"), reference));
                first.Extract(Read("minimum-input"), reference);
                Check("real", first.Extract(Read("real-input"), reference));
            }
            using (var second = MNNSpExPlusExtraction.Load(Directory, 4, MNNBackendType.Metal))
                Check("real", second.Extract(Read("real-input"), reference));
        }
        [Test]
        public void StudioRunsTwoRealRecordingsWithNormalizedPlayback()
        {
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(Directory), 4, false))
            {
                var request = new MNNStudioRequest { Audio = Path.Combine(Reference, "real.wav"), SpeakerReference = Path.Combine(Reference, "target-reference.wav") };
                var result = backend.Run(request); Assert.AreEqual(8000, result.SampleRate);
                var expected = Read("real-target"); float scale = .95f / expected.Max(v => Math.Abs(v));
                Assert.AreEqual(expected.Length, result.Waveform.Length);
                double energy = 0, error = 0;
                for (int i = 0; i < expected.Length; ++i)
                {
                    double value = expected[i] * scale; energy += value * value;
                    double delta = result.Waveform[i] - value; error += delta * delta;
                }
                Assert.Less(Math.Sqrt(error / energy), .001); Assert.LessOrEqual(result.Waveform.Max(v => Math.Abs(v)), .95001f);
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest { Audio = request.Audio }));
                request.Cancellation = new CancellationToken(true); Assert.Throws<OperationCanceledException>(() => backend.Run(request));
            }
        }
        [Test]
        public void MeasureWholeAndCachedCallsAndAlternatingLengths()
        {
            foreach (int threads in new[] { 1, 2, 4 }) Measure(MNNBackendType.CPU, threads);
            Measure(MNNBackendType.Metal, 4);
        }
        private static void Measure(MNNBackendType backend, int threads)
        {
            using (var model = MNNSpExPlusExtraction.Load(Directory, threads, backend))
            {
                var input = Read("real-input"); var speech = Read("real-reference"); var reference = model.EncodeReference(speech);
                model.Extract(input, reference);
                foreach (bool cached in new[] { false, true })
                {
                    var times = new double[5];
                    for (int i = 0; i < times.Length; ++i)
                    {
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        var conditioning = cached ? reference : model.EncodeReference(speech);
                        var output = model.Extract(input, conditioning); timer.Stop(); times[i] = timer.Elapsed.TotalMilliseconds; Check("real", output);
                    }
                    Array.Sort(times); TestContext.WriteLine("backend=" + backend + ";threads=" + threads + ";cached=" + cached + ";warm_ms=" + times[2]);
                }
                foreach (string name in new[] { "minimum", "real", "long_reference", "maximum", "real" })
                {
                    var x = Read(name + "-input"); var r = Read(name + "-reference"); var timer = System.Diagnostics.Stopwatch.StartNew();
                    var conditioning = model.EncodeReference(r); var output = model.Extract(x, conditioning); timer.Stop(); Check(name, output);
                    TestContext.WriteLine("alternating_backend=" + backend + ";threads=" + threads + ";case=" + name + ";ms=" + timer.Elapsed.TotalMilliseconds);
                }
            }
        }
        [TestCase(MNNBackendType.CUDA), TestCase(MNNBackendType.OpenCL), TestCase(MNNBackendType.NN), TestCase(MNNBackendType.OpenGL), TestCase(MNNBackendType.Vulkan)]
        public void UnverifiedAcceleratorsAreRejected(MNNBackendType backend) => Assert.Throws<NotSupportedException>(() => MNNSpExPlusExtraction.Load(Directory, backendType: backend));
        [Test]
        public void ResamplingRestoresOriginalSampleCount()
        {
            using (var model = MNNSpExPlusExtraction.Load(Directory, 1, MNNBackendType.CPU))
            {
                var reference = model.EncodeReference(new float[1680], 48000);
                var output = model.Extract(new float[121], reference, 48000); Assert.AreEqual(121, output.Length);
                Assert.True(output.All(v => !float.IsNaN(v) && !float.IsInfinity(v)));
            }
        }
        [Test]
        public void BoundsCancellationAndDisposalAreExplicit()
        {
            var model = MNNSpExPlusExtraction.Load(Directory, 1, MNNBackendType.CPU);
            var reference = model.EncodeReference(new float[280]);
            try
            {
                Assert.Throws<ArgumentException>(() => model.EncodeReference(new float[279]));
                Assert.Throws<ArgumentException>(() => model.EncodeReference(new float[64001]));
                Assert.Throws<ArgumentException>(() => model.Extract(new float[19], reference));
                Assert.Throws<ArgumentException>(() => model.Extract(new float[32001], reference));
                Assert.Throws<ArgumentNullException>(() => model.Extract(new float[20], null));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.EncodeReference(new float[280], 7999));
                var invalid = new float[280]; invalid[0] = float.NaN;
                Assert.Throws<ArgumentException>(() => model.EncodeReference(invalid));
                var pcm = new float[20]; pcm[0] = float.PositiveInfinity;
                Assert.Throws<ArgumentException>(() => model.Extract(pcm, reference));
                Assert.Throws<OperationCanceledException>(() => model.EncodeReference(new float[280], cancellationToken: new CancellationToken(true)));
                Assert.Throws<OperationCanceledException>(() => model.Extract(new float[20], reference, cancellationToken: new CancellationToken(true)));
                var output = model.Extract(Read("encoder_after-input"), reference); Assert.AreEqual(0, output[output.Length - 1]);
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.EncodeReference(new float[280]));
            Assert.Throws<ObjectDisposedException>(() => model.Extract(new float[20], reference));
        }
    }
}
