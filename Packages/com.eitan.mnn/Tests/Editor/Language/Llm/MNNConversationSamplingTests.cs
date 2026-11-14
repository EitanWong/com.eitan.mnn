using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class MNNConversationSamplingTests
    {
        [TestCase("Adreno (TM) 740|OpenGL ES 3.2", "Qwen3.5-0.8B-MNN", MNNBackendType.Auto, MNNBackendType.CPU)]
        [TestCase("Adreno (TM)740|Vulkan", "Qwen3.5-0.8B-MNN/", MNNBackendType.Auto, MNNBackendType.CPU)]
        [TestCase("adreno 740|driver", "qwen3.5-0.8b-mnn", MNNBackendType.Auto, MNNBackendType.CPU)]
        [TestCase("Mali-G57 MC2|driver", "Qwen3.5-0.8B-MNN", MNNBackendType.Auto, MNNBackendType.CPU)]
        [TestCase("Adreno (TM) 740|driver", "SmolLM2-135M-Instruct-MNN", MNNBackendType.Auto, MNNBackendType.Auto)]
        [TestCase("Adreno (TM) 750|driver", "Qwen3.5-0.8B-MNN", MNNBackendType.Auto, MNNBackendType.Auto)]
        [TestCase("Adreno (TM) 7400|driver", "Qwen3.5-0.8B-MNN", MNNBackendType.Auto, MNNBackendType.Auto)]
        [TestCase("Adreno (TM) 740|driver", "Qwen3.5-0.8B-MNN", MNNBackendType.OpenCL, MNNBackendType.OpenCL)]
        [TestCase("", "Qwen3.5-0.8B-MNN", MNNBackendType.Auto, MNNBackendType.Auto)]
        public void AndroidAutoAvoidsOnlyMeasuredSlowOrFailingModelDevicePairs(string gpu, string model,
            MNNBackendType requested, MNNBackendType expected)
        {
            Assert.AreEqual(expected, MNNModelOptions.ForAndroidDevice(requested,
                Path.Combine("TestArtifacts~", "DevicePolicy", model), gpu));
        }

        [Serializable]
        private sealed class Sampling
        {
            public string sampler_type;
            public string[] mixed_samplers;
            public float temperature;
            public int topK;
            public float topP;
            public float repetition_penalty;
            public int penalty_window;
        }

        [TestCase("{}", 1.1f, 128)]
        [TestCase("{\"repetition_penalty\":1.0,\"penalty_window\":0}", 1f, 0)]
        [TestCase("{\"repetition_penalty\":1.2,\"penalty_window\":64}", 1.2f, 64)]
        [TestCase("{\"penalty\":1.05}", 1.05f, 128)]
        [TestCase("{\"repetition_penalty\":1.15,\"penalty\":1.3}", 1.15f, 128)]
        public void ConversationPreservesModelPenaltyAndBoundsOnlyTheUnconfiguredWindow(string configuration,
            float penalty, int window)
        {
            string directory = Path.GetFullPath("TestArtifacts~/ConversationSampling/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string config = Path.Combine(directory, "config.json");
            File.WriteAllText(config, configuration);
            try
            {
                foreach (float? temperature in new float?[] { null, .85f, 0f })
                {
                    var options = MNNModelOptions.Prepare(directory, 4, false, Path.Combine(directory, "cache"),
                        MNNPrecisionMode.Normal, true, conversationTemperature: temperature);
                    var sampling = JsonUtility.FromJson<Sampling>(options.Json);
                    Assert.AreEqual(penalty, sampling.repetition_penalty, "Keep the model's calibrated penalty.");
                    Assert.AreEqual(window, sampling.penalty_window, "An absent window must not penalize the entire growing history.");
                }
                Assert.AreEqual(configuration, File.ReadAllText(config));
            }
            finally { Directory.Delete(directory, true); }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ConversationUsesEffectiveMetadataSampling(bool externalBase, bool customMetadata)
        {
            string directory = Path.GetFullPath("TestArtifacts~/ConversationSampling/" + Guid.NewGuid().ToString("N"));
            string metadataDirectory = externalBase ? Path.Combine(directory, "weights") : directory;
            Directory.CreateDirectory(metadataDirectory);
            string name = customMetadata ? "model-info.json" : "llm_config.json";
            string configuration = "{\"sampler_type\":\"greedy\",\"repetition_penalty\":1.04,\"penalty_window\":7" +
                (externalBase ? ",\"base_dir\":\"" + metadataDirectory.Replace("\\", "/") + "/\"" : "") +
                (customMetadata ? ",\"llm_config\":\"" + name + "\"" : "") + "}";
            const string metadata = "{\"sampler_type\":\"mixed\",\"mixed_samplers\":[\"topP\",\"temperature\"],\"repetition_penalty\":1.23,\"penalty_window\":41}";
            string config = Path.Combine(directory, "config.json"), info = Path.Combine(metadataDirectory, name);
            File.WriteAllText(config, configuration);
            File.WriteAllText(info, metadata);
            try
            {
                foreach (float? temperature in new float?[] { null, 0f, .85f })
                {
                    var options = MNNModelOptions.Prepare(directory, 4, false, Path.Combine(directory, "cache"),
                        MNNPrecisionMode.Normal, true, MNNBackendType.CPU, conversationTemperature: temperature);
                    var sampling = JsonUtility.FromJson<Sampling>(options.Json);
                    Assert.AreEqual(1.23f, sampling.repetition_penalty, "Native metadata overrides config.json before caller overrides.");
                    Assert.AreEqual(41, sampling.penalty_window);
                    if (!temperature.HasValue)
                        CollectionAssert.AreEqual(new[] { "penalty", "topP", "temperature" }, sampling.mixed_samplers);
                    else Assert.AreEqual(temperature.Value == 0 ? "greedy" : "mixed", sampling.sampler_type);
                }
                Assert.AreEqual(configuration, File.ReadAllText(config));
                Assert.AreEqual(metadata, File.ReadAllText(info));
            }
            finally { Directory.Delete(directory, true); }
        }

        [TestCase(0f)]
        [TestCase(.85f)]
        [TestCase(2f)]
        public void ExplicitTemperatureConfiguresSamplingWithoutChangingDownloadedModel(float temperature)
        {
            string directory = Path.GetFullPath("TestArtifacts~/ConversationSampling/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string config = Path.Combine(directory, "config.json");
            const string original = "{\"sampler_type\":\"greedy\"}";
            File.WriteAllText(config, original);
            try
            {
                var options = MNNModelOptions.Prepare(directory, 4, false, Path.Combine(directory, "cache"),
                    MNNPrecisionMode.Normal, true, conversationTemperature: temperature);
                var sampling = JsonUtility.FromJson<Sampling>(options.Json);
                Assert.AreEqual(temperature, sampling.temperature);
                Assert.AreEqual(temperature == 0 ? "greedy" : "mixed", sampling.sampler_type);
                if (temperature > 0)
                {
                    CollectionAssert.AreEqual(new[] { "penalty", "topK", "topP", "temperature" }, sampling.mixed_samplers);
                    Assert.AreEqual(40, sampling.topK);
                    Assert.AreEqual(.95f, sampling.topP);
                }
                Assert.AreEqual(original, File.ReadAllText(config));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void RemoteGenerationSendsTokenBudgetAndCultureIndependentTemperature()
        {
            var culture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                const string events = "data: {\"choices\":[{\"index\":0,\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
                using (var handler = new OpenAITransportFixture(Encoding.UTF8.GetBytes(events), "text/event-stream"))
                using (var http = new HttpClient(handler))
                using (var client = new MNNOpenAIClient(new Uri("https://example.invalid/v1/"), null, TimeSpan.FromSeconds(3), http))
                {
                    client.GenerateTextAsync("model", Array.Empty<MNNOpenAIMessage>(), _ => Task.CompletedTask,
                        CancellationToken.None, maximumTokens: 512, temperature: .85f).GetAwaiter().GetResult();
                    StringAssert.Contains("\"max_tokens\":512", handler.Body);
                    StringAssert.Contains("\"temperature\":0.85", handler.Body);
                }
            }
            finally { CultureInfo.CurrentCulture = culture; }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-.01f)]
        [TestCase(2.01f)]
        public void InvalidTemperatureFailsBeforeNetworkOrModelLoading(float temperature)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNLlm.LoadForConversation(".", temperature));
            using (var client = new MNNOpenAIClient(new Uri("https://example.invalid/v1/"), null, TimeSpan.FromSeconds(3)))
                Assert.Throws<ArgumentOutOfRangeException>(() => client.GenerateTextAsync("model", Array.Empty<MNNOpenAIMessage>(),
                    _ => Task.CompletedTask, CancellationToken.None, temperature: temperature).GetAwaiter().GetResult());
        }
    }
}
