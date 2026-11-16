using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the local Hojo MNN export and HOJO_TEST_ARTIFACTS."), Category("Hojo")]
    public sealed class HojoSpeechContinuityTests
    {
        private static string Model => Path.Combine(Application.streamingAssetsPath, "MNN/Models/Hojo-TTS-Light-40M-V2-MNN");
        private static string Artifacts => Path.Combine(Environment.GetEnvironmentVariable("HOJO_TEST_ARTIFACTS"), "Continuity20261116");

        [Test]
        public void CaptureSegmentTiming()
        {
            string state = Environment.GetEnvironmentVariable("HOJO_CONTINUITY_STATE") ?? "MODIFIED";
            string[] texts = { "你好，欢迎使用语音合成。", "今天天气很好，我们一起出去走走吧。", "这是一段连续的语音。接下来，我们继续介绍这个模型。" };
            using (var runner = MNNHojoTts.Load(Model, 4, MNNBackendType.CPU))
                for (int i = 0; i < texts.Length; ++i)
                {
                    var audio = runner.Synthesize(texts[i]);
                    var bytes = new byte[audio.Waveform.Length * 4];
                    Buffer.BlockCopy(audio.Waveform, 0, bytes, 0, bytes.Length);
                    File.WriteAllBytes(Path.Combine(Artifacts, state + "-" + i + ".bin"), bytes);
                    TestContext.WriteLine("STATE=" + state + ";SEGMENT=" + i + ";TEXT=" + texts[i] + ";RATE=" + audio.SampleRate +
                        ";SAMPLES=" + audio.Waveform.Length + ";SECONDS=" + audio.Waveform.Length / 24000d);
                    Assert.True(audio.Waveform.All(value => !float.IsNaN(value) && !float.IsInfinity(value)));
                }
        }

        [TestCase("你好，欢迎使用语音合成。")]
        [TestCase("今天天气很好，我们一起出去走走吧。")]
        [TestCase("这是一段连续的语音。接下来，我们继续介绍这个模型。")]
        public void NaturalTimingShortensSpeechAndKeepsSegmentRelease(string text)
        {
            using (var runner = MNNHojoTts.Load(Model))
            {
                var original = runner.Synthesize(text, naturalTiming: false);
                var actual = runner.Synthesize(text);
                Assert.AreEqual(24000, actual.SampleRate);
                Assert.Less(actual.Waveform.Length, original.Waveform.Length * .9);
                Assert.Greater(actual.Waveform.Length, original.Waveform.Length * .7);
                // Last audible 10 ms window must have a short, nonzero release for joining.
                int last = 0;
                for (int i = 0; i < actual.Waveform.Length; i += 240)
                {
                    int count = Math.Min(240, actual.Waveform.Length - i);
                    double energy = 0;
                    float peak = 0;
                    for (int j = i; j < i + count; ++j)
                    {
                        energy += (double)actual.Waveform[j] * actual.Waveform[j];
                        peak = Math.Max(peak, Math.Abs(actual.Waveform[j]));
                    }
                    if (peak > .003162278f || energy > count * 1e-6) last = i + count;
                }
                double release = (actual.Waveform.Length - last) / 24000d;
                TestContext.WriteLine("RAW_SECONDS=" + original.Waveform.Length / 24000d + ";NATURAL_SECONDS=" + actual.Waveform.Length / 24000d +
                    ";RELEASE_SECONDS=" + release);
                Assert.That(release, Is.InRange(.02, .1));
            }
        }

        [Test]
        public void SharedSpeedControlsDuration()
        {
            const string text = "你好，欢迎使用语音合成。";
            using (var normal = MNNSpeechSynthesizer.Load(Model, new MNNSpeechSynthesisOptions { Speed = 1 }))
            using (var faster = MNNSpeechSynthesizer.Load(Model, new MNNSpeechSynthesisOptions { Speed = 1.2f }))
            {
                var a = normal.Synthesize(text);
                var b = faster.Synthesize(text);
                TestContext.WriteLine("NORMAL_SAMPLES=" + a.Waveform.Length + ";FASTER_SAMPLES=" + b.Waveform.Length);
                Assert.AreEqual(24000, b.SampleRate);
                Assert.AreEqual(a.Waveform.Length / 1.2, b.Waveform.Length, 2, "Hojo must honor the shared Speed option.");
            }
        }
    }
}
