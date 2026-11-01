using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("DtlnAec")]
    public sealed class DtlnAecSourceParityTests
    {
        private static string Reference
        {
            get
            {
                string value = Environment.GetEnvironmentVariable("MNN_TEST_DTLN_AEC_REFERENCE");
                if (string.IsNullOrWhiteSpace(value)) Assert.Ignore("Set MNN_TEST_DTLN_AEC_REFERENCE to the original TFLite/builtin LiteRT oracle.");
                return value;
            }
        }
        private static string ModelDirectory(int units) => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/DTLN-aec-" + units + "-MNN");
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        private static void Check(float[] expected, float[] actual, double budget, string context)
        {
            Assert.AreEqual(expected.Length, actual.Length, context);
            Assert.True(actual.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), context);
            double squared = 0, energy = 0, maximum = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                double error = (double)actual[i] - expected[i];
                squared += error * error;
                energy += (double)expected[i] * expected[i];
                maximum = Math.Max(maximum, Math.Abs(error));
            }
            double nrmse = Math.Sqrt(squared / Math.Max(energy, 1e-30));
            TestContext.WriteLine(context + " NRMSE=" + nrmse + "; max absolute=" + maximum);
            if (energy == 0) Assert.That(maximum, Is.LessThan(1e-6), context);
            else Assert.That(nrmse, Is.LessThan(budget), context);
        }

        [TestCase(128, MNNBackendType.CPU), TestCase(256, MNNBackendType.CPU), TestCase(512, MNNBackendType.CPU)]
        [TestCase(128, MNNBackendType.Metal), TestCase(256, MNNBackendType.Metal), TestCase(512, MNNBackendType.Metal)]
        [TestCase(128, MNNBackendType.Auto), TestCase(256, MNNBackendType.Auto), TestCase(512, MNNBackendType.Auto)]
        public void WaveformsMatchOriginalTflitePipeline(int units, MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            using (var model = MNNDtlnAec.Load(ModelDirectory(units), backend == MNNBackendType.Auto ? 0 : 1, backend))
            {
                foreach (string name in new[] { "doubletalk", "farend_singletalk", "nearend_singletalk", "quiet", "mic_only", "reference_only", "silence", "noise", "short", "long", "doubletalk" })
                    Check(Read(name + "-" + units + "-enhanced"), model.Process(Read(name + "-mic"), Read(name + "-ref")), .001, name);
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend, model.Backend);
            }
        }

        [TestCase(128, MNNBackendType.CPU), TestCase(256, MNNBackendType.CPU), TestCase(512, MNNBackendType.CPU)]
        [TestCase(128, MNNBackendType.Metal), TestCase(256, MNNBackendType.Metal), TestCase(512, MNNBackendType.Metal)]
        public void StageOutputsAndStatesMatchOriginalTflite(int units, MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            for (int stage = 1; stage <= 2; ++stage)
            using (var runner = new MNNDtlnAecStage(ModelDirectory(units), units, stage, 1, backend))
            {
                int width = stage == 1 ? 257 : 512;
                var first = new float[width]; var second = new float[width]; var state = new float[units * 4];
                var sourceInputs = Enumerable.Range(0, 3).Select(i => Read("trace-" + units + "-" + stage + "-input_" + (3 + (stage - 1) * 3 + i))).ToArray();
                var expectedSignal = Read("trace-" + units + "-" + stage + "-Identity");
                var expectedState = Read("trace-" + units + "-" + stage + "-Identity_1");
                for (int frame = 0; frame < 32; ++frame)
                {
                    Array.Copy(sourceInputs[0], frame * width, first, 0, width);
                    Array.Copy(sourceInputs[1], frame * width, second, 0, width);
                    Array.Copy(sourceInputs[2], frame * state.Length, state, 0, state.Length);
                    runner.Run(first, second, state);
                    var mapped = runner.Signal.MapForRead<float>();
                    try { Check(expectedSignal.Skip(frame * width).Take(width).ToArray(), mapped.ToArray(), .0003, "stage" + stage + " frame" + frame + " signal"); }
                    finally { runner.Signal.Unmap(); }
                    Check(expectedState.Skip(frame * state.Length).Take(state.Length).ToArray(), state, .0003, "stage" + stage + " frame" + frame + " state");
                }
                Assert.AreEqual(backend, runner.Backend);
            }
        }

        [TestCase(128, MNNBackendType.CPU), TestCase(256, MNNBackendType.CPU), TestCase(512, MNNBackendType.CPU)]
        [TestCase(128, MNNBackendType.Metal), TestCase(256, MNNBackendType.Metal), TestCase(512, MNNBackendType.Metal)]
        public void PreparedHopsKeepZeroManagedAllocation(int units, MNNBackendType backend)
        {
            var mic = new float[128]; var reference = new float[128]; var output = new float[128];
            using (var model = MNNDtlnAec.Load(ModelDirectory(units), 1, backend))
            {
                for (int i = 0; i < 8; ++i) model.ProcessHop(mic, reference, output);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 64; ++i) model.ProcessHop(mic, reference, output);
                Assert.Zero(GC.GetAllocatedBytesForCurrentThread() - before);
            }
        }

        [TestCase(128), TestCase(256), TestCase(512)]
        public void MeasureWarmWholeCallBackendAndThreadChoices(int units)
        {
            var mic = Read("doubletalk-mic"); var reference = Read("doubletalk-ref");
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4 } : new[] { 1 })
            using (var model = MNNDtlnAec.Load(ModelDirectory(units), threads, backend))
            {
                model.Process(mic, reference);
                var samples = new double[5]; var timer = new System.Diagnostics.Stopwatch();
                for (int i = 0; i < samples.Length; ++i)
                {
                    timer.Restart(); var result = model.Process(mic, reference); timer.Stop();
                    Assert.AreEqual(mic.Length, result.Length); samples[i] = timer.Elapsed.TotalMilliseconds;
                }
                Array.Sort(samples);
                TestContext.WriteLine("units=" + units + "; backend=" + backend + "; threads=" + threads + "; samples=" + mic.Length + "; median_ms=" + samples[2]);
            }
        }
    }
}
