using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class WhisperTests
    {
        [Test]
        public void CatalogPinsOnlyTheVerifiedRepositoryAndProvider()
        {
            foreach (var provider in new[] {MNNModelSource.ModelScope, MNNModelSource.HuggingFace})
            {
                var model = new MNNRemoteModel {id = "hf:" + MNNWhisperModel.Repository, repositoryId = MNNWhisperModel.Repository, name = "whisper-tiny-en-mnn", provider = provider};
                MNNModelCatalog.ApplyArtifactEvidence(model, MNNWhisperModel.Paths);
                Assert.AreEqual(provider == MNNModelSource.HuggingFace, model.runtimeAvailable);
                Assert.AreEqual("SpeechRecognition", model.category);
                if (provider == MNNModelSource.HuggingFace)
                {
                    Assert.AreEqual(MNNWhisperModel.Revision, model.revision);
                    Assert.AreEqual(MNNWhisperModel.Sizes.Sum(), model.fileSize);
                }
            }
            Assert.False(MNNWhisperModel.IsRepository("other/whisper-base-mnn"));
        }

        [TestCase("developerabu-tiny-en"), TestCase("modelscope-tiny-en"), TestCase("developerabu-tiny"), TestCase("developerabu-base"), TestCase("developerabu-small")]
        public void EveryProfileHasAnIsolatedProtocolPinnedPlanAndMatchingCatalogProvider(string id)
        {
            var profile = MNNWhisperModel.Profiles.Single(p => p.Id == id);
            Assert.AreEqual(profile.Id, MNNWhisperModel.ForConfig(profile.Config()).Id);
            Assert.AreEqual(profile.Paths.Length, profile.Sizes.Length);
            Assert.AreEqual(profile.Paths.Length, profile.Hashes.Length);
            Assert.True(profile.Hashes.All(hash => hash.Length == 64));
            foreach (var provider in new[] {MNNModelSource.ModelScope, MNNModelSource.HuggingFace})
            {
                var model = new MNNRemoteModel {id = profile.Repository, repositoryId = profile.Repository, name = profile.Repository.Split('/').Last(), provider = provider};
                MNNModelCatalog.ApplyArtifactEvidence(model, profile.Paths);
                Assert.AreEqual(profile.ModelScope == (provider == MNNModelSource.ModelScope), model.runtimeAvailable);
                if (model.runtimeAvailable) Assert.AreEqual(profile.Sizes.Sum(), model.fileSize);
            }
            if (!profile.ChineseAvailable)
            {
                var invalid = profile.Config(); invalid.language = "zh";
                Assert.Throws<ArgumentException>(() => MNNWhisperRecognizer.Load("missing-whisper", invalid));
            }
            Assert.AreEqual(profile.Revision, MNNWhisperModel.ResolveRevision(profile.ModelScope ? "master" : "main", profile.Repository));
            Assert.Throws<ArgumentException>(() => MNNWhisperModel.ResolveRevision("unknown", profile.Repository));
        }

        [Test]
        public void InvalidFeaturesProfileAndTokensAreRejected()
        {
            Assert.Throws<ArgumentException>(() => MNNWhisperFeatures.Extract(new float[399], out _, out _, CancellationToken.None));
            Assert.Throws<OperationCanceledException>(() => MNNWhisperFeatures.Extract(new float[16000], out _, out _, new CancellationToken(true)));
            Assert.Throws<ArgumentException>(() => MNNWhisperModel.Assets(new MNNWhisperConfig {profile = "other"}));
            string directory = Path.Combine("TestArtifacts~", "WhisperTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.False(MNNWhisperModel.IsComplete(directory));
                File.WriteAllText(Path.Combine(directory, "tokens.txt"), "invalid-base64 0\n");
                Assert.Throws<InvalidDataException>(() => MNNWhisperRecognizer.ReadTokens(Path.Combine(directory, "tokens.txt")));
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
