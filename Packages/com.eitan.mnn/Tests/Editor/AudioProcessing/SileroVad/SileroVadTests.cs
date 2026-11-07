using System;
using System.IO;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class SileroVadTests
    {
        [Serializable] private sealed class Segment { public int start, end; }
        [Serializable] private sealed class Case { public int samples; public float threshold; public float[] probabilities; public Segment[] segments; }
        [Serializable] private sealed class Cases { public Case[] cases; }

        [Test]
        public void HysteresisDurationsAndPaddingMatchPublishedIndependentAlgorithm()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SILERO_ROOT");
            if (root == null) Assert.Ignore("Set MNN_TEST_SILERO_ROOT with generated upstream segmentation reference fixtures.");
            var cases = JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(root, "Reference/segment-cases.json"))).cases;
            Assert.AreEqual(100, cases.Length);
            foreach (var test in cases)
            {
                var actual = MNNSileroVad.Segment(test.probabilities, test.samples, test.threshold);
                Assert.AreEqual(test.segments.Length, actual.Length);
                for (int i = 0; i < actual.Length; ++i)
                {
                    Assert.AreEqual(test.segments[i].start / 16000.0, actual[i].StartSeconds);
                    Assert.AreEqual(test.segments[i].end / 16000.0, actual[i].EndSeconds);
                }
            }
        }

        [TestCase(false), TestCase(true)]
        public void CatalogPinsOnlyVerifiedHuggingFaceIdentity(bool modelScope)
        {
            var model = new MNNRemoteModel {provider = modelScope ? MNNModelSource.ModelScope : MNNModelSource.HuggingFace,
                repositoryId = MNNSileroModel.Repository, name = "silero_vad-MNN"};
            typeof(MNNModelCatalog).GetMethod("ApplyArtifactEvidence", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] {model, new[] {MNNSileroModel.Graph}});
            Assert.AreEqual(!modelScope, model.runtimeAvailable);
            Assert.AreEqual("VoiceActivityDetection", model.category);
            if (!modelScope)
            {
                Assert.AreEqual(MNNSileroModel.Revision, model.revision);
                Assert.AreEqual(MNNSileroModel.Bytes + 249, model.fileSize);
            }
            Assert.AreEqual(MNNSileroModel.Revision, MNNSileroModel.ResolveRevision("main"));
            Assert.Throws<ArgumentException>(() => MNNSileroModel.ResolveRevision("other"));
            Assert.False(MNNSileroModel.IsRepository("other/silero_vad-MNN"));
        }

        [Test]
        public void IncompleteUnknownAndMalformedProfilesCannotRun()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "Silero-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.False(MNNSileroModel.IsComplete(directory));
                File.WriteAllText(Path.Combine(directory, MNNSileroModel.Graph), "bad");
                Assert.Throws<InvalidDataException>(() => MNNSileroModel.Prepare(directory));
                File.WriteAllText(Path.Combine(directory, MNNSileroVadConfig.FileName), "{\"repository\":\"unknown\"}");
                Assert.False(MNNSileroModel.IsComplete(directory));
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
                Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.AudioProcessing).CanRun(model));
                Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
                Assert.Throws<ArgumentNullException>(() => MNNSileroGraph.Prepare(null));
                Assert.Throws<InvalidDataException>(() => MNNSileroGraph.Prepare(new byte[4]));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void AudioCategoryKeepsDedicatedRoutingAndResampledEndpointsStayInsideOriginalDuration()
        {
            Assert.AreEqual(MNNStudioTask.AudioProcessing, MNNStudioTasks.FromCategory("AudioProcessing"));
            var result = new MNNVoiceActivityResult(new[] {new MNNSpeechSegment(0, 16000)}, new[] {.9f}, .99999);
            Assert.AreEqual(.99999, result.Segments[0].EndSeconds);
            Assert.AreEqual(0, result.Segments[0].StartSeconds);
        }
    }
}
