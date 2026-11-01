using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class SmartTurnTests
    {
        [Test]
        public void FrontendRetainsTailAndPrepadsBeforeNormalization()
        {
            var samples = Enumerable.Range(0, 129000).Select(i => (float)(.2 * Math.Sin(i * .043))).ToArray();
            var tail = samples.Skip(1000).ToArray();
            CollectionAssert.AreEqual(MNNSmartTurnFeatures.Extract(samples, default), MNNSmartTurnFeatures.Extract(tail, default));
            CollectionAssert.AreEqual(MNNSmartTurnFeatures.Extract(new float[1], default), MNNSmartTurnFeatures.Extract(new float[128000], default));
            Assert.Throws<OperationCanceledException>(() => MNNSmartTurnFeatures.Extract(samples, new CancellationToken(true)));
            var boundary = new MNNTurnDetectionResult(.5f, .5f, 1);
            Assert.False(boundary.IsComplete, "Upstream uses strict greater-than at the decision threshold.");
        }
        [TestCase(16000), TestCase(44100), TestCase(48000), TestCase(95999)]
        public void TailOnlyResamplingPreservesWholeRecordingPhaseAndFilterContext(int sampleRate)
        {
            var samples = Enumerable.Range(0, sampleRate * 9 + 13).Select(i => (float)(.2 * Math.Sin(i * .043))).ToArray();
            var whole = MNNSpeechDsp.Resample(samples, sampleRate, 16000, default);
            var tail = MNNSpeechDsp.Resample(samples, sampleRate, 16000, default, tailSamples: 128000);
            CollectionAssert.AreEqual(whole.Skip(whole.Length - 128000).ToArray(), tail);
        }
        [Test]
        public void PinnedProfileRejectsMutableOrAlteredAssetsAndRoutesExactIdentity()
        {
            Assert.AreEqual(MNNSmartTurnModel.Revision, MNNSmartTurnModel.ResolveRevision("main"));
            Assert.Throws<ArgumentException>(() => MNNSmartTurnModel.ResolveRevision("different"));
            var model = new MNNModelInfo {provider = MNNModelProvider.HuggingFace, repositoryId = MNNSmartTurnModel.Repository, name = "smart-turn-v3.2-gpu-MNN"};
            typeof(MNNModelRepository).GetMethod("ApplyArtifactEvidence", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] {model, new[] {MNNSmartTurnModel.Graph}});
            Assert.AreEqual(MNNModelCategory.AudioProcessing.ToString(), model.category);
            Assert.AreEqual(MNNSmartTurnModel.Revision, model.revision);
            Assert.True(model.runtimeAvailable);
            string folder = Path.GetFullPath("TestArtifacts~/SmartTurnTests/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllBytes(Path.Combine(folder, MNNSmartTurnModel.Graph), new byte[16]);
                Assert.Throws<InvalidDataException>(() => MNNSmartTurnModel.Prepare(folder));
                Assert.False(MNNSmartTurnModel.IsComplete(folder));
                File.WriteAllText(Path.Combine(folder, MNNSmartTurnConfig.FileName), JsonUtility.ToJson(new MNNSmartTurnConfig()));
                Assert.AreEqual(MNNStudioTask.AudioProcessing, MNNStudioModel.Read(folder).Task);
                Assert.False(MNNStudioModel.Read(folder).HasAudioProcessingPipeline);
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
