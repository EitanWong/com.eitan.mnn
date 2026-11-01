using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SmartTurn")]
    public class SmartTurnPerformanceTests
    {
        [Serializable] private sealed class Reference { public Case[] rows; }
        [Serializable] private sealed class Case { public string name; public float probability; public bool complete; }
        [Serializable] private sealed class Row
        {
            public string requested, actual, rejection;
            public int threads, qualityCases;
            public bool qualityPassed = true;
            public double loadMs, medianMs, maxProbabilityError;
            public double[] samplesMs;
        }
        [TestCase(MNNBackendType.CPU, 1), TestCase(MNNBackendType.CPU, 2)]
        [TestCase(MNNBackendType.CPU, 4), TestCase(MNNBackendType.CPU, 8)]
        [TestCase(MNNBackendType.Metal, 1)]
        [Explicit("Uses existing pinned Smart Turn and independently generated ONNX references; no downloads.")]
        public void CompareBackendsWithIndependentProbabilityQualityGate(MNNBackendType backend, int threads)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SMART_TURN_ROOT");
            if (root == null) Assert.Ignore("Set MNN_TEST_SMART_TURN_ROOT.");
            var bytes = File.ReadAllBytes(Path.Combine(root, "Reference/two-utterances-pcm.f32"));
            var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
            var row = new Row {requested = backend.ToString(), threads = threads, samplesMs = new double[21]};
            var timer = Stopwatch.StartNew();
            try
            {
                using (var vad = MNNSmartTurn.Load(Path.Combine(root, "Models"), threads, backend))
                {
                    row.loadMs = timer.Elapsed.TotalMilliseconds; row.actual = vad.Backend.ToString();
                    var reference = JsonUtility.FromJson<Reference>(File.ReadAllText(Path.Combine(root, "Reference/manifest.json")));
                    foreach (var test in reference.rows)
                    {
                        var data = File.ReadAllBytes(Path.Combine(root, "Reference", test.name + "-pcm.f32"));
                        var audio = new float[data.Length / 4]; Buffer.BlockCopy(data, 0, audio, 0, data.Length);
                        var result = vad.Predict(audio, 16000);
                        row.maxProbabilityError = Math.Max(row.maxProbabilityError, Math.Abs(result.CompletionProbability - test.probability));
                        row.qualityPassed &= Math.Abs(result.CompletionProbability - test.probability) < .04 && result.IsComplete == test.complete;
                        ++row.qualityCases;
                    }
                    for (int i = -1; i < row.samplesMs.Length; ++i)
                    {
                        timer.Restart(); var result = vad.Predict(pcm, 16000);
                        if (i >= 0) row.samplesMs[i] = timer.Elapsed.TotalMilliseconds;
                        row.maxProbabilityError = Math.Max(row.maxProbabilityError, Math.Abs(result.CompletionProbability - .9795916080474854));
                        row.qualityPassed &= row.maxProbabilityError < .04 && result.IsComplete;
                    }
                    row.medianMs = row.samplesMs.OrderBy(x => x).ElementAt(row.samplesMs.Length / 2);
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

        [Test, Explicit("Measures full versus tail-only resampling on an existing long recording; no downloads.")]
        public void TailOnlyResamplingAvoidsDiscardedWorkWithoutChangingSamples()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SMART_TURN_ROOT");
            if (root == null) Assert.Ignore("Set MNN_TEST_SMART_TURN_ROOT.");
            var bytes = File.ReadAllBytes(Path.Combine(root, "Reference/two-utterances-pcm.f32"));
            var shortPcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, shortPcm, 0, bytes.Length);
            var source = new float[16000 * 60];
            for (int i = 0; i < source.Length; ++i) source[i] = shortPcm[i % shortPcm.Length];
            var pcm = MNNSpeechDsp.Resample(source, 16000, 44100, default);
            var fullTimes = new double[9]; var tailTimes = new double[9];
            var timer = Stopwatch.StartNew();
            for (int i = -1; i < 9; ++i)
            {
                timer.Restart(); var full = MNNSpeechDsp.Resample(pcm, 44100, 16000, default);
                if (i >= 0) fullTimes[i] = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); var tail = MNNSpeechDsp.Resample(pcm, 44100, 16000, default, tailSamples: 128000);
                if (i >= 0) tailTimes[i] = timer.Elapsed.TotalMilliseconds;
                CollectionAssert.AreEqual(full.Skip(full.Length - tail.Length).ToArray(), tail);
            }
            double fullMedian = fullTimes.OrderBy(x => x).ElementAt(4), tailMedian = tailTimes.OrderBy(x => x).ElementAt(4);
            File.WriteAllText(Path.Combine(root, "tail-resampling-performance.json"),
                "{\"sourceSeconds\":60,\"sampleRate\":44100,\"outputSamples\":128000,\"identical\":true,\"fullMedianMs\":" + fullMedian.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ",\"tailMedianMs\":" + tailMedian.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}");
        }
    }
}
