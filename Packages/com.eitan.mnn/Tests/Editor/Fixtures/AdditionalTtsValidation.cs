using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    internal static class AdditionalTtsValidation
    {
        internal static string Artifacts => Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? Path.GetFullPath("TestArtifacts~/AdditionalTts");
        internal static string ValidateAndSave(MNNGeneratedAudio audio, string filename, int sampleRate)
        {
            Assert.AreEqual(sampleRate, audio.SampleRate);
            Assert.That(audio.Waveform.Length, Is.InRange(sampleRate, sampleRate * 30));
            Assert.True(audio.Waveform.All(v => !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) <= 1));
            Assert.Greater(Math.Sqrt(audio.Waveform.Select(v => (double)v * v).Average()), .005);
            Directory.CreateDirectory(Artifacts);
            string path = Path.Combine(Artifacts, filename);
            File.WriteAllBytes(path, MNNStudioAudio.EncodeWave(audio.Waveform, audio.SampleRate));
            return path;
        }

        internal static void Transcribe(string wav, bool chinese)
        {
            string model = chinese ? Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT") : Environment.GetEnvironmentVariable("MNN_TEST_ASR_ROOT");
            Assert.That(model, Is.Not.Null.And.Not.Empty, "ASR model required to verify generated speech content.");
            string cache = Path.Combine(Artifacts, chinese ? "bertvits-asr-cache" : "piper-asr-cache");
            try
            {
                using (var asr = MNNLlm.Load(model, cacheDirectory: cache))
                {
                    var result = asr.GenerateMultimodal(chinese ? "请逐字转写音频中的中文内容，只输出原文。" : "Transcribe the spoken sentence in English.", audioPath: wav, maxNewTokens: 96);
                    File.WriteAllText(Path.ChangeExtension(wav, "asr.txt"), result.Text);
                    if (chinese)
                    {
                        StringAssert.Contains("你好", result.Text);
                        StringAssert.Contains("语音合成", result.Text);
                    }
                    else
                    {
                        StringAssert.Contains("paris", result.Text.ToLowerInvariant());
                        StringAssert.Contains("france", result.Text.ToLowerInvariant());
                    }
                }
            }
            finally
            {
                if (Directory.Exists(cache))
                    Directory.Delete(cache, true);
            }
        }
    }
}
