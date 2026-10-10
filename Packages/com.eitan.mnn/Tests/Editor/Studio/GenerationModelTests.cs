using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    /// <summary>Offline contract tests. Tiny placeholder files verify discovery/routing only,
    /// never model integrity or image/audio generation.</summary>
    public class GenerationModelTests
    {
        private string _root;
        [SetUp]
        public void Setup()
        {
            _root = Path.GetFullPath(Path.Combine("TestArtifacts~", "GenerationModelTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void Cleanup()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        private string Repository(string name, params string[] files)
        {
            string directory = Path.Combine(_root, name);
            Directory.CreateDirectory(directory);
            foreach (string file in files)
            {
                string path = Path.Combine(directory, file);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, file.EndsWith(".json", StringComparison.Ordinal) ? "{}" : "test placeholder, not model weights");
            }

            return directory;
        }

        [TestCase("general"), TestCase("opencl")]
        public void OfficialDiffusionLayoutWithoutConfigOrMarkerIsDiscoverable(string variant)
        {
            string directory = Repository("stable-diffusion-v1-5-mnn", variant + "/text_encoder.mnn", variant + "/unet.mnn", variant + "/vae_decoder.mnn", variant + "/alphas.txt", variant + "/merges.txt", variant + "/vocab.json");
            var models = MNNStudioModel.Discover(new[]{_root, directory});
            Assert.AreEqual(1, models.Count);
            Assert.AreEqual(MNNStudioTask.ImageGeneration, models[0].Task);
            Assert.AreEqual(directory, models[0].Directory);
            Assert.False(MNNStudioTasks.Get(0).Accepts(models[0]));
        }

        [Test]
        public void RenamedCompleteGenerationFoldersRouteByFormat()
        {
            string tts = Repository("custom-voice", "config.json", "mnn_models/tts.json", "mnn_models/unicode_indexer.json", "voice_styles/M1.json", "mnn_models/fp16/duration_predictor.mnn", "mnn_models/fp16/text_encoder.mnn", "mnn_models/fp16/vector_estimator.mnn", "mnn_models/fp16/vocoder.mnn");
            File.WriteAllText(Path.Combine(tts, "config.json"), "{\"model_type\":\"supertonic\"}");
            var voice = MNNStudioModel.Read(tts);
            Assert.AreEqual(MNNStudioTask.SpeechSynthesis, voice.Task);
            Assert.AreEqual("fp16", voice.SupertonicPrecision);
            Assert.True(MNNStudioTasks.Get(5).CanRun(voice), "Routing only: files are placeholders, not valid graphs.");
            string sd = Repository("general", "text_encoder.mnn", "text_encoder.mnn.weight", "unet.mnn", "unet.mnn.weight", "vae_decoder.mnn", "vae_decoder.mnn.weight", "vocab.json", "merges.txt");
            var diffusion = MNNStudioModel.Read(sd);
            Assert.AreEqual(MNNStudioTask.ImageGeneration, diffusion.Task);
            Assert.True(MNNStudioTasks.Get(3).CanRun(diffusion));
            File.Delete(Path.Combine(sd, "unet.mnn.weight"));
            Assert.False(MNNStudioTasks.Get(3).CanRun(diffusion));
        }

        [Test]
        public void CompleteSanaGraphFilesRouteToManagedPipeline()
        {
            string path = Repository("custom-edit", "config.json", "connector.mnn", "connector.mnn.weight", "projector.mnn", "projector.mnn.weight", "transformer.mnn", "transformer.mnn.weight", "vae_encoder.mnn", "vae_encoder.mnn.weight", "vae_decoder.mnn", "vae_decoder.mnn.weight", "llm/llm.mnn", "llm/llm.mnn.weight", "llm/llm_config.json", "llm/tokenizer.txt", "llm/meta_queries.mnn");
            File.WriteAllText(Path.Combine(path, "config.json"), "{\"model_name\":\"Sana-Distill-v2\"}");
            var model = MNNStudioModel.Read(path);
            Assert.AreEqual(MNNStudioTask.ImageGeneration, model.Task);
            Assert.True(model.IsSana);
            Assert.True(MNNStudioTasks.Get(3).CanRun(model), "Routing only; placeholders are not valid graphs.");
        }

        [Test]
        public void SupertonicQuantizationFoldersAreOneTtsRepository()
        {
            var files = new List<string>{"config.json", "mnn_models/tts.json", "mnn_models/unicode_indexer.json", "voice_styles/F1.json"};
            foreach (string precision in new[]{"fp32", "fp16", "int8"})
                foreach (string stage in new[]{"duration_predictor", "text_encoder", "vector_estimator", "vocoder"})
                    files.Add("mnn_models/" + precision + "/" + stage + ".mnn");
            string directory = Repository("supertonic-tts-mnn", files.ToArray());
            var models = MNNStudioModel.Discover(new[]{_root, directory});
            Assert.AreEqual(1, models.Count);
            Assert.AreEqual(MNNStudioTask.SpeechSynthesis, models[0].Task);
            Assert.False(models[0].Supports(MNNModelCapabilities.SpeechOutput), "Standalone TTS must not advertise the Omni waveform contract.");
        }

        [TestCase("MNN/stable-diffusion-v1-5-mnn", 3)]
        [TestCase("MNN/MNN-Sana-Edit-V2", 3)]
        [TestCase("MNN/supertonic-tts-mnn", 5)]
        [TestCase("MNN/bert-vits2-MNN", 5)]
        [TestCase("MNN/piper-voices-MNN", 5)]
        public void IncompleteGenerationRepositoriesAreRejected(string id, int taskIndex)
        {
            string directory = Repository("renamed-" + taskIndex, "config.json", "llm_config.json");
            File.WriteAllText(Path.Combine(directory, MNNModelDownloadTasks.InstallMarker), id);
            // Even stray Omni metadata must not route TTS/Diffusion through the LLM loader.
            File.WriteAllText(Path.Combine(directory, "llm_config.json"), "{\"is_visual\":true,\"is_audio\":true,\"has_talker\":true,\"hidden_size\":2048,\"image_pad\":151655}");
            var model = MNNStudioModel.Read(directory);
            var task = MNNStudioTasks.Get(taskIndex);
            Assert.AreEqual((MNNStudioTask)taskIndex, model.Task);
            Assert.True(task.Accepts(model));
            Assert.False(task.CanRun(model));
            Assert.False(MNNStudioTasks.Get(0).CanRun(model));
            var error = Assert.Throws<NotSupportedException>(() => MNNStudioNativeBackend.Load(model, 4, false));
            StringAssert.Contains(task.Name, error.Message);
            StringAssert.Contains("not connected", error.Message);
        }

        [TestCase(3), TestCase(5)]
        public void StudioRejectsIncompleteGenerationBeforeCreatingNativeSession(int taskIndex)
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            var type = typeof(MNNChatStudio);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                var task = (MNNStudioTask)taskIndex;
                var model = new MNNStudioModel("local-generation-model", _root, task, MNNModelCapabilities.Text);
                type.GetField("_taskIndex", flags).SetValue(window, taskIndex);
                type.GetField("_available", flags).SetValue(window, new List<MNNStudioModel>{model});
                type.GetField("_modelIndex", flags).SetValue(window, 0);
                var error = Assert.Throws<TargetInvocationException>(() => type.GetMethod("EnsureSession", flags).Invoke(window, null));
                Assert.IsInstanceOf<NotSupportedException>(error.InnerException);
                StringAssert.Contains(MNNStudioTasks.Get(taskIndex).Name, error.InnerException.Message);
                Assert.IsNull(type.GetField("_session", flags).GetValue(window));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TtsRecommendationsAreStandaloneModelsAndDiffusionHasSeparateRequirements()
        {
            var tts = MNNStudioTasks.Get((int)MNNStudioTask.SpeechSynthesis);
            CollectionAssert.AreEquivalent(new[]{"MNN/supertonic-tts-mnn", "MNN/bert-vits2-MNN", "MNN/piper-voices-MNN"}, tts.Repositories);
            foreach (string id in tts.Repositories)
                Assert.True(tts.MatchesCatalog(new MNNModelInfo{modelScopeId = id}));
            Assert.False(tts.MatchesCatalog(new MNNModelInfo{modelScopeId = "MNN/Qwen2.5-Omni-3B-MNN"}));
            StringAssert.Contains("vocoder", tts.Requirements);
            StringAssert.Contains("VAE", MNNStudioTasks.Get((int)MNNStudioTask.ImageGeneration).Requirements);
        }
    }
}
