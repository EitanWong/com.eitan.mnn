using System;
using NUnit.Framework;

namespace MNN.Unity.Editor.Tests
{
    public class TextDecisionStudioTests
    {
        [Test]
        public void DedicatedPageParsesChoiceScoreAndBooleanInOrder()
        {
            var choice = MNNChatStudio.ParseDecisionQuestion(MNNDecisionKind.Choice, "pick", " billing | refunds\n technical | outages\n\nother");
            Assert.AreEqual("billing", choice.Options[0].Label);
            Assert.AreEqual("refunds", choice.Options[0].Description);
            Assert.AreEqual("other", choice.Options[2].Label);
            var score = MNNChatStudio.ParseDecisionQuestion(MNNDecisionKind.Score, "priority", "low\nmedium\nhigh");
            Assert.AreEqual("0", score.Options[0].Label); Assert.AreEqual("high", score.Options[2].Description);
            var boolean = MNNChatStudio.ParseDecisionQuestion(MNNDecisionKind.Boolean, "holds?", "does not hold\nholds");
            Assert.AreEqual("does not hold", boolean.Options[0].Description);
            Assert.AreEqual("holds", boolean.Options[1].Description);
            Assert.Throws<ArgumentException>(() => MNNChatStudio.ParseDecisionQuestion(MNNDecisionKind.Boolean, "holds?", "only one"));
            Assert.Throws<ArgumentException>(() => MNNChatStudio.ParseDecisionQuestion(MNNDecisionKind.Choice, "pick", "a\na"));
        }
    }
}
