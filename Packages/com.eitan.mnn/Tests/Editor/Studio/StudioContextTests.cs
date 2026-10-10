using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class StudioContextTests
    {
        private static List<MNNStudioMessage> History(int turns, int length = 800)
        {
            var messages = new List<MNNStudioMessage>();
            for (int i = 0; i < turns; ++i)
            {
                messages.Add(new MNNStudioMessage(true, "FACT-" + i + ": " + new string ('q', length)));
                messages.Add(new MNNStudioMessage(false, "REPLY-" + i + ": " + new string ('a', length)));
            }

            return messages;
        }

        [Test]
        public void Compression_CoversEveryOldMessageInOrderAndDoesNotRecompressCoveredHistory()
        {
            var history = History(20);
            var excerpts = new List<string>();
            int checkpoint = 0;
            var first = MNNStudioContext.Prepare(history, "next", "", 0, 8192, 256, 0, text => text.Length / 4, prompt =>
            {
                excerpts.Add(prompt[1].Content);
                return "The user's name is Ada.";
            }, (summary, covered) =>
            {
                Assert.Greater(covered, checkpoint);
                checkpoint = covered;
            }, default);
            Assert.AreEqual(32, first.Covered);
            for (int i = 0; i < 16; ++i)
            {
                Assert.AreEqual(1, excerpts.Count(value => value.Contains("FACT-" + i + ":")));
                Assert.AreEqual(1, excerpts.Count(value => value.Contains("REPLY-" + i + ":")));
            }

            int calls = 0;
            var second = MNNStudioContext.Prepare(history, "follow-up", first.Summary, first.Covered, 8192, 256, 0, text => text.Length / 4, prompt =>
            {
                ++calls;
                return "Ada";
            }, null, default);
            Assert.AreEqual(0, calls);
            Assert.AreEqual(first.Covered, second.Covered);
            history.AddRange(History(8));
            var third = MNNStudioContext.Prepare(history, "new", first.Summary, first.Covered, 8192, 256, 0, text => text.Length / 4, prompt =>
            {
                StringAssert.Contains("Ada", prompt[1].Content);
                return "Ada";
            }, null, default);
            Assert.Greater(third.Covered, second.Covered);
        }

        [Test]
        public void TokenBudget_CompactsDenseTextEvenBelowCharacterThreshold()
        {
            var history = History(5, 600);
            var result = MNNStudioContext.Prepare(history, "current", "", 0, 4096, 256, 0, text => text.Length, prompt => "Compressed facts", null, default);
            Assert.Greater(result.Covered, 0);
            Assert.LessOrEqual(MNNStudioContext.Estimate(result.Conversation, text => text.Length), 4096 - 256 - 512);
            Assert.Throws<ArgumentException>(() => MNNStudioContext.Prepare(history, new string ('中', 4000), "", 0, 4096, 256, 0, text => text.Length, prompt => "facts", null, default));
        }

        [Test]
        public void LargeMessage_IsSplitWithoutSkippingItsBeginningOrCommittingPartialCoverage()
        {
            var history = new List<MNNStudioMessage>{new MNNStudioMessage(true, "BEGIN " + new string ('中', 18000) + " END"), new MNNStudioMessage(false, "Answer")};
            var excerpts = new List<string>();
            int commits = 0;
            var result = MNNStudioContext.Prepare(history, "next", "", 0, 4096, 256, 0, text => text.Length, prompt =>
            {
                Assert.LessOrEqual(MNNStudioContext.Estimate(prompt, text => text.Length), 4096 - 384 - 512);
                excerpts.Add(prompt[1].Content);
                return "memory";
            }, (summary, covered) =>
            {
                ++commits;
                Assert.AreEqual(2, covered);
            }, default);
            Assert.Greater(excerpts.Count, 5);
            StringAssert.Contains("BEGIN", excerpts[0]);
            StringAssert.Contains("END", excerpts.Last());
            Assert.AreEqual(1, commits);
            Assert.AreEqual(2, result.Covered);
        }

        [Test]
        public void Compaction_RetainsExplicitUserFactsEvenWhenModelSummaryOmitsThem()
        {
            var history = new List<MNNStudioMessage>{new MNNStudioMessage(true, "My name is Ada. Remember this important fact. " + new string ('q', 900)), new MNNStudioMessage(false, "Acknowledged. " + new string ('a', 900)), new MNNStudioMessage(true, "Please explain this detail. " + new string ('q', 900)), new MNNStudioMessage(false, "Understood. " + new string ('a', 900))};
            var result = MNNStudioContext.Prepare(history, "What is my name?", "", 0, 8192, 128, 0, text => text.Length * 3, prompt => "The earlier dialogue established a user identity.", null, default);
            StringAssert.Contains("Ada", result.Summary);
            StringAssert.Contains("My name is Ada", result.Summary);
        }

        [Test]
        public void SummaryFailureAndCancellation_DoNotAdvancePastSuccessfullyCompressedBatches()
        {
            var history = History(30);
            int calls = 0, covered = 0;
            Assert.Throws<InvalidOperationException>(() => MNNStudioContext.Prepare(history, "next", "", 0, 8192, 256, 0, text => text.Length / 4, prompt => ++calls == 2 ? "" : "Ada", (summary, count) => covered = count, default));
            Assert.Greater(covered, 0);
            Assert.Less(covered, 52);
            using (var cancellation = new CancellationTokenSource())
            {
                int originalCovered = covered;
                calls = 0;
                Assert.Throws<OperationCanceledException>(() => MNNStudioContext.Prepare(history, "next", "Ada", covered, 8192, 256, 0, text => text.Length / 4, prompt =>
                {
                    ++calls;
                    cancellation.Cancel();
                    return "new memory";
                }, (summary, count) => covered = count, cancellation.Token));
                Assert.AreEqual(originalCovered, covered);
            }
        }

        [Test]
        public void RepetitionGuard_StopsLongTextLoopsAndKeepsFirstCopy()
        {
            const string unit = "The answer is the same sentence repeated without adding any useful new information. ";
            Assert.IsTrue(MNNStudioRepetition.TryTrim("Introduction. " + unit + unit + unit, out var trimmed));
            Assert.AreEqual(("Introduction. " + unit).TrimEnd(), trimmed);
        }

        [TestCase("ha ha ha ha ha ha ha ha ha ha")]
        [TestCase("```csharp\nvar line = new string('a', 200);\n```")]
        [TestCase("First item. Second item. Third item. Different explanation.")]
        public void RepetitionGuard_DoesNotStopShortRepetitionsOrNormalContent(string text)
        {
            Assert.IsFalse(MNNStudioRepetition.TryTrim(text, out _));
        }

        private sealed class CheckpointBackend : IMNNStudioBackend
        {
            internal bool Cancel;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                request.MemoryProgress("Ada likes blue", 4);
                if (Cancel)
                    throw new OperationCanceledException();
                throw new InvalidOperationException("Reply failed after summary");
            }

            public void Dispose()
            {
            }
        }

        [UnityTest]
        public IEnumerator Session_PreservesMemoryCheckpointWhenReplyFailsOrIsCancelled()
        {
            foreach (bool cancel in new[]{false, true})
            {
                using (var session = new MNNStudioSession(new CheckpointBackend{Cancel = cancel}))
                {
                    session.Send(new MNNStudioRequest{Prompt = "next"});
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    while (session.Busy && timer.Elapsed.TotalSeconds < 10)
                    {
                        MNNStudioJobs.Update();
                        yield return null;
                    }

                    Assert.IsFalse(session.Busy);
                    Assert.AreEqual("Ada likes blue", session.MemorySummary);
                    Assert.AreEqual(4, session.MemoryCovered);
                    if (cancel)
                        Assert.IsTrue(session.Result.Cancelled);
                    else
                        Assert.AreEqual("Reply failed after summary", session.Error);
                }
            }
        }
    }
}
