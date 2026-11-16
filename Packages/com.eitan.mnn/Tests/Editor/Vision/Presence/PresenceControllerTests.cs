using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class PresenceControllerTests
    {
        private static MNNPresenceObservation Face(double time, string track = "a", string identity = null) =>
            new MNNPresenceObservation(time, MNNPresenceKind.Face, track, .9f, identity);

        [Test]
        public void BriefOcclusionKeepsPresentationButNeverAdmitsNewSpeech()
        {
            var gate = new MNNPresenceController();
            Assert.That(gate.Observe(Face(0), 0).CanStartInteraction, Is.False);
            Assert.That(gate.Current.PresentationTarget, Is.EqualTo(0));
            Assert.That(gate.Observe(Face(.4), .4).CanStartInteraction, Is.True);
            var grace = gate.Observe(MNNPresenceObservation.Absent(.5), .5);
            Assert.That(grace.State, Is.EqualTo(MNNPresenceState.Grace));
            Assert.That(grace.PresentationTarget, Is.EqualTo(1));
            Assert.That(grace.CanStartInteraction, Is.False);
            Assert.That(gate.Observe(Face(1), 1).CanStartInteraction, Is.True);
        }

        [Test]
        public void DisconnectedCameraExpiresWithoutAnotherObservation()
        {
            var gate = new MNNPresenceController();
            gate.Observe(Face(0), 0);
            gate.Observe(Face(.4), .4);
            Assert.That(gate.Tick(1.2).CanStartInteraction, Is.False);
            Assert.That(gate.Tick(4).State, Is.EqualTo(MNNPresenceState.Standby));
            Assert.That(gate.Tick(16).State, Is.EqualTo(MNNPresenceState.Sleeping));
        }

        [Test]
        public void StaleFramesCannotWakeOrRefreshTheGate()
        {
            var gate = new MNNPresenceController();
            gate.Observe(Face(0), 2);
            Assert.That(gate.Tick(2).State, Is.EqualTo(MNNPresenceState.Sleeping));
            gate.Observe(Face(2), 2);
            Assert.That(gate.Observe(Face(2), 2.4).CanStartInteraction, Is.False);
            Assert.That(gate.Observe(Face(2.4), 2.4).CanStartInteraction, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(() => gate.Tick(2));
        }

        [Test]
        public void TrackChangesRequireFreshConfirmationAndInvalidateOldTurn()
        {
            var gate = new MNNPresenceController();
            gate.Observe(Face(0), 0);
            gate.Observe(Face(.4), .4);
            var changed = gate.Observe(Face(.5, "b"), .5, true);
            Assert.That(changed.CanStartInteraction, Is.False);
            Assert.That(changed.KeepCurrentInteraction, Is.False);
            Assert.That(gate.Observe(Face(.9, "b"), .9).TrackId, Is.EqualTo("b"));
        }

        [Test]
        public void IdentityAllowListAndFaceRequirementAreExplicit()
        {
            var gate = new MNNPresenceController(allowedIdentities: new[] { "operator" });
            gate.Observe(Face(0, identity: "visitor"), 0);
            Assert.That(gate.Observe(Face(.4, identity: "visitor"), .4).CanStartInteraction, Is.False);
            gate.Observe(Face(.5, identity: "operator"), .5);
            Assert.That(gate.Observe(Face(.9, identity: "operator"), .9).CanStartInteraction, Is.True);
            Assert.That(gate.Observe(new MNNPresenceObservation(1, MNNPresenceKind.Person, "a", .9f, "operator"), 1).CanStartInteraction, Is.False);
        }

        [Test]
        public void BusyTurnGetsBoundedHoldAndCannotPreventSleepForever()
        {
            var gate = new MNNPresenceController();
            gate.Observe(Face(0), 0);
            gate.Observe(Face(.4), .4);
            Assert.That(gate.Tick(4, true).KeepCurrentInteraction, Is.True);
            Assert.That(gate.Tick(31, true).State, Is.EqualTo(MNNPresenceState.Sleeping));
            Assert.That(gate.Tick(32, true).KeepCurrentInteraction, Is.False);
        }

        [Test]
        public void InvalidInputIsRejectedAndOptionsAreValidated()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNPresenceObservation(double.NaN, MNNPresenceKind.Face, "a", .9f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNPresenceOptions(sleepSeconds: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNPresenceOptions(minimumConfidence: float.NaN));
            var gate = new MNNPresenceController();
            Assert.Throws<ArgumentOutOfRangeException>(() => gate.Observe(Face(1), 0));
            Assert.That(gate.Tick(0).State, Is.EqualTo(MNNPresenceState.Sleeping));
        }

        [Test]
        public void FaceGalleryCopiesEnrollmentAndRejectsAmbiguousMatches()
        {
            var gallery = new MNNFaceGallery(.7f, .1f);
            var vector = new float[512]; vector[0] = 1;
            gallery.Enroll("operator", vector);
            vector[0] = 0; vector[1] = 1;
            var query = new float[512]; query[0] = 1;
            Assert.That(gallery.Recognize(query), Is.EqualTo("operator"));
            gallery.Enroll("duplicate", query);
            Assert.That(gallery.Recognize(query), Is.Null);
            Assert.That(gallery.Remove("duplicate"), Is.True);
            Assert.That(gallery.Recognize(query), Is.EqualTo("operator"));
            Assert.Throws<ArgumentException>(() => gallery.Enroll("zero", new float[512]));
        }

        [Test]
        public void RejectedDifferentIdentityCannotKeepAnOldConversationAlive()
        {
            var gate = new MNNPresenceController(allowedIdentities: new[] { "operator" });
            gate.Observe(Face(0, identity: "operator"), 0);
            gate.Observe(Face(.4, identity: "operator"), .4);
            Assert.That(gate.Observe(Face(.5, "b", "visitor"), .5, true).KeepCurrentInteraction, Is.False);
        }

        [Test]
        public void LateBusyFlagCannotReviveAnAlreadySleepingTarget()
        {
            var gate = new MNNPresenceController();
            gate.Observe(Face(0), 0);
            gate.Observe(Face(.4), .4);
            Assert.That(gate.Tick(16).State, Is.EqualTo(MNNPresenceState.Sleeping));
            Assert.That(gate.Tick(17, true).State, Is.EqualTo(MNNPresenceState.Sleeping));
        }
    }
}
