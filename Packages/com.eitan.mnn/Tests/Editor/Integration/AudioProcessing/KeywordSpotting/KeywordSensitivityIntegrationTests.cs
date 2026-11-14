using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [TestFixture, Category("Integration"), Category("KWS")]
    public sealed class KeywordSensitivityIntegrationTests
    {
        // Corpus quality gates, not an assertion of perfect recognition for every voice.
        // All misses/confusions are exported, including the model's difficult Siri examples.
        [TestCase(false, .95)]
        [TestCase(true, .95)]
        public void StreamingCorpusMeetsRecallConfusionAndRealtimeBudgets(bool chinese, double minimumRecall)
        {
            string directory = Environment.GetEnvironmentVariable(chinese ? "MNN_TEST_KWS_MODELS" : "MNN_ENGLISH_KWS_MODELS");
            string recordings = Environment.GetEnvironmentVariable("MNN_TEST_WAKE_SENSITIVITY_AUDIO");
            if (directory == null || recordings == null) Assert.Ignore("Provide installed KWS models and local sensitivity fixtures.");
            string keywords = chinese ? "小爱同学\n你好助手\n嘿西瑞" : "Siri\nHi Siri\nHey Siri";
            string[] voices = chinese ? new[] { "Tingting", "Eddy" } : new[] { "Samantha", "Daniel", "EddyEnglish", "FloEnglish" };
            string[] phrases = chinese ? new[] { "xiaoai", "nihao", "siri", "command" } : new[] { "siri", "hi-siri", "hey-siri", "command" };
            foreach (string voice in voices)
                Assert.AreEqual(7, Directory.GetFiles(recordings, voice + "-negative*.wav").Length,
                    "Regenerate the complete corpus with prepare_english_wake_fixtures.py --sensitivity.");
            var report = new StringBuilder("voice,phrase,variant,matches\n");
            var misses = new List<string>();
            var confusions = new List<string>();
            var unexpected = new List<string>();
            int total = 0, hits = 0;
            double audioSeconds = 0;
            var watch = new Stopwatch();
            // SiriAI uses one inference thread; test the same capture-side budget.
            using (var model = MNNKeywordSpotter.Load(directory, 1))
            {
                watch.Start();
                foreach (string voice in voices)
                foreach (string phrase in phrases)
                {
                    var wave = MNNWaveAudio.Read(Path.Combine(recordings, voice + "-" + phrase + ".wav"), out int rate);
                    Assert.AreEqual(16000, rate);
                    for (int variant = 0; variant < 4; ++variant)
                    {
                        float[] pcm = Variant(wave, variant);
                        var matches = Stream(model, pcm, keywords);
                        audioSeconds += pcm.Length / 16000.0;
                        string key = voice + "," + phrase + "," + variant;
                        report.AppendLine(key + "," + string.Join(";", matches.Select(match => match.Keyword + ":" + match.Confidence)));
                        ++total;
                        if (matches.Count > 0) ++hits;
                        else misses.Add(key);
                    }
                }
                foreach (string voice in voices)
                foreach (string file in Directory.GetFiles(recordings, voice + "-negative*.wav").OrderBy(path => path))
                {
                    var negative = MNNWaveAudio.Read(file, out int rate);
                    Assert.AreEqual(16000, rate);
                    foreach (int variant in new[] { 0, 3 })
                    {
                        float[] pcm = Variant(negative, variant);
                        var matches = Stream(model, pcm, keywords);
                        audioSeconds += pcm.Length / 16000.0;
                        string key = Path.GetFileName(file) + ":" + variant;
                        report.AppendLine(voice + ",negative," + key + "," + matches.Count);
                        if (matches.Count == 0) continue;
                        confusions.Add(key);
                        // Seriously is a measured acoustic confusion for this English model.
                        // All other near words and unrelated speech must remain silent.
                        if (!file.EndsWith("-negative-seriously.wav", StringComparison.Ordinal)) unexpected.Add(key);
                    }
                }
                foreach (float noiseRms in new[] { 0f, .0005f, .005f, .02f })
                {
                    var noise = new float[160000];
                    var random = new System.Random(1701);
                    for (int i = 0; i < noise.Length; ++i) noise[i] = noiseRms * (float)Math.Sqrt(3) * (float)(random.NextDouble() * 2 - 1);
                    var matches = Stream(model, noise, keywords);
                    audioSeconds += noise.Length / 16000.0;
                    if (matches.Count > 0) unexpected.Add("Noise RMS=" + noiseRms);
                }
                watch.Stop();
            }
            string artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestArtifacts~/WakeSensitivity"));
            Directory.CreateDirectory(artifacts);
            File.WriteAllText(Path.Combine(artifacts, chinese ? "chinese.csv" : "english.csv"), report.ToString());
            TestContext.WriteLine(report.ToString());
            TestContext.WriteLine($"Hits={hits}/{total}; confusions={confusions.Count}; compute/audio={watch.Elapsed.TotalSeconds / audioSeconds:F3}; misses={string.Join(";", misses)}; confusions={string.Join(";", confusions)}");
            Assert.That(hits / (double)total, Is.GreaterThanOrEqualTo(minimumRecall), string.Join("\n", misses));
            Assert.That(confusions.Count, Is.LessThanOrEqualTo(chinese ? 0 : 1), string.Join("\n", confusions));
            Assert.IsEmpty(unexpected, string.Join("\n", unexpected));
            Assert.Less(watch.Elapsed.TotalSeconds, audioSeconds, "Streaming inference must keep up with capture on this test host.");
        }

        private static List<MNNKeywordDetection> Stream(MNNKeywordSpotter model, float[] pcm, string keywords)
        {
            model.Reset(keywords);
            var matches = new List<MNNKeywordDetection>();
            for (int at = 0; at < pcm.Length; at += 512)
            {
                var block = new float[Math.Min(512, pcm.Length - at)];
                Array.Copy(pcm, at, block, 0, block.Length);
                matches.AddRange(model.AcceptWaveform(block));
            }
            return matches;
        }

        private static float[] Variant(float[] wave, int variant)
        {
            // Linear resampling changes playback speed and pitch together.
            float speed = variant == 2 ? 1.12f : 1;
            float gain = variant == 1 || variant == 3 ? .1f : 1;
            int count = (int)(wave.Length / speed);
            var pcm = new float[16000 + count + 16000];
            double energy = wave.Sum(sample => (double)sample * sample) / wave.Length;
            float noise = variant == 3 ? (float)Math.Sqrt(energy) * gain * .1f * (float)Math.Sqrt(3) : 0;
            var random = new System.Random(1701);
            for (int i = 0; i < pcm.Length; ++i)
            {
                int at = i - 16000;
                float speech = 0;
                if (at >= 0 && at < count)
                {
                    float position = at * speed;
                    int left = (int)position;
                    float fraction = position - left;
                    speech = (wave[left] * (1 - fraction) + wave[Math.Min(left + 1, wave.Length - 1)] * fraction) * gain;
                }
                pcm[i] = speech + noise * (float)(random.NextDouble() * 2 - 1);
            }
            return pcm;
        }
    }
}
