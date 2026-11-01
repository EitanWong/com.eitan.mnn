using System;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class LlmContextTests
    {
        [Test]
        public void PreservesSystemCurrentAndCompleteTurnsWithoutEditingCallerHistory()
        {
            var messages = new[] {
                new MNNChatMessage(MNNChatRole.System, "rules"),
                new MNNChatMessage(MNNChatRole.User, "old question"),
                new MNNChatMessage(MNNChatRole.Assistant, "old answer"),
                new MNNChatMessage(MNNChatRole.Assistant, "extra answer"),
                new MNNChatMessage(MNNChatRole.User, "recent question"),
                new MNNChatMessage(MNNChatRole.Assistant, "recent answer"),
                new MNNChatMessage(MNNChatRole.User, "latest") };
            var result = MNNConversationContext.Prepare(messages, 6, 2, candidate => candidate.Count);
            Assert.AreEqual(4, result.Messages.Count);
            Assert.AreEqual(3, result.RemovedMessages);
            Assert.AreEqual(4, result.InputTokens);
            CollectionAssert.AreEqual(new[] { messages[0], messages[4], messages[5], messages[6] }, result.Messages);
            Assert.AreEqual(7, messages.Length);
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<MNNChatMessage>)result.Messages).Clear());
        }

        [Test]
        public void RecountsWholeTemplateForEveryCandidateAndFailsIfMandatoryMessagesCannotFit()
        {
            var messages = new[] { new MNNChatMessage(MNNChatRole.System, "rules"),
                new MNNChatMessage(MNNChatRole.User, "old"), new MNNChatMessage(MNNChatRole.Assistant, "answer"),
                new MNNChatMessage(MNNChatRole.User, "latest") };
            int calls = 0;
            var result = MNNConversationContext.Prepare(messages, 20, 4, candidate => { ++calls; return candidate.Count == 4 ? 17 : 11; });
            Assert.AreEqual(11, result.InputTokens);
            Assert.AreEqual(2, calls);
            Assert.Throws<ArgumentException>(() => MNNConversationContext.Prepare(messages, 20, 4, _ => 17));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNConversationContext.Prepare(messages, 20, 20, _ => 0));
            Assert.Throws<ArgumentException>(() => MNNConversationContext.Prepare(messages.Take(3).ToArray(), 20, 4, _ => 0));
        }

        [TestCase("<img>image.png</img>")]
        [TestCase("<audio>audio.wav</audio>")]
        [TestCase("<video>video.mp4</video>")]
        public void DoesNotMislabelMediaTextCountsAsEmbeddingBudgets(string text)
        {
            Assert.Throws<NotSupportedException>(() => MNNConversationContext.Prepare(
                new[] { new MNNChatMessage(MNNChatRole.User, text) }, 1024, 128, _ => 1));
        }
    }
}
