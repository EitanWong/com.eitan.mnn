using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SileroVad")]
    public class SileroVadPerformanceTests
    {
        [Serializable] private sealed class Row
        {
            public string requested, actual, rejection;
            public int threads;
            public bool qualityPassed = true;
            public double loadMs, medianMs, maxProbabilityError;
            public double[] samplesMs;
        }
        [TestCase(MNNBackendType.CPU, 1), TestCase(MNNBackendType.CPU, 2)]
        [TestCase(MNNBackendType.CPU, 4), TestCase(MNNBackendType.CPU, 8)]
        [TestCase(MNNBackendType.Metal, 1)]
        [Explicit("Uses existing pinned VAD and independently generated ONNX references; no downloads.")]
        public void CompareBackendsWithProbabilityAndIntervalQualityGates(MNNBackendType backend, int threads)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SILERO_ROOT");
            if (root == null) Assert.Ignore("Set MNN_TEST_SILERO_ROOT.");
            var bytes = File.ReadAllBytes(Path.Combine(root, "Reference/two-utterances-pcm.f32"));
            var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
            bytes = File.ReadAllBytes(Path.Combine(root, "Reference/two-utterances-probs.f32"));
            var reference = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, reference, 0, bytes.Length);
            var row = new Row {requested = backend.ToString(), threads = threads, samplesMs = new double[9]};
            var timer = Stopwatch.StartNew();
            try
            {
                using (var vad = MNNSileroVad.Load(Path.Combine(root, "Models"), threads, backend))
                {
                    row.loadMs = timer.Elapsed.TotalMilliseconds; row.actual = vad.Backend.ToString();
                    for (int i = -1; i < row.samplesMs.Length; ++i)
                    {
                        timer.Restart(); var result = vad.Detect(pcm, 16000);
                        if (i >= 0) row.samplesMs[i] = timer.Elapsed.TotalMilliseconds;
                        row.maxProbabilityError = result.Probabilities.Select((p, n) => (double)Math.Abs(p - reference[n])).Max();
                        row.qualityPassed &= row.maxProbabilityError < .08 && result.Segments.Count == 2 &&
                            result.Segments[0].StartSeconds == 6176 / 16000.0 && result.Segments[0].EndSeconds == 99808 / 16000.0 &&
                            result.Segments[1].StartSeconds == 128032 / 16000.0 && result.Segments[1].EndSeconds == 222176 / 16000.0;
                    }
                    row.medianMs = row.samplesMs.OrderBy(x => x).ElementAt(4);
                }
            }
            catch (Exception error) when (backend != MNNBackendType.CPU && (error is MNNException || error is InvalidDataException))
            {
                row.qualityPassed = false; row.rejection = error.GetType().Name + ": " + error.Message; row.samplesMs = Array.Empty<double>();
            }
            Directory.CreateDirectory(Path.Combine(root, "Performance"));
            File.WriteAllText(Path.Combine(root, "Performance", backend + "-" + threads + ".json"), JsonUtility.ToJson(row, true));
            TestContext.WriteLine(JsonUtility.ToJson(row));
            if (backend == MNNBackendType.CPU) Assert.True(row.qualityPassed);
        }
    }
}
