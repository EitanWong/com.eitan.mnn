using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("TurnPublishedDownload")]
    public sealed class TurnPublishedDownloadTests
    {
        [TestCase("SmartTurn-v3.2-FP16-MNN")]
        [TestCase("Bairong-TurnSense-1.0-MNN")]
        [TestCase("Bairong-TurnSense-1.1-MNN")]
        [TestCase("Easy-Turn-MNN")]
        [TestCase("TEN-Turn-Detection-7B-MNN")]
        [TestCase("DualTurn-Endpointing-MNN")]
        [TestCase("LiveKit-Turn-Detector-Multilingual-MNN")]
        [TestCase("LiveKit-Turn-Detector-MNN")]
        [Timeout(3600000)]
        public async Task PackageInstallerDownloadsPinnedReleaseAndLoadsRuntimeAdapter(string name)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            var registration = MNNModelRegistry.Find(MNNModelSource.ModelScope, "EitanWang/" + name);
            Assert.IsNotNull(registration);
            string destination = Path.Combine(root, "VerifiedDownloads", name);
            using (var downloader = new MNNModelDownloader(options: new MNNModelTransferOptions { MaxConnections = 4, PreservePartialDownloads = true }))
                await MNNModelInstallation.EnsureAsync(destination, registration.CreateModel(), downloader: downloader,
                    progress: (done, total, file) => { if (done == total) TestContext.Progress.WriteLine(name + ": downloaded " + total); });
            Assert.True(MNNModelInstallation.IsInstalled(destination, registration.CreateModel()));
            Assert.True(registration.IsComplete(destination));
            bool downloadedResult;
            using (var model = MNNTurnDetector.Load(destination))
            {
                string acceptance = Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT");
                byte[] bytes = File.ReadAllBytes(Path.Combine(acceptance, "Bairong", "fixtures-1.0", "complete_real_1-input.f32"));
                var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
                bool result = model.IsComplete(pcm, 16000, "That is all, thank you.");
                TestContext.WriteLine(name + "@" + registration.Revision + ": downloaded installation loaded; complete=" + result);
                downloadedResult = result;
            }
            using (var local = MNNTurnDetector.Load(Path.Combine(root, name)))
            {
                string acceptance = Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT");
                byte[] bytes = File.ReadAllBytes(Path.Combine(acceptance, "Bairong", "fixtures-1.0", "complete_real_1-input.f32"));
                var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
                Assert.AreEqual(local.IsComplete(pcm, 16000, "That is all, thank you."), downloadedResult);
            }
        }
    }
}
