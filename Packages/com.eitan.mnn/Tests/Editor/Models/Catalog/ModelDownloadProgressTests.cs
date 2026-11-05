using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ModelDownloadProgressTests
    {
        [Test]
        public void BatchSeparatesLocalReuseAndWeightsOverallProgressByRepositoryBytes()
        {
            var batch = new MNNModelDownloadProgress();
            batch.Add("local", "Local", "Disk", 900, "/local");
            batch.Add("a", "Conversation", "ModelScope", 100);
            batch.Add("b", "Speech", "Hugging Face", 300);
            batch.Add("a", "Conversation", "ModelScope", 100);
            batch.Report("a", 50, 100, "llm.mnn.weight");
            var snapshot = batch.Read();
            Assert.AreEqual(3, snapshot.TotalModels);
            Assert.AreEqual(1, snapshot.ReusedModels);
            Assert.AreEqual(2, snapshot.DownloadModels);
            Assert.AreEqual(400, snapshot.TotalBytes);
            Assert.AreEqual(.125f, snapshot.Fraction);
            Assert.AreEqual("Conversation", snapshot.ModelName);
            StringAssert.Contains("llm.mnn.weight", snapshot.Details);
            batch.SetPhase("a", MNNModelDownloadProgress.Phase.Complete);
            Assert.AreEqual(.25f, batch.Read().Fraction);
            Assert.AreEqual(1, batch.Read().DownloadedModels);
            batch.Report("b", 300, 300, "speech.mnn");
            Assert.AreEqual(MNNModelDownloadProgress.Phase.Preparing, batch.Read().State);
            Assert.AreEqual(1, batch.Read().DownloadedModels, "Receiving every byte does not finish installation.");
            batch.SetPhase("b", MNNModelDownloadProgress.Phase.Loading);
            Assert.AreEqual(1, batch.Read().Fraction);
        }

        [Test]
        public void UnknownSizeIsIndeterminateAndRepositoryCallbacksAreNotAddedPerFile()
        {
            var batch = new MNNModelDownloadProgress();
            batch.Add("a", "A", "Source", 0);
            batch.Report("a", 20, 0, "first.mnn");
            batch.Report("a", 30, 0, "second.mnn");
            Assert.AreEqual(30, batch.Read().ReceivedBytes);
            Assert.AreEqual(-1, batch.Read().Fraction);
            StringAssert.Contains("unknown", batch.Read().Summary);
            StringAssert.Contains("30 B", batch.Read().Details);
            batch.SetPhase("a", MNNModelDownloadProgress.Phase.Complete);
            Assert.AreEqual(1, batch.Read().Fraction);
        }

        [Test]
        public void SpeedExcludesInitialResumeOffsetDecaysOnStallAndElapsedStopsAfterCancellation()
        {
            double now = 0;
            var batch = new MNNModelDownloadProgress(() => now);
            batch.Add("a", "A", "Source", 10000);
            batch.Report("a", 5000, 10000, "weights");
            now = 1;
            Assert.AreEqual(0, batch.Read().BytesPerSecond);
            batch.Report("a", 6000, 10000, "weights");
            Assert.AreEqual(1000, batch.Read().BytesPerSecond);
            now = 5;
            Assert.AreEqual(0, batch.Read().BytesPerSecond);
            batch.SetPhase("a", MNNModelDownloadProgress.Phase.Cancelled);
            now = 10;
            Assert.AreEqual(5, batch.Read().ElapsedSeconds);
            Assert.AreEqual(.6f, batch.Read().Fraction);
        }

    }
}
