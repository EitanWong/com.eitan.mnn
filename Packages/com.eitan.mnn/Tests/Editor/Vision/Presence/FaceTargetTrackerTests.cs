using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class FaceTargetTrackerTests
    {
        private static MNNDetection Box(float x, float size = 100) => new MNNDetection(0, "face", .9f, x, 0, size, size);

        [Test]
        public void LargerNewcomerCannotStealAnExistingTarget()
        {
            var tracker = new MNNFaceTargetTracker();
            Assert.That(tracker.Select(new[] { Box(0) }, 0), Is.EqualTo(0));
            string track = tracker.TrackId;
            Assert.That(tracker.Select(new[] { Box(300, 200), Box(2) }, .1), Is.EqualTo(1));
            Assert.That(tracker.TrackId, Is.EqualTo(track));
            tracker.Select(new[] { Box(300, 200) }, .2);
            Assert.That(tracker.TrackId, Is.Not.EqualTo(track));
        }

        [Test]
        public void SimilarCrossingFacesInvalidateContinuityAndMissingFramesExpire()
        {
            var tracker = new MNNFaceTargetTracker();
            tracker.Select(new[] { Box(0) }, 0);
            string track = tracker.TrackId;
            Assert.That(tracker.Select(new[] { Box(1), Box(2) }, .1), Is.EqualTo(-1));
            Assert.That(tracker.TrackId, Is.Null);
            tracker.Select(new[] { Box(0) }, .2);
            Assert.That(tracker.TrackId, Is.Not.EqualTo(track));
            track = tracker.TrackId;
            Assert.That(tracker.Select(new MNNDetection[0], .3), Is.EqualTo(-1));
            tracker.Select(new[] { Box(0) }, 2);
            Assert.That(tracker.TrackId, Is.Not.EqualTo(track));
        }
    }
}
