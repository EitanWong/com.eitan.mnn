using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class ModelClassificationTests
    {
        [TestCase("budaoshou/InsightFace-2D106Det-MNN", "face-2d-keypoints", MNNModelCategory.FaceLandmarks)]
        [TestCase("jingluo20190901/modnet_mnn", "universal-matting", MNNModelCategory.ImageMatting)]
        [TestCase("dr3334/PP-DocLayoutV3-mnn", "table-recognition", MNNModelCategory.DocumentLayout)]
        [TestCase("lincent/VGGT-MNN", "image-to-3D", MNNModelCategory.Reconstruction3D)]
        [TestCase("MNN/TaoAvatar-NNR-MNN", "face-reconstruction", MNNModelCategory.Reconstruction3D)]
        [TestCase("cola9936/UniTalker-MNN", "any-to-any", MNNModelCategory.AvatarAnimation)]
        [TestCase("cix/anonymous-mnn", "image-text-to-text", MNNModelCategory.VisionLanguageModel)]
        [TestCase("huangzhengxiang/Qwen3-TTS-0.6B-Base-INT8-MNN", "audio-generation", MNNModelCategory.SpeechSynthesis)]
        [TestCase("wangjazz/kokoro-82m-mnn", null, MNNModelCategory.SpeechSynthesis)]
        [TestCase("Hosstia/opus-mt-zh-en-mnn", "translation", MNNModelCategory.Translation)]
        [TestCase("playtranslate/qwen2.5-1.5b-instruct-mnn", "translation", MNNModelCategory.Translation)]
        [TestCase("ghost7/mnn_android_so_zip", null, MNNModelCategory.RuntimeResources)]
        [TestCase("klay11/PGL-Net", "image-dehazing", MNNModelCategory.ImageRestoration)]
        [TestCase("budaoshou/Glint360K-R100-MNN", null, MNNModelCategory.FaceRecognition)]
        [TestCase("budaoshou/MobileCLIP-MNN", "general-recognition", MNNModelCategory.ImageEmbedding)]
        [TestCase("Hakureirm/BioCLIP2-ViT-L14-MNN", null, MNNModelCategory.ImageEmbedding)]
        [TestCase("kiven2008/sherpa-mnn-streaming-paraformer-bilingual-zh-en", null, MNNModelCategory.SpeechRecognition)]
        [TestCase("developerabu/multilingual-e5-small-mnn", null, MNNModelCategory.EmbeddingModel)]
        [TestCase("wo105090/SenseVoiceSmall-MNN", null, MNNModelCategory.SpeechRecognition)]
        [TestCase("Jeryuk/kws-zipformer-wenetspeech-3.3M-MNN", "automatic-speech-recognition", MNNModelCategory.KeywordSpotting)]
        [TestCase("Jeryuk/kws-zipformer-wenetspeech-3.3M-MNN", null, MNNModelCategory.KeywordSpotting)]
        [TestCase("a/custom-MNN", "keyword-spotting", MNNModelCategory.KeywordSpotting)]
        [TestCase("a/custom-MNN", "wake-word-detection", MNNModelCategory.KeywordSpotting)]
        [TestCase("Jeryuk/silero_vad-MNN", null, MNNModelCategory.AudioProcessing)]
        [TestCase("Jeryuk/smart-turn-v3.2-gpu-MNN", null, MNNModelCategory.AudioProcessing)]
        [TestCase("simota1987/AnimateDiff-Lightning-MNN", null, MNNModelCategory.VideoGeneration)]
        [TestCase("daveyeke/rn-mnn-bench-models", null, MNNModelCategory.ModelCollection)]
        [TestCase("nu1lp7r/mfv-mnn-external-oob", null, MNNModelCategory.ModelDiagnostics)]
        [TestCase("currentlyawey/mnn-initnet-index-oob-write", null, MNNModelCategory.ModelDiagnostics)]
        [TestCase("a/DepthAnything-MNN", "depth-estimation", MNNModelCategory.DepthEstimation)]
        [TestCase("a/Policy-MNN", "robotics", MNNModelCategory.Robotics)]
        [TestCase("MNN/stable-diffusion-v1-5-mnn", "text-generation", MNNModelCategory.ImageGenerationModel)]
        [TestCase("MNN/Qwen2.5-VL-3B", "text-generation", MNNModelCategory.VisionLanguageModel)]
        [TestCase("MNN/MobileLLM-125M-MNN", "text-generation", MNNModelCategory.LargeLanguageModel)]
        [TestCase("MNN/SmolLM2-135M-Instruct-MNN", "text-generation", MNNModelCategory.LargeLanguageModel)]
        [TestCase("MNN/Qwen2.5-0.5B-Instruct-MNN", "text-generation", MNNModelCategory.LargeLanguageModel)]
        [TestCase("MNN/Qwen3-0.6B-MNN", "text-generation", MNNModelCategory.LargeLanguageModel)]
        [TestCase("ArcherFD/SAFlow", null, MNNModelCategory.StyleTransfer)]
        [TestCase("cgisky/stable-audio-3-mnn", "audio-generation", MNNModelCategory.AudioGeneration)]
        [TestCase("yunfengwang/wavlm-large-mnn", null, MNNModelCategory.AudioProcessing)]
        [TestCase("dr3334/UVDoc-MNN", null, MNNModelCategory.DocumentRectification)]
        [TestCase("yunfengwang/IndexTTS-2.5-mnn", null, MNNModelCategory.SpeechSynthesis)]
        [TestCase("yunfengwang/music3-mnn", null, MNNModelCategory.AudioGeneration)]
        [TestCase("MNN/Qwen3Guard-Gen-8B-MNN", "text-generation", MNNModelCategory.SafetyModel)]
        [TestCase("MNN/Fun-Audio-Chat-8B-MNN", "text-generation", MNNModelCategory.AudioLanguageModel)]
        [TestCase("MNN/LFM2.5-Audio-1.5B-MNN", "text-generation", MNNModelCategory.AudioLanguageModel)]
        [TestCase("MNN/Qwen2.5-Omni-3B", "image-text-to-text", MNNModelCategory.OmniModel)]
        [TestCase("a/Qwen3-VL-Embedding-2B-MNN", "image-text-to-text", MNNModelCategory.ImageEmbedding)]
        [TestCase("a/bge-reranker-MNN", "sentence-similarity", MNNModelCategory.Reranker)]
        [TestCase("a/model.mnn", null, MNNModelCategory.Other)]
        [TestCase("a/InsightFace-MNN", null, MNNModelCategory.Other)]
        [TestCase("omni-audio-vision-coder/model-MNN", null, MNNModelCategory.Other)]
        [TestCase("a/fluxcapacitor-model", null, MNNModelCategory.Other)]
        public void UsesTaskAndSpecificFamilyEvidence(string id, string task, MNNModelCategory expected)
        {
            Assert.AreEqual(expected.ToString(), MNNModelRepository.Classify(id, tasks: task == null ? null : new[] { task }));
            if (task != null)
                Assert.AreEqual(expected.ToString(), MNNModelRepository.Classify(id, tags: new[] { "task:" + task }));
        }

        [Test]
        public void DescriptionsAndInternalStageNamesDoNotOverrideTheTask()
        {
            Assert.AreEqual("ObjectDetection", MNNModelRepository.Classify("a/custom-MNN",
                description: "Use after Whisper ASR or compare with Stable-Diffusion, BGE, and Omni models.",
                tasks: new[] { "object-detection" }, paths: new[] { "text_encoder.mnn", "vision.mnn", "audio.mnn" }));
            Assert.AreEqual("Other", MNNModelRepository.Classify("a/custom-MNN", description: "weights for NLP audio vision"));
            Assert.AreEqual("LargeLanguageModel", MNNModelRepository.Classify("a/custom-MNN", paths: new[] { "variant/llm_config.json", "variant/llm.mnn" }));
            Assert.AreEqual("ImageGenerationModel", MNNModelRepository.Classify("a/custom-MNN", paths: new[] { "general/text_encoder.mnn", "general/unet.mnn", "general/vae_decoder.mnn" }));
        }

        [Test]
        public void ConcurrentClassificationKeepsFamilyAndWordBoundaries()
        {
            string[] ids = { "a/Qwen3-ASR-MNN", "a/Qwen3-VL-Embedding-MNN", "a/whisper-tiny-MNN",
                "a/bge-reranker-MNN", "a/YOLOv8-MNN", "omni-audio-vision-coder/model-MNN", "a/fluxcapacitor-model" };
            string[] expected = { "SpeechRecognition", "ImageEmbedding", "SpeechRecognition", "Reranker",
                "ObjectDetection", "Other", "Other" };
            var categories = new string[512];
            Parallel.For(0, categories.Length, index => categories[index] = MNNModelRepository.Classify(ids[index % ids.Length]));
            for (int index = 0; index < categories.Length; ++index)
                Assert.AreEqual(expected[index % ids.Length], categories[index], "Concurrent request " + index);
        }

        [Test]
        public void MuseTalkAvatarArtifactsCorrectTheDefaultDetectionLabel()
        {
            string[] files = { "nested/musetalk_unet.mnn", "nested/musetalk_vae_decode.mnn" };
            Assert.AreEqual("AvatarAnimation", MNNModelRepository.Classify("bruice/MuseTalk-MNN",
                tasks: new[] { "image-object-detection" }, paths: files));
            Assert.AreEqual("AvatarAnimation", MNNModelRepository.Classify("a/MuseTalk-MNN"));
            Assert.AreEqual("ObjectDetection", MNNModelRepository.Classify("a/MuseTalk-MNN",
                tasks: new[] { "image-object-detection" }, paths: new[] { "detector.mnn" }));
            Assert.AreEqual("ObjectDetection", MNNModelRepository.Classify("a/custom-MNN",
                tasks: new[] { "image-object-detection" }, paths: files));
        }

        [Test]
        public void MixedArchiveTasksAreCollectionsAndQuantizationsKeepTheirTask()
        {
            Assert.AreEqual("ModelCollection", MNNModelRepository.Classify("iceCream2025/Qwen-MNN", tasks: new[] { "any-to-any" },
                paths: new[] { "Qwen2.5-Math-7B-MNN.zip", "Qwen2.5-Omni-3B-MNN.zip", "Qwen2.5-VL-3B-MNN.zip" }));
            Assert.AreEqual("LargeLanguageModel", MNNModelRepository.Classify("a/variants-MNN", paths: new[] { "Qwen3-4B-int4.zip", "Qwen3-4B-int8.zip" }));
            Assert.AreEqual("ModelCollection", MNNModelRepository.Classify("a/mixed-MNN", paths: new[] { "deeplabv3.mnn", "quartznet.mnn", "movenet.mnn" }));
            Assert.AreEqual("ImageEmbedding", MNNModelRepository.Classify("a/mixed-MNN", paths: new[] { "qnn_models_min/clip_v2.mnn" }));
            Assert.AreEqual("ModelDiagnostics", MNNModelRepository.Classify("a/custom-MNN", paths: new[] { "control.mnn", "malicious.mnn" }));
        }

        [Test]
        public void DocumentedUnrelatedMnnAcronymDoesNotQualifyAsAnArchiveCandidate()
        {
            Assert.IsFalse(MNNModelRepository.HasMnnRepositoryArtifacts("ritzz01/cnn-mnn-model", null, new[] { "Flickr8k_text.zip" }));
            Assert.IsTrue(MNNModelRepository.HasMnnRepositoryArtifacts("ritzz01/cnn-mnn-model", null, new[] { "converted/model.mnn" }));
            Assert.IsTrue(MNNModelRepository.HasMnnRepositoryArtifacts("other/model-mnn", null, new[] { "model.zip" }));
        }

        [Test]
        public void AllCategoryLabelsExistAndSerializedValuesAreStable()
        {
            Assert.AreEqual(12, (int)MNNModelCategory.Other);
            Assert.AreEqual(25, (int)MNNModelCategory.SpeechRecognition);
            foreach (MNNModelCategory category in Enum.GetValues(typeof(MNNModelCategory)))
            {
                string key = "category." + category.ToString().ToLowerInvariant();
                Assert.AreNotEqual(key, MNNLocalization.Get(key), category.ToString());
            }
        }

        [Test, Explicit("Reclassifies a local discovery snapshot without downloading weights or executing graphs.")]
        public void AuditCachedCatalog()
        {
            string input = Environment.GetEnvironmentVariable("MNN_TEST_CLASSIFICATION_INPUT");
            string output = Environment.GetEnvironmentVariable("MNN_TEST_CLASSIFICATION_OUTPUT");
            Assert.IsTrue(File.Exists(input), "Set MNN_TEST_CLASSIFICATION_INPUT to a discovered package-catalog.json.");
            Assert.IsFalse(string.IsNullOrEmpty(output), "Set MNN_TEST_CLASSIFICATION_OUTPUT inside TestArtifacts~/.");
            var repository = JsonUtility.FromJson<MNNModelRepository>(File.ReadAllText(input));
            Directory.CreateDirectory(output);
            var excluded = repository.models.Where(model => MNNModelRepository.IsKnownArchiveSearchFalsePositive(model.repositoryId ?? model.id, model.mnnFiles ?? Array.Empty<string>())).ToArray();
            File.WriteAllLines(Path.Combine(output, "excluded.tsv"), new[] { "provider\trepository\treason" }.Concat(excluded.Select(model => model.provider + "\t" + model.repositoryId + "\tModel card defines Multi-Head Attention Network; no MNN artifacts.")));
            repository.models.RemoveAll(model => excluded.Contains(model));
            var changes = repository.models.Select(model =>
            {
                string before = model.category;
                model.category = MNNModelRepository.Classify(model.repositoryId ?? model.id, model.name, model.description, model.tasks, model.tags, model.mnnFiles);
                return model.provider + "\t" + model.repositoryId + "\t" + before + "\t" + model.category;
            }).ToArray();
            File.WriteAllText(Path.Combine(output, "catalog.json"), JsonUtility.ToJson(repository, true));
            File.WriteAllLines(Path.Combine(output, "classification.tsv"), new[] { "provider\trepository\tbefore\tafter" }.Concat(changes));
            File.WriteAllLines(Path.Combine(output, "remaining-other.tsv"), repository.models.Where(model => model.category == "Other").Select(model => model.provider + "\t" + model.repositoryId));
            Assert.AreEqual(repository.models.Count, changes.Length);
            TestContext.WriteLine("Reclassified " + changes.Length + " repositories; Other: " + repository.models.Count(model => model.category == "Other"));
        }
    }
}
