using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SudoRmRf")]
    public sealed class SudoRmRfSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/SudoRMRF-U16-WSJ02Mix-8K-MNN");
        [Test]
        public void PublicApiHasCompleteWaveformSeparation()
        {
            var type = typeof(MNNInterpreter).Assembly.GetType("MNN.Unity.MNNSudoRmRfSeparation");
            Assert.NotNull(type, "Missing SudoRMRF waveform API");
            Assert.NotNull(type.GetMethod("Separate"));
            Assert.NotNull(type.GetMethod("SeparateWaveFile"));
        }
        [Test]
        public void StudioDiscoversCompleteSeparationPipeline() => Assert.True(MNNStudioModel.Read(Directory).HasAudioProcessingPipeline);
        private static readonly string[] Cases = { "minimum", "silence", "constant", "boundary_before", "boundary_at", "boundary_after", "short", "noise", "tone", "speech", "quiet", "gain2", "mixture", "long", "maximum" };
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_SUDORMRF_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_SUDORMRF_REFERENCE to the author's waveform oracle.");
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        private static void Check(string name, MNNSeparatedAudio actual)
        {
            var expected = Read(name + "-sources");
            CheckSpeaker(expected, actual.Speaker0, 0, name);
            CheckSpeaker(expected, actual.Speaker1, 1, name);
            Assert.AreEqual(8000, actual.SampleRate);
        }
        private static void CheckSpeaker(float[] expected, float[] actual, int speaker, string name)
        {
            Assert.AreEqual(expected.Length / 2, actual.Length, name);
            double error = 0, energy = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), name);
                double value = expected[i * 2 + speaker];
                energy += value * value;
                double delta = actual[i] - value;
                error += delta * delta;
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ";speaker=" + speaker + ";nrmse=" + nrmse);
            Assert.Less(nrmse, .001, name + " speaker " + speaker);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void CompleteWaveformsMatchOriginalAuthor(MNNBackendType backend)
        {
            using (var model = MNNSudoRmRfSeparation.Load(Directory, 4, backend))
            {
                foreach (string name in Cases.Concat(new[] { "minimum" }))
                    for (int repeat = 0; repeat < 2; ++repeat)
                        Check(name, model.Separate(Read(name + "-input")));
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.Backend);
            }
        }
        [Test]
        public void StudioExecutesActualWaveformAndCancellation()
        {
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(Directory), 4, false))
            {
                string path = Path.Combine(Reference, "speech.wav");
                var result = backend.Run(new MNNStudioRequest { Audio = path });
                Assert.AreEqual(8000, result.SampleRate);
                CheckSpeaker(Read("speech-sources"), result.Waveform, 0, "Studio WAV");
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
            using (var model = MNNSudoRmRfSeparation.Load(Directory, threads, backend))
            {
                var pcm = Read("mixture-input"); model.Separate(pcm); var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var actual = model.Separate(pcm); timer.Stop();
                    timings[i] = timer.Elapsed.TotalMilliseconds; Check("mixture", actual);
                }
                Array.Sort(timings);
                TestContext.WriteLine("backend=" + backend + ";threads=" + threads + ";warm_ms=" + timings[2]);
                foreach (string name in new[] { "mixture", "minimum", "long", "maximum", "mixture" })
                {
                    var input = Read(name + "-input"); var timer = System.Diagnostics.Stopwatch.StartNew();
                    var actual = model.Separate(input); timer.Stop(); Check(name, actual);
                    TestContext.WriteLine("alternating_backend=" + backend + ";threads=" + threads + ";case=" + name + ";ms=" + timer.Elapsed.TotalMilliseconds);
                }
            }
        }
        [TestCase(MNNBackendType.CUDA), TestCase(MNNBackendType.OpenCL), TestCase(MNNBackendType.NN),
         TestCase(MNNBackendType.OpenGL), TestCase(MNNBackendType.Vulkan)]
        public void UnverifiedAcceleratorsAreRejected(MNNBackendType backend) =>
            Assert.Throws<NotSupportedException>(() => MNNSudoRmRfSeparation.Load(Directory, backendType: backend));
        [Test]
        public void InputBoundsCancellationAndDisposalAreExplicit()
        {
            var model = MNNSudoRmRfSeparation.Load(Directory, 1, MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(() => model.Separate(new float[1]));
                Assert.Throws<ArgumentException>(() => model.Separate(new float[32001]));
                var invalid = new float[2]; invalid[1] = float.PositiveInfinity;
                Assert.Throws<ArgumentException>(() => model.Separate(invalid));
                Assert.Throws<OperationCanceledException>(() => model.Separate(new float[2], cancellationToken: new System.Threading.CancellationToken(true)));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Separate(new float[2]));
        }
    }
}
