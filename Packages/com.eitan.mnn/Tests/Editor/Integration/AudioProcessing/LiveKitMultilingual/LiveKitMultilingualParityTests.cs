using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("LiveKitMultilingual")]
    public sealed class LiveKitMultilingualParityTests
    {
        [TestCase("en", false)]
        [TestCase("zh", true)]
        public async Task ConversationProviderUsesConfiguredLanguageThreshold(string language, bool expected)
        {
            string directory = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "LiveKit-Turn-Detector-Multilingual-MNN");
            using (var provider = MNNConversationLocalProviders.Create(MNNConversationModelAdapter.TextTurn, directory,
                       new MNNConversationLocalModelOptions { Language = language }))
            {
                await provider.PrepareAsync(CancellationToken.None);
                var turn = (IMNNConversationTranscriptTurnProvider)provider;
                Assert.True(turn.RequiresTranscript);
                Assert.AreEqual(expected, await turn.IsTurnCompleteAsync(null, 16000, "hold on let me finish", CancellationToken.None));
            }
        }

        [Serializable] private sealed class Row { public string text; public int[] ids; public float probability; }
        [Serializable] private sealed class Rows { public Row[] rows; }
        [Test]
        public void QwenTokenizerProbabilitiesAndLanguageThresholdsMatchOfficialSource()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT");
            var rows = JsonUtility.FromJson<Rows>("{\"rows\":" + File.ReadAllText(Path.Combine(root, "LiveKit", "reference.json")) + "}").rows;
            string directory = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "LiveKit-Turn-Detector-Multilingual-MNN");
            using (var model = MNNLiveKitMultilingual.Load(directory))
            using (var adapter = MNNTurnDetector.Load(directory))
            {
                Assert.AreEqual(.0066f, model.Threshold("zh-CN")); Assert.AreEqual(.011f, model.Threshold("en-US"));
                Assert.Throws<NotSupportedException>(() => model.Threshold("unsupported"));
                foreach (var row in rows)
                {
                    CollectionAssert.AreEqual(row.ids, model.Tokenize(new[] { new MNNLiveKitTurnMessage("user", row.text) }), row.text);
                    float actual = model.Predict(row.text);
                    TestContext.WriteLine(row.text + ": source=" + row.probability + "; MNN=" + actual);
                    Assert.Less(Math.Abs(actual - row.probability), .001f, row.text);
                    string language = row.text[0] > 127 ? "zh" : "en";
                    Assert.AreEqual(row.probability >= model.Threshold(language), model.IsEndOfTurn(row.text, language));
                    Assert.AreEqual(actual >= model.Threshold("en"), adapter.IsComplete(null, 16000, row.text));
                    Assert.AreEqual(actual >= model.Threshold(language), adapter.IsComplete(null, 16000, row.text, language: language));
                }
                Assert.False(adapter.IsComplete(null, 16000, "hold on let me finish", language: "en"));
                Assert.True(adapter.IsComplete(null, 16000, "hold on let me finish", language: "zh"));
            }
        }
    }
}
