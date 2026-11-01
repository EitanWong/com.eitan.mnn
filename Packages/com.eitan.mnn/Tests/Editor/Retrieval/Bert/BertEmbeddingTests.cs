using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class BertEmbeddingTests
    {
        private string _directory;
        [SetUp]
        public void Setup()
        {
            _directory = Path.GetFullPath(Path.Combine("TestArtifacts~/BertEmbeddingTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_directory);
            File.WriteAllLines(Path.Combine(_directory, "vocab.txt"), new[] {"[PAD]", "[UNK]", "[CLS]", "[SEP]", "[MASK]", "hello", "world", "!", "cafe", "un", "##aff", "##able", "中", "文", "a"});
        }
        [TearDown]
        public void Cleanup() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

        [Test]
        public void WordPieceMatchesBasicNormalizationGreedyPiecesAndSpecialTokens()
        {
            var tokenizer = new MNNWordPieceTokenizer(_directory, new MNNBertEmbeddingConfig());
            CollectionAssert.AreEqual(new[] {2, 5, 6, 7, 3}, tokenizer.Encode("Héllo\tWORLD!", 16));
            CollectionAssert.AreEqual(new[] {2, 8, 9, 10, 11, 12, 13, 3}, tokenizer.Encode("Café unaffable中文", 16));
            CollectionAssert.AreEqual(new[] {2, 1, 3}, tokenizer.Encode("unaffablemissing", 16), "An unknown suffix makes the whole word unknown.");
            CollectionAssert.AreEqual(new[] {2, 5, 4, 6, 3}, tokenizer.Encode("hello[MASK]world", 16));
            CollectionAssert.AreEqual(new[] {2, 1, 3}, tokenizer.Encode(char.ConvertFromUtf32(0x1f600), 16));
            CollectionAssert.AreEqual(new[] {2, 1, 3}, tokenizer.Encode(new string('a', 101), 16));
        }

        [Test]
        public void TruncationAlwaysReservesClsAndSep()
        {
            var tokenizer = new MNNWordPieceTokenizer(_directory, new MNNBertEmbeddingConfig());
            CollectionAssert.AreEqual(new[] {2, 3}, tokenizer.Encode("hello world!", 2));
            CollectionAssert.AreEqual(new[] {2, 5, 6, 3}, tokenizer.Encode("hello world!", 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => tokenizer.Encode("hello", 1));
        }

        [Test]
        public void PoolingUsesTheSelectedSentenceContractAndRejectsInvalidValues()
        {
            float[] hidden = {2, 4, 6, 8};
            CollectionAssert.AreEqual(new float[] {2, 4}, MNNBertEmbedding.Pool(hidden, 2, 2, MNNBertPooling.Cls, false));
            CollectionAssert.AreEqual(new float[] {4, 6}, MNNBertEmbedding.Pool(hidden, 2, 2, MNNBertPooling.Mean, false));
            var normalized = MNNBertEmbedding.Pool(hidden, 2, 2, MNNBertPooling.Mean, true);
            Assert.That(normalized.Sum(value => value * value), Is.EqualTo(1).Within(1e-6));
            Assert.Throws<InvalidDataException>(() => MNNBertEmbedding.Pool(new[] {0f, 0f}, 1, 2, MNNBertPooling.Cls, true));
            Assert.Throws<InvalidDataException>(() => MNNBertEmbedding.Pool(new[] {1f, float.NaN}, 1, 2, MNNBertPooling.Cls, true));
            Assert.Throws<InvalidDataException>(() => MNNBertEmbedding.Pool(hidden, 3, 2, MNNBertPooling.Mean, true));
        }

        [Test]
        public void ExplicitProfileRoutesRenamedModelsWithoutModifyingUpstreamConfig()
        {
            File.WriteAllText(Path.Combine(_directory, "encoder.mnn"), "placeholder, not a real graph");
            File.WriteAllText(Path.Combine(_directory, "config.json"), "{\"model_type\":\"bert\",\"unrelated\":true}");
            string upstream = File.ReadAllText(Path.Combine(_directory, "config.json"));
            Assert.IsFalse(MNNBertEmbeddingConfig.IsConfigured(_directory));
            File.WriteAllText(Path.Combine(_directory, MNNBertEmbeddingConfig.FileName), JsonUtility.ToJson(new MNNBertEmbeddingConfig {graph = "encoder.mnn"}));
            Assert.IsTrue(MNNBertEmbeddingConfig.IsConfigured(_directory));
            Assert.AreEqual("encoder.mnn", MNNBertEmbeddingConfig.Read(_directory).graph);
            var model = MNNStudioModel.Read(_directory);
            Assert.AreEqual(MNNStudioTask.Embedding, model.Task);
            Assert.AreEqual(upstream, File.ReadAllText(Path.Combine(_directory, "config.json")));
            Assert.Throws<InvalidDataException>(() => MNNBertEmbeddingConfig.ResolveFile(_directory, Path.GetFullPath(".")));
        }

        [TestCase(MNNBertEmbeddingModel.BgeRepository)]
        [TestCase(MNNBertEmbeddingModel.AlternateBgeRepository)]
        [TestCase(MNNBertEmbeddingModel.MiniLmRepository)]
        public void CuratedProfilesPinOnlyTheRequiredMnnAssetsAndRejectOtherRevisions(string repository)
        {
            var files = MNNBertEmbeddingModel.Files(repository);
            Assert.AreEqual(3, files.Length);
            Assert.AreEqual(1, files.Count(file => file.Path.EndsWith(".mnn")));
            Assert.IsTrue(files.All(file => file.Bytes > 0 && file.Sha256.Length == 64));
            Assert.IsFalse(files.Any(file => file.Path.EndsWith(".onnx") || file.Path.EndsWith(".gguf") || file.Path.EndsWith(".tflite")));
            Assert.AreEqual(40, MNNBertEmbeddingModel.ResolveRevision(repository, "main").Length);
            Assert.Throws<ArgumentException>(() => MNNBertEmbeddingModel.ResolveRevision(repository, "another-revision"));
            Assert.Throws<FileNotFoundException>(() => MNNBertEmbeddingModel.Prepare(_directory, repository));
            Assert.IsFalse(File.Exists(Path.Combine(_directory, MNNBertEmbeddingConfig.FileName)));
        }
    }
}
