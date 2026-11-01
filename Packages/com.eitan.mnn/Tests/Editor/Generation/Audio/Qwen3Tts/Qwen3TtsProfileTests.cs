using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    // Sparse placeholders exercise admission checks only, never inference.
    public class Qwen3TtsProfileTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestArtifacts~", "Qwen3TtsProfileTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        private string Voice(string name = "demo", string ids = "[[151644,77091,198,42,151645,198]]")
        {
            string path = Path.Combine(_root, "voices", name);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "meta.json"), "{\"ref_frames\":1,\"ref_ids\":" + ids + "}");
            File.WriteAllBytes(Path.Combine(path, "ref_code.i32.bin"), new byte[64]);
            var speaker = new byte[4096];
            Buffer.BlockCopy(new[]{1f}, 0, speaker, 0, 4);
            File.WriteAllBytes(Path.Combine(path, "spk_emb.f32.bin"), speaker);
            return path;
        }

        [Test]
        public void InstalledVoicesIncludesPreparedProfilesAndExcludesBrokenAssets()
        {
            Voice("custom", "[151644,77091,198,42,151645,198]");
            Voice();
            string broken = Voice("broken");
            File.WriteAllBytes(Path.Combine(broken, "ref_code.i32.bin"), new byte[63]);
            CollectionAssert.AreEqual(new[]{"demo", "custom"}, MNNQwen3ExplicitTts.InstalledVoices(_root));
            Assert.Throws<InvalidDataException>(() => MNNQwen3TtsVoice.Read(broken));
        }

        [TestCase("[]")]
        [TestCase("[[151644,77091,198,151936,151645,198]]")]
        [TestCase("[[151644,77091,198,42,151645,198],[1]]")]
        [TestCase("[1,2,3,4,5,6]")]
        public void InvalidReferenceTokenTemplateIsRejected(string ids)
        {
            string voice = Voice(ids: ids);
            Assert.Throws<InvalidDataException>(() => MNNQwen3TtsVoice.Read(voice));
            Assert.IsEmpty(MNNQwen3ExplicitTts.InstalledVoices(_root));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(0f)]
        public void InvalidSpeakerValuesAreRejected(float value)
        {
            string voice = Voice();
            var speaker = new byte[4096];
            Buffer.BlockCopy(new[]{value}, 0, speaker, 0, 4);
            File.WriteAllBytes(Path.Combine(voice, "spk_emb.f32.bin"), speaker);
            Assert.Throws<InvalidDataException>(() => MNNQwen3TtsVoice.Read(voice));
        }

        [Test]
        public void CompleteProfileRequiresExactWeightSizesAndAValidPreparedVoice()
        {
            foreach (var file in MNNVerifiedTtsModel.Qwen3ExplicitFiles().Where(file => !file.Path.StartsWith("voices/")))
            {
                string path = Path.Combine(_root, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = File.Create(path)) stream.SetLength(file.Bytes);
            }
            Voice();
            Assert.True(MNNQwen3ExplicitTts.IsComplete(_root));
            var model = MNNStudioModel.Read(_root);
            Assert.AreEqual(MNNStudioTask.SpeechSynthesis, model.Task);
            Assert.True(model.IsQwen3Explicit);
            Assert.False(model.RequiresReferenceAudio);
            CollectionAssert.AreEqual(new[]{"zh", "en"}, model.SpeechLanguages);
            CollectionAssert.AreEqual(new[]{"demo"}, model.TtsVoices);
            Assert.True(model.HasGenerationPipeline);
            Voice("custom");
            Directory.Delete(Path.Combine(_root, "voices", "demo"), true);
            Assert.True(MNNQwen3ExplicitTts.IsComplete(_root), "A valid custom prepared voice can replace demo.");
            CollectionAssert.AreEqual(new[]{"custom"}, MNNStudioModel.Read(_root).TtsVoices);
            string weight = Path.Combine(_root, "weights", "cp_head_14.bin");
            using (var stream = File.OpenWrite(weight)) stream.SetLength(1);
            Assert.False(MNNQwen3ExplicitTts.IsComplete(_root));
            Assert.False(model.HasGenerationPipeline);
            Assert.True(MNNStudioModel.Read(_root).IsQwen3Explicit, "Incomplete assets retain their task identity.");
        }
    }
}
