using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    /// <summary>Explicit, bounded CTC evidence export for replaying decoder experiments without native inference.</summary>
    public sealed class SenseVoiceAccuracyExportTests
    {
        [Serializable] private sealed class Corpus { public int rate; public Item[] cases; }
        [Serializable] private sealed class Item
        {
            public string name, split, dataset, condition, cluster, language, text, pcm;
        }
        [Serializable] private sealed class Segment { public string name, path; public int start, end; }
        [Serializable] private sealed class Segments { public Segment[] segments; }
        [Serializable] private sealed class Export
        {
            public int vocabulary = 25055, slots = 9;
            public string tokens;
            public List<Row> rows = new List<Row>();
        }
        [Serializable] private sealed class Row
        {
            public string name, split, dataset, condition, cluster, language, reference, pipeline;
            public string[] probabilities;
            public int[] frames;
        }

        [Test, Explicit("Export top eight + blank log probabilities from installed SenseVoice. No downloads or model edits.")]
        public void ExportPairedLogProbabilities()
        {
            string model = Environment.GetEnvironmentVariable("SIRIAI_RECOGNITION_MODEL");
            string output = Environment.GetEnvironmentVariable("SIRIAI_CTC_OUTPUT");
            string split = Environment.GetEnvironmentVariable("SIRIAI_PUBLIC_SPLIT") ?? "discovery";
            var corpus = JsonUtility.FromJson<Corpus>(File.ReadAllText(Environment.GetEnvironmentVariable("SIRIAI_RECOGNITION_CORPUS")));
            var segments = JsonUtility.FromJson<Segments>(File.ReadAllText(Environment.GetEnvironmentVariable("SIRIAI_CTC_SEGMENTS")));
            Assert.AreEqual(16000, corpus.rate);
            var config = MNNSenseVoiceConfig.Read(model);
            Assert.AreEqual("modelscope-small", config.profile);
            var result = new Export { tokens = Path.Combine(model, config.tokens) };
            string root = Path.Combine(Path.GetDirectoryName(output), "LogProbabilities", split);
            Directory.CreateDirectory(root);
            using (var graph = new MNNGenerationGraph(Path.Combine(model, config.model), 1, backendType: MNNBackendType.CPU))
            foreach (var item in corpus.cases.Where(c => c.split == split &&
                (c.condition == "clean" || c.condition == "noise5" || c.condition == "recorded")))
            {
                foreach (string pipeline in new[] { "whole", "endpoint" })
                {
                    string[] files = pipeline == "whole" ? new[] { item.pcm } :
                        segments.segments.Where(s => s.name == item.name).Select(s => s.path).ToArray();
                    var row = new Row { name = item.name, split = split, dataset = item.dataset, condition = item.condition,
                        cluster = item.cluster, language = item.language, reference = item.text, pipeline = pipeline,
                        probabilities = new string[files.Length], frames = new int[files.Length] };
                    for (int i = 0; i < files.Length; ++i)
                    {
                        var bytes = File.ReadAllBytes(files[i]);
                        Assert.AreEqual(0, bytes.Length % 4);
                        var pcm = new float[bytes.Length / 4];
                        Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
                        var features = MNNSenseVoiceFeatures.Extract(pcm, out int frames, default);
                        var inputs = new[] {
                            new MNNGenerationGraph.Input("speech", features, 1, frames, 560),
                            new MNNGenerationGraph.Input("speech_lengths", new[] { frames }, 1),
                            new MNNGenerationGraph.Input("language", new[] { 0 }, 1),
                            new MNNGenerationGraph.Input("text_norm", new[] { 14 }, 1) };
                        var logits = graph.RunOutputs(new[] { "logits" }, inputs)[0].Values;
                        Assert.AreEqual((frames + 4) * result.vocabulary, logits.Length);
                        row.frames[i] = frames + 4;
                        row.probabilities[i] = Path.Combine(root, item.name + "-" + pipeline + "-" + i + ".ctc");
                        WriteProbabilities(row.probabilities[i], logits, result.vocabulary);
                    }
                    result.rows.Add(row);
                }
                File.WriteAllText(output, JsonUtility.ToJson(result, true));
            }
            Assert.IsNotEmpty(result.rows);
            TestContext.WriteLine("Exported " + result.rows.Count + " paired inputs; this does not accept a decoder candidate.");
        }

        private static void WriteProbabilities(string path, float[] logits, int vocabulary)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            for (int offset = 0; offset < logits.Length; offset += vocabulary)
            {
                var best = Enumerable.Repeat(-1, 8).ToArray();
                for (int k = 0; k < vocabulary; ++k)
                {
                    float value = logits[offset + k];
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new InvalidDataException("Nonfinite CTC logit.");
                    for (int slot = 0; slot < best.Length; ++slot)
                    {
                        if (best[slot] >= 0 && value <= logits[offset + best[slot]]) continue;
                        for (int next = best.Length - 1; next > slot; --next) best[next] = best[next - 1];
                        best[slot] = k;
                        break;
                    }
                }
                double maximum = logits[offset + best[0]], sum = 0;
                for (int k = 0; k < vocabulary; ++k) sum += Math.Exp(logits[offset + k] - maximum);
                double normalizer = maximum + Math.Log(sum);
                foreach (int id in best.Concat(new[] { 0 }))
                {
                    writer.Write(id);
                    writer.Write((float)(logits[offset + id] - normalizer));
                }
            }
        }
    }
}
