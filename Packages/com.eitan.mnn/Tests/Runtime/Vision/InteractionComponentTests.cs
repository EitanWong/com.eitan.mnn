using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class InteractionComponentTests
    {
        private sealed class ReferenceModel : IMNNTargetSpeechExtractor
        {
            public bool Supports(MNNMultimodalSpeechInput input) => true;
            public float[] Extract(MNNMultimodalSpeechInput input, CancellationToken token) => input.CopyAudio();
        }

        private sealed class AlternateVisualModel : IMNNTargetSpeechExtractor
        {
            public bool Supports(MNNMultimodalSpeechInput input) => input.Visual.Width == 16 && input.Visual.FramesPerSecond == 30;
            public float[] Extract(MNNMultimodalSpeechInput input, CancellationToken token) => input.CopyAudio();
        }

        [Test]
        public void AlternateModelControlsItsOwnVisualGeometryWithoutChangingComponents()
        {
            var gate = new MNNPresenceController();
            gate.Observe(new MNNPresenceObservation(0, MNNPresenceKind.Face, "a", 1), 0);
            var present = gate.Observe(new MNNPresenceObservation(.4, MNNPresenceKind.Face, "a", 1), .4);
            var frames = new MNNVisualSpeechFrames(new float[3 * 16 * 16], 3, .5, "a", 16, 16, 30);
            var input = new MNNMultimodalSpeechInput(new float[1600], 16000, .5, "a", 2, frames);
            var processor = MNNMultimodalSpeechModels.CreateFromProviders(new AlternateVisualModel());
            Assert.That(processor.Process(input, present, .6).Route, Is.EqualTo(MNNMultimodalSpeechRoute.AudioVisualTarget));
        }

        [Test]
        public void EnabledTargetFeaturesRequireReadyProvidersAndIndependentSwitches()
        {
            var root = new GameObject("interaction-settings-test");
            try
            {
                var owner = root.AddComponent<MNNInteractionComponent>();
                owner.Configure(new MNNInteractionSettings { referenceSpeaker = true });
                Assert.Throws<InvalidOperationException>(() => owner.ConfigureSpeech(null, null, null, null,
                    (audio, token) => new MNNMultimodalSpeechInput(audio, 16000, 0, "a")));
                owner.ConfigureSpeech(null, new ReferenceModel(), null, null,
                    (audio, token) => new MNNMultimodalSpeechInput(audio, 16000, 0, "a"));
                owner.Configure(new MNNInteractionSettings());
                Assert.That(owner.CanStartInteraction, Is.True);
                Assert.That(owner.ReadSettings().referenceSpeaker, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void PresetsAndSettingsOwnTheirCopiesAndNeverInventQualityEvidence()
        {
            var settings = new MNNInteractionSettings { allowedIdentities = new[] { "a" } };
            var quality = new MNNRecognitionQuality("pipeline-revision", "corpus", "device", "zh", "CER", 1, 100, 40, true);
            var preset = new MNNRecognitionPreset(quality, new MNNAcousticSettings(), settings);
            settings.allowedIdentities[0] = "changed";
            Assert.That(preset.InteractionSettings.allowedIdentities[0], Is.EqualTo("a"));
            var catalog = new MNNRecognitionDefaults(new[] { preset });
            Assert.That(catalog.Select("corpus", "device", "zh", "CER", 100), Is.SameAs(preset));
            Assert.That(catalog.Select("other-corpus", "device", "zh", "CER", 100), Is.Null);
            Assert.Throws<ArgumentException>(() => new MNNInteractionSettings { faceRecognition = true }.Validate());
        }
    }
}
