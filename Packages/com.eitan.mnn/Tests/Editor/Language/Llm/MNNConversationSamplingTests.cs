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
