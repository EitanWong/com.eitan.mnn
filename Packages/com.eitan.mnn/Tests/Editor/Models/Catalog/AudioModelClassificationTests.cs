using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class AudioModelClassificationTests
    {
        [TestCase("voice-activity-detection", "VoiceActivityDetection")]
        [TestCase("turn-detection", "TurnDetection")]
        [TestCase("end-of-turn-detection", "TurnDetection")]
        [TestCase("speech-enhancement", "SpeechEnhancement")]
        [TestCase("acoustic-noise-suppression", "SpeechEnhancement")]
        [TestCase("speech-separation", "SpeechSeparation")]
        [TestCase("target-speaker-extraction", "TargetSpeakerExtraction")]
        [TestCase("speaker-diarization", "SpeakerDiarization")]
        [TestCase("speaker-verification", "SpeakerRecognition")]
        [TestCase("audio-classification", "AudioClassification")]
        [TestCase("keyword-spotting", "KeywordSpotting")]
        [TestCase("acoustic-echo-cancellation", "EchoCancellation")]
        public void HubTasksHaveSpecificCategories(string task, string expected)
        {
            Assert.AreEqual(expected, MNNModelRepository.Classify("publisher/custom-MNN", tasks: new[] { task }));
            Assert.AreEqual(expected, MNNModelRepository.Classify("publisher/custom-MNN", tags: new[] { "task:" + task }));
        }

        [TestCase("Silero-v5.1-MNN", "VoiceActivityDetection")]
        [TestCase("MarbleNet-VAD-MNN", "VoiceActivityDetection")]
        [TestCase("FSMN-VAD-16K-MNN", "VoiceActivityDetection")]
        [TestCase("SmartTurn-v3.2-FP32-MNN", "TurnDetection")]
        [TestCase("NAMO-Turn-Detector-v1-Multilingual-MNN", "TurnDetection")]
        [TestCase("TurnSense-SmolLM2-135M-MNN", "TurnDetection")]
        [TestCase("VoxTurn-v1-MNN", "TurnDetection")]
        [TestCase("CueLight-Nano-MNN", "TurnDetection")]
        [TestCase("GTCRN-MNN", "SpeechEnhancement")]
        [TestCase("DPTNet-Libri1Mix-16K-MNN", "SpeechEnhancement")]
        [TestCase("MossFormer2-SS-16K-MNN", "SpeechSeparation")]
        [TestCase("SpExPlus-WSJ02Mix-8K-MNN", "TargetSpeakerExtraction")]
        [TestCase("AV-DPRNN-VoxCeleb2-16K-MNN", "TargetSpeakerExtraction")]
        [TestCase("AV-MossFormer2-TSE-16K-MNN", "TargetSpeakerExtraction")]
        [TestCase("Sortformer-4spk-v2.1-MNN", "SpeakerDiarization")]
        [TestCase("LS-EEND-Simu-8K-MNN", "SpeakerDiarization")]
        [TestCase("ECAPA-TDNN-MNN", "SpeakerRecognition")]
        public void PublishedAndDiscoveredModelsAgreeOnTheirTask(string name, string expected)
        {
            var published = MNNModelCatalog.CreateDefault().Models.Single(model => model.name == name);
            Assert.AreEqual(expected, published.category);
            Assert.AreEqual(expected, MNNModelRepository.Classify(published.repositoryId, tasks: published.tasks));
            Assert.AreEqual(expected, MNNModelRepository.Classify(published.repositoryId));
        }

        [TestCase("EitanWang/SpExPlus-WSJ02Mix-8K-MNN", "speech-separation", "TargetSpeakerExtraction")]
        [TestCase("EitanWang/CueLight-Nano-MNN", "voice-activity-detection", "TurnDetection")]
        [TestCase("Jeryuk/smart-turn-v3.2-gpu-MNN", "automatic-speech-recognition", "TurnDetection")]
        public void LegacyGenericAudioLabelsAreRefinedBySpecificFamilies(string id, string task, string expected)
        {
            Assert.AreEqual(expected, MNNModelRepository.Classify(id, tasks: new[] { task }));
        }

        [Test]
        public void EveryPublishedAudioProfileHasASpecificCategoryWithoutChangingSupport()
        {
            var models = MNNModelCatalog.CreateDefault().Models.Where(model => MNNModelCatalog.IsPublishedSpeechRepository(model.repositoryId)).ToArray();
            Assert.AreEqual(46, models.Length);
            Assert.False(models.Any(model => model.category == "AudioProcessing" || model.category == "Other"));
            Assert.True(models.All(model => model.category == MNNModelRepository.Classify(model.repositoryId, tasks: model.tasks)));
            Assert.AreEqual(1, models.Count(model => model.runtimeAvailable));
        }

        [Test]
        public void SpecificUnrelatedHubTasksAndOwnerNamesAreNotReclassifiedAsAudio()
        {
            Assert.AreEqual("ObjectDetection", MNNModelRepository.Classify("publisher/Silero-vad-MNN", tasks: new[] { "object-detection" }));
            Assert.AreEqual("Other", MNNModelRepository.Classify("vad-turn-detection/custom-MNN", description: "speech-enhancement VAD model"));
            Assert.AreEqual("AudioProcessing", MNNModelRepository.Classify("publisher/custom-MNN", tasks: new[] { "audio-to-audio" }));
        }

        [TestCase("VoiceActivityDetection", "vad", "语音活动检测")]
        [TestCase("TurnDetection", "turn detection", "轮次检测")]
        [TestCase("SpeechEnhancement", "denoise", "降噪")]
        [TestCase("SpeechSeparation", "speech separation", "语音分离")]
        [TestCase("TargetSpeakerExtraction", "target speaker", "目标说话人")]
        [TestCase("SpeakerDiarization", "diarization", "说话人分离")]
        [TestCase("AudioClassification", "audio classification", "声音分类")]
        public void RuntimeAndEditorSearchUseCategoryAliases(string category, string english, string chinese)
        {
            var runtime = new MNNModelCatalog();
            runtime.models.Add(new MNNRemoteModel { name = "anonymous", category = category });
            var editor = new MNNModelRepository();
            editor.models.Add(new MNNModelInfo { name = "anonymous", category = category });
            foreach (string query in new[] { english, chinese })
            {
                Assert.AreEqual(1, runtime.SearchModels(query).Count, query);
                Assert.AreEqual(1, editor.SearchModels(query).Count, query);
            }
            Assert.IsEmpty(runtime.SearchModels("object detection"));
        }

        [Test]
        public void ModelManagerBuildsAndAppliesSpecificCategoryFiltersIncludingSearch()
        {
            var repository = MNNModelRepository.GetDefaultRepository();
            var previousCache = MNNModelCatalogCache.Shared;
            string directory = Path.GetFullPath("TestArtifacts~/AudioCategoryPicker/" + Guid.NewGuid().ToString("N"));
            MNNModelCatalogCache.Shared = new MNNModelCatalogCache(directory, _ => Task.FromResult(new MNNModelRepository()));
            MNNModelManagerWindow window = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                Task.WhenAll(MNNModelCatalogCache.Shared.LoadAsync(MNNModelProvider.ModelScope),
                    MNNModelCatalogCache.Shared.LoadAsync(MNNModelProvider.HuggingFace)).GetAwaiter().GetResult();
                window = ScriptableObject.CreateInstance<MNNModelManagerWindow>();
                typeof(MNNModelManagerWindow).GetField("_repository", flags).SetValue(window, repository);
                typeof(MNNModelManagerWindow).GetMethod("RebuildCategoryOptions", flags).Invoke(window, null);
                var categories = (MNNModelCategory[])typeof(MNNModelManagerWindow).GetField("_categories", flags).GetValue(window);
                Assert.That(categories.Select(value => value.ToString()), Does.Contain("VoiceActivityDetection"));
                Assert.True(Enum.TryParse("VoiceActivityDetection", out MNNModelCategory vad));
                typeof(MNNModelManagerWindow).GetField("_selectedCategory", flags).SetValue(window, vad);
                typeof(MNNModelManagerWindow).GetField("_searchQuery", flags).SetValue(window, "vad");
                typeof(MNNModelManagerWindow).GetMethod("RefreshModelList", flags).Invoke(window, null);
                var selected = (System.Collections.Generic.List<MNNModelInfo>)typeof(MNNModelManagerWindow).GetField("_filteredModels", flags).GetValue(window);
                Assert.That(selected.Select(model => model.name), Does.Contain("Silero-v5.1-MNN"));
                Assert.True(selected.All(model => model.category == "VoiceActivityDetection"));
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                MNNModelCatalogCache.Shared = previousCache;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void CategoryEnumsKeepExistingSerializedValuesAndShareNewNames()
        {
            CollectionAssert.AreEqual(Enum.GetNames(typeof(MNNModelCatalogCategory)), Enum.GetNames(typeof(MNNModelCategory)));
            Assert.AreEqual(9, (int)MNNModelCatalogCategory.AudioProcessing);
            Assert.AreEqual(44, (int)MNNModelCatalogCategory.EchoCancellation);
            Assert.True(Enum.TryParse("TurnDetection", out MNNModelCatalogCategory _));
        }
    }
}
