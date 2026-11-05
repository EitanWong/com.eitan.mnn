using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class SharedMotionTests
    {
        [TestCase(.8f)]
        [TestCase(1f)]
        public void VectorSpringPreservesTrajectoryWhenTheFrameIsSubdivided(float damping)
        {
            var position = new Vector3(.2f, 2, -1);
            var velocity = new Vector3(1.5f, -1, .5f);
            var reference = position; var referenceVelocity = velocity;
            var target = new Vector3(.65f, 0, 1);
            MNNMotion.Spring(ref reference, ref referenceVelocity, target, 18, damping, .5f);
            for (int i = 0; i < 60; ++i)
                MNNMotion.Spring(ref position, ref velocity, target, 18, damping, 1f / 120);
            Assert.Less((position - reference).magnitude, .00001f);
            Assert.Less((velocity - referenceVelocity).magnitude, .00001f);
        }
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void SpringConvergesAndHasSameTrajectoryAcrossRefreshRates(int fps)
        {
            float position = .2f, velocity = 1.5f;
            float reference = position, referenceVelocity = velocity;
            MNNMotion.Spring(ref reference, ref referenceVelocity, .65f, .5f);
            for (int i = 0; i < fps / 2; ++i)
                MNNMotion.Spring(ref position, ref velocity, .65f, 1f / fps);
            Assert.AreEqual(reference, position, .00001f);
            Assert.AreEqual(referenceVelocity, velocity, .00001f);
            for (int i = 0; i < fps * 2; ++i)
                MNNMotion.Spring(ref position, ref velocity, .65f, 1f / fps);
            Assert.AreEqual(.65f, position, .00001f);
            Assert.Less(Mathf.Abs(velocity), .00001f);
        }

        [Test]
        public void RubberBandResistsMoreAsDistanceIncreasesAndStaysBounded()
        {
            float first = MNNMotion.RubberBand(20);
            float second = MNNMotion.RubberBand(40);
            float third = MNNMotion.RubberBand(60);
            Assert.Greater(first, second - first);
            Assert.Greater(second - first, third - second);
            Assert.Less(MNNMotion.RubberBand(100000f), 28f);
            Assert.AreEqual(-first, MNNMotion.RubberBand(-20));
        }

    }
}
