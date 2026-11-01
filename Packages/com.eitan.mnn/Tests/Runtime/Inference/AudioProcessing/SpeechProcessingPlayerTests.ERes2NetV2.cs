using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void ERes2NetEmbeddingsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            string directory = Model("ERes2NetV2-MNN");
            var config = UnityEngine.JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(Path.Combine(directory, MNNSpeakerModelConfig.FileName)));
            Assert.True(MNNSpeakerModel.IsOfficial(config), "The Player must contain the original-checkpoint normalized-PCM profile.");
            using (var model = MNNSpeakerEmbedding.Load(directory, 4, backend))
            {
                foreach (string name in new[] { "speaker1_a_cn_16k", "speaker1_b_cn_16k", "speaker2_a_cn_16k", "quiet", "gain2", "silence", "noise", "short", "long", "speaker1_a_cn_16k" })
                    Verify(() => model.Encode(Read("MNN_TEST_ERES2NET_REFERENCE", name + "-input")), Read("MNN_TEST_ERES2NET_REFERENCE", name + "-embedding"), .001);
                Assert.AreEqual(backend, model.Backend);
            }
        }
    }
}
