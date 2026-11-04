using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class TurnSenseStudioTests
    {
        [Serializable] private sealed class Reference { public Row[] cases; }
        [Serializable] private sealed class Row { public string text; public float[] probabilities; public int decision; }

        [Test]
        public void DiscoveredTranscriptModelRunsOriginalProbabilitiesAndUiPreservesWhitespaceWithoutAudio()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_TURNSENSE");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Set MNN_TEST_TURNSENSE to the frozen original references.");
            var model = MNNStudioModel.Read(Path.Combine(root, "candidate-v418"));
            Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task, "Missing TurnSense Studio discovery");
            Assert.True(model.HasAudioProcessingPipeline);
            Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            Assert.AreEqual(MNNModelCatalogCategory.AudioProcessing.ToString(), MNNModelCatalog.Classify("example/TurnSense-SmolLM2-135M-MNN"));
            using (var backend = MNNStudioNativeBackend.Load(model, 1, false))
            {
                var reference = JsonUtility.FromJson<Reference>(File.ReadAllText(Path.Combine(root, "reference-v407", "cases.json")));
                foreach (var row in reference.cases)
                {
                    var result = backend.Run(new MNNStudioRequest { Prompt = row.text });
                    Assert.Less(Math.Abs(result.TurnDetection.CompletionProbability - row.probabilities[1]), .001);
                    Assert.AreEqual(row.decision == 1, result.TurnDetection.IsComplete);
                    StringAssert.Contains("TurnSense", result.BackendDescription);
                }
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Prompt = "Hello", Cancellation = new CancellationToken(true) }));
                Assert.Throws<ArgumentNullException>(() => backend.Run(new MNNStudioRequest { Prompt = null }));
            }
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            try
            {
                Set(window, "_available", new List<MNNStudioModel> { model });
                Set(window, "_taskIndex", (int)MNNStudioTask.AudioProcessing);
                Set(window, "_modelIndex", 0);
                Set(window, "_audioPath", null);
                foreach (string text in new[] { "", " \t\n", "  Hello\t world \n" })
                {
                    Set(window, "_query", text);
                    Assert.IsNull(Invoke(window, "GetTaskRunBlockedReason"));
                    Assert.AreEqual(text, Invoke(window, "BuildTurnSensePrompt"));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        private static void Set(MNNChatStudio window, string field, object value) =>
            typeof(MNNChatStudio).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, value);
        private static object Invoke(MNNChatStudio window, string method) =>
            typeof(MNNChatStudio).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
    }
}
