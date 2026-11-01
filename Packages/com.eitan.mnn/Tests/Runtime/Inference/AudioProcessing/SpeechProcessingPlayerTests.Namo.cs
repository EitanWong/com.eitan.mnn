using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [Serializable] private sealed class NamoRow { public string name, text; public bool has_text; public float probability; }
        [Serializable] private sealed class NamoCases { public NamoRow[] rows; }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void NamoTranscriptMatchesOriginalOnnxInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_NAMO_REFERENCE");
            Assert.That(directory, Is.Not.Null.And.Not.Empty);
            var rows = JsonUtility.FromJson<NamoCases>(File.ReadAllText(Path.Combine(directory, "unity-reference.json"))).rows
                .Where(row => row.has_text).ToArray();
            Assert.That(rows.Length, Is.GreaterThanOrEqualTo(26));
            using (var model = MNNNamoTurnDetector.Load(Model("NAMO-Turn-Detector-v1-Multilingual-MNN"), 4, backend))
            {
                foreach (var row in rows.Concat(new[] { rows[0] }))
                {
                    float probability = model.Predict(row.text);
                    TestContext.WriteLine("case=" + row.name + ";probability_error=" + Math.Abs(probability - row.probability));
                    Assert.Less(Math.Abs(probability - row.probability), .001, row.name);
                    Assert.AreEqual(row.probability > .5f, probability > .5f, row.name);
                }
                Assert.AreEqual(backend, model.Backend);
                Assert.Throws<OperationCanceledException>(() => model.Predict("hello", new System.Threading.CancellationToken(true)));
            }
        }
    }
}
