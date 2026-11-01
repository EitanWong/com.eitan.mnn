using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class CosyVoice300MConservativeTests
    {
        [Test]
        public void Int8CandidatesMeasureCompleteSynthesisWithOriginalAcoustics()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Set existing local model root; no downloads.");
            if (!File.Exists(Path.Combine(root, MNNCosyVoice300MModel.ConservativeGraph(MNNCosyVoice300MDecoderMode.ExperimentalInt8))))
                Assert.Ignore("Generate the explicitly experimental INT8 candidates first.");
            Task.Run(() =>
            {
                string[] texts = {"你好，欢迎使用语音合成。", "The nearest station is across the street, next to the post office.",
                    "图书馆下午五点关门，请提前归还借阅的杂志。", "Please keep the microphone close to your mouth and speak clearly.",
                    "请把桌上的蓝色文件夹交给王先生，明天上午九点开会。",
                    "After I finish checking the numbers, I will send you a report."};
                var modes = new[] {MNNCosyVoice300MDecoderMode.Float16, MNNCosyVoice300MDecoderMode.ExperimentalInt8,
                    MNNCosyVoice300MDecoderMode.ExperimentalFfnInt8};
                var models = new MNNCosyVoice300M[3];
                Directory.CreateDirectory(AdditionalTtsValidation.Artifacts);
                string csv = Path.Combine(AdditionalTtsValidation.Artifacts, "int8-timings.csv");
                File.WriteAllText(csv, "mode,phrase,run,tokens,token_seconds,flow_seconds,vocoder_seconds,seconds,audio_seconds,rtf\n");
                try
                {
                    for (int i = 0; i < 3; ++i)
                        models[i] = MNNCosyVoice300M.Load(root, acousticBackend: MNNBackendType.CPU, decoderMode: modes[i]);
                    for (int phrase = 0; phrase < texts.Length; ++phrase)
                    for (int run = 0; run < 3; ++run)
                    for (int order = 0; order < 3; ++order)
                    {
                        int index = (order + phrase + run) % 3;
                        var model = models[index];
                        var audio = model.Synthesize(texts[phrase]);
                        string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
                        File.AppendAllText(csv, string.Join(",", modes[index], phrase, run, model.LastSpeechTokens,
                            F(model.LastTokenSeconds), F(model.LastFlowSeconds), F(model.LastVocoderSeconds), F(model.LastSynthesisSeconds),
                            F(audio.Waveform.Length / 22050d), F(model.LastRealTimeFactor)) + "\n");
                        AdditionalTtsValidation.ValidateAndSave(audio, $"int8-{index}-{phrase}-{run}.wav", 22050);
                        if (run == 2) File.WriteAllText(Path.Combine(AdditionalTtsValidation.Artifacts, $"int8-{index}-{phrase}-{run}.expected.txt"), texts[phrase]);
                    }
                }
                finally { foreach (var model in models) model?.Dispose(); }
            }).GetAwaiter().GetResult();
        }
    }
}
