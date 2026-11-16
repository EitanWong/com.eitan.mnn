using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MultimodalSpeechProcessorTests
    {
        private static MNNPresenceSnapshot Present()
        {
            var gate = new MNNPresenceController();
            gate.Observe(new MNNPresenceObservation(0, MNNPresenceKind.Face, "a", 1), 0);
            return gate.Observe(new MNNPresenceObservation(.4, MNNPresenceKind.Face, "a", 1), .4);
        }

        private static MNNMultimodalSpeechInput Input(int speakers = 1, MNNVisualSpeechFrames visual = null, string track = "a") =>
            new MNNMultimodalSpeechInput(new float[1600], 16000, .5, track, speakers, visual);

        [Test]
        public void AlignedVisualTargetTakesPriorityOverAudioFallback()
        {
            int calls = 0;
            var processor = new MNNMultimodalSpeechProcessor(audioVisual: (input, token) => { ++calls; return input.CopyAudio(); });
            var frames = new MNNVisualSpeechFrames(new float[3 * 112 * 112], 3, .5, "a");
            var result = processor.Process(Input(2, frames), Present(), .6);
            Assert.That(result.Route, Is.EqualTo(MNNMultimodalSpeechRoute.AudioVisualTarget));
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void UnsynchronizedOrWrongTargetVideoNeverReachesVisualModel()
        {
            var processor = new MNNMultimodalSpeechProcessor(audioVisual: (input, token) => throw new Exception("Wrong visual route"));
            var old = new MNNVisualSpeechFrames(new float[3 * 112 * 112], 3, 0, "a");
            Assert.That(processor.Process(Input(2, old), Present(), .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.Rejected));
            var other = new MNNVisualSpeechFrames(new float[3 * 112 * 112], 3, .5, "b");
            Assert.That(processor.Process(Input(2, other), Present(), .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.Rejected));
        }

        [Test]
        public void AudioOnlyOverlapRequiresExplicitSpeakerSelection()
        {
            var processor = new MNNMultimodalSpeechProcessor(
                separate: (input, token) => new MNNSeparatedAudio(new float[1600], new float[1600], 16000),
                selectSpeaker: (audio, track) => -1);
            Assert.That(processor.Process(Input(2), Present(), .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.Rejected));
            processor = new MNNMultimodalSpeechProcessor(
                separate: (input, token) => { var target = new float[1600]; target[0] = .25f; return new MNNSeparatedAudio(new float[1600], target, 16000); },
                selectSpeaker: (audio, track) => 1);
            var result = processor.Process(Input(2), Present(), .6);
            Assert.That(result.Route, Is.EqualTo(MNNMultimodalSpeechRoute.SeparatedTarget));
            Assert.That(result.Audio[0], Is.EqualTo(.25f));
        }

        [Test]
        public void PresenceAndTrackAndFreshnessGateEveryRoute()
        {
            var processor = new MNNMultimodalSpeechProcessor();
            Assert.That(processor.Process(Input(), default, .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.Rejected));
            Assert.That(processor.Process(Input(track: "b"), Present(), .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.Rejected));
            Assert.That(processor.Process(Input(), Present(), 2).Route, Is.EqualTo(MNNMultimodalSpeechRoute.Rejected));
            Assert.That(processor.Process(Input(), Present(), .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.SingleSpeaker));
        }

        [Test]
        public void UnknownCrowdAndMoreThanTwoSpeakersAreNeverBlindlySeparated()
        {
            var processor = new MNNMultimodalSpeechProcessor(separate: (input, token) => throw new Exception("Unsupported source count"));
            Assert.That(processor.Process(Input(0), Present(), .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.Rejected));
            Assert.That(processor.Process(Input(3), Present(), .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.Rejected));
        }

        [Test]
        public void ReferenceTargetWorksWithoutVideoAndFaultsNeverLeakRawMixture()
        {
            var processor = new MNNMultimodalSpeechProcessor(referenceTarget: (input, token) => input.CopyAudio());
            Assert.That(processor.Process(Input(3), Present(), .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.ReferenceTarget));
            processor = new MNNMultimodalSpeechProcessor(referenceTarget: (input, token) => new float[1]);
            Assert.Throws<InvalidOperationException>(() => processor.Process(Input(), Present(), .6));
            processor = new MNNMultimodalSpeechProcessor(referenceTarget: (input, token) => throw new InvalidOperationException("model failed"));
            Assert.Throws<InvalidOperationException>(() => processor.Process(Input(), Present(), .6));
        }

        [Test]
        public void InputsAreOwnedBoundedAndValidated()
        {
            var samples = new float[1600]; samples[0] = .5f;
            var input = new MNNMultimodalSpeechInput(samples, 16000, .5, "a");
            samples[0] = 0;
            Assert.That(input.CopyAudio()[0], Is.EqualTo(.5f));
            Assert.Throws<ArgumentException>(() => new MNNMultimodalSpeechInput(new float[48001], 16000, 0, "a"));
            Assert.Throws<ArgumentException>(() => new MNNMultimodalSpeechInput(new[] { float.NaN }, 16000, 0, "a"));
            Assert.Throws<ArgumentException>(() => new MNNVisualSpeechFrames(new float[1], 1, 0, "a"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNMultimodalSpeechProcessor().Process(Input(), Present(), .55));
        }

        [Test]
        public void ContextExpiresAndCannotCrossTargetBoundary()
        {
            var context = new MNNVisionContext();
            var image = new MNNRgbImage(new byte[12], 2, 2);
            context.Update(image, .5, "a");
            Assert.That(context.TryGet("a", .6, out var found), Is.True);
            Assert.That(found, Is.SameAs(image));
            Assert.That(context.TryGet("b", .6, out _), Is.False);
            Assert.That(context.TryGet("a", 2, out _), Is.False);
            context.Clear();
            Assert.That(context.TryGet("a", 2, out _), Is.False);
        }

        [Test]
        public void AudioCapturedBeforeConfirmationIsRejected()
        {
            var input = new MNNMultimodalSpeechInput(new float[8000], 16000, 0, "a", 1);
            Assert.That(new MNNMultimodalSpeechProcessor().Process(input, Present(), .6).Route,
                Is.EqualTo(MNNMultimodalSpeechRoute.Rejected));
        }
    }
}
