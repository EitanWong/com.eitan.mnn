using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void TitaNetEmbeddingsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            string directory = Model("TitaNet-MNN");
            var config = UnityEngine.JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(Path.Combine(directory, MNNSpeakerModelConfig.FileName)));
            Assert.True(MNNSpeakerModel.IsOfficial(config));
            Assert.AreEqual("nvidia/speakerverification_en_titanet_large", config.repository);
            Assert.AreEqual("0dc382f40121a5fbd34db10a2bb04d826c2be6a8", config.source_revision);
            Assert.AreEqual(MNNTitaNetFeatures.Frontend, config.frontend);
            using (var model = MNNSpeakerEmbedding.Load(directory, 4, backend))
            {
                Assert.AreEqual("TitaNet", model.ModelName);
                foreach (string name in new[] { "speech", "second_speaker", "natural_speech", "quiet", "gain2", "silence", "noise", "tone", "short", "long", "minimum", "maximum", "speech" })
                    Verify(() => model.Encode(Read("MNN_TEST_TITANET_REFERENCE", name + "-input")), Read("MNN_TEST_TITANET_REFERENCE", name + "-unit"), .001);
                Assert.AreEqual(backend, model.Backend);
            }
        }
    }
}
