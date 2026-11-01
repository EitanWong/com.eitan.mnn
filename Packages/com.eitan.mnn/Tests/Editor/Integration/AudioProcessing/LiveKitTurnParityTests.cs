using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class LiveKitTurnParityTests
    {
        [Serializable] private sealed class Message { public string role, content; }
        [Serializable] private sealed class Row
        {
            public string name, prompt;
            public Message[] messages;
            public int[] ids;
            public float probability_q8, probability_fp32;
        }
        [Serializable] private sealed class Cases { public Row[] rows; }
        private static string DirectoryPath => Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models", "LiveKit-Turn-Detector-MNN"));

        [TestCase("complete"), TestCase("unfinished"), TestCase("numbers"), TestCase("chinese"), TestCase("chinese_unfinished"), TestCase("compatibility"), TestCase("unicode"), TestCase("history")]
        [Explicit("Requires the independent official LiveKit prompt and Hugging Face tokenizer oracle.")]
        public void PromptAndTokensMatchOfficial(string name)
        {
            var row = Read().Single(value => value.name == name);
            var messages = row.messages.Select(value => new MNNLiveKitTurnMessage(value.role, value.content)).ToArray();
            string prompt = MNNLiveKitTurnDetector.FormatChatContext(messages);
            Assert.AreEqual(row.prompt, prompt);
            CollectionAssert.AreEqual(row.ids, Tail(new MNNLiveKitTokenizer(DirectoryPath).Encode(prompt)));
        }

        [Test, Explicit("Requires all official prompt/tokenizer edge-case references, including 128-token truncation.")]
        public void AllPromptAndTokenCasesMatchOfficial()
        {
            var tokenizer = new MNNLiveKitTokenizer(DirectoryPath);
            foreach (var row in Read())
            {
                var messages = row.messages.Select(value => new MNNLiveKitTurnMessage(value.role, value.content)).ToArray();
                string prompt = MNNLiveKitTurnDetector.FormatChatContext(messages);
                Assert.AreEqual(row.prompt, prompt, row.name);
                CollectionAssert.AreEqual(row.ids, Tail(tokenizer.Encode(prompt)), row.name);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        [Explicit("Requires the original LiveKit ONNX actual-transcript probability oracle.")]
        public void TranscriptProbabilitiesMatchSource(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore(backend + " unavailable.");
            using (var model = MNNLiveKitTurnDetector.Load(DirectoryPath, 2, backend))
            {
                var warmTimes = new System.Collections.Generic.List<double>();
                foreach (var row in Read())
                {
                    var messages = row.messages.Select(value => new MNNLiveKitTurnMessage(value.role, value.content)).ToArray();
                    float actual = model.Predict(messages);
                    TestContext.WriteLine(row.name + ": source=" + row.probability_fp32 + "; actual=" + actual);
                    Assert.That(Math.Abs(actual - row.probability_fp32), Is.LessThan(backend == MNNBackendType.CPU ? .00002 : .0005), row.name);
                    Assert.AreEqual(row.probability_fp32 >= .5f, actual >= .5f);
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    float repeated = model.Predict(messages);
                    warmTimes.Add(timer.Elapsed.TotalMilliseconds);
                    Assert.That(Math.Abs(actual - repeated), Is.LessThan(1e-6), row.name + " repeated");
                }
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNAcceleration.PreferredBackend : backend, model.Backend);
                warmTimes.Sort();
                TestContext.WriteLine("Warm repeated prompt median ms=" + warmTimes[warmTimes.Count / 2] + "; max=" + warmTimes[warmTimes.Count - 1]);
                Assert.Throws<OperationCanceledException>(() => model.Predict("hello", cancellationToken: new System.Threading.CancellationToken(true)));
                Assert.That(model.Predict("hello"), Is.InRange(0f, 1f));
            }
        }

        private static int[] Tail(int[] ids) => ids.Skip(Math.Max(0, ids.Length - 128)).ToArray();

        private static Row[] Read()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_LIVEKIT_REFERENCE");
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Set MNN_TEST_LIVEKIT_REFERENCE to the saved official transcript oracle.");
            return JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(directory, "cases.json"))).rows;
        }
    }
}
