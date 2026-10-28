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
        [Test]
        public void AutoVoiceProducesAudibleWaveform()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "OmniVoice");
            if (!File.Exists(Path.Combine(root, "omnivoice_lm.mnn")))
                Assert.Ignore("OmniVoice MNN assets are not installed.");
            using (var model = MNNOmniVoice.Load(root, lmBackend: MNNBackendType.Metal, precision: MNNPrecisionMode.Low))
            {
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
            string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "OmniVoice");
            string referencePath = Path.Combine(root, "zero_shot_prompt.wav");
            if (!File.Exists(Path.Combine(root, "omnivoice_lm.mnn")) || !File.Exists(referencePath))
                Assert.Ignore("OmniVoice MNN assets are not installed.");
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
    }
}
