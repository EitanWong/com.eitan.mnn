using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("EasyTurn")]
    public sealed class EasyTurnParityTests
    {
        [Serializable] private sealed class Row { public string @case, text, label; public int[] tokens; }
        [Serializable] private sealed class Rows { public Row[] rows; }
        private static string Root => Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT");
        private static float[] Pcm(string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Root, "Bairong", "fixtures-1.0", name + "-input.f32"));
            var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length); return pcm;
        }
        [Test]
        public void QuantizedPipelinePreservesOfficialTranscriptionAndAllFourLabels()
        {
            var rows = JsonUtility.FromJson<Rows>("{\"rows\":" + File.ReadAllText(Path.Combine(Root, "Easy", "reference.json")) + "}").rows;
            string directory = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "Easy-Turn-MNN");
            using (var model = MNNEasyTurn.Load(directory))
                foreach (var row in rows)
                {
                    var result = model.Predict(Pcm(row.@case), 16000);
                    TestContext.WriteLine(row.@case + ": source=" + row.text + "; MNN=" + result.Text);
                    Assert.AreEqual(row.text, result.Text); Assert.AreEqual(row.label, result.Label);
                    CollectionAssert.AreEqual(row.tokens, result.Tokens);
                    Assert.AreEqual(row.label == "complete", result.IsComplete);
                }
            using (var adapter = MNNTurnDetector.Load(directory))
            {
                Assert.False(adapter.RequiresTranscript);
                Assert.True(adapter.IsComplete(Pcm("complete_real_1"), 16000));
                Assert.False(adapter.IsComplete(Pcm("wait_real_1"), 16000));
            }
        }
    }
}
