using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void EcapaTdnnEmbeddingsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            string directory = Model("ECAPA-TDNN-MNN");
            var config = UnityEngine.JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(Path.Combine(directory, MNNSpeakerModelConfig.FileName)));
            Assert.True(MNNSpeakerModel.IsOfficial(config));
            Assert.AreEqual("speechbrain/spkrec-ecapa-voxceleb", config.repository);
            Assert.AreEqual("0f99f2d0ebe89ac095bcc5903c4dd8f72b367286", config.source_revision);
            Assert.AreEqual(MNNSpeechBrainFeatures.Frontend, config.frontend);
            using (var model = MNNSpeakerEmbedding.Load(directory, 4, backend))
            {
                Assert.AreEqual("ECAPA-TDNN", model.ModelName);
                foreach (string name in new[] { "speech", "second_speaker", "natural_speech", "quiet", "gain2", "silence", "noise", "tone", "short", "long", "minimum", "speech" })
                    Verify(() => model.Encode(Read("MNN_TEST_ECAPA_REFERENCE", name + "-input")), Read("MNN_TEST_ECAPA_REFERENCE", name + "-unit"), .001);
                Assert.AreEqual(backend, model.Backend);
            }
        }
    }
}
