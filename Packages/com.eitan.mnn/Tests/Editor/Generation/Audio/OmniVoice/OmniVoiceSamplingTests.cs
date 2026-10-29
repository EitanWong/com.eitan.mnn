using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class OmniVoiceSamplingTests
    {
        [Test]
        public void GuidanceChangesTheChosenAudioCode()
        {
            var conditional = new float[1025];
            var unconditional = new float[1025];
            Array.Fill(conditional, -100);
            Array.Fill(unconditional, -100);
            conditional[0] = 3;
            conditional[1] = 2;
            unconditional[0] = 5;
            unconditional[1] = 0;
            Assert.AreEqual(1, MNNOmniVoiceSampling.Predict(conditional, unconditional, 2, out float confidence));
            Assert.That(confidence, Is.LessThan(0).And.GreaterThan(-0.1f));
        }

        [Test]
        public void MaskClassAffectsConfidenceButCannotBeSelected()
        {
            var conditional = new float[1025];
            var unconditional = new float[1025];
            Array.Fill(conditional, -100);
            Array.Fill(unconditional, -100);
            conditional[1] = 2;
            unconditional[1] = 2;
            conditional[1024] = 10;
            unconditional[1024] = 0;
            Assert.AreEqual(1, MNNOmniVoiceSampling.Predict(conditional, unconditional, 2, out float confidence));
            Assert.That(confidence, Is.LessThan(-20f));
        }

        [Test]
        public void DefaultScheduleFillsEveryCodeExactlyOnce()
        {
            int[] schedule = MNNOmniVoiceSampling.Schedule(8 * 50, 32);
            Assert.AreEqual(32, schedule.Length);
            Assert.AreEqual(8 * 50, System.Linq.Enumerable.Sum(schedule));
            Assert.That(schedule, Has.All.GreaterThanOrEqualTo(0));
        }
    }
}
