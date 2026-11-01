using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void CampPlusEmbeddingsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            string directory = Model("CAMPlus-MNN");
            var config = UnityEngine.JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(Path.Combine(directory, MNNSpeakerModelConfig.FileName)));
            Assert.True(MNNSpeakerModel.IsOfficial(config));
            Assert.AreEqual("iic/speech_campplus_sv_zh-cn_16k-common", config.repository);
            Assert.AreEqual("013c18884dffb82846cf472dd06dd4a08d97e2cd", config.source_revision);
            using (var model = MNNSpeakerEmbedding.Load(directory, 4, backend))
            {
                Assert.AreEqual("CAM++", model.ModelName);
                foreach (string name in new[] { "speaker1_a_cn_16k", "speaker1_b_cn_16k", "speaker2_a_cn_16k", "quiet", "gain2", "silence", "noise", "short", "long", "speaker1_a_cn_16k" })
                    Verify(() => model.Encode(Read("MNN_TEST_CAMPPLUS_REFERENCE", name + "-input")), Read("MNN_TEST_CAMPPLUS_REFERENCE", name + "-embedding"), .001);
                Assert.AreEqual(backend, model.Backend);
            }
        }
    }
}
