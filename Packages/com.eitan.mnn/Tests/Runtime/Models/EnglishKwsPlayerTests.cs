using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class EnglishKwsPlayerTests
    {
        private const string Repository = "EitanWang/English-KWS-GigaSpeech-MNN";

        [Test]
        public void BuiltPlayerFindsEnglishModelWithoutInstalledWeights()
        {
            var model = MNNModelCatalog.CreateDefault().Models.Single(item => item.repositoryId == Repository);
            Assert.IsTrue(MNNModelCatalog.MatchesSearch(model, "English-KWS-GigaSpeech"));
            Assert.IsTrue(MNNModelCatalog.MatchesSearch(model, "唤醒词"));
            Assert.IsTrue(model.runtimeAvailable);
            Assert.AreEqual("KeywordSpotting", model.category);
            Assert.AreEqual(MNNKeywordSpotterConfig.EnglishDirectoryName, MNNConversationModelCatalog.DefaultInstallationName(model));
            TestContext.WriteLine("PLAYER_CATALOG platform=" + Application.platform + " repository=" + model.repositoryId + " revision=" + model.revision);
        }

        [Test, Explicit("Build with the English-KWS-GigaSpeech directory selected for bundling.")]
        public void SelectedEnglishProfileRunsFromBuiltStreamingAssets()
        {
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", MNNKeywordSpotterConfig.EnglishDirectoryName);
            Assert.AreEqual(MNNKeywordLanguage.English, MNNKeywordSpotterConfig.Read(directory).Language);
            using (var model = MNNKeywordSpotter.Load(directory, 1))
                Assert.IsEmpty(model.Detect(new float[16000], 16000, "Siri\nHey Siri"));
            Assert.IsFalse(Directory.GetFiles(directory, "*.onnx", SearchOption.AllDirectories).Any());
            Assert.IsFalse(Directory.GetFiles(directory, "*.meta", SearchOption.AllDirectories).Any());
            TestContext.WriteLine("BUNDLED_ENGLISH_KWS platform=" + Application.platform + " path=" + directory + " profile=English graphs=verified silence=quiet");
        }

        [UnityTest, Explicit("Downloads the 14 MB public English KWS deployment and runs local regression audio."), Category("Network")]
        public IEnumerator LivePlayerDiscoversDownloadsAndRunsEnglishWakeModel()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_ENGLISH_KWS_INSTALL_ROOT");
            string recordings = Environment.GetEnvironmentVariable("MNN_TEST_WAKE_FIX_AUDIO");
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(recordings))
                Assert.Ignore("Provide a fresh model installation root and prepared wake WAVs to opt into this network test.");
            var model = MNNModelCatalog.CreateDefault().Models.Single(item => item.repositoryId == Repository);
            string directory = MNNModelInstallation.DirectoryFor(root, model, MNNConversationModelCatalog.DefaultInstallationName(model));
            Assert.IsFalse(Directory.Exists(directory), "Use a fresh directory to prove a real cold installation.");
            using (var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            using (var downloader = new MNNModelDownloader())
            {
                var search = new MNNModelCatalog().FetchModelScopeSearchPageAsync(0, 1, "English-KWS-GigaSpeech",
                    cancellationToken: cancellation.Token);
                while (!search.IsCompleted) yield return null;
                if (search.IsFaulted) throw search.Exception;
                Assert.IsTrue(search.Result.Models.Any(item => item.repositoryId == Repository), "Public search must expose the published model.");
                long progress = 0;
                var install = MNNModelInstallation.EnsureAsync(directory, model,
                    (received, total, path) => progress = received, cancellation.Token, downloader);
                while (!install.IsCompleted) yield return null;
                if (install.IsFaulted) throw install.Exception;
                Assert.IsFalse(install.IsCanceled);
                Assert.AreEqual(directory, install.Result);
                Assert.IsTrue(MNNModelInstallation.IsInstalled(directory, model));
                Assert.Greater(progress, 14000000);
                Assert.AreEqual(MNNKeywordLanguage.English, MNNKeywordSpotterConfig.Read(directory).Language);
                Assert.IsFalse(Directory.GetFiles(directory, "*.onnx", SearchOption.AllDirectories).Any());
                Assert.IsFalse(Directory.GetFiles(directory, "*.meta", SearchOption.AllDirectories).Any());
                using (var spotter = MNNKeywordSpotter.Load(directory, 1))
                {
                    Assert.IsNotEmpty(spotter.DetectWaveFile(Path.Combine(recordings, "Samantha-hey-siri.wav"), "Siri\nHey Siri"));
                    Assert.IsEmpty(spotter.DetectWaveFile(Path.Combine(recordings, "Samantha-negative.wav"), "Siri\nHey Siri"));
                }
                TestContext.WriteLine("LIVE_ENGLISH_KWS platform=" + Application.platform + " bytes=" + progress + " path=" + directory + " receipt=verified positive=detected negative=quiet");
            }
        }
    }
}
