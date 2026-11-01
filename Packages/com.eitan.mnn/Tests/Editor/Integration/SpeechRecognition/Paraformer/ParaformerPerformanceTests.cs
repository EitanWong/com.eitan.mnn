using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Paraformer")]
    public class ParaformerPerformanceTests
    {
        [Serializable]
        private sealed class Row
        {
            public string requested, encoder, decoder, transcript, rejection;
            public int threads;
            public bool qualityPassed;
            public double loadMs, warmupMs, medianMs;
            public double[] samplesMs;
        }

        [TestCase(MNNBackendType.CPU, 1), TestCase(MNNBackendType.CPU, 2)]
        [TestCase(MNNBackendType.CPU, 4), TestCase(MNNBackendType.CPU, 8)]
        [Explicit("Measures existing local models with an independent Chinese quality gate; no downloads.")]
        public void MeasureRepeatedChineseTranscription(MNNBackendType backend, int threads)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_PARAFORMER_ROOT");
            if (root == null) Assert.Ignore("Set MNN_TEST_PARAFORMER_ROOT.");
            if (backend == MNNBackendType.Metal && !MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore("Metal unavailable.");
            var pcm = MNNWaveAudio.Read(Path.Combine(root, "Inspection/Reference/example--asr_example.wav"), out int rate);
            var row = new Row {requested = backend.ToString(), threads = threads, qualityPassed = true, samplesMs = new double[5]};
            var timer = Stopwatch.StartNew();
            try
            {
                using (var recognizer = MNNParaformerRecognizer.Load(Path.Combine(root, "Models/KindBrave-paraformer-online"), threadCount: threads, backendType: backend))
                {
                    row.loadMs = timer.Elapsed.TotalMilliseconds;
                    row.encoder = recognizer.Backend.ToString(); row.decoder = recognizer.DecoderBackend.ToString();
                    for (int i = -1; i < row.samplesMs.Length; ++i)
                    {
                        timer.Restart();
                        var result = recognizer.Recognize(pcm, rate);
                        double elapsed = timer.Elapsed.TotalMilliseconds;
                        row.transcript = result.Text;
                        row.qualityPassed &= result.Text == "欢迎大家来体验达摩院推出的语音识别模型";
                        if (i == -1) row.warmupMs = elapsed;
                        else row.samplesMs[i] = elapsed;
                    }
                }
                row.medianMs = row.samplesMs.OrderBy(x => x).ElementAt(2);
            }
            catch (Exception error) when (backend != MNNBackendType.CPU && (error is InvalidDataException || error is MNNException))
            {
                row.qualityPassed = false;
                row.rejection = error.GetType().Name + ": " + error.Message;
                row.samplesMs = Array.Empty<double>();
            }
            Directory.CreateDirectory(Path.Combine(root, "Performance"));
            File.WriteAllText(Path.Combine(root, "Performance", backend + "-" + threads + ".json"), JsonUtility.ToJson(row, true));
            TestContext.WriteLine(JsonUtility.ToJson(row));
            // Accelerator candidates can fail the quality gate and are then excluded from default selection.
            if (backend == MNNBackendType.CPU) Assert.True(row.qualityPassed, row.transcript);
        }

        [Test, Explicit("Records Metal correctness rejection as benchmark data; does not certify accelerator support.")]
        public void MeasureMetalCandidate() => MeasureRepeatedChineseTranscription(MNNBackendType.Metal, 4);
    }
}
