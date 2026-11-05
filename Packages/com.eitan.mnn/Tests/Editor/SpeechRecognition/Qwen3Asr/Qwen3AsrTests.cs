using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class Qwen3AsrTests
    {
        [TestCase("auto", ""), TestCase("", ""), TestCase("en", "English"), TestCase(" ENGLISH ", "English")]
        [TestCase("zh", "Chinese"), TestCase("ja", "Japanese"), TestCase("ko", "Korean"), TestCase("yue", "Cantonese")]
        public void LanguageConditioningUsesModelNamesInsteadOfLiteralAuto(string language, string expected)
        {
            string normalized = MNNQwen3AsrPrompt.NormalizeLanguage(language);
            Assert.AreEqual(expected, normalized);
            string json = MNNQwen3AsrPrompt.Configuration(normalized);
            Assert.That(json, Does.Contain("\"asr_language\":\"" + expected + "\""));
            Assert.That(json, Does.Contain("if asr_language"));
            Assert.That(json, Does.Not.Contain("language auto"));
            Assert.IsNull(MNNQwen3AsrPrompt.Configuration(MNNQwen3AsrPrompt.NormalizeLanguage(null)));
        }

        [TestCase("language English<asr_text>Hello world.", "Hello world.")]
        [TestCase("language Chinese\n<asr_text>你好。", "你好。")]
        [TestCase("language None<asr_text>", "")]
        [TestCase("language None<asr_text> audible", "audible")]
        [TestCase("Plain text.", "Plain text.")]
        public void AutomaticLanguageHeadersAreNotUserTranscript(string raw, string expected)
        {
            Assert.AreEqual(expected, MNNQwen3AsrPrompt.Transcript(raw, true));
            Assert.AreEqual(raw.Trim(), MNNQwen3AsrPrompt.Transcript(raw, false));
        }

        [Test]
        public void InvalidRecognitionLanguageIsRejectedBeforeOpeningWeights()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "Qwen3AsrLanguage-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, MNNQwen3AsrConfig.FileName), "{}");
                Assert.Throws<ArgumentException>(() => MNNSpeechRecognizer.Load(directory, language: "not-a-language"),
                    "The profile facade must apply the requested language instead of ignoring it.");
            }
            finally { Directory.Delete(directory, true); }
        }

        [TestCase(false), TestCase(true)]
        public void CatalogUsesOnlyTheVerifiedProviderAndImmutablePlan(bool modelScope)
        {
            string repository = modelScope ? MNNQwen3AsrModel.ModelScopeRepository : MNNQwen3AsrModel.Repository;
            foreach (var provider in new[] {MNNModelSource.ModelScope, MNNModelSource.HuggingFace})
            {
                var model = new MNNRemoteModel {repositoryId = repository, name = repository.Split('/').Last(), provider = provider};
                MNNModelCatalog.ApplyArtifactEvidence(model, MNNQwen3AsrModel.Paths);
                Assert.AreEqual(modelScope == (provider == MNNModelSource.ModelScope), model.runtimeAvailable);
                Assert.AreEqual("SpeechRecognition", model.category);
                if (model.runtimeAvailable)
                    Assert.AreEqual(MNNQwen3AsrModel.Sizes.Sum() + (modelScope ? 1456 : 11357), model.fileSize);
            }
            Assert.AreEqual(modelScope ? MNNQwen3AsrModel.ModelScopeConfigRevision : MNNQwen3AsrModel.Revision,
                MNNQwen3AsrModel.ResolveRevision(modelScope ? "master" : "main", repository));
            Assert.Throws<ArgumentException>(() => MNNQwen3AsrModel.ResolveRevision("unknown", repository));
            Assert.AreNotEqual(MNNQwen3AsrModel.FileRevision(0, true), MNNQwen3AsrModel.FileRevision(1, true),
                "Never pin the newer llm_config to the earlier weight commit.");
        }

        [Test]
        public void UnpreparedAsrMetadataInARenamedFolderCannotBecomeChat()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "renamed-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "config.json"), "{}");
                File.WriteAllText(Path.Combine(directory, "llm_config.json"), "{\"model_type\":\"qwen3_asr\",\"is_audio\":true}");
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.SpeechRecognition, model.Task);
                Assert.AreEqual("SpeechRecognition", model.Category);
                Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
                Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.SpeechRecognition).CanRun(model));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void IncompleteTamperedAndUnknownProfilesAreRejected()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "Qwen3AsrTests-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.False(MNNQwen3AsrModel.IsComplete(directory));
                File.WriteAllText(Path.Combine(directory, "config.json"), "{}");
                Assert.Throws<InvalidDataException>(() => MNNQwen3AsrModel.Verify(directory));
                Assert.Throws<ArgumentException>(() => MNNQwen3AsrModel.Prepare(directory, "yunfengwang/Qwen3-ASR-MNN"));
                File.WriteAllText(Path.Combine(directory, MNNQwen3AsrConfig.FileName), "{\"repository\":\"unknown\"}");
                Assert.False(MNNQwen3AsrModel.IsComplete(directory));
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
