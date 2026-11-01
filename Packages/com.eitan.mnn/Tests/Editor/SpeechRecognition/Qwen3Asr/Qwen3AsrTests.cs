using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class Qwen3AsrTests
    {
        [TestCase(false), TestCase(true)]
        public void CatalogUsesOnlyTheVerifiedProviderAndImmutablePlan(bool modelScope)
        {
            string repository = modelScope ? MNNQwen3AsrModel.ModelScopeRepository : MNNQwen3AsrModel.Repository;
            foreach (var provider in new[] {MNNModelProvider.ModelScope, MNNModelProvider.HuggingFace})
            {
                var model = new MNNModelInfo {repositoryId = repository, name = repository.Split('/').Last(), provider = provider};
                typeof(MNNModelRepository).GetMethod("ApplyArtifactEvidence", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] {model, MNNQwen3AsrModel.Paths});
                Assert.AreEqual(modelScope == (provider == MNNModelProvider.ModelScope), model.runtimeAvailable);
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
