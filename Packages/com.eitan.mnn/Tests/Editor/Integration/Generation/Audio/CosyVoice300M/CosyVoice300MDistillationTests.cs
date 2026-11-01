using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class CosyVoice300MDistillationTests
    {
        [Test]
        public void HeldOutRefinerMatchesExportAndRendersSpeech()
        {
            string data = Environment.GetEnvironmentVariable("MNN_COSYVOICE_DISTILL_DATA");
            string training = Environment.GetEnvironmentVariable("MNN_COSYVOICE_DISTILL_TRAINING");
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(training))
                Assert.Ignore("Requires explicitly collected sentence-disjoint pairs and trained refiner.");
            string root = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
            Task.Run(() =>
            {
                Directory.CreateDirectory(AdditionalTtsValidation.Artifacts);
                string csv = Path.Combine(AdditionalTtsValidation.Artifacts, "heldout.csv");
                File.WriteAllText(csv, "sentence,seed,ar_seconds,teacher_flow_seconds,two_flow_seconds,refiner_seconds,teacher_vocoder_seconds,refined_vocoder_seconds,audio_seconds,teacher_rtf,refined_rtf,export_relative_rmse,endpoint_mse_ratio\n");
                using (var model = MNNCosyVoice300M.Load(root, acousticBackend: MNNBackendType.CPU))
                using (var graph = new MNNGenerationGraph(Path.Combine(root, "experimental-flow-distill/mel_refiner.mnn"),
                    4, backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High))
                for (int sentence = 12; sentence < 16; ++sentence)
                foreach (int seed in new[] {17, 43})
                {
                    string name = $"test-{sentence:D2}-{seed}";
                    string prefix = Path.Combine(data, name);
                    string text = File.ReadAllText(prefix + ".text.txt");
                    var random = new MNNCosyVoice300MSampler(seed);
                    var watch = Stopwatch.StartNew();
                    var tokens = (int[])Invoke(model, "GenerateTokens", text, random, CancellationToken.None);
                    double ar = watch.Elapsed.TotalSeconds;
                    var twoRandom = random.Fork(); var tenRandom = random.Fork();
                    watch.Restart();
                    var mel = (float[])Invoke(model, "RenderMel", tokens, 2, twoRandom, CancellationToken.None);
                    double two = watch.Elapsed.TotalSeconds;
                    Assert.AreEqual(0, SquaredError(mel, Read(prefix + ".input.bin")), "Collected input must reproduce exactly.");
                    watch.Restart();
                    var refined = graph.Run("refined_mel", new MNNGenerationGraph.Input("mel", mel, 1, 80, mel.Length / 80));
                    double refiner = watch.Elapsed.TotalSeconds;
                    var expected = Read(Path.Combine(training, name + ".torch.bin"));
                    double exportError = Math.Sqrt(SquaredError(refined, expected) / Energy(expected));
                    Assert.Less(exportError, .001, "MNN must agree with trained Torch export.");
                    watch.Restart();
                    var teacherMel = (float[])Invoke(model, "RenderMel", tokens, 10, tenRandom, CancellationToken.None);
                    double ten = watch.Elapsed.TotalSeconds;
                    double ratio = SquaredError(refined, teacherMel) / SquaredError(mel, teacherMel);
                    Assert.Less(ratio, 1, "Held-out endpoint error must improve.");
                    watch.Restart(); var teacher = Vocoder(model, teacherMel, tenRandom);
                    double teacherVocoder = watch.Elapsed.TotalSeconds;
                    watch.Restart(); var audio = Vocoder(model, refined, twoRandom);
                    double vocoder = watch.Elapsed.TotalSeconds;
                    double duration = audio.Waveform.Length / 22050d;
                    string F(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                    File.AppendAllText(csv, string.Join(",", sentence, seed, F(ar), F(ten), F(two), F(refiner), F(teacherVocoder),
                        F(vocoder), F(duration), F((ar + ten + teacherVocoder) / duration),
                        F((ar + two + refiner + vocoder) / duration), F(exportError), F(ratio)) + "\n");
                    AdditionalTtsValidation.ValidateAndSave(audio, name + "-refined.wav", 22050);
                    AdditionalTtsValidation.ValidateAndSave(teacher, name + "-teacher.wav", 22050);
                    File.Copy(prefix + "-two.wav", Path.Combine(AdditionalTtsValidation.Artifacts, name + "-two.wav"), true);
                    File.WriteAllText(Path.Combine(AdditionalTtsValidation.Artifacts, name + ".text.txt"), text);
                }
            }).GetAwaiter().GetResult();
        }

        private static float[] Read(string path)
        {
            var bytes = File.ReadAllBytes(path); var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }

        private static double SquaredError(float[] actual, float[] expected)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            double sum = 0;
            for (int i = 0; i < actual.Length; ++i) { double d = actual[i] - expected[i]; sum += d * d; }
            return sum;
        }

        private static double Energy(float[] values)
        {
            double sum = 0; foreach (float v in values) sum += v * v; return sum;
        }

        [Test]
        public void CollectFullPromptFlowTeacherPairs()
        {
            if (Environment.GetEnvironmentVariable("MNN_COSYVOICE_DISTILL_COLLECT") != "1")
                Assert.Ignore("Explicit small-sample collection only; uses existing weights, no downloads.");
            string root = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
            Task.Run(() =>
            {
                // Text split is fixed before collecting targets. Both seeds for a
                // sentence stay in its assigned split; no random frame leakage.
                string[] texts = {"早上好，今天我们来学习如何使用本地语音合成。", "我想订一张明天上午去上海的火车票。",
                    "Welcome back. Your files are ready, and you can start whenever you like.",
                    "Open the application and choose a voice for the next message.",
                    "请先把窗户关上，然后帮我倒一杯温水。", "这本书介绍了海洋、森林和生活在那里的动物。",
                    "The train leaves at nine. Please bring your ticket and a bottle of water.",
                    "I saved a copy of the report in the folder beside your notes.",
                    "下雨了，出门的时候记得带上雨伞。", "我们可以在周末一起去公园散步。",
                    "You can change the volume using the buttons on the left.", "She smiled and put the blue notebook on the table.",
                    "图书馆下午五点关门，请提前归还借阅的杂志。", "沿着这条小路往前走，就能看见山脚下的湖泊。",
                    "My brother planted tomatoes in the garden last spring.", "After lunch, we will visit the museum near the old bridge."};
                Directory.CreateDirectory(AdditionalTtsValidation.Artifacts);
                using (var model = MNNCosyVoice300M.Load(root, acousticBackend: MNNBackendType.CPU))
                {
                    for (int sentence = 0; sentence < texts.Length; ++sentence)
                    foreach (int seed in new[] {17, 43})
                    {
                        string split = sentence < 8 ? "train" : sentence < 12 ? "dev" : "test";
                        string prefix = Path.Combine(AdditionalTtsValidation.Artifacts, $"{split}-{sentence:D2}-{seed}");
                        var random = new MNNCosyVoice300MSampler(seed);
                        var watch = Stopwatch.StartNew();
                        var tokens = (int[])Invoke(model, "GenerateTokens", texts[sentence], random, CancellationToken.None);
                        double ar = watch.Elapsed.TotalSeconds;
                        var tenRandom = random.Fork();
                        watch.Restart();
                        var target = (float[])Invoke(model, "RenderMel", tokens, 10, tenRandom, CancellationToken.None);
                        double ten = watch.Elapsed.TotalSeconds;
                        var twoRandom = random.Fork();
                        watch.Restart();
                        var input = (float[])Invoke(model, "RenderMel", tokens, 2, twoRandom, CancellationToken.None);
                        double two = watch.Elapsed.TotalSeconds;
                        Assert.AreEqual(target.Length, input.Length);
                        Write(prefix + ".input.bin", input); Write(prefix + ".target.bin", target);
                        File.WriteAllText(prefix + ".text.txt", texts[sentence]);
                        watch.Restart();
                        var teacherAudio = Vocoder(model, target, tenRandom);
                        double teacherVocoder = watch.Elapsed.TotalSeconds;
                        watch.Restart();
                        var twoAudio = Vocoder(model, input, twoRandom);
                        double twoVocoder = watch.Elapsed.TotalSeconds;
                        File.WriteAllText(prefix + ".json", FormattableString.Invariant(
                            $"{{\"sentence\":{sentence},\"seed\":{seed},\"frames\":{target.Length / 80},\"tokens\":{tokens.Length},\"ar_seconds\":{ar:R},\"teacher_seconds\":{ten:R},\"two_step_seconds\":{two:R},\"teacher_vocoder_seconds\":{teacherVocoder:R},\"two_vocoder_seconds\":") + twoVocoder.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}");
                        AdditionalTtsValidation.ValidateAndSave(teacherAudio, $"{split}-{sentence:D2}-{seed}-teacher.wav", 22050);
                        AdditionalTtsValidation.ValidateAndSave(twoAudio, $"{split}-{sentence:D2}-{seed}-two.wav", 22050);
                    }
                }
            }).GetAwaiter().GetResult();
        }

        internal static object Invoke(MNNCosyVoice300M model, string method, params object[] arguments)
            => typeof(MNNCosyVoice300M).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model, arguments);

        internal static MNNGeneratedAudio Vocoder(MNNCosyVoice300M model, float[] mel, MNNCosyVoice300MSampler random)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var f0Graph = (MNNGenerationGraph)typeof(MNNCosyVoice300M).GetField("_f0", flags).GetValue(model);
            var hift = (MNNGenerationGraph)typeof(MNNCosyVoice300M).GetField("_hift", flags).GetValue(model);
            int frames = mel.Length / 80;
            var f0 = f0Graph.Run("f0", new MNNGenerationGraph.Input("mel", mel, 1, 80, frames));
            var source = (float[])Invoke(model, "Source", f0, random, CancellationToken.None);
            int spectral = source.Length / 18;
            var spectrum = hift.Run("spectrum", new MNNGenerationGraph.Input("mel", mel, 1, 80, frames),
                new MNNGenerationGraph.Input("source_stft", source, 1, 18, spectral));
            return new MNNGeneratedAudio(MNNSpeechSpectral16.Istft(spectrum, spectral, CancellationToken.None), 22050, "");
        }

        private static void Write(string path, float[] values)
        {
            var bytes = new byte[values.Length * 4]; Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length); File.WriteAllBytes(path, bytes);
        }
    }
}
