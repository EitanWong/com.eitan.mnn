using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class VoxTurnStudioTests
    {
        private static string Root => Environment.GetEnvironmentVariable("MNN_TEST_VOXTURN");
        private static string ModelDirectory => Environment.GetEnvironmentVariable("MNN_TEST_VOXTURN_MODEL") ??
            Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "VoxTurn-v1-MNN");

        [Test]
        public void DiscoveryConnectsTurnPipeline()
        {
            var model = MNNStudioModel.Read(ModelDirectory);
            Assert.That(model.Task, Is.EqualTo(MNNStudioTask.AudioProcessing));
            Assert.That(model.HasAudioProcessingPipeline, Is.True);
            Assert.That(MNNStudioTasks.Get((int)model.Task).CanRun(model), Is.True);
        }

        [Test]
        public void CatalogCategorizesVoxTurn()
        {
            Assert.That(MNNModelCatalog.Classify("example/VoxTurn-v1-MNN"),
                Is.EqualTo(MNNModelCatalogCategory.TurnDetection.ToString()));
        }

        [Test]
        public void StudioRunsTextAndAudioWithAllOriginalChoices()
        {
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(ModelDirectory), 1, false))
            {
                var text = backend.Run(new MNNStudioRequest
                {
                    Prompt = "I am finished.", TurnLanguage = "en", TurnSpeaker = "user",
                    TurnQuestions = MNNVoxTurnPrompt.Tasks
                });
                Check("neural-unity-v11", "text_en", text);
                StringAssert.Contains("CPU+CPU", text.BackendDescription);
                var audio = backend.Run(new MNNStudioRequest
                {
                    Audio = Path.Combine(Path.GetDirectoryName(Root), "ERes2NetV2", "source", "examples", "speaker1_a_cn_16k.wav"),
                    Prompt = "我说完了。", TurnLanguage = "zh", TurnSpeaker = "user",
                    TurnQuestions = MNNVoxTurnPrompt.Tasks
                });
                Check("real-speech-unity-v21", "speaker1_a_cn_16k", audio);
                StringAssert.Contains("audio CPU", audio.BackendDescription);
                StringAssert.Contains("Metal+Metal", audio.BackendDescription);
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Cancellation = new CancellationToken(true) }));
            }
        }

        [Test]
        public void TranscriptOnlyUiRequiresChoicesAndRetainsLanguageAndSpeaker()
        {
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            try
            {
                Set(window, "_audioPath", string.Empty);
                Set(window, "_query", "I am finished.");
                Assert.That(Invoke(window, "VoxTurnValidation"), Is.Null);
                Set(window, "_voxTurnQuestions", 0);
                Assert.That(Invoke(window, "VoxTurnValidation"), Is.Not.Null);
                Set(window, "_voxTurnQuestions", 63);
                Assert.That((string[])Invoke(window, "BuildVoxTurnQuestions"), Is.EqualTo(MNNVoxTurnPrompt.Tasks));
                Set(window, "_query", string.Empty);
                Assert.That(Invoke(window, "VoxTurnValidation"), Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [TestCase(1), TestCase(2), TestCase(8)]
        public void UnqualifiedThreadContractsAreRejectedBeforeLoading(int threads)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNVoxTurn.Load("unused", threads));
        }

        private static void Check(string folder, string name, MNNStudioResult result)
        {
            object source = MNNModelData.Array(MNNModelData.Read(Path.Combine(Root, folder, "public-inputs.json")))
                .Single(row => (string)MNNModelData.Get(row, "case") == name);
            object probabilities = MNNModelData.Array(MNNModelData.Get(source, "probabilities"))[0];
            Assert.That(result.TurnDecisions, Is.Not.Null);
            Assert.That(result.TurnDecisions.Answers.Count, Is.EqualTo(6));
            foreach (var task in result.TurnDecisions.Answers)
                foreach (string label in task.Value.Labels)
                {
                    double expected = Convert.ToDouble(MNNModelData.Get(MNNModelData.Get(probabilities, task.Key), label));
                    Assert.That(Math.Abs(task.Value.Probability(label) - expected), Is.LessThan(.001), task.Key + "/" + label);
                }
            Assert.That(result.TurnDetection.CompletionProbability,
                Is.EqualTo(result.TurnDecisions.Answers["eot"].Probability("yes")));
        }

        private static void Set(MNNChatStudio window, string name, object value) =>
            typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, value);
        private static object Invoke(MNNChatStudio window, string name) =>
            typeof(MNNChatStudio).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
    }
}
