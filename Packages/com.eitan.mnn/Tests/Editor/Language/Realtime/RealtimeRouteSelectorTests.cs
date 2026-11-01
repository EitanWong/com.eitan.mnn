using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    /// <summary>Managed selection policy with synthetic evidence; no native/model/device qualification.</summary>
    public sealed class RealtimeRouteSelectorTests
    {
        private const MNNRealtimeFeature Binding = MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn;
        private const MNNRealtimeFeature Model = Binding | MNNRealtimeFeature.BoundedCancellation |
            MNNRealtimeFeature.ContextRecovery;
        private const MNNRealtimeFeature AudioText = Model | MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.StableSpokenText;
        private const MNNRealtimeFeature TextModel = Model | MNNRealtimeFeature.TextInput | MNNRealtimeFeature.StableSpokenText;
        private const MNNRealtimeFeature FullModel = AudioText | MNNRealtimeFeature.TextInput |
            MNNRealtimeFeature.StreamingSpeech | MNNRealtimeFeature.ContinuousUnderstanding | MNNRealtimeFeature.BoundedScheduling;
        private const MNNRealtimeFeature Tts = Binding | MNNRealtimeFeature.IndependentTts |
            MNNRealtimeFeature.StreamingSpeech | MNNRealtimeFeature.BoundedCancellation;
        private const MNNRealtimeFeature Asr = Binding | MNNRealtimeFeature.MemoryAudioInput |
            MNNRealtimeFeature.SpeechRecognition | MNNRealtimeFeature.BoundedCancellation;
        private const MNNRealtimeFeature Vad = Binding | MNNRealtimeFeature.MemoryAudioInput |
            MNNRealtimeFeature.VoiceActivityDetection | MNNRealtimeFeature.BoundedScheduling;
        private const MNNRealtimeFeature Device = MNNRealtimeFeature.DuplexDevice |
            MNNRealtimeFeature.PresentationClock | MNNRealtimeFeature.EchoControl;

        [TestCase(false, MNNRealtimeRoute.AudioTextToSpeech)]
        [TestCase(true, MNNRealtimeRoute.CascadedSpeech)]
        public void ExplicitTextRepliesSkipTtsEchoAndThroughputQualificationButRetainInputRequirements(bool cascade, MNNRealtimeRoute expected)
        {
            var model = Caps(Model | (cascade ? MNNRealtimeFeature.TextInput : MNNRealtimeFeature.MemoryAudioInput));
            var device = Caps(MNNRealtimeFeature.DuplexDevice);
            var selection = MNNRealtimeRouteSelector.SelectBest(model, device, asr: Caps(Asr), vad: Caps(Vad), speechOutput: false);
            var report = selection.RequireSelected();
            Assert.AreEqual(expected, report.Route);
            Assert.IsFalse(report.ProducesSpeech);
            Assert.IsFalse(report.RequiresMeasuredSpeech);
            Assert.IsNull(report.TtsContract);
            Assert.AreEqual(MNNRealtimeFeature.None, report.MissingTts);
            Assert.AreEqual(cascade, report.UsesSpeechRecognition);
            Assert.IsFalse(MNNRealtimeRouteSelector.SelectBest(model, device, asr: Caps(Asr), vad: Caps(Vad)).CanStart,
                "Disabling speech must be explicit; existing speech defaults stay strict.");
            Assert.IsFalse(MNNRealtimePreflight.Check(expected, model, device, asr: Caps(Asr), speechOutput: false).CanStart,
                "Voice input still requires a VAD implementation.");
            if (cascade)
                Assert.IsFalse(MNNRealtimePreflight.Check(expected, model, device, vad: Caps(Vad), speechOutput: false).CanStart,
                    "Disabling TTS must not skip speech recognition for a text model.");
        }

        [Test]
        public void TextReplySelectionCannotActivateNativeSpeechEvenWhenFullyQualified()
        {
            var selection = MNNRealtimeRouteSelector.SelectBest(Caps(FullModel, .6), Caps(Device), Caps(Tts, .7),
                Caps(Asr), Caps(Vad), speechOutput: false);
            Assert.AreEqual(MNNRealtimeRoute.AudioTextToSpeech, selection.RequireSelected().Route);
            Assert.IsFalse(selection.Selected.ProducesSpeech);
            Assert.AreEqual(1, selection.Attempts.Count);
            Assert.Throws<ArgumentException>(() => MNNRealtimePreflight.Check(MNNRealtimeRoute.NativeDuplexSpeech,
                Caps(FullModel), Caps(Device), speechOutput: false));
        }

        [Test]
        public void PrefersContinuousModelSpeechAndStopsEvaluatingLowerRoutes()
        {
            var selection = Select(Caps(FullModel, .6));
            Assert.AreEqual(MNNRealtimeRoute.NativeDuplexSpeech, selection.RequireSelected().Route);
            Assert.AreEqual(1, selection.Attempts.Count);
            Assert.False(selection.Selected.UsesSpeechRecognition);
            Assert.AreEqual(MNNRealtimeMode.NativeDuplex, selection.Selected.Mode);
            Assert.AreEqual(MNNSpeechRoute.OmniSpeech, selection.Selected.SpeechRoute);
            Assert.Null(selection.Selected.TtsContract, "Unused providers must not be attributed to a model-speech route.");
            Assert.Null(selection.Selected.AsrCapabilities);
            Assert.Null(selection.Selected.VadCapabilities);
        }

        [Test]
        public void ContinuousUnderstandingWithIndependentTtsRanksBeforeTurnBasedModelSpeech()
        {
            var selection = Select(Caps(FullModel & ~MNNRealtimeFeature.StreamingSpeech, .6));
            Assert.AreEqual(MNNRealtimeRoute.NativeDuplexTextToSpeech, selection.RequireSelected().Route);
            Assert.AreEqual(2, selection.Attempts.Count);
            Assert.AreEqual(MNNRealtimeFeature.StreamingSpeech, selection.Attempts[0].MissingModel);
            Assert.AreEqual(MNNRealtimeMode.NativeDuplex, selection.Selected.Mode);
        }

        [Test]
        public void TurnBasedAudioToSpeechRemainsEndToEndWithoutClaimingNativeDuplex()
        {
            var selection = Select(Caps(AudioText | MNNRealtimeFeature.StreamingSpeech, .8));
            Assert.AreEqual(MNNRealtimeRoute.EndToEndSpeech, selection.RequireSelected().Route);
            Assert.AreEqual(3, selection.Attempts.Count);
            Assert.AreEqual(MNNRealtimeMode.InterruptibleConversation, selection.Selected.Mode);
            Assert.That(selection.Attempts[0].MissingModel & MNNRealtimeFeature.ContinuousUnderstanding,
                Is.EqualTo(MNNRealtimeFeature.ContinuousUnderstanding));
        }

        [Test]
        public void AudioInputWithStableTextFallsBackToIndependentTts()
        {
            var selection = Select(Caps(AudioText));
            Assert.AreEqual(MNNRealtimeRoute.AudioTextToSpeech, selection.RequireSelected().Route);
            Assert.AreEqual(4, selection.Attempts.Count);
            Assert.AreEqual(MNNRealtimeMode.InterruptibleConversation, selection.Selected.Mode);
            Assert.AreEqual(MNNSpeechRoute.IndependentTts, selection.Selected.SpeechRoute);
            Assert.False(selection.Selected.UsesSpeechRecognition);
        }

        [TestCase(MNNRealtimeFeature.MemoryAudioInput)]
        [TestCase(MNNRealtimeFeature.VoiceActivityDetection)]
        [TestCase(MNNRealtimeFeature.BoundedScheduling)]
        public void AudioToTtsChecksVadNeededForTurnSegmentation(MNNRealtimeFeature missing)
        {
            var report = MNNRealtimePreflight.Check(MNNRealtimeRoute.AudioTextToSpeech, Caps(AudioText), Caps(Device),
                Caps(Tts, .7), vad: Caps(Vad & ~missing));

            Assert.False(report.CanStart);
            Assert.AreEqual(missing, report.MissingVad);
            Assert.NotNull(report.VadCapabilities);
        }

        [Test]
        public void AudioToTtsWithoutVadIsRejectedBeforeRuntimeCreation()
        {
            var selection = MNNRealtimeRouteSelector.SelectBest(Caps(AudioText), Caps(Device), Caps(Tts, .7));

            Assert.False(selection.CanStart);
            Assert.AreEqual(MNNRealtimeFeature.SupportedAbi | MNNRealtimeFeature.InProcessMnn |
                MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection |
                MNNRealtimeFeature.BoundedScheduling, selection.Attempts[3].MissingVad);
        }

        [Test]
        public void PlainTextModelUsesAsrAndVadInsteadOfRequiringModelAudioInput()
        {
            var model = Caps(TextModel);
            var asr = Caps(Asr);
            var vad = Caps(Vad);
            var selection = MNNRealtimeRouteSelector.SelectBest(model, Caps(Device), Caps(Tts, .7), asr, vad);
            var report = selection.RequireSelected();
            Assert.AreEqual(MNNRealtimeRoute.CascadedSpeech, report.Route);
            Assert.AreEqual(5, selection.Attempts.Count);
            Assert.True(report.UsesSpeechRecognition);
            Assert.AreEqual(MNNRealtimeFeature.None, report.MissingModel);
            Assert.AreEqual(MNNRealtimeMode.InterruptibleConversation, report.Mode);
            Assert.AreSame(asr, report.AsrCapabilities);
            Assert.AreSame(vad, report.VadCapabilities);
            Assert.AreEqual(model.Contract, report.ModelContract);
            Assert.AreEqual(model.Evidence, report.ModelEvidence);
        }

        [TestCase(MNNRealtimeFeature.ContinuousUnderstanding)]
        [TestCase(MNNRealtimeFeature.BoundedScheduling)]
        public void BothContinuousInputAndBoundedSchedulingAreRequiredForNativeDuplex(MNNRealtimeFeature missing)
        {
            var selection = Select(Caps(FullModel & ~missing, .7));
            Assert.AreEqual(MNNRealtimeRoute.EndToEndSpeech, selection.RequireSelected().Route);
            Assert.AreEqual(missing, selection.Attempts[0].MissingModel);
            Assert.AreEqual(missing, selection.Attempts[1].MissingModel);
        }

        [Test]
        public void SlowModelSpeechCanFallBackToSeparatelyMeasuredTts()
        {
            var selection = Select(Caps(FullModel, 1.0));
            Assert.AreEqual(MNNRealtimeRoute.NativeDuplexTextToSpeech, selection.RequireSelected().Route);
            Assert.AreEqual(MNNRealtimeFeature.SustainedRtfBelowOne, selection.Attempts[0].MissingModel);
        }

        [TestCase(1.0, 2.0)]
        [TestCase(1.1, 2.0)]
        [TestCase(.9, .99)]
        public void FallbackNeverPromotesUnqualifiedTtsThroughput(double rtf, double seconds)
        {
            var selection = MNNRealtimeRouteSelector.SelectBest(Caps(TextModel), Caps(Device),
                Caps(Tts, rtf, seconds), Caps(Asr), Caps(Vad));
            Assert.False(selection.CanStart);
            Assert.AreEqual(MNNRealtimeFeature.SustainedRtfBelowOne, selection.Attempts[4].MissingTts);
        }

        [Test]
        public void BatchTtsCannotClaimIncrementalPcmByEmittingSlicesAfterSynthesis()
        {
            var selection = MNNRealtimeRouteSelector.SelectBest(Caps(AudioText | MNNRealtimeFeature.TextInput),
                Caps(Device), Caps(Tts & ~MNNRealtimeFeature.StreamingSpeech, .5), Caps(Asr), Caps(Vad));
            Assert.False(selection.CanStart);
            Assert.AreEqual(MNNRealtimeFeature.StreamingSpeech, selection.Attempts[3].MissingTts);
            Assert.AreEqual(MNNRealtimeFeature.StreamingSpeech, selection.Attempts[4].MissingTts);
        }

        [TestCase(MNNRealtimeFeature.SupportedAbi)]
        [TestCase(MNNRealtimeFeature.InProcessMnn)]
        [TestCase(MNNRealtimeFeature.BoundedCancellation)]
        [TestCase(MNNRealtimeFeature.ContextRecovery)]
        public void SharedModelFailureCannotBeBypassedByAnyTopology(MNNRealtimeFeature missing)
        {
            var selection = Select(Caps(FullModel & ~missing, .6));
            Assert.False(selection.CanStart);
            Assert.AreEqual(5, selection.Attempts.Count);
            foreach (var attempt in selection.Attempts)
                Assert.AreEqual(missing, attempt.MissingModel);
            Assert.Throws<NotSupportedException>(() => selection.RequireSelected());
        }

        [TestCase(MNNRealtimeFeature.DuplexDevice)]
        [TestCase(MNNRealtimeFeature.PresentationClock)]
        [TestCase(MNNRealtimeFeature.EchoControl)]
        public void SharedDeviceFailureCannotBeBypassedByFallback(MNNRealtimeFeature missing)
        {
            var selection = MNNRealtimeRouteSelector.SelectBest(Caps(FullModel, .6), Caps(Device & ~missing),
                Caps(Tts, .7), Caps(Asr), Caps(Vad));
            Assert.False(selection.CanStart);
            foreach (var attempt in selection.Attempts)
                Assert.AreEqual(missing, attempt.MissingDevice);
        }

        [Test]
        public void ExplicitHeadphonesPermitInterruptibleFallbackButNeverNativeDuplex()
        {
            var headphones = Caps((Device & ~MNNRealtimeFeature.EchoControl) | MNNRealtimeFeature.HeadphoneRoute);
            var selection = MNNRealtimeRouteSelector.SelectBest(Caps(FullModel, .6), headphones, Caps(Tts, .7));
            Assert.AreEqual(MNNRealtimeRoute.EndToEndSpeech, selection.RequireSelected().Route);
            Assert.AreEqual(MNNRealtimeFeature.EchoControl, selection.Attempts[0].MissingDevice);
            Assert.AreEqual(MNNRealtimeFeature.EchoControl, selection.Attempts[1].MissingDevice);
        }

        [TestCase(MNNRealtimeFeature.SupportedAbi)]
        [TestCase(MNNRealtimeFeature.MemoryAudioInput)]
        [TestCase(MNNRealtimeFeature.SpeechRecognition)]
        [TestCase(MNNRealtimeFeature.BoundedCancellation)]
        public void CascadeChecksActualRecognizerCapabilities(MNNRealtimeFeature missing)
        {
            var report = MNNRealtimePreflight.Check(MNNRealtimeRoute.CascadedSpeech, Caps(TextModel), Caps(Device),
                Caps(Tts, .7), Caps(Asr & ~missing), Caps(Vad));
            Assert.False(report.CanStart);
            Assert.AreEqual(missing, report.MissingAsr);
            Assert.AreEqual(MNNRealtimeFeature.None, report.MissingModel);
            Assert.AreEqual(MNNRealtimeFeature.None, report.MissingVad);
        }

        [TestCase(MNNRealtimeFeature.SupportedAbi)]
        [TestCase(MNNRealtimeFeature.VoiceActivityDetection)]
        [TestCase(MNNRealtimeFeature.BoundedScheduling)]
        public void CascadeChecksActualVadCapabilities(MNNRealtimeFeature missing)
        {
            var report = MNNRealtimePreflight.Check(MNNRealtimeRoute.CascadedSpeech, Caps(TextModel), Caps(Device),
                Caps(Tts, .7), Caps(Asr), Caps(Vad & ~missing));
            Assert.False(report.CanStart);
            Assert.AreEqual(missing, report.MissingVad);
        }

        [Test]
        public void MissingRecognizerAndVadAreExplicitFailuresNotNullReferenceErrors()
        {
            var selection = MNNRealtimeRouteSelector.SelectBest(Caps(TextModel), Caps(Device), Caps(Tts, .7));
            Assert.False(selection.CanStart);
            Assert.AreEqual(Asr, selection.Attempts[4].MissingAsr);
            Assert.AreEqual(Vad, selection.Attempts[4].MissingVad);
            Assert.Null(selection.Attempts[4].AsrCapabilities);
            Assert.Null(selection.Attempts[4].VadCapabilities);
            StringAssert.Contains("ASR:", selection.ToString());
            StringAssert.Contains("VAD:", selection.ToString());
        }

        [Test]
        public void AudioOnlyModelCannotBeSilentlyReplacedWithATextModel()
        {
            var selection = MNNRealtimeRouteSelector.SelectBest(Caps(Model | MNNRealtimeFeature.MemoryAudioInput),
                Caps(Device), Caps(Tts, .7), Caps(Asr), Caps(Vad));
            Assert.False(selection.CanStart);
            Assert.AreEqual(MNNRealtimeFeature.TextInput | MNNRealtimeFeature.StableSpokenText,
                selection.Attempts[4].MissingModel);
        }

        [Test]
        public void FailedRoutesCanBeExcludedUntilTheFiniteFallbackChainIsExhausted()
        {
            var excluded = new List<MNNRealtimeRoute>();
            foreach (MNNRealtimeRoute expected in Enum.GetValues(typeof(MNNRealtimeRoute)))
            {
                var selection = Select(Caps(FullModel, .6), excluded);
                Assert.AreEqual(expected, selection.RequireSelected().Route);
                Assert.AreEqual(1, selection.Attempts.Count);
                excluded.Add(expected);
            }
            var exhausted = Select(Caps(FullModel, .6), excluded);
            Assert.False(exhausted.CanStart);
            Assert.AreEqual(0, exhausted.Attempts.Count);
            Assert.AreEqual(5, exhausted.ExcludedRoutes.Count);
            StringAssert.Contains("Excluded:", exhausted.ToString());
            Assert.Throws<NotSupportedException>(() => exhausted.RequireSelected());
        }

        [Test]
        public void SelectionSnapshotsExclusionsAndReportsCannotBeMutated()
        {
            var excluded = new List<MNNRealtimeRoute>
            {
                MNNRealtimeRoute.NativeDuplexSpeech, MNNRealtimeRoute.NativeDuplexSpeech
            };
            var selection = Select(Caps(FullModel, .6), excluded);
            excluded.Clear();
            Assert.AreEqual(MNNRealtimeRoute.NativeDuplexTextToSpeech, selection.Selected.Route);
            Assert.AreEqual(1, selection.ExcludedRoutes.Count);
            Assert.Throws<NotSupportedException>(() => ((IList<MNNRealtimeReport>)selection.Attempts).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<MNNRealtimeRoute>)selection.ExcludedRoutes).Clear());
            Assert.AreEqual(MNNRealtimeRoute.NativeDuplexSpeech, Select(Caps(FullModel, .6)).Selected.Route);
        }

        [Test]
        public void UnsupportedResultRetainsAllFiveRouteReasonsAndProviderLimitations()
        {
            var none = new MNNRealtimeCapabilities(MNNRealtimeFeature.None, "unavailable/v1", null, "ABI not established");
            var selection = MNNRealtimeRouteSelector.SelectBest(none, Caps(Device));
            Assert.False(selection.CanStart);
            Assert.AreEqual(5, selection.Attempts.Count);
            var error = Assert.Throws<NotSupportedException>(() => selection.RequireSelected());
            foreach (MNNRealtimeRoute route in Enum.GetValues(typeof(MNNRealtimeRoute)))
                StringAssert.Contains(route.ToString(), error.Message);
            StringAssert.Contains("ABI not established", error.Message);
        }

        [TestCase(MNNRealtimeMode.NativeDuplex, MNNSpeechRoute.OmniSpeech, MNNRealtimeRoute.NativeDuplexSpeech)]
        [TestCase(MNNRealtimeMode.NativeDuplex, MNNSpeechRoute.IndependentTts, MNNRealtimeRoute.NativeDuplexTextToSpeech)]
        [TestCase(MNNRealtimeMode.InterruptibleConversation, MNNSpeechRoute.OmniSpeech, MNNRealtimeRoute.EndToEndSpeech)]
        [TestCase(MNNRealtimeMode.InterruptibleConversation, MNNSpeechRoute.IndependentTts, MNNRealtimeRoute.AudioTextToSpeech)]
        public void LegacyPreflightPreservesExistingModeAndOutputSemantics(MNNRealtimeMode mode,
            MNNSpeechRoute speech, MNNRealtimeRoute expected)
        {
            var report = MNNRealtimePreflight.Check(mode, speech, Caps(FullModel, .6), Caps(Device), Caps(Tts, .7),
                vad: Caps(Vad));
            Assert.True(report.CanStart);
            Assert.AreEqual(expected, report.Route);
            Assert.AreEqual(mode, report.Mode);
            Assert.AreEqual(speech, report.SpeechRoute);
            Assert.AreEqual(MNNRealtimeFeature.None, report.MissingAsr | report.MissingVad);
        }

        [Test]
        public void InvalidInputsFailBeforeSelectingEvenWhenEveryRouteIsExcluded()
        {
            var model = Caps(FullModel, .6);
            var device = Caps(Device);
            Assert.Throws<ArgumentNullException>(() => MNNRealtimeRouteSelector.SelectBest(null, device));
            Assert.Throws<ArgumentNullException>(() => MNNRealtimeRouteSelector.SelectBest(model, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Select(model, new[] { (MNNRealtimeRoute)(-1) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Select(model, new[] { (MNNRealtimeRoute)32 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNRealtimePreflight.Check((MNNRealtimeRoute)5, model, device));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNRealtimePreflight.Check((MNNRealtimeMode)2,
                MNNSpeechRoute.OmniSpeech, model, device));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNRealtimePreflight.Check(MNNRealtimeMode.NativeDuplex,
                (MNNSpeechRoute)2, model, device));
            Assert.Throws<ArgumentNullException>(() => MNNRealtimeRouteSelector.SelectBest(null, device,
                excludedRoutes: (MNNRealtimeRoute[])Enum.GetValues(typeof(MNNRealtimeRoute))));
        }

        [Test]
        public void ConcurrentIndependentSelectionsDoNotShareFailureState()
        {
            var model = Caps(FullModel, .6);
            var device = Caps(Device);
            var tts = Caps(Tts, .7);
            Parallel.For(0, 64, index =>
            {
                bool exclude = index % 2 == 0;
                var selection = MNNRealtimeRouteSelector.SelectBest(model, device, tts,
                    excludedRoutes: exclude ? new[] { MNNRealtimeRoute.NativeDuplexSpeech } : null);
                Assert.AreEqual(exclude ? MNNRealtimeRoute.NativeDuplexTextToSpeech : MNNRealtimeRoute.NativeDuplexSpeech,
                    selection.RequireSelected().Route);
            });
        }

        [Test]
        public void BufferedWaveformsRequireExplicitOptInAndHalfDuplexDevice()
        {
            var model = Caps(Binding | MNNRealtimeFeature.ContextRecovery | MNNRealtimeFeature.MemoryAudioInput |
                MNNRealtimeFeature.BufferedSpeech);
            var vad = Caps(Binding | MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection |
                MNNRealtimeFeature.BoundedScheduling);
            var device = Caps(MNNRealtimeFeature.DuplexDevice | MNNRealtimeFeature.PresentationClock |
                MNNRealtimeFeature.HalfDuplexDevice);

            Assert.False(MNNRealtimePreflight.Check(MNNRealtimeRoute.EndToEndSpeech, model, device,
                vad: vad, requireMeasuredSpeech: false).CanStart);
            Assert.True(MNNRealtimePreflight.Check(MNNRealtimeRoute.EndToEndSpeech, model, device,
                vad: vad, requireMeasuredSpeech: false, allowBufferedSpeech: true).CanStart);
            Assert.False(MNNRealtimePreflight.Check(MNNRealtimeRoute.EndToEndSpeech, model, device,
                vad: vad, requireMeasuredSpeech: true, allowBufferedSpeech: true).CanStart);

            var noHalfDuplex = Caps(MNNRealtimeFeature.DuplexDevice | MNNRealtimeFeature.PresentationClock);
            Assert.False(MNNRealtimePreflight.Check(MNNRealtimeRoute.EndToEndSpeech, model, noHalfDuplex,
                vad: vad, requireMeasuredSpeech: false, allowBufferedSpeech: true).CanStart);
            Assert.False(MNNRealtimePreflight.Check(MNNRealtimeRoute.NativeDuplexSpeech, model, device,
                vad: vad, requireMeasuredSpeech: false, allowBufferedSpeech: true).CanStart);
        }

        private static MNNRealtimeCapabilities Caps(MNNRealtimeFeature features, double? rtf = null, double seconds = 2)
        {
            return new MNNRealtimeCapabilities(features, "synthetic-backend/v1", "managed-policy-test-only",
                sustainedRtf: rtf, sustainedRtfWindowAudioSeconds: rtf.HasValue ? seconds : 0);
        }

        private static MNNRealtimeRouteSelection Select(MNNRealtimeCapabilities model,
            IReadOnlyCollection<MNNRealtimeRoute> excluded = null)
        {
            return MNNRealtimeRouteSelector.SelectBest(model, Caps(Device), Caps(Tts, .7), Caps(Asr), Caps(Vad), excluded);
        }
    }
}
