using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class CosyVoice300MOptimizationTests
    {
        [Test]
        public void OriginalAndCompactGraphsSynthesizeFourPhrasesOnWorker()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
            string reference = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_REFERENCE_ROOT");
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(reference))
                Assert.Ignore("Use run_unity_optimization.sh with existing local assets.");
            Directory.CreateDirectory(AdditionalTtsValidation.Artifacts);
            Task.Run(() =>
            {
                using (var original = MNNCosyVoice300M.Load(reference))
                using (var compact = MNNCosyVoice300M.Load(root))
                {
                    Assert.IsFalse(original.UsesCompactDecoder);
                    Assert.IsTrue(compact.UsesCompactDecoder);
                    string[] phrases = {
                        "你好，欢迎使用语音合成。",
                        "The nearest station is across the street, next to the post office.",
                        "图书馆下午五点关门，请提前归还借阅的杂志。",
                        "Please keep the microphone close to your mouth and speak at a steady pace."
                    };
                    string csv = Path.Combine(AdditionalTtsValidation.Artifacts, "paired-timings.csv");
                    File.WriteAllText(csv, "profile,phrase,run,token_seconds,flow_seconds,vocoder_seconds,seconds,audio_seconds,rtf,tokens,flow,cpu_retry\n");
                    for (int phrase = 0; phrase < phrases.Length; ++phrase)
                    for (int repeat = 0; repeat < 2; ++repeat)
                    for (int order = 0; order < 2; ++order)
                    {
                        bool optimized = repeat % 2 == 0 ? order == 1 : order == 0;
                        var model = optimized ? compact : original;
                        var audio = model.Synthesize(phrases[phrase]);
                        Assert.IsFalse(MNNCosyVoice300M.IsSaturated(audio.Waveform));
                        string tag = (optimized ? "compact" : "original") + "-" + phrase + "-" + repeat;
                        AdditionalTtsValidation.ValidateAndSave(audio, "paired-" + tag + ".wav", 22050);
                        string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
                        string row = string.Join(",", optimized ? "compact" : "original", phrase, repeat,
                            F(model.LastTokenSeconds), F(model.LastFlowSeconds), F(model.LastVocoderSeconds), F(model.LastSynthesisSeconds),
                            F(audio.Waveform.Length / 22050d), F(model.LastRealTimeFactor), model.LastSpeechTokens,
                            model.FlowBackend, model.LastAcousticCpuRetry);
                        File.AppendAllText(csv, row + "\n"); TestContext.WriteLine(row);
                    }
                }
            }).GetAwaiter().GetResult();
        }
    }
}
