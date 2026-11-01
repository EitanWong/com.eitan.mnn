using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class TextDecisionTests
    {
        [Test]
        public void QuestionsOwnTheirOptionsAndRejectAmbiguousLabels()
        {
            var options = new[] {new MNNDecisionOption("a"), new MNNDecisionOption("b")};
            var question = MNNDecisionQuestion.Choice("pick", options);
            options[0] = new MNNDecisionOption("changed");
            Assert.AreEqual("a", question.Options[0].Label);
            Assert.Throws<ArgumentException>(() => MNNDecisionQuestion.Choice("pick", new MNNDecisionOption("a"), new MNNDecisionOption("a")));
            Assert.Throws<ArgumentException>(() => MNNDecisionQuestion.Score("pick", "one"));
            Assert.Throws<ArgumentException>(() => MNNDecisionQuestion.Boolean(" "));
        }

        [Test]
        public void InvalidTextIsRejectedBeforeUtf8Replacement()
        {
            // Construct at runtime: custom-attribute UTF-8 serialization replaces
            // unpaired surrogate literals before NUnit receives TestCase values.
            foreach (char invalid in new[] {(char)0xd800, (char)0xdc00, (char)0})
                Assert.Throws<ArgumentException>(() => MNNTextDecision.ValidateText("bad" + invalid, 100));
        }

        [Test]
        public void ScoreUsesLevelIndicesAndStableSoftmax()
        {
            var logits = new float[64]; logits[0] = 10000; logits[1] = 10001; logits[2] = 10002;
            var input = new MNNTextDecision.Input {TokenCount = 512, StateTruncated = true};
            var result = MNNTextDecision.Decode(MNNDecisionQuestion.Score("priority", "low", "medium", "high"), input, logits, new[] {10000f, 9999f});
            Assert.AreEqual(2, result.BestIndex);
            Assert.That(result.Probabilities.Sum(), Is.EqualTo(1).Within(1e-12));
            Assert.That(result.ExpectedScore, Is.EqualTo(result.Probabilities[1] + 2 * result.Probabilities[2]).Within(1e-12));
            Assert.Greater(result.ActProbability, .7); Assert.Less(result.ActProbability, .8);
            Assert.IsTrue(result.StateTruncated); Assert.AreEqual(512, result.InputTokens);
            Assert.IsNull(result.TrueProbability); Assert.IsNull(result.Choice);
        }

        [Test]
        public void BooleanOrderAndUncertaintyAreExplicit()
        {
            var result = MNNTextDecision.Decode(MNNDecisionQuestion.Boolean("holds?"), new MNNTextDecision.Input(), new float[64], new[] {0f, 0f});
            CollectionAssert.AreEqual(new[] {"false", "true"}, result.Labels);
            Assert.AreEqual(.5, result.TrueProbability); Assert.AreEqual(.5, result.Confidence);
            Assert.AreEqual(.5, result.ActProbability); Assert.IsNull(result.ExpectedScore);
            var logits = new float[64]; logits[0] = float.NaN;
            Assert.Throws<InvalidDataException>(() => MNNTextDecision.Decode(MNNDecisionQuestion.Boolean("holds?"), new MNNTextDecision.Input(), logits, new[] {0f, 0f}));
        }

        [Test]
        public void ManyChoiceTemperatureMatchesClampedPublisherContract()
        {
            var question = MNNDecisionQuestion.Choice("pick", Enumerable.Range(0, 11).Select(i => new MNNDecisionOption(i.ToString())).ToArray());
            var result = MNNTextDecision.Decode(question, new MNNTextDecision.Input(), new float[64], new[] {0f, 0f});
            Assert.AreEqual(.5, result.Temperature);
            Assert.That(result.Confidence, Is.EqualTo(0).Within(1e-12));
        }
    }
}
