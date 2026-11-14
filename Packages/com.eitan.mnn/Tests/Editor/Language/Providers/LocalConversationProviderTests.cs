using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class LocalConversationProviderTests
    {
        [Test]
        public void OmniPreparationAcceptsAudioInputWhenExternalTtsProvidesSpeech()
        {
            var settings = new MNNConversationOptions { speechOutput = true, endRealtimeOnExitIntent = false };
            var stages = MNNConversationStagePlan.ForRoute(settings, MNNRealtimeRoute.AudioTextToSpeech, allowBufferedSpeech: true);
            Assert.IsTrue(stages.Tts, "The selected route delegates waveform generation to its independent TTS provider.");
            var available = MNNModelCapabilities.AudioInput;
            var required = MNNConversationLocalProviders.OmniPreparationCapabilities;
            Assert.AreEqual(required, available & required,
                "Preparing an audio-to-text model must not require the independent synthesizer's native waveform capability.");
            Assert.AreNotEqual(required, (MNNModelCapabilities)0 & required, "A model without audio input must still be rejected.");
        }

        [Test]
        public void OmniDoesNotAdvertiseAnUnavailableWaveformOrContinuousNativeSession()
        {
            var capabilities = MNNConversationLocalProviders.DescribeCapabilities(MNNConversationModelAdapter.Omni);
            const MNNRealtimeFeature unavailable = MNNRealtimeFeature.BufferedSpeech | MNNRealtimeFeature.StreamingSpeech |
                MNNRealtimeFeature.ContinuousUnderstanding | MNNRealtimeFeature.BoundedScheduling |
                MNNRealtimeFeature.BoundedCancellation | MNNRealtimeFeature.SustainedRtfBelowOne;
            Assert.AreEqual(MNNRealtimeFeature.None, capabilities.Features & unavailable);
            Assert.AreNotEqual(MNNRealtimeFeature.None, capabilities.Features & MNNRealtimeFeature.MemoryAudioInput,
                "Turn-based file-backed audio input remains available through the managed PCM adapter.");
            var tts = MNNConversationLocalProviders.DescribeCapabilities(MNNConversationModelAdapter.Kokoro);
            Assert.AreNotEqual(MNNRealtimeFeature.None, tts.Features & MNNRealtimeFeature.BufferedSpeech,
                "Independent waveform synthesizers retain their implemented output capability.");
        }

        [Test]
        public void OmniSpeechSelectionRequiresAnIndependentImplementedSynthesizer()
        {
            if (!MNNPlatformSupport.IsSupported) Assert.Ignore("Route requires an implemented local inference ABI.");
            var model = MNNConversationLocalProviders.DescribeCapabilities(MNNConversationModelAdapter.Omni);
            var device = new MNNRealtimeCapabilities(MNNRealtimeFeature.DuplexDevice | MNNRealtimeFeature.PresentationClock |
                MNNRealtimeFeature.HalfDuplexDevice, "Test duplex device", "Managed route qualification only.", execution: MNNRealtimeExecution.Managed);
            var vad = MNNConversationStageRegistration.Energy.Capabilities;
            Assert.False(MNNRealtimeRouteSelector.SelectBest(model, device, vad: vad,
                requireMeasuredSpeech: false, allowBufferedSpeech: true).CanStart,
                "An Omni model name cannot substitute for an unavailable waveform callback ABI.");
            var tts = MNNConversationLocalProviders.DescribeCapabilities(MNNConversationModelAdapter.Kokoro);
            var selection = MNNRealtimeRouteSelector.SelectBest(model, device, tts, vad: vad,
                requireMeasuredSpeech: false, allowBufferedSpeech: true);
            Assert.AreEqual(MNNRealtimeRoute.AudioTextToSpeech, selection.RequireSelected().Route);
        }

        [TestCase(MNNConversationModelAdapter.Language, typeof(IMNNConversationTextProvider))]
        [TestCase(MNNConversationModelAdapter.Omni, typeof(IMNNConversationSpeechModelProvider))]
        [TestCase(MNNConversationModelAdapter.Paraformer, typeof(IMNNConversationStreamingAsrProvider))]
        [TestCase(MNNConversationModelAdapter.SpeechRecognition, typeof(IMNNConversationAsrProvider))]
        [TestCase(MNNConversationModelAdapter.Kokoro, typeof(IMNNConversationTtsProvider))]
        [TestCase(MNNConversationModelAdapter.SpeechSynthesis, typeof(IMNNConversationSpeechLanguages))]
        [TestCase(MNNConversationModelAdapter.Silero, typeof(IMNNRealtimeVoiceActivity))]
        [TestCase(MNNConversationModelAdapter.TextTurn, typeof(IMNNConversationTranscriptTurnProvider))]
        public void DescribingAndCreatingTasksDoesNotLoadNativeModels(MNNConversationModelAdapter task, Type contract)
        {
            using (var provider = MNNConversationLocalProviders.Create(task, "missing-model-directory"))
            {
                Assert.That(contract.IsInstanceOfType(provider), Is.True);
                Assert.That(((IMNNConversationProviderDiagnostics)provider).RuntimeBackend, Is.EqualTo("Not loaded"));
                Assert.That(provider.Capabilities.Features, Is.EqualTo(MNNConversationLocalProviders.DescribeCapabilities(task).Features));
            }
        }
        [Test]
        public void ClosedProviderRejectsPreparationBeforeLoadingNativeModels()
        {
            var provider = MNNConversationLocalProviders.Create(MNNConversationModelAdapter.Language, "missing-model-directory");
            provider.Dispose(); provider.Dispose();
            Assert.Throws<ObjectDisposedException>(() => provider.PrepareAsync(CancellationToken.None).GetAwaiter().GetResult());
        }
        [Test]
        public void InvalidConfigurationFailsAtThePublicBoundary()
        {
            Assert.Throws<ArgumentException>(() => MNNConversationLocalProviders.Create(MNNConversationModelAdapter.Language, " "));
            Assert.Throws<ArgumentException>(() => MNNConversationLocalProviders.Create(MNNConversationModelAdapter.Language, "model", new MNNConversationLocalModelOptions { Temperature = float.NaN }));
            Assert.Throws<ArgumentException>(() => MNNConversationLocalProviders.Create(MNNConversationModelAdapter.Language, "model", new MNNConversationLocalModelOptions { RecognitionThreads = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNConversationLocalProviders.Create((MNNConversationModelAdapter)1000, "model"));
        }
        [Test]
        public void MultimodalHistoryRetainsEveryAudioTurnAndSeparatesTheCurrentInput()
        {
            var oldAudio = new float[] { .1f }; var currentAudio = new float[] { .2f };
            var history = new[] { new MNNOpenAIMessage(oldAudio, 16000), new MNNOpenAIMessage(MNNChatRole.Assistant, "reply"), new MNNOpenAIMessage(currentAudio, 24000) };
            int saved = 0;
            var messages = MNNConversationLocalProviders.ConversationMessages(history, (audio, rate) =>
            {
                ++saved; Assert.That(rate, Is.EqualTo(saved == 1 ? 16000 : 24000));
                Assert.That(audio, Is.SameAs(saved == 1 ? oldAudio : currentAudio));
                return "wave-" + saved;
            }, out string latest);
            Assert.That(saved, Is.EqualTo(2));
            Assert.That(latest, Is.EqualTo("wave-2"));
            Assert.That(messages[0].Content, Is.EqualTo("<audio>wave-1</audio>\n请听取这条语音并简短回答。"));
            Assert.That(messages[1].Content, Is.EqualTo("reply"));
            Assert.That(messages[2].Content, Does.Not.Contain("<audio>"));
        }
        [TestCase("总金额206元，共4条记录。", true, "总金额二百零六元，共四条记录。")]
        [TestCase("总金额1,160.50元。", true, "总金额一千一百六十点五零元。")]
        [TestCase("共10条，编号006。", true, "共十条，编号零零六。")]
        [TestCase("余额-12.05元。", true, "余额负十二点零五元。")]
        [TestCase("完成率25%。", true, "完成率百分之二十五。")]
        [TestCase("已完成0项。", true, "已完成零项。")]
        [TestCase("总额10001元。", true, "总额一万零一元。")]
        [TestCase("总额99999999元。", true, "总额九千九百九十九万九千九百九十九元。")]
        [TestCase("编号123456789。", true, "编号一二三四五六七八九。")]
        [TestCase("金额２０６元。", true, "金额二百零六元。")]
        [TestCase("共四条记录。", true, "共四条记录。")]
        [TestCase("Total 206 across 4 records.", true, "Total 206 across 4 records.")]
        [TestCase(" ~ ~ ", false, "")]
        [TestCase("你好~world", true, "你好 world")]
        public void KokoroNormalizationPreservesSpokenContent(string input, bool pronounceable, string expected)
        {
            string cleaned = MNNConversationLocalProviders.PrepareKokoroText(input);
            Assert.That(cleaned, Is.EqualTo(expected));
            Assert.That(MNNConversationLocalProviders.HasPronounceableText(cleaned), Is.EqualTo(pronounceable));
        }
    }
}
