using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class OmniVoiceIntegrationTests
    {
        private const string ModelFolder = "OmniVoice-MNN";

        [TestCase(MNNPrecisionMode.High)]
        [TestCase(MNNPrecisionMode.Low)]
        public void AutoVoiceProducesAudibleWaveform(MNNPrecisionMode precision)
        {
            string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", ModelFolder);
            if (!File.Exists(Path.Combine(root, "omnivoice_lm.mnn")))
                Assert.Ignore("OmniVoice-MNN assets are not installed.");
            using (var model = MNNOmniVoice.Load(root, lmBackend: MNNBackendType.Metal, precision: precision))
            {
                Assert.That(model.UsesPackedCfg, Is.EqualTo(precision == MNNPrecisionMode.High));
                var audio = model.Synthesize("欢迎使用。", 1f, instruction: "female");
                Assert.That(audio.Waveform.Length, Is.InRange(24000, 48000));
                Assert.That(audio.Waveform.Max(x => Math.Abs(x)), Is.GreaterThan(0.01f).And.LessThanOrEqualTo(1f));
            }
        }

        [TestCase(MNNPrecisionMode.High, 32)]
        [TestCase(MNNPrecisionMode.Low, 32)]
        [TestCase(MNNPrecisionMode.Low, 16)]
        public void ReferenceCloneProducesAudibleWaveform(MNNPrecisionMode precision, int steps)
        {
            string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", ModelFolder);
            string referencePath = Path.Combine(root, "zero_shot_prompt.wav");
            if (!File.Exists(Path.Combine(root, "omnivoice_lm.mnn")) || !File.Exists(referencePath))
                Assert.Ignore("OmniVoice-MNN assets are not installed.");
            var timer = Stopwatch.StartNew();
            using (var model = MNNOmniVoice.Load(root, lmBackend: MNNBackendType.Metal, precision: precision))
            {
                var voice = model.CreateVoicePrompt(MNNReferenceAudio.FromWaveFile(referencePath), "希望你以后过的比我还好哟！");
                timer.Restart();
                model.Synthesize("你好，欢迎来到 OmniVoice。", 2f, voice, steps: steps);
                double coldSeconds = timer.Elapsed.TotalSeconds;
                timer.Restart();
                var audio = model.Synthesize("你好，欢迎来到 OmniVoice。", 2f, voice, steps: steps);
                timer.Stop();
                Assert.AreEqual(24000, audio.SampleRate);
                Assert.That(audio.Waveform.Length, Is.InRange(24000 * 2, 24000 * 3));
                Assert.That(audio.Waveform.Max(x => Math.Abs(x)), Is.GreaterThan(0.01f).And.LessThanOrEqualTo(1f));
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "unity-output-" + precision + "-s" + steps + ".wav"));
                File.WriteAllBytes(output, MNNWaveAudio.EncodePcm16(audio.Waveform, audio.SampleRate));
                TestContext.WriteLine("OmniVoice Unity " + precision + " steps=" + steps +
                    " cold=" + coldSeconds.ToString("F3") + "s warm=" + timer.Elapsed.TotalSeconds.ToString("F3") +
                    "s warm RTF=" + (timer.Elapsed.TotalSeconds * audio.SampleRate / audio.Waveform.Length).ToString("F3") + ", " + output);
                string asrRoot = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "hb-dev--Qwen3-ASR-0.6B-INT8-MNN");
                if (File.Exists(Path.Combine(asrRoot, "llm.mnn")))
                    using (var recognizer = MNNQwen3AsrRecognizer.Load(asrRoot))
                    {
                        string recognized = recognizer.Recognize(audio.Waveform, audio.SampleRate).Text;
                        TestContext.WriteLine("OmniVoice " + precision + " steps=" + steps + " ASR: " + recognized);
                        StringAssert.Contains("欢迎", recognized);
                    }
            }
        }

        [Test]
        public void PackedHighCfgMatchesLegacyWaveform()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", ModelFolder);
            if (!File.Exists(Path.Combine(root, "omnivoice_lm.mnn")) ||
                !File.Exists(Path.Combine(root, "zero_shot_prompt.wav")))
                Assert.Ignore("OmniVoice-MNN assets are not installed.");
            MNNGeneratedAudio original;
            MNNOmniVoicePrompt voice;
            double legacySeconds;
            using (var legacy = MNNOmniVoice.Load(root, lmBackend: MNNBackendType.Metal,
                       precision: MNNPrecisionMode.High, enablePackedCfg: false))
            {
                Assert.That(legacy.UsesPackedCfg, Is.False);
                voice = legacy.CreateVoicePrompt(MNNReferenceAudio.FromWaveFile(Path.Combine(root, "zero_shot_prompt.wav")),
                    "希望你以后过的比我还好哟！");
                legacy.Synthesize("你好，欢迎来到 OmniVoice。", 2f, voice);
                var timer = Stopwatch.StartNew();
                original = legacy.Synthesize("你好，欢迎来到 OmniVoice。", 2f, voice);
                legacySeconds = timer.Elapsed.TotalSeconds;
            }
            using (var packed = MNNOmniVoice.Load(root, lmBackend: MNNBackendType.Metal,
                       precision: MNNPrecisionMode.High))
            {
                Assert.That(packed.UsesPackedCfg, Is.True);
                var timer = Stopwatch.StartNew();
                packed.Synthesize("你好，欢迎来到 OmniVoice。", 2f, voice);
                timer.Restart();
                var actual = packed.Synthesize("你好，欢迎来到 OmniVoice。", 2f, voice);
                timer.Stop();
                Assert.That(actual.Waveform.Length, Is.EqualTo(original.Waveform.Length));
                float maxDifference = actual.Waveform.Zip(original.Waveform, (a, b) => Math.Abs(a - b)).Max();
                Assert.That(maxDifference, Is.LessThan(1e-5f));
                TestContext.WriteLine("OmniVoice High legacy warm=" + legacySeconds.ToString("F3") +
                    "s RTF=" + (legacySeconds * actual.SampleRate / actual.Waveform.Length).ToString("F3") +
                    ", packed warm=" + timer.Elapsed.TotalSeconds.ToString("F3") +
                    "s RTF=" + (timer.Elapsed.TotalSeconds * actual.SampleRate / actual.Waveform.Length).ToString("F3") +
                    " max PCM difference=" + maxDifference.ToString("G6"));
            }
        }

        [Test]
        public void PackedHighCfgKeepsSecondSeedTranscript()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", ModelFolder);
            string asrRoot = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "hb-dev--Qwen3-ASR-0.6B-INT8-MNN");
            if (!File.Exists(Path.Combine(root, "omnivoice_lm.mnn")) ||
                !File.Exists(Path.Combine(asrRoot, "llm.mnn")))
                Assert.Ignore("OmniVoice-MNN and Qwen3-ASR MNN assets are required.");
            const string text = "今天天气很好，我们一起出去走走。";
            MNNGeneratedAudio original, actual;
            MNNOmniVoicePrompt voice;
            using (var legacy = MNNOmniVoice.Load(root, lmBackend: MNNBackendType.Metal,
                       precision: MNNPrecisionMode.High, enablePackedCfg: false))
            {
                voice = legacy.CreateVoicePrompt(MNNReferenceAudio.FromWaveFile(Path.Combine(root, "zero_shot_prompt.wav")),
                    "希望你以后过的比我还好哟！");
                original = legacy.Synthesize(text, 2f, voice, seed: 13);
            }
            using (var packed = MNNOmniVoice.Load(root, lmBackend: MNNBackendType.Metal,
                       precision: MNNPrecisionMode.High))
                actual = packed.Synthesize(text, 2f, voice, seed: 13);
            using (var recognizer = MNNQwen3AsrRecognizer.Load(asrRoot))
            {
                string originalText = recognizer.Recognize(original.Waveform, original.SampleRate).Text;
                string packedText = recognizer.Recognize(actual.Waveform, actual.SampleRate).Text;
                TestContext.WriteLine("OmniVoice second seed ASR legacy: " + originalText + "; packed: " + packedText);
                Assert.That(packedText, Is.EqualTo(originalText).And.Not.Empty);
            }
        }
    }
}
