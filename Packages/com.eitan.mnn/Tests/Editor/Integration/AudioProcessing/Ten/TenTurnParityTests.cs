using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("TenTurn")]
    public sealed class TenTurnParityTests
    {
        [Serializable] private sealed class Row { public string text, label; }
        [Serializable] private sealed class Rows { public Row[] rows; }
        [Test]
        public void OneGreedyTokenMatchesOfficialFp32ClassesAndWaitNeverCompletes()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT");
            var rows = JsonUtility.FromJson<Rows>("{\"rows\":" + File.ReadAllText(Path.Combine(root, "TEN", "source-reference.json")) + "}").rows;
            string directory = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "TEN-Turn-Detection-7B-MNN");
            using (var model = MNNTenTurnDetector.Load(directory))
            using (var adapter = MNNTurnDetector.Load(directory, 2))
                foreach (var row in rows)
                {
                    string label = model.Predict(row.text);
                    TestContext.WriteLine(row.text + ": source=" + row.label + "; MNN=" + label);
                    Assert.AreEqual(row.label, label);
                    Assert.AreEqual(label == "finished", adapter.IsComplete(null, 16000, row.text));
                }
        }
    }
}
