using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class OmniVoiceCompactIntegrationTests
    {
        [TestCase(47, false)]
        [TestCase(13, true)]
        [TestCase(29, false)]
        public void CompactSamplingPreservesHighWaveform(int seed, bool compactFirst)
        {
            string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "OmniVoice-MNN");
            if (!File.Exists(Path.Combine(root, "omnivoice_lm_compact.mnn")))
                Assert.Ignore("Optional OmniVoice-MNN compact graph is not installed.");
            MNNOmniVoicePrompt voice = null;
            if (seed != 29)
                using (var model = MNNOmniVoice.Load(root))
                    voice = model.CreateVoicePrompt(MNNReferenceAudio.FromWaveFile(Path.Combine(root, "zero_shot_prompt.wav")),
                    "希望你以后过的比我还好哟！");
            string text = seed == 47 ? "你好，欢迎来到 OmniVoice。" : "今天天气很好，我们一起出去走走。";
            var first = Measure(root, voice, text, seed, compactFirst);
            var second = Measure(root, voice, text, seed, !compactFirst);
            var standard = compactFirst ? second : first;
            var compact = compactFirst ? first : second;
            Assert.That(compact.Item1.Waveform.Length, Is.EqualTo(standard.Item1.Waveform.Length));
            float difference = compact.Item1.Waveform.Zip(standard.Item1.Waveform, (a, b) => Math.Abs(a - b)).Max();
            Assert.That(difference, Is.LessThan(1e-5f));
            string artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TestArtifacts~", "OmniVoiceCompact"));
            Directory.CreateDirectory(artifacts);
            File.WriteAllBytes(Path.Combine(artifacts, "standard-" + seed + ".wav"), MNNWaveAudio.EncodePcm16(standard.Item1.Waveform, standard.Item1.SampleRate));
            File.WriteAllBytes(Path.Combine(artifacts, "compact-" + seed + ".wav"), MNNWaveAudio.EncodePcm16(compact.Item1.Waveform, compact.Item1.SampleRate));
            TestContext.WriteLine("OmniVoice compact seed=" + seed + " compactFirst=" + compactFirst +
                " standard=" + standard.Item2.ToString("F4") + "s compact=" + compact.Item2.ToString("F4") +
                "s standard RTF=" + (standard.Item2 * 24000 / standard.Item1.Waveform.Length).ToString("F4") +
                " compact RTF=" + (compact.Item2 * 24000 / compact.Item1.Waveform.Length).ToString("F4") +
                " max PCM difference=" + difference.ToString("G6"));
        }

        private static Tuple<MNNGeneratedAudio, double> Measure(string root, MNNOmniVoicePrompt voice, string text, int seed, bool compact)
        {
            using (var model = compact ? MNNOmniVoice.LoadCompact(root, lmBackend: MNNBackendType.Metal) :
                       MNNOmniVoice.Load(root, lmBackend: MNNBackendType.Metal))
            {
                Assert.That(model.UsesCompactSampling, Is.EqualTo(compact));
                model.Synthesize(text, 2f, voice, seed: seed);
                var timer = Stopwatch.StartNew();
                var audio = model.Synthesize(text, 2f, voice, seed: seed);
                timer.Stop();
                return Tuple.Create(audio, timer.Elapsed.TotalSeconds);
            }
        }
    }
}
