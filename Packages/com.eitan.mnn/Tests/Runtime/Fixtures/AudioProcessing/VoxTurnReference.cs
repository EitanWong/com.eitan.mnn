using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class VoxTurnReference
    {
        internal sealed class Case
        {
            internal string Name;
            internal MNNVoxTurnRequest[] Requests;
            internal string[] Tasks;
            internal float[] Logits;
            internal List<object> Probabilities;
        }

        internal static List<Case> ReadAll(string root)
        {
            var cases = new List<Case>();
            foreach (string name in new[] { "neural-unity-v11", "real-speech-unity-v21" })
                cases.AddRange(Read(Path.Combine(root, name)));
            Assert.That(cases.Count, Is.EqualTo(31), "All frozen boundary and real speech cases are required.");
            return cases;
        }

        private static IEnumerable<Case> Read(string folder)
        {
            foreach (object row in MNNModelData.Array(MNNModelData.Read(Path.Combine(folder, "public-inputs.json"))))
            {
                string name = (string)MNNModelData.Get(row, "case");
                int index = 0;
                var requests = new List<MNNVoxTurnRequest>();
                foreach (object item in MNNModelData.Array(MNNModelData.Get(row, "items")))
                {
                    float[] audio = (bool)MNNModelData.Get(item, "has_audio")
                        ? Floats(Path.Combine(folder, name + "-audio-" + index + ".f32")) : null;
                    requests.Add(new MNNVoxTurnRequest(audio, (string)MNNModelData.Get(item, "text"),
                        (string)MNNModelData.Get(item, "lang"), (string)MNNModelData.Get(item, "speaker")));
                    ++index;
                }
                yield return new Case
                {
                    Name = name, Requests = requests.ToArray(),
                    Tasks = MNNModelData.Array(MNNModelData.Get(row, "questions")).Select(value => (string)value).ToArray(),
                    Logits = Floats(Path.Combine(folder, name + "-logits.f32")),
                    Probabilities = MNNModelData.Array(MNNModelData.Get(row, "probabilities"))
                };
            }
        }

        internal static void Check(Case source, IReadOnlyList<MNNVoxTurnResult> result)
        {
            Assert.That(result.Count, Is.EqualTo(source.Requests.Length), source.Name);
            int width = result[0].Answers.Values.Max(answer => answer.Labels.Count), option = 0;
            double maxLogit = 0, maxProbability = 0, error = 0, energy = 0;
            for (int b = 0; b < result.Count; ++b)
            {
                Assert.That(result[b].Answers.Keys, Is.EquivalentTo(source.Tasks));
                foreach (string task in source.Tasks)
                {
                    var answer = result[b].Answers[task];
                    int winner = 0, expectedWinner = 0;
                    double kl = 0, total = 0;
                    var probabilities = MNNModelData.Object(MNNModelData.Get(source.Probabilities[b], task));
                    Assert.That(answer.Labels, Is.EquivalentTo(probabilities.Keys));
                    for (int j = 0; j < answer.Labels.Count; ++j)
                    {
                        float logit = answer.Logits[j], probability = answer.Probabilities[j];
                        Assert.That(float.IsNaN(logit) || float.IsInfinity(logit), Is.False);
                        Assert.That(probability, Is.InRange(0f, 1f));
                        double expectedLogit = source.Logits[option * width + j];
                        double expectedProbability = Convert.ToDouble(probabilities[answer.Labels[j]]);
                        double difference = logit - expectedLogit;
                        maxLogit = Math.Max(maxLogit, Math.Abs(difference));
                        error += difference * difference;
                        energy += expectedLogit * expectedLogit;
                        maxProbability = Math.Max(maxProbability, Math.Abs(probability - expectedProbability));
                        total += probability;
                        if (expectedProbability > 0) kl += expectedProbability * Math.Log(expectedProbability / Math.Max(probability, 1e-30));
                        if (logit > answer.Logits[winner]) winner = j;
                        if (expectedLogit > source.Logits[option * width + expectedWinner]) expectedWinner = j;
                    }
                    Assert.That(winner, Is.EqualTo(expectedWinner), source.Name + "/" + task);
                    Assert.That(total, Is.EqualTo(1).Within(1e-6), source.Name + "/" + task);
                    Assert.That(kl, Is.LessThan(.0001), source.Name + "/" + task);
                    ++option;
                }
            }
            TestContext.WriteLine(source.Name + " logit abs=" + maxLogit.ToString("R") +
                ";probability abs=" + maxProbability.ToString("R"));
            Assert.That(maxLogit, Is.LessThan(.001), source.Name);
            Assert.That(Math.Sqrt(error / Math.Max(energy, 1e-30)), Is.LessThan(.001), source.Name);
            Assert.That(maxProbability, Is.LessThan(.001), source.Name);
        }

        private static float[] Floats(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes.Length % 4, Is.Zero);
            var result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }
    }
}
