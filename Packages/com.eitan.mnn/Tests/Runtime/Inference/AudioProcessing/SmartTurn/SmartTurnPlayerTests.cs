using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("SmartTurnPlayer")]
    public class SmartTurnPlayerTests
    {
        [Serializable] private class Manifest { public Row[] rows; }
        [Serializable] private class Row { public string name; public float probability; public bool complete, label; }
        [Test]
        public void EndpointProbabilityMatchesIndependentOnnxAndAsyncOwnershipInActualPlayer()
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_SMART_TURN_ROOT");
            if (string.IsNullOrWhiteSpace(root)) Assert.Ignore("Provide --smart-turn-root with prepared local weights and reference PCM; no downloads.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(root, "Reference/manifest.json")));
            var model = MNNSmartTurn.Load(Path.Combine(root, "Models"));
            try
            {
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                foreach (var row in manifest.rows)
                {
                    var bytes = File.ReadAllBytes(Path.Combine(root, "Reference", row.name + "-pcm.f32"));
                    var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
                    var result = model.Predict(pcm, 16000);
                    Assert.AreEqual(row.probability, result.CompletionProbability, .04, row.name);
                    Assert.AreEqual(row.complete, result.IsComplete, row.name);
                    if (row.name.StartsWith("dataset-", StringComparison.Ordinal)) Assert.AreEqual(row.label, result.IsComplete, "Published endpoint label: " + row.name);
                    var pending = model.PredictAsync(pcm, 16000);
                    Array.Clear(pcm, 0, pcm.Length);
                    Assert.AreEqual(result.CompletionProbability, pending.GetAwaiter().GetResult().CompletionProbability);
                }
                Assert.Throws<OperationCanceledException>(() => model.Predict(new float[16000], 16000, cancellationToken: new CancellationToken(true)));
                Assert.Throws<ArgumentException>(() => model.Predict(new[] {float.NaN}, 16000));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Predict(new float[16000], 16000));
        }
    }
}
