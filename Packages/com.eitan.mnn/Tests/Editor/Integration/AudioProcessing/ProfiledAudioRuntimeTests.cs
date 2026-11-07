using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    /// <summary>Opt-in checks of existing managed runners and installed weights. No downloads or ABI claims.</summary>
    public sealed class ProfiledAudioRuntimeTests
    {
        private static string DirectoryFor(string name)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_AUDIO_MODELS") ?? Environment.GetEnvironmentVariable("SIRIAI_ACOUSTIC_MODELS");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Set SIRIAI_ACOUSTIC_MODELS to reuse local deployment assets.");
            string directory = Path.Combine(root, name);
            Assert.That(Directory.Exists(directory), Is.True, directory);
            return directory;
        }
        private static float[] Audio(int length = 16000)
        {
            var audio = new float[length];
            for (int i = 0; i < length; ++i) audio[i] = .04f * (float)Math.Sin(i * .04);
            return audio;
        }
        [TestCase("MarbleNet-VAD-MNN")]
        [TestCase("FSMN-VAD-16K-MNN")]
        [TestCase("Silero-v5.1-MNN")]
        [TestCase("CueLight-Nano-MNN")]
        [TestCase("CueLight-Nano-SSM-MNN")]
        public void ProfiledVadLoadsInfersAndResetsStream(string name)
        {
            string directory = DirectoryFor(name);
            using (var detector = MNNVoiceActivityDetector.Load(directory, 2, MNNBackendType.CPU))
            {
                var result = detector.Detect(Audio(), 16000);
                Assert.That(result.Probabilities.Count, Is.GreaterThan(0));
                Assert.That(result.Probabilities.All(value => value >= 0 && value <= 1), Is.True);
            }
            using (var stream = MNNProfiledVoiceActivity.Load(directory, 2, MNNBackendType.CPU))
            {
                var first = stream.ProcessFrame(Audio(512), 0);
                stream.Reset();
                var repeated = stream.ProcessFrame(Audio(512), 0);
                Assert.That(repeated.StartSample, Is.Zero);
                Assert.That(repeated.SpeechProbability, Is.EqualTo(first.SpeechProbability).Within(.0001));
            }
        }
        [TestCase("SmartTurn-v3.2-FP32-MNN", false)]
        [TestCase("NAMO-Turn-Detector-v1-Multilingual-MNN", true)]
        [TestCase("TurnSense-SmolLM2-135M-MNN", true)]
        [TestCase("CueLight-Nano-MNN", false)]
        [TestCase("CueLight-Nano-SSM-MNN", false)]
        [TestCase("VoxTurn-v1-MNN", false)]
        public void ProfiledTurnUsesItsAudioOrTranscriptContract(string name, bool text)
        {
            using (var detector = MNNTurnDetector.Load(DirectoryFor(name), 2, MNNBackendType.CPU))
            {
                Assert.That(detector.RequiresTranscript, Is.EqualTo(text));
                bool result = detector.IsComplete(Audio(), 16000, "Thank you, that is all.");
                TestContext.WriteLine(name + " complete=" + result + "; backend=" + detector.Backend);
                if (text) Assert.That(detector.IsComplete(Audio(), 16000), Is.False);
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    Assert.Throws<OperationCanceledException>(() => detector.IsComplete(Audio(), 16000, "Hello", cancellation.Token));
                }
            }
        }
        [TestCase("GTCRN-MNN", 64000)]
        [TestCase("DeepFilterNet3-MNN", 64000)]
        [TestCase("DCCRN-Mayavoz-VCTK-16K-MNN", 64000)]
        [TestCase("DTLN-MNN", 16000)]
        [TestCase("FullSubNet-MNN", 16000)]
        [TestCase("FullSubNetPlus-MNN", 16000)]
        [TestCase("MossFormerGAN-SE-16K-MNN", 16000)]
        [TestCase("MossFormer2-SE-48K-MNN", 16000)]
        [TestCase("FRCRN-SE-16K-MNN", 16000)]
        [TestCase("DPTNet-Libri1Mix-16K-MNN", 16000)]
        [TestCase("DCCRNet-Libri1Mix-16K-MNN", 16000)]
        [TestCase("DCUNet-Libri1Mix-16K-MNN", 16000)]
        [TestCase("Resemble-Denoiser-44K-MNN", 16000)]
        [TestCase("Resemble-Enhance-44K-MNN", 16000)]
        public void ProfiledEnhancementPreservesCallerRateAndDuration(string name, int length)
        {
            using (var model = MNNNoiseSuppressor.Load(DirectoryFor(name), 2, MNNBackendType.CPU))
            {
                var enhanced = model.Enhance(Audio(length), 16000);
                Assert.That(enhanced.Length, Is.EqualTo(length));
                Assert.That(enhanced.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
            }
        }
        [TestCase("ConvTasNet-Libri2Mix-8K-MNN", 100000)]
        [TestCase("SepReformer-B-WSJ02Mix-8K-MNN", 100000)]
        [TestCase("MossFormer2-SS-16K-MNN", 16000)]
        [TestCase("SepFormer-WSJ02Mix-MNN", 16000)]
        [TestCase("DPRNN-WHAM-8K-MNN", 16000)]
        [TestCase("SudoRMRF-U16-WSJ02Mix-8K-MNN", 16000)]
        [TestCase("TIGER-Speech-16K-MNN", 16000)]
        public void ProfiledSeparationRestoresSixteenKhzForSpeakerSelection(string name, int length)
        {
            using (var model = MNNSpeechSeparator.Load(DirectoryFor(name), 2, MNNBackendType.CPU))
            {
                var result = model.Separate(Audio(length), 16000);
                Assert.That(result.SampleRate, Is.EqualTo(16000));
                Assert.That(result.Speaker0.Length, Is.EqualTo(length));
                Assert.That(result.Speaker1.Length, Is.EqualTo(length));
                Assert.That(result.Speaker0.Concat(result.Speaker1).All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
            }
        }
        [TestCase("TitaNet-MNN")]
        [TestCase("VBx-ResNet101-8K-MNN")]
        [TestCase("VBx-ResNet101-16K-MNN")]
        [TestCase("ECAPA-TDNN-MNN")]
        [TestCase("ERes2NetV2-MNN")]
        [TestCase("CAMPlus-MNN")]
        [TestCase("WeSpeaker-ResNet34-LM-MNN")]
        public void ProfiledSpeakerEncoderSupportsInstalledFrontends(string name)
        {
            using (var model = MNNSpeakerEncoder.Load(DirectoryFor(name), 2, MNNBackendType.CPU))
            {
                var embedding = model.Encode(Audio(), 16000);
                Assert.That(embedding.Length, Is.GreaterThan(0));
                Assert.That(embedding.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                Assert.That(embedding.Any(value => Math.Abs(value) > 0), Is.True);
            }
        }
    }
}
