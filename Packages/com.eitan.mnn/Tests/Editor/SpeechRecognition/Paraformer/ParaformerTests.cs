using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class ParaformerTests
    {
        [TestCase(false, false), TestCase(false, true), TestCase(true, false)]
        public void CatalogPinsOnlyVerifiedRepositoryAndProvider(bool modelScope, bool duplicate)
        {
            string repository = duplicate ? MNNParaformerModel.DuplicateRepository : MNNParaformerModel.Repository;
            var model = new MNNRemoteModel {provider = modelScope ? MNNModelSource.ModelScope : MNNModelSource.HuggingFace,
                repositoryId = repository, name = repository.Split('/').Last()};
            MNNModelCatalog.ApplyArtifactEvidence(model, MNNParaformerModel.Paths);
            Assert.True(model.runtimeAvailable);
            Assert.AreEqual("SpeechRecognition", model.category);
            Assert.AreEqual(MNNParaformerModel.ResolveRevision(modelScope ? "master" : "main", repository, modelScope), model.revision);
            Assert.AreEqual(MNNParaformerModel.Sizes.Sum() + (modelScope ? 664 : 780), model.fileSize);
            Assert.Throws<ArgumentException>(() => MNNParaformerModel.ResolveRevision("other", repository, modelScope));
        }

        [TestCase("a/speech_paraformer-large_asr_nat-zh-cn-16k-common-vocab8404-online-mnn", false)]
        [TestCase(MNNParaformerModel.DuplicateRepository, true)]
        [TestCase("kiven2008/sherpa-mnn-streaming-paraformer-bilingual-zh-en", true)]
        public void UnverifiedExportsCannotUseTheVerifiedGraphProtocol(string repository, bool modelScope)
        {
            Assert.Throws<ArgumentException>(() => MNNParaformerModel.ResolveRevision(null, repository, modelScope));
            var model = new MNNRemoteModel {provider = modelScope ? MNNModelSource.ModelScope : MNNModelSource.HuggingFace,
                repositoryId = repository, name = repository.Split('/').Last()};
            MNNModelCatalog.ApplyArtifactEvidence(model, MNNParaformerModel.Paths);
            Assert.False(model.runtimeAvailable);
        }

        [TestCase(new[] {"昨", "天", "是", "mon@@", "day", "today", "is", "礼", "拜", "二"}, "昨天是 monday today is 礼拜二")]
        [TestCase(new[] {"hello", "wor@@", "ld"}, "hello world")]
        public void DecodeJoinsBpePiecesAndSeparatesEnglishWords(string[] tokens, string expected)
        {
            Assert.AreEqual(expected, MNNParaformerRecognizer.Decode(tokens));
        }

        [Test]
        public void EmptyUnknownIncompleteAndRenamedProfilesAreRejected()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "Paraformer-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.False(MNNParaformerModel.IsComplete(directory));
                File.WriteAllText(Path.Combine(directory, "config.json"), "{}");
                File.WriteAllText(Path.Combine(directory, "asr_config.json"), "{}");
                File.WriteAllText(Path.Combine(directory, "encoder.mnn"), "bad");
                File.WriteAllText(Path.Combine(directory, "decoder.mnn"), "bad");
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.SpeechRecognition, model.Task);
                Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
                Assert.False(model.HasSpeechRecognitionPipeline);
                Assert.Throws<InvalidDataException>(() => MNNParaformerModel.Verify(directory));
                File.WriteAllText(Path.Combine(directory, MNNParaformerConfig.FileName), "{\"repository\":\"unknown\"}");
                Assert.False(MNNParaformerModel.IsComplete(directory));
                Assert.Throws<ArgumentException>(() => MNNParaformerModel.Prepare(directory, "unknown"));
                File.WriteAllText(Path.Combine(directory, "tokenizer.txt"), "bad");
                Assert.Throws<InvalidDataException>(() => MNNParaformerRecognizer.ReadTokens(Path.Combine(directory, "tokenizer.txt")));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void SerializedPreparationRejectsTruncatedGraphsAndUnknownChunkSizes()
        {
            Assert.Throws<ArgumentNullException>(() => MNNParaformerGraph.PrepareEncoder(null, 20));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNParaformerGraph.PrepareEncoder(new byte[4], 10));
            Assert.Throws<InvalidDataException>(() => MNNParaformerGraph.PrepareEncoder(new byte[1], 20));
            Assert.Throws<InvalidDataException>(() => MNNParaformerGraph.PrepareEncoder(new byte[4], 20));
        }
    }
}
