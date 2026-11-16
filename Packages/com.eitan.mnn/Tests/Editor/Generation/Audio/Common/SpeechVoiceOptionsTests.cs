using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class SpeechVoiceOptionsTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.GetFullPath(Path.Combine("TestArtifacts~", "SpeechVoiceOptions", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        [Test]
        public void UnsupportedPresetIsRejectedBeforeReferenceOrNativeLoading()
        {
            File.WriteAllBytes(Path.Combine(_root, "gpt_prefill_fp16.mnn"), Array.Empty<byte>());
            var error = Assert.Throws<ArgumentException>(() => MNNSpeechSynthesizer.Load(_root,
                new MNNSpeechSynthesisOptions { Voice = "unsupported" }));
            Assert.AreEqual("Voice", error.ParamName);
        }

        [Test]
        public void ConversationProviderUsesTheSameValidationBeforeNativeLoading()
        {
            File.WriteAllText(Path.Combine(_root, "config-cpu-cosyvoice-ras.json"), "{}");
            using var provider = MNNConversationLocalProviders.Create(MNNConversationModelAdapter.CosyVoice, _root,
                new MNNConversationLocalModelOptions { Voice = "unsupported" });
            var error = Assert.Throws<ArgumentException>(() => provider.PrepareAsync(CancellationToken.None).GetAwaiter().GetResult());
            Assert.AreEqual("Voice", error.ParamName);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("missing")]
        public void UnknownExportHasNoInventedVoices(string directory)
        {
            var options = MNNSpeechSynthesizer.GetVoiceOptions(directory);
            Assert.AreEqual(MNNSpeechModelKind.Unknown, options.ModelKind);
            Assert.IsEmpty(options.VoiceIds);
            Assert.False(options.RequiresReferenceAudio);
            Assert.IsNull(options.ResolveVoice(""));
            Assert.False(options.SupportsVoice("M1"));
        }

        [Test]
        public void RegisteredModelUsesItsOwnMetadataWithoutWeightsAndReturnsAnOwnedSnapshot()
        {
            string path = Path.Combine(_root, "mnn_hojo_tts.json");
            const string metadata = "{\"voices\":[\"hojo_zh_f_01\",\"hojo_en_m_02\"]}";
            File.WriteAllText(path, metadata);
            var options = MNNSpeechSynthesizer.GetVoiceOptions(_root);
            Assert.AreEqual(MNNSpeechModelKind.Hojo, options.ModelKind);
            CollectionAssert.AreEqual(new[] { "hojo_zh_f_01", "hojo_en_m_02" }, options.VoiceIds);
            Assert.False(options.RequiresReferenceAudio);
            Assert.AreEqual("hojo_en_m_02", options.ResolveVoice("hojo_en_m_02"));
            Assert.False(options.SupportsVoice("HOJO_EN_M_02"));
            Assert.Throws<ArgumentException>(() => options.ResolveVoice("other-model-voice"));
            options.VoiceIds[0] = "changed";
            Assert.AreEqual("hojo_zh_f_01", options.VoiceIds[0]);
            File.WriteAllText(path, "{\"voices\":[\"replacement\"]}");
            Assert.AreEqual("hojo_zh_f_01", options.VoiceIds[0]);
            CollectionAssert.AreEqual(new[] { "replacement" }, MNNSpeechSynthesizer.GetVoiceOptions(_root).VoiceIds);
            Assert.False(MNNSpeechSynthesizer.HasProfile(_root), "Voice discovery is not installation/inference validation.");
        }

        [Test]
        public void ReferenceRecordingIsNotExposedAsAPresetId()
        {
            File.WriteAllBytes(Path.Combine(_root, "gpt_prefill_fp16.mnn"), Array.Empty<byte>());
            var options = MNNSpeechSynthesizer.GetVoiceOptions(_root);
            Assert.AreEqual(MNNSpeechModelKind.IndexTts25, options.ModelKind);
            Assert.True(options.RequiresReferenceAudio);
            Assert.IsEmpty(options.VoiceIds);
            Assert.IsNull(options.ResolveVoice(null));
            Assert.False(options.SupportsVoice("Reference audio"));
        }

        [Test]
        public void InstalledStyleFilesDetermineChoicesAndRefreshAfterRemoval()
        {
            File.WriteAllText(Path.Combine(_root, "config.json"), "{\"model_type\":\"supertonic\"}");
            string styles = Path.Combine(_root, "voice_styles");
            Directory.CreateDirectory(styles);
            File.WriteAllText(Path.Combine(styles, "F3.json"), "{}");
            File.WriteAllText(Path.Combine(styles, "M2.json"), "{}");
            CollectionAssert.AreEqual(new[] { "M2", "F3" }, MNNSpeechSynthesizer.GetVoiceOptions(_root).VoiceIds);
            File.Delete(Path.Combine(styles, "F3.json"));
            CollectionAssert.AreEqual(new[] { "M2" }, MNNSpeechSynthesizer.GetVoiceOptions(_root).VoiceIds);
        }

        [Test]
        public void SingleVoiceModelRetainsItsDocumentedVoice()
        {
            File.WriteAllText(Path.Combine(_root, "config-cpu-cosyvoice-ras.json"), "{}");
            CollectionAssert.AreEqual(new[] { "Default" }, MNNSpeechSynthesizer.GetVoiceOptions(_root).VoiceIds);
        }

        [Test]
        public void MalformedRegisteredMetadataIsReported()
        {
            File.WriteAllText(Path.Combine(_root, "mnn_hojo_tts.json"), "{}");
            Assert.Throws<InvalidDataException>(() => MNNSpeechSynthesizer.GetVoiceOptions(_root));
        }

        [TestCase("[1]")]
        [TestCase("[null]")]
        [TestCase("[\"\"]")]
        [TestCase("[\"  \"]")]
        public void InvalidVoiceIdentifiersCannotBecomeChoices(string voices)
        {
            File.WriteAllText(Path.Combine(_root, "mnn_hojo_tts.json"), "{\"voices\":" + voices + "}");
            Assert.Throws<InvalidDataException>(() => MNNSpeechSynthesizer.GetVoiceOptions(_root));
        }

        [Test]
        public void DuplicateIdsAreOfferedOnceInModelOrder()
        {
            File.WriteAllText(Path.Combine(_root, "mnn_hojo_tts.json"), "{\"voices\":[\"b\",\"a\",\"b\"]}");
            CollectionAssert.AreEqual(new[] { "b", "a" }, MNNSpeechSynthesizer.GetVoiceOptions(_root).VoiceIds);
        }
    }
}
