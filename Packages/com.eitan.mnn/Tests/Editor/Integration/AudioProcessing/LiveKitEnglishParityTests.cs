using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class LiveKitEnglishParityTests
    {
        [Serializable] private sealed class Row { public string text; public int[] ids; public float probability; }
        [Serializable] private sealed class Rows { public Row[] rows; }
        [Test]
        public void PublishedEnglishPipelineMatchesOriginalFloatingOnnx()
        {
            string acceptance = Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT");
            var rows = JsonUtility.FromJson<Rows>("{\"rows\":" + File.ReadAllText(Path.Combine(acceptance, "LiveKitEnglish", "reference.json")) + "}").rows;
            string directory = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "LiveKit-Turn-Detector-MNN");
            using (var model = MNNLiveKitTurnDetector.Load(directory, 1, MNNBackendType.CPU))
            foreach (var row in rows)
            {
                float probability = model.Predict(row.text);
                Assert.Less(Math.Abs(probability - row.probability), .00002f, row.text);
                Assert.AreEqual(row.probability >= .5f, probability >= .5f);
                TestContext.WriteLine(row.text + ": source=" + row.probability + "; MNN=" + probability);
            }
        }
    }
}
