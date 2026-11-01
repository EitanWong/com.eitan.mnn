using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class ModelPathTests
    {
        private MNNModelConfig _config;
        [SetUp]
        public void Setup()
        {
            _config = ScriptableObject.CreateInstance<MNNModelConfig>();
            _config.modelFileName = "Qwen3.5-0.8B-MNN/llm.mnn";
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(_config);
        [Test]
        public void StreamingAssets_UsesBundledDirectory()
        {
            _config.storageLocation = ModelStorageLocation.StreamingAssets;
            Assert.AreEqual(Path.Combine(Application.streamingAssetsPath, "MNN/Models", _config.modelFileName), _config.GetModelPath());
        }

        [Test]
        public void PersistentData_UsesWritableDirectory()
        {
            _config.storageLocation = ModelStorageLocation.PersistentData;
            Assert.AreEqual(_config.GetPersistentPath(), _config.GetModelPath());
            StringAssert.StartsWith(Application.persistentDataPath, _config.GetModelPath());
        }

        [Test]
        public void Custom_UsesConfiguredDirectory()
        {
            _config.storageLocation = ModelStorageLocation.Custom;
            _config.customPath = Path.GetFullPath("TestArtifacts~/MNNValidation/Models");
            Assert.AreEqual(Path.Combine(_config.customPath, _config.modelFileName), _config.GetModelPath());
        }

        [Test]
        public void PlatformOverride_AppliesFileStorageAndUrl_IgnoresNullEntries()
        {
            _config.platformConfigs = new[]{null, new PlatformModelConfig{platform = Application.platform, platformModelFileName = "optimized.mnn", platformStorageLocation = ModelStorageLocation.PersistentData, platformDownloadUrl = "https://example.com/optimized.mnn"}};
            Assert.AreEqual("optimized.mnn", _config.GetEffectiveModelFileName());
            Assert.AreEqual(ModelStorageLocation.PersistentData, _config.GetEffectiveStorageLocation());
            Assert.AreEqual("https://example.com/optimized.mnn", _config.GetEffectiveDownloadUrl());
            StringAssert.StartsWith(Application.persistentDataPath, _config.GetModelPath());
        }

        [Test]
        public void EmptyPlatformFields_FallBackToModelDefaults()
        {
            _config.downloadUrl = "https://example.com/default.mnn";
            _config.platformConfigs = new[]{new PlatformModelConfig{platform = Application.platform}};
            Assert.AreEqual(_config.modelFileName, _config.GetEffectiveModelFileName());
            Assert.AreEqual(_config.downloadUrl, _config.GetEffectiveDownloadUrl());
        }

        [Test]
        public void MissingFile_IsUnavailable() => Assert.IsFalse(_config.IsModelAvailable());
    }
}
