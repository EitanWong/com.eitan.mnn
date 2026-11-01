using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SpeechEnhancement")]
    public sealed class MossFormerGanSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/MossFormerGAN-SE-16K-MNN");
        private static string Reference
        {
            get
            {
                string value = Environment.GetEnvironmentVariable("MNN_TEST_MOSSFORMERGAN_REFERENCE");
                if (string.IsNullOrWhiteSpace(value)) Assert.Ignore("Set MNN_TEST_MOSSFORMERGAN_REFERENCE to the original checkpoint oracle.");
                return value;
            }
        }
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        private static void Check(float[] actual, float[] expected, string name, double tolerance = .001)
        {
            Assert.AreEqual(expected.Length, actual.Length, name);
            Assert.True(actual.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), name);
            double energy = expected.Sum(value => (double)value * value);
            double error = Math.Sqrt(actual.Select((value, i) => Math.Pow((double)value - expected[i], 2)).Sum() / Math.Max(1e-30, energy));
            TestContext.WriteLine(name + ": nrmse=" + error); Assert.Less(error, tolerance, name);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void NeuralCoreMatchesOriginalAcrossShapesAndRepeats(MNNBackendType backend)
        {
            using (var graph = new MNNGenerationGraph(Path.Combine(Directory, "mossformergan_fp32.mnn"), 1,
                backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None))
            {
                foreach (string name in new[] { "speech", "natural_speech", "quiet", "gain2", "noise", "tone", "short", "long", "speech" })
                {
                    var input = Read(name + "-features"); int frames = input.Length / (2 * 201);
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var output = graph.RunOutputs(new[] { "mask", "residual" },
                            new MNNGenerationGraph.Input("compressed_spectrum", input, 1, 2, frames, 201));
                        Check(output[0].Values, Read(name + "-mask"), name + " mask " + repeat);
                        Check(output[1].Values, Read(name + "-residual"), name + " residual " + repeat);
                    }
                }
                Assert.AreEqual(backend, graph.Backend);
            }
        }
        [Test]
        public void StudioExecutesActualOriginalWaveform()
        {
            var profile = MNNStudioModel.Read(Directory);
            Assert.True(profile.HasAudioProcessingPipeline);
            using (var backend = MNNStudioNativeBackend.Load(profile, 1, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = Path.Combine(Reference, "speech.wav") });
                Check(result.Waveform, Read("speech-enhanced"), "Studio");
                Assert.AreEqual(16000, result.SampleRate);
                StringAssert.Contains("MossFormerGAN", result.BackendDescription);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void EnhancedWaveformsMatchOriginalCheckpoint(MNNBackendType backend)
        {
            using (var model = MNNMossFormerGan.Load(Directory, 1, backend))
            {
                foreach (string name in new[] { "speech", "natural_speech", "quiet", "gain2", "noise", "tone", "short", "long", "minimum", "speech" })
                {
                    var pcm = Read(name + "-input"); var expected = Read(name + "-enhanced");
                    Check(model.Enhance(pcm), expected, name);
                    Check(model.Enhance(pcm), expected, name + " repeated");
                }
                Assert.AreNotEqual(MNNBackendType.Auto, model.Backend);
                if (backend != MNNBackendType.Auto) Assert.AreEqual(backend, model.Backend);
            }
        }

        [Test]
        public void SilenceInvalidInputsCancellationAndDisposalHaveExplicitContracts()
        {
            var model = MNNMossFormerGan.Load(Directory, 1, MNNBackendType.CPU);
            try
            {
                Assert.That(model.Enhance(new float[16000]), Is.All.EqualTo(0));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[200]));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[160001]));
                Assert.Throws<ArgumentException>(() => model.Enhance(new[] { float.NaN }));
                Assert.Throws<NotSupportedException>(() => MNNMossFormerGan.Load(Directory, backendType: MNNBackendType.NN));
                Assert.Throws<OperationCanceledException>(() => model.Enhance(Read("speech-input"), cancellationToken: new CancellationToken(true)));
                Check(model.Enhance(Read("speech-input")), Read("speech-enhanced"), "after cancellation");
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Enhance(new float[4096]));
        }

        [Test]
        public void MeasureWarmAndAlternatingShapeBackendChoices()
        {
            var pcm = Read("speech-input"); var expected = Read("speech-enhanced");
            var inputs = new[] { pcm, Read("natural_speech-input"), Read("short-input"), pcm };
            var outputs = new[] { expected, Read("natural_speech-enhanced"), Read("short-enhanced"), expected };
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4 } : new[] { 1 })
            using (var model = MNNMossFormerGan.Load(Directory, threads, backend))
            {
                Check(model.Enhance(pcm), expected, "warmup");
                var warm = new double[5];
                for (int i = 0; i < warm.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var actual = model.Enhance(pcm);
                    timer.Stop(); warm[i] = timer.Elapsed.TotalMilliseconds; Check(actual, expected, "timing");
                }
                Array.Sort(warm);
                TestContext.WriteLine("backend=" + backend + "; threads=" + threads + "; median_ms=" + warm[2]);
                var alternating = new double[inputs.Length];
                for (int i = 0; i < inputs.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var actual = model.Enhance(inputs[i]);
                    timer.Stop(); alternating[i] = timer.Elapsed.TotalMilliseconds; Check(actual, outputs[i], "alternating " + i);
                }
                TestContext.WriteLine("alternating_backend=" + backend + "; threads=" + threads + "; milliseconds=" + string.Join(",", alternating));
            }
        }

        [Test]
        public void MeasureMetalTuningModesOnSourceCore()
        {
            var input = Read("speech-features");
            foreach (var tuning in new[] { MNNGpuTuningMode.None, MNNGpuTuningMode.Fast, MNNGpuTuningMode.Wide })
            using (var graph = new MNNGenerationGraph(Path.Combine(Directory, "mossformergan_fp32.mnn"), 1,
                backendType: MNNBackendType.Metal, precisionMode: MNNPrecisionMode.High,
                gpuTuningMode: tuning))
            {
                var times = new double[5];
                for (int i = -2; i < times.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var values = graph.RunOutputs(new[] { "mask", "residual" },
                        new MNNGenerationGraph.Input("compressed_spectrum", input, 1, 2, input.Length / 402, 201));
                    timer.Stop();
                    Check(values[0].Values, Read("speech-mask"), "tuning mask " + tuning);
                    Check(values[1].Values, Read("speech-residual"), "tuning residual " + tuning);
                    if (i >= 0) times[i] = timer.Elapsed.TotalMilliseconds;
                }
                Array.Sort(times);
                TestContext.WriteLine("metal_tuning=" + tuning + "; median_core_ms=" + times[2]);
                foreach (string name in new[] { "natural_speech", "short", "speech" })
                {
                    var x = Read(name + "-features");
                    var values = graph.RunOutputs(new[] { "mask", "residual" },
                        new MNNGenerationGraph.Input("compressed_spectrum", x, 1, 2, x.Length / 402, 201));
                    Check(values[0].Values, Read(name + "-mask"), "tuning changed shape mask " + name);
                    Check(values[1].Values, Read(name + "-residual"), "tuning changed shape residual " + name);
                }
            }
        }


    }
}
