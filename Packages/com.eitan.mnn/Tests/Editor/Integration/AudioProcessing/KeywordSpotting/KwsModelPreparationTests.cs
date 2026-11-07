using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [TestFixture, Category("Integration"), Category("KWS")]
    public class KwsModelPreparationTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.GetFullPath("TestArtifacts~/KwsPreparation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_directory, true);

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        public void PreparationKeepsTheVerifiedLanguageAcrossRepeatedCalls(bool english, bool nested)
        {
            CopyAssets(english, nested);
            string profilePath = Path.Combine(_directory, MNNKeywordSpotterConfig.FileName);
            if (english) WriteProfile(true);

            for (int attempt = 0; attempt < 2; ++attempt)
            {
                MNNKwsModel.Prepare(_directory);
                var profile = MNNKeywordSpotterConfig.Read(_directory);
                Assert.AreEqual(english ? MNNEnglishKwsModel.Repository : MNNKwsModel.Repository, profile.repository);
                Assert.AreEqual(english ? MNNKeywordLanguage.English : MNNKeywordLanguage.Chinese, profile.Language);
                Assert.True(MNNKwsModel.IsComplete(_directory));
                Assert.DoesNotThrow(() => MNNKwsModel.Verify(_directory));
            }
            Assert.True(File.Exists(profilePath));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void PreparationRejectsDamagedAssetsWithoutChangingTheProfile(bool english, bool truncate)
        {
            string assets = CopyAssets(english, false);
            WriteProfile(english);
            string profilePath = Path.Combine(_directory, MNNKeywordSpotterConfig.FileName);
            string before = File.ReadAllText(profilePath);
            using (var stream = new FileStream(Path.Combine(assets, MNNKwsModel.Encoder), FileMode.Open, FileAccess.ReadWrite))
            {
                if (truncate) stream.SetLength(stream.Length - 1);
                else
                {
                    int value = stream.ReadByte();
                    stream.Position = 0;
                    stream.WriteByte((byte)(value ^ 1));
                }
            }

            Assert.Throws<InvalidDataException>(() => MNNKwsModel.Prepare(_directory));
            Assert.AreEqual(before, File.ReadAllText(profilePath));
        }

        private string CopyAssets(bool english, bool nested)
        {
            string source = Environment.GetEnvironmentVariable(english ? "MNN_ENGLISH_KWS_MODELS" : "MNN_TEST_KWS_MODELS");
            if (string.IsNullOrEmpty(source)) Assert.Ignore("Provide prepared KWS assets; these tests do not download or run native inference.");
            string assets = nested ? Path.Combine(_directory, MNNKwsModel.Prefix.TrimEnd('/')) : _directory;
            Directory.CreateDirectory(assets);
            source = MNNKwsModel.AssetDirectory(source);
            string[] paths = english ? MNNEnglishKwsModel.Paths : new[] { MNNKwsModel.Encoder, MNNKwsModel.Decoder, MNNKwsModel.Joiner, "tokens.txt" };
            foreach (string path in paths) File.Copy(Path.Combine(source, path), Path.Combine(assets, path));
            return assets;
        }

        private void WriteProfile(bool english) => File.WriteAllText(Path.Combine(_directory, MNNKeywordSpotterConfig.FileName),
            JsonUtility.ToJson(new MNNKeywordSpotterConfig { repository = english ? MNNEnglishKwsModel.Repository : MNNKwsModel.Repository }, true));
    }
}
