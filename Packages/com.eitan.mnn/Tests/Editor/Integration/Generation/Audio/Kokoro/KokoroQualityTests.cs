using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class KokoroQualityTests
    {
        [Test, Explicit("Diagnose existing v1.1 WAVs with a second recognizer; this is not a synthesis quality gate.")]
        public void DiagnoseExternalVoiceContentDisagreements()
        {
            string[] voices = {"zf_006", "zf_040", "zf_046", "zf_067", "zf_079", "zm_012", "zm_013", "zm_014", "zm_015", "zm_016", "zm_020", "zm_030", "zm_034", "zm_035", "zm_037", "zm_050", "zm_052", "zm_053", "zm_055", "zm_056", "zm_057", "zm_058", "zm_061", "zm_066", "zm_080", "zm_096", "zm_100"};
            string cache = Path.Combine(AdditionalTtsValidation.Artifacts, "kokoro11-diagnostic-asr-cache");
            try
            {
                using (var asr = MNNLlm.Load(Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT"), cacheDirectory: cache, backendType: MNNBackendType.CPU))
                    foreach (string voice in voices)
                    {
                        string path = Path.Combine(AdditionalTtsValidation.Artifacts, "kokoro11-all-" + voice + ".wav");
                        string text = asr.GenerateMultimodal("请逐字转写音频中的中文内容，只输出原文。", audioPath: path, maxNewTokens: 96).Text;
                        File.WriteAllText(Path.ChangeExtension(path, "qwen-asr.txt"), text);
                        TestContext.WriteLine(voice + " => " + text);
                    }
            }
            finally { if (Directory.Exists(cache)) Directory.Delete(cache, true); }
        }

        [Test, Explicit("Diagnose expanded-export content failures with Whisper; does not replace the strict synthesis gate.")]
        public void DiagnoseExpandedVoiceContentWithWhisper()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_TAMIL_ASR_ROOT");
            using (var asr = MNNWhisperRecognizer.Load(root))
            {
                asr.Language = "zh";
                foreach (string voice in new[]{"zf_046", "zf_079", "zm_030", "zm_035", "zm_080", "zm_100"})
                {
                    string path = Path.Combine(AdditionalTtsValidation.Artifacts, "kokoro-expanded-all-" + voice + ".wav");
                    string text = asr.RecognizeWaveFile(path).Text;
                    File.WriteAllText(Path.ChangeExtension(path, "whisper-asr.txt"), text);
                    TestContext.WriteLine(voice + " Whisper: " + text);
                    Assert.IsNotEmpty(text);
                }
            }
        }

        [Test, Explicit("Compare independent ASR outputs for the Sherpa Xiaoni voice at several speeds.")]
        public void DiagnoseXiaoniAlignment()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SHERPA_KOKORO_ROOT");
            using (var model = MNNKokoro.Load(root))
            using (var sense = MNNSenseVoiceRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_INDEPENDENT_CHINESE_ASR_ROOT")))
            using (var para = MNNParaformerRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_PARAFORMER_ROOT")))
                foreach (float speed in new[]{1f, 1.05f, 1.2f})
                {
                    var audio = model.Synthesize("你好，这是语音合成测试。", "zf_xiaoni", speed);
                    string path = AdditionalTtsValidation.ValidateAndSave(audio, "kokoro-xiaoni-quality-" + speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".wav", 24000);
                    string a = sense.RecognizeWaveFile(path).Text;
                    string b = para.Recognize(audio.Waveform, audio.SampleRate).Text;
                    File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), "SenseVoice: " + a + "\nParaformer: " + b);
                    TestContext.WriteLine(speed + " SenseVoice: " + a + " / Paraformer: " + b);
                }
        }
    }
}
