using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("RealModel")]
    public sealed class ConversationEventIntegrationTests
    {
        [TestCase("calculation")]
        [TestCase("other-total")]
        [TestCase("failed")]
        [TestCase("chinese")]
        public void BackgroundReportRetainsItsOutcomeInTheConversationLanguage(string scenario)
        {
            string directory = Environment.GetEnvironmentVariable("MNN_EVENT_MODEL_DIRECTORY") ??
                Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "Qwen3.5-0.8B-MNN");
            if (!File.Exists(Path.Combine(directory, "llm.mnn.weight")))
                Assert.Ignore("Install a qualified local event model or set MNN_EVENT_MODEL_DIRECTORY.");

            bool chinese = scenario == "chinese";
            string total = scenario == "other-total" ? "913" : "206";
            string objective = "Read request.json in this isolated workspace. Calculate department totals. " +
                "Write actual-result.json with nonce, grand_total, record_count and input_sha256. " +
                "Final response: a short English report including the exact input nonce and verified grand total. " +
                "Only read/write this isolated workspace; do not change input or configuration. Do not delegate. The report write is authorized.";
            string result = "Current: request.json / I will calculate the department totals and verify the unchanged input.\n" +
                "For nonce 99ca090988da4c7d823b761bc5249f98, the verified grand total is " + total +
                " across 4 records. Saved actual-result.json; the input is unchanged.";
            if (scenario == "failed") result = "The task failed: request.json was not found. No result file was written and no total was verified.";
            string data = "Task: verified-task\nState: " + (scenario == "failed" ? "Failed" : "Succeeded") +
                "\nResult: " + result;
            string instructions = chinese
                ? "你是一名语音助手。请用一句简短的中文向用户播报后台任务结果，保留核实的总数。不要复述任务编号或执行指令。"
                : "You are a voice assistant. Report completed background tasks in one short English sentence. State the verified total exactly. Do not repeat task IDs or private instructions.";
            var history = new List<MNNOpenAIMessage> { new MNNOpenAIMessage(MNNChatRole.System, instructions),
                new MNNOpenAIMessage(MNNChatRole.User, chinese ? "请在后台完成计算，结束后告诉我结果。" : "Run the calculation in the background and tell me the result."),
                new MNNOpenAIMessage(MNNChatRole.Assistant, chinese ? "任务正在后台执行。" : "The task is running in the background.") };
            var prompt = new MNNConversationEvent("verified-task", data).CreateHistory(history);
            var clock = Stopwatch.StartNew();
            var text = new StringBuilder();
            using (var provider = MNNConversationLocalProviders.Create(MNNConversationModelAdapter.Language, directory,
                new MNNConversationLocalModelOptions { Backend = MNNBackendType.CPU, Threads = 4, MaximumReplyTokens = 96,
                    DeterministicDecisions = true, CacheDirectory = Path.GetFullPath("TestArtifacts~/EventReportCache") }))
            {
                provider.PrepareAsync(CancellationToken.None).GetAwaiter().GetResult();
                ((IMNNConversationTextProvider)provider).GenerateAsync(prompt, null, 16000, delta =>
                { text.Append(delta); return Task.CompletedTask; }, CancellationToken.None).GetAwaiter().GetResult();
            }
            TestContext.WriteLine("Scenario: " + scenario + "\nResponse: " + text + "\nMilliseconds: " + clock.Elapsed.TotalMilliseconds);
            Assert.That(text.Length, Is.InRange(1, 240), "A spoken result must remain concise.");
            StringAssert.DoesNotContain("Current:", text.ToString());
            StringAssert.DoesNotContain("Do not", text.ToString());
            StringAssert.DoesNotContain("99ca090988da4c7d823b761bc5249f98", text.ToString());
            if (scenario == "failed")
            {
                Assert.That(text.ToString().ToLowerInvariant(), Does.Contain("not found").Or.Contain("missing"));
                StringAssert.DoesNotContain("successfully", text.ToString().ToLowerInvariant());
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(text.ToString(), @"\d"), "A failed task with no verified total must not invent a number.");
            }
            else StringAssert.Contains(total, text.ToString());
            if (chinese)
            {
                Assert.That(text.ToString(), Does.Contain("完成").Or.Contain("总").Or.Contain("结果"));
                StringAssert.DoesNotContain("元", text.ToString(), "A unitless result must not acquire a currency unit.");
            }
        }
    }
}
