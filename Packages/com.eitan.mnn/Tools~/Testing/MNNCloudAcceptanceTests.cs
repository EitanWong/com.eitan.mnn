using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.CloudAcceptance
{
    // Imported only by run_cloud_acceptance.py into its isolated Editor test assembly.
    // Credentials remain in inherited environment variables; output contains metrics only.
    public sealed class MNNCloudAcceptanceTests
    {
        [Serializable]
        public sealed class Plan { public int schema_version; public Scenario[] scenarios; }

        [Serializable]
        public sealed class Scenario
        {
            public string id, task, protocol, endpoint, model, credential_env, prompt, expected_text, audio, text, voice;
            public string application_id_env, resource_id_env;
            public int timeout_seconds;
            public float maximum_latency_seconds;
        }

        private static IEnumerable<TestCaseData> Cases()
        {
            string path = Environment.GetEnvironmentVariable("MNN_CLOUD_ACCEPTANCE_PLAN");
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new InvalidOperationException("Provide a preflighted nonsecret MNN_CLOUD_ACCEPTANCE_PLAN.");
            var plan = JsonUtility.FromJson<Plan>(File.ReadAllText(path));
            if (plan == null || plan.schema_version != 1 || plan.scenarios == null || plan.scenarios.Length == 0)
                throw new InvalidOperationException("Cloud acceptance plan is empty or invalid.");
            foreach (var scenario in plan.scenarios)
                yield return new TestCaseData(scenario).SetName("Cloud_" + scenario.id);
        }

        [TestCaseSource(nameof(Cases))]
        public void SharedProviderMeetsReferenceAndLatencyBudget(Scenario scenario) =>
            Task.Run(() => ExecuteAsync(scenario)).GetAwaiter().GetResult();

        private static async Task ExecuteAsync(Scenario scenario)
        {
            if (Environment.GetEnvironmentVariable("MNN_CLOUD_ACCEPTANCE_EXECUTE") != "1")
                Assert.Fail("This fixture executes real requests only through run_cloud_acceptance.py --execute.");
            string key = Environment.GetEnvironmentVariable(scenario.credential_env);
            Assert.IsFalse(string.IsNullOrWhiteSpace(key), "The required credential environment variable is absent.");
            var clock = Stopwatch.StartNew();
            long samples = 0;
            double squareSum = 0;
            int chunks = 0, characters = 0;
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(scenario.timeout_seconds)))
            {
                if (scenario.task == "chat" || scenario.protocol == "OpenAIRecognition")
                {
                    var options = new MNNOpenAIConversationOptions
                    {
                        Endpoint = scenario.endpoint, Model = scenario.model, TimeoutSeconds = scenario.timeout_seconds,
                        MaximumTokens = 128, MaximumCharacters = 2048, Temperature = 0
                    };
                    using (var provider = new MNNOpenAIConversationProvider(options, key,
                        scenario.task == "chat" ? MNNOpenAIConversationStage.Model : MNNOpenAIConversationStage.Asr))
                    {
                        await provider.PrepareAsync(timeout.Token).ConfigureAwait(false);
                        string answer;
                        if (scenario.task == "chat")
                        {
                            var text = new StringBuilder();
                            await provider.GenerateAsync(new[] { new MNNOpenAIMessage(MNNChatRole.User, scenario.prompt) },
                                null, 16000, delta => { ++chunks; text.Append(delta); return Task.CompletedTask; }, timeout.Token).ConfigureAwait(false);
                            answer = text.ToString();
                            Assert.Greater(chunks, 0, "The service returned no text deltas.");
                        }
                        else
                        {
                            float[] pcm = MNNCloudSpeechAudio.Decode(File.ReadAllBytes(scenario.audio), true, 16000);
                            answer = await provider.TranscribeAsync(pcm, 16000, timeout.Token).ConfigureAwait(false);
                        }
                        characters = answer.Length;
                        Assert.IsTrue(Normalize(answer) == Normalize(scenario.expected_text),
                            "Shared provider output did not match the independent expected reference.");
                    }
                }
                else
                {
                    Assert.IsTrue(Enum.TryParse(scenario.protocol, out MNNCloudSpeechProtocol protocol), "Unsupported speech protocol.");
                    var options = new MNNCloudSpeechOptions
                    {
                        Endpoint = scenario.endpoint, Model = scenario.model, Voice = scenario.voice, Protocol = protocol,
                        Task = scenario.task == "recognition" ? MNNCloudSpeechTask.Recognition : MNNCloudSpeechTask.Synthesis,
                        TimeoutSeconds = scenario.timeout_seconds,
                        ApplicationId = OptionalEnvironment(scenario.application_id_env), ResourceId = OptionalEnvironment(scenario.resource_id_env)
                    };
                    using (var provider = new MNNCloudSpeechProvider(options, key))
                    {
                        await provider.PrepareAsync(timeout.Token).ConfigureAwait(false);
                        if (scenario.task == "recognition")
                        {
                            float[] pcm = MNNCloudSpeechAudio.Decode(File.ReadAllBytes(scenario.audio), true, 16000);
                            string answer = await provider.TranscribeAsync(pcm, 16000, timeout.Token).ConfigureAwait(false);
                            characters = answer.Length;
                            Assert.IsTrue(Normalize(answer) == Normalize(scenario.expected_text),
                                "Shared speech recognition did not match the independent expected reference.");
                        }
                        else
                        {
                            await provider.SynthesizeAsync(scenario.text, (pcm, count) =>
                            {
                                Assert.That(count, Is.GreaterThan(0).And.LessThanOrEqualTo(pcm.Length));
                                for (int i = 0; i < count; ++i)
                                {
                                    Assert.IsFalse(float.IsNaN(pcm[i]) || float.IsInfinity(pcm[i]) || Math.Abs(pcm[i]) > 1,
                                        "Shared synthesis returned invalid normalized PCM.");
                                    squareSum += pcm[i] * pcm[i];
                                }
                                samples += count;
                                ++chunks;
                                Assert.LessOrEqual(samples, provider.SampleRate * 30L, "Synthesis exceeded the acceptance duration budget.");
                                return Task.CompletedTask;
                            }, timeout.Token).ConfigureAwait(false);
                            Assert.GreaterOrEqual(samples, provider.SampleRate / 10, "Shared synthesis returned less than 100 ms of audio.");
                            Assert.Greater(squareSum / samples, 1e-8, "Shared synthesis returned silence.");
                        }
                    }
                }
            }
            clock.Stop();
            Assert.LessOrEqual(clock.Elapsed.TotalSeconds, scenario.maximum_latency_seconds, "Service exceeded its acceptance latency budget.");
            TestContext.WriteLine("scenario=" + scenario.id + "; elapsed_ms=" + clock.ElapsedMilliseconds +
                "; chunks=" + chunks + "; characters=" + characters + "; pcm_samples=" + samples);
        }

        private static string OptionalEnvironment(string name) => string.IsNullOrEmpty(name) ? null : Environment.GetEnvironmentVariable(name);
        private static string Normalize(string text) => new string((text ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
