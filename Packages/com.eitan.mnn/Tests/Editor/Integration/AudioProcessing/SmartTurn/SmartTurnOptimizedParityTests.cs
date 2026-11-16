using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SmartTurn")]
    public sealed class SmartTurnOptimizedParityTests
    {
        [Serializable] private sealed class Row { public string @case; public float source; }
        [Serializable] private sealed class Selection { public Row[] cases; }
        [Test]
        public void Fp16DeploymentPreservesSourceDecisionsWithAndWithoutTrailingSilence()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            string directory = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "SmartTurn-v3.2-FP16-MNN");
            var rows = JsonUtility.FromJson<Selection>(File.ReadAllText(Path.Combine(root, "SmartTurnQuantization", "selected.json"))).cases;
            using (var model = MNNSmartTurn.Load(directory, 4, MNNBackendType.CPU))
            using (var adapter = MNNTurnDetector.Load(directory))
                foreach (var row in rows)
                {
                    int split = row.@case.LastIndexOf("-tail", StringComparison.Ordinal);
                    string name = row.@case.Substring(0, split);
                    int silence = int.Parse(row.@case.Substring(split + 5)) * 16;
                    byte[] bytes = File.ReadAllBytes(Path.Combine(root, "Bairong", "fixtures-1.0", name + "-input.f32"));
                    var pcm = new float[bytes.Length / 4 + silence]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
                    var result = model.Predict(pcm, 16000);
                    TestContext.WriteLine(row.@case + ": source=" + row.source + "; MNN=" + result.CompletionProbability);
                    Assert.Less(Math.Abs(result.CompletionProbability - row.source), .01f, row.@case);
                    Assert.AreEqual(row.source > .5f, result.IsComplete, row.@case);
                    Assert.AreEqual(result.IsComplete, adapter.IsComplete(pcm, 16000), row.@case);
                }
        }
    }
}
