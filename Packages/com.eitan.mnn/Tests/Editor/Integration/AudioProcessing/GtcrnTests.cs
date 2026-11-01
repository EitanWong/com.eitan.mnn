using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class GtcrnTests
    {
        private static string ModelDirectory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/GTCRN-MNN");

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires independent physical-signal and eight-second recurrent-state references.")]
        public void PhysicalSignalsMatchSourceAcrossRepeatedRecordings(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            using (var model = MNNGtcrn.Load(ModelDirectory, 1, backend))
                foreach (string name in new[]{"speech_0.001", "speech_0.01", "speech_0.1", "speech_1", "speech_2", "tone", "noise", "silence", "long", "speech_1"})
                {
                    var expected = Read("conditions/" + name + "-enhanced");
                    var actual = model.Enhance(Read("conditions/" + name + "-input"));
                    TestContext.WriteLine(backend + " condition=" + name);
                    Assert.That(actual.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                    if (expected.All(value => value == 0)) Assert.That(actual.Select(Math.Abs).Max(), Is.LessThan(1e-8));
                    else Assert.That(Error(actual, expected), Is.LessThan(.002));
                }
        }

        [TestCase(1), TestCase(2), TestCase(4)]
        [Explicit("Measures complete warmed CPU enhancement under explicit thread budgets.")]
        public void CpuThreadBudgetsPreserveSourceOutput(int threads)
        {
            var input = Read("input");
            var expected = Read("enhanced");
            using (var model = MNNGtcrn.Load(ModelDirectory, threads, MNNBackendType.CPU))
            {
                model.Enhance(input);
                var milliseconds = new double[5];
                for (int i = 0; i < milliseconds.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var actual = model.Enhance(input);
                    milliseconds[i] = timer.Elapsed.TotalMilliseconds;
                    Assert.That(Error(actual, expected), Is.LessThan(.002));
                }
                Array.Sort(milliseconds);
                TestContext.WriteLine("threads=" + threads + " warm median ms=" + milliseconds[2]);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires official GTCRN PyTorch waveform and fixed-frame references.")]
        public void WaveformMatchesOriginalAndPreservesLength(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            using (var model = MNNGtcrn.Load(ModelDirectory, 1, backend))
            {
                var input = Read("input");
                var expected = Read("enhanced");
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var actual = model.Enhance(input);
                TestContext.WriteLine(backend + " first waveform ms=" + timer.Elapsed.TotalMilliseconds);
                Assert.That(Error(actual, expected), Is.LessThan(.002));
                var warmed = new double[3];
                for (int i = 0; i < warmed.Length; ++i)
                {
                    timer.Restart();
                    Assert.That(Error(model.Enhance(input), expected), Is.LessThan(.002));
                    warmed[i] = timer.Elapsed.TotalMilliseconds;
                }
                Array.Sort(warmed);
                TestContext.WriteLine(backend + " warm median ms=" + warmed[1]);
                var repeated = model.Enhance(input);
                Assert.That(actual.Select((value, i) => Math.Abs(value - repeated[i])).Max(), Is.LessThan(1e-6));
                Assert.AreEqual(backend, model.Backend);
                Assert.AreEqual(11063, model.Enhance(new float[11063], 22050).Length);
                Assert.AreEqual(1, model.Enhance(new float[1]).Length);
                Assert.Throws<ArgumentException>(() => model.Enhance(new[]{float.NaN}));
                Assert.Throws<OperationCanceledException>(() => model.Enhance(input, cancellationToken: new CancellationToken(true)));
                Assert.That(Error(model.Enhance(input), expected), Is.LessThan(.002));
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires all official streaming PyTorch spectrum frames and tests recurrent cache ownership.")]
        public void FixedFramesMatchSourceWithoutManagedAllocations(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            var features = Read("features");
            var expected = Read("spectrum");
            int frames = features.Length / (257 * 2);
            var frame = new float[514];
            var output = new float[514];
            var actual = new float[expected.Length];
            using (var model = MNNGtcrn.Load(ModelDirectory, 1, backend))
            {
                model.ProcessSpectrum(frame, output);
                model.Reset();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int f = 0; f < frames; ++f)
                {
                    for (int bin = 0; bin < 257; ++bin)
                        for (int part = 0; part < 2; ++part) frame[bin * 2 + part] = features[(bin * frames + f) * 2 + part];
                    model.ProcessSpectrum(frame, output);
                    for (int bin = 0; bin < 257; ++bin)
                        for (int part = 0; part < 2; ++part) actual[(bin * frames + f) * 2 + part] = output[bin * 2 + part];
                }
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                TestContext.WriteLine(backend + " prepared spectrum managed bytes=" + allocated);
                Assert.Zero(allocated);
                Assert.That(Error(actual, expected), Is.LessThan(.002));
                model.Reset();
                model.ProcessSpectrum(frame, output);
                var first = (float[])output.Clone();
                Assert.Throws<OperationCanceledException>(() => model.ProcessSpectrum(frame, output, new CancellationToken(true)));
                model.Reset();
                model.ProcessSpectrum(frame, output);
                CollectionAssert.AreEqual(first, output);
            }
        }

        [Test, Explicit("Requires the graph pair and original waveform to exercise Studio end to end.")]
        public void StudioRunsEnhancementAndPreservesInputRate()
        {
            var model = MNNStudioModel.Read(ModelDirectory);
            Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
            Assert.True(model.HasAudioProcessingPipeline);
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "gtcrn-studio-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                var input = Read("input");
                string wave = Path.Combine(directory, "input.wav");
                File.WriteAllBytes(wave, MNNWaveAudio.EncodePcm16(input, 16000));
                using (var backend = MNNStudioNativeBackend.Load(model, 1, false))
                {
                    var result = backend.Run(new MNNStudioRequest{Audio = wave});
                    Assert.AreEqual(16000, result.SampleRate);
                    Assert.That(Error(result.Waveform, Read("enhanced")), Is.LessThan(.003));
                    Assert.That(result.BackendDescription, Does.StartWith("GTCRN"));
                    File.WriteAllBytes(wave, MNNWaveAudio.EncodePcm16(new float[11063], 22050));
                    var other = backend.Run(new MNNStudioRequest{Audio = wave});
                    Assert.AreEqual(11063, other.Waveform.Length);
                    Assert.AreEqual(22050, other.SampleRate);
                }
            }
            finally { Directory.Delete(directory, true); }
        }

        private static float[] Read(string name)
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_GTCRN_REFERENCE");
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Set MNN_TEST_GTCRN_REFERENCE to the saved official reference.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, name + ".f32"));
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        private static double Error(float[] actual, float[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            double error = Math.Sqrt(actual.Select((value, i) => Math.Pow((double)value - expected[i], 2)).Sum() / expected.Sum(value => (double)value * value));
            TestContext.WriteLine("NRMSE=" + error);
            return error;
        }
    }
}
