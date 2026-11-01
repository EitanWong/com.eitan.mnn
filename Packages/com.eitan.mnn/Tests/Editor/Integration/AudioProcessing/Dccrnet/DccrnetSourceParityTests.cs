using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Dccrnet")]
    public sealed class DccrnetSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/DCCRNet-Libri1Mix-16K-MNN");
        [Test]
        public void PublicApiHasCompleteWaveformEnhancement()
        {
            var type = typeof(MNNInterpreter).Assembly.GetType("MNN.Unity.MNNDccrnetEnhancement");
            Assert.NotNull(type, "Missing DCCRNet waveform API");
            Assert.NotNull(type.GetMethod("Enhance"));
            Assert.NotNull(type.GetMethod("EnhanceWaveFile"));
        }
        [Test]
        public void StudioDiscoversCompleteEnhancementPipeline() => Assert.True(MNNStudioModel.Read(Directory).HasAudioProcessingPipeline);
        private static readonly string[] Cases = { "mixture", "speech", "quiet", "gain2", "silence", "constant", "noise", "tone", "minimum", "maximum", "long", "boundary_before", "boundary_at", "boundary_after" };
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_DCCRNET_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_DCCRNET_REFERENCE to the original Asteroid oracle.");
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        private static void Check(float[] expected, float[] actual, string name)
        {
            Assert.AreEqual(expected.Length, actual.Length, name);
            double error = 0, energy = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                energy += (double)expected[i] * expected[i];
                double delta = (double)actual[i] - expected[i]; error += delta * delta;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";waveform_nrmse=" + nrmse);
            Assert.Less(nrmse, .001, name);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Auto), TestCase(MNNBackendType.Metal)]
        public void CompleteWaveformsMatchOriginalComplexAsteroid(MNNBackendType backend)
        {
            using (var model = MNNDccrnetEnhancement.Load(Directory, 4, backend))
            {
                foreach (string name in Cases.Concat(new[] { "mixture" }))
                    for (int repeat = 0; repeat < 2; ++repeat)
                        Check(Read(name + "-enhanced"), model.Enhance(Read(name + "-input")), name);
                Assert.AreEqual(backend == MNNBackendType.CPU ? MNNBackendType.CPU : MNNBackendType.Metal, model.Backend);
                Assert.AreEqual(backend == MNNBackendType.CPU ? MNNBackendType.CPU : MNNBackendType.Metal, model.DecoderBackend);
                Assert.AreEqual(MNNBackendType.CPU, model.RecurrentBackend);
            }
        }
        [Test]
        public void StudioExecutesActualWaveformAndCancellation()
        {
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(Directory), 4, false))
            {
                string path = Path.Combine(Reference, "speech.wav");
                var result = backend.Run(new MNNStudioRequest { Audio = path });
                Assert.AreEqual(16000, result.SampleRate);
                Check(Read("speech-enhanced"), result.Waveform, "Studio WAV");
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest
                {
                    Audio = path, Cancellation = new System.Threading.CancellationToken(true)
                }));
            }
        }
        [Test]
        public void MeasureWholeCallsAndAlternatingLengths()
        {
            foreach (int threads in new[] { 1, 2, 4 }) Measure(MNNBackendType.CPU, threads);
            Measure(MNNBackendType.Metal, 4);
        }
        private static void Measure(MNNBackendType backend, int threads)
        {
            using (var model = MNNDccrnetEnhancement.Load(Directory, threads, backend))
            {
                var pcm = Read("mixture-input"); model.Enhance(pcm); var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var actual = model.Enhance(pcm); timer.Stop();
                    timings[i] = timer.Elapsed.TotalMilliseconds; Check(Read("mixture-enhanced"), actual, "mixture");
                }
                Array.Sort(timings);
                TestContext.WriteLine("backend=" + backend + ";threads=" + threads + ";warm_ms=" + timings[2]);
                foreach (string name in new[] { "mixture", "minimum", "long", "maximum", "mixture" })
                {
                    var input = Read(name + "-input"); var timer = System.Diagnostics.Stopwatch.StartNew();
                    var actual = model.Enhance(input); timer.Stop(); Check(Read(name + "-enhanced"), actual, name);
                    TestContext.WriteLine("alternating_backend=" + backend + ";threads=" + threads + ";case=" + name + ";ms=" + timer.Elapsed.TotalMilliseconds);
                }
            }
        }
        [TestCase(MNNBackendType.CUDA), TestCase(MNNBackendType.OpenCL),
         TestCase(MNNBackendType.NN), TestCase(MNNBackendType.OpenGL), TestCase(MNNBackendType.Vulkan)]
        public void UnverifiedAcceleratorsAreRejected(MNNBackendType backend) =>
            Assert.Throws<NotSupportedException>(() => MNNDccrnetEnhancement.Load(Directory, backendType: backend));
        [Test]
        public void InputBoundsCancellationAndDisposalAreExplicit()
        {
            var model = MNNDccrnetEnhancement.Load(Directory, 1, MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[999]));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[48001]));
                var invalid = new float[1000]; invalid[7] = float.PositiveInfinity;
                Assert.Throws<ArgumentException>(() => model.Enhance(invalid));
                Assert.Throws<OperationCanceledException>(() => model.Enhance(new float[1000], cancellationToken: new System.Threading.CancellationToken(true)));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Enhance(new float[1000]));
        }
    }
}
