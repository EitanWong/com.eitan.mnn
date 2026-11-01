using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class DhVaaniIntegrationTests
    {
        private static string Root => Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT"), "DhVaani");

        [Test, Explicit("Measures real CPU inference and checks speech content for every thread candidate.")]
        public void MeasureWarmSynthesisAcrossCpuThreads()
        {
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(AdditionalTtsValidation.Artifacts, "moss-nano-cached-en.wav"));
            var report = new StringBuilder("["); float[] baseline = null;
            foreach (int threads in new[]{1, 2, 4, 8})
            using (var model = MNNDhVaani.Load(Root, threads))
            {
                var watch = Stopwatch.StartNew();
                var audio = model.Synthesize("The capital of France is Paris.", reference, "Paris is the capital of France.");
                watch.Stop(); double cold = watch.Elapsed.TotalSeconds;
                var samples = new double[3];
                for (int i = 0; i < samples.Length; ++i)
                {
                    watch.Restart();
                    audio = model.Synthesize("The capital of France is Paris.", reference, "Paris is the capital of France.");
                    watch.Stop(); samples[i] = watch.Elapsed.TotalSeconds;
                }
                if (baseline == null) baseline = (float[])audio.Waveform.Clone();
                Assert.AreEqual(baseline.Length, audio.Waveform.Length);
                Assert.Less(baseline.Zip(audio.Waveform, (a, b) => Math.Abs(a - b)).Max(), .02);
                string path = AdditionalTtsValidation.ValidateAndSave(audio, "dhvaani-cpu-" + threads + ".wav", 24000);
                AdditionalTtsValidation.Transcribe(path, false);
                double seconds = (double)audio.Waveform.Length / audio.SampleRate, median = samples.OrderBy(n => n).ElementAt(1);
                if (report.Length > 1) report.Append(',');
                report.AppendFormat(CultureInfo.InvariantCulture,
                    "{{\"threads\":{0},\"coldSeconds\":{1:R},\"medianSeconds\":{2:R},\"audioSeconds\":{3:R},\"rtf\":{4:R}",
                    threads, cold, median, seconds, median / seconds);
                report.Append('}');
                File.WriteAllText(Path.Combine(AdditionalTtsValidation.Artifacts, "dhvaani-cpu-performance.json"), report + "]");
                TestContext.WriteLine("CPU" + threads + ": cold=" + cold + ", warm median=" + median + ", RTF=" + median / seconds);
            }
        }

        [Test]
        public void NativeVocoderProjectionMatchesIndependentLinearCalculation()
        {
            string path = Path.Combine(Root, "vocoder_backbone.mnn");
            var mels = Enumerable.Range(0, 1600).Select(i => -5f + (float)Math.Sin(i * .17) * .3f).ToArray();
            float[] hidden, actual;
            using (var original = new MNNSingleInputModule(path, "mels", "hidden", 4, MNNBackendType.CPU, precision: MNNPrecisionMode.High))
                hidden = original.Run(mels, 1, 100, 16);
            using (var model = MNNDhVaani.Load(Root))
            using (var optimized = new MNNSingleInputModule(path, "mels", MNNGraphLinearHead.OutputName, 4, MNNBackendType.CPU,
                (byte[])typeof(MNNDhVaani).GetField("_vocoderGraph", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model), MNNPrecisionMode.High))
                actual = optimized.Run(mels, 1, 100, 16);
            byte[] bytes = File.ReadAllBytes(Path.Combine(Root, "vocos_head.bin"));
            var weights = new float[1026 * 512]; var bias = new float[1026];
            Buffer.BlockCopy(bytes, 28, weights, 0, weights.Length * 4);
            Buffer.BlockCopy(bytes, 28 + weights.Length * 4, bias, 0, bias.Length * 4);
            Assert.AreEqual(16 * 512, hidden.Length); Assert.AreEqual(16 * 1026, actual.Length);
            double maxError = 0;
            for (int t = 0; t < 16; ++t)
                for (int o = 0; o < 1026; ++o)
                {
                    double expected = bias[o];
                    for (int i = 0; i < 512; ++i) expected += hidden[t * 512 + i] * (double)weights[o * 512 + i];
                    maxError = Math.Max(maxError, Math.Abs(expected - actual[t * 1026 + o]));
                }
            TestContext.WriteLine("Native linear projection max error: " + maxError);
            Assert.Less(maxError, 2e-4);
        }

        [Test]
        public void HindiSpeechWithEnglishReferenceHasCorrectContent()
        {
            string referencePath = Path.Combine(AdditionalTtsValidation.Artifacts, "moss-nano-cached-en.wav");
            using (var model = MNNDhVaani.Load(Root))
            {
                var audio = model.Synthesize("नमस्ते, यह आवाज़ का परीक्षण है।", MNNReferenceAudio.FromWaveFile(referencePath), "Paris is the capital of France.");
                string path = AdditionalTtsValidation.ValidateAndSave(audio, "dhvaani-hi.wav", 24000);
                using (var asr = MNNWhisperRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_TAMIL_ASR_ROOT")))
                {
                    // Independent test-only Hindi prefix from the public Whisper tokenizer.
                    // This does not enable Hindi in the package's public ASR API.
                    typeof(MNNWhisperRecognizer).GetField("_initialTokens", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(asr, new[]{50258, 50276, 50359, 50363});
                    string text = asr.Recognize(audio.Waveform, audio.SampleRate).Text;
                    File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), text);
                    if (!text.Contains("परीक्षण"))
                    {
                        using (var second = MNNLlm.Load(Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT"),
                            cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "dhvaani-hindi-asr-cache"), backendType: MNNBackendType.CPU))
                        {
                            string transcript = second.GenerateMultimodal("Transcribe the Hindi speech exactly in Devanagari. Output only the transcript.", audioPath: path, maxNewTokens: 96).Text;
                            File.WriteAllText(Path.ChangeExtension(path, "independent-asr.txt"), transcript);
                            text += "\n" + transcript;
                        }
                    }
                    StringAssert.Contains("नमस्ते", text);
                    StringAssert.Contains("परीक्षण", text);
                }
            }
        }

        [Test]
        public void ManagedFrontendAndVocoderMatchIndependentNumpyReference()
        {
            string sources = Path.Combine(AdditionalTtsValidation.Artifacts, "Sources", "DhVaani");
            float[] Read(string name)
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(sources, name + ".bin"));
                var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
            }
            using (var model = MNNDhVaani.Load(Root))
            {
                var melArgs = new object[]{Read("reference-pcm"), 0, CancellationToken.None};
                var mel = (float[])typeof(MNNDhVaani).GetMethod("Mel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model, melArgs);
                float[] expected = Read("reference-mel");
                Assert.AreEqual(expected.Length / 100, melArgs[1]); Assert.AreEqual(expected.Length, mel.Length);
                Assert.Less(mel.Zip(expected, (a, b) => Math.Abs(a - b)).Max(), 2e-5);
                var pcm = (float[])typeof(MNNDhVaani).GetMethod("Decode", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(model, new object[]{Read("reference-projected"), CancellationToken.None});
                expected = Read("reference-decoded"); Assert.AreEqual(expected.Length, pcm.Length);
                Assert.Less(pcm.Zip(expected, (a, b) => Math.Abs(a - b)).Max(), 2e-5);
            }
        }

        [Test]
        public void EnglishSpeechWithReferenceUsesAllThreeOfficialModules()
        {
            string referencePath = Path.Combine(AdditionalTtsValidation.Artifacts, "moss-nano-cached-en.wav");
            using (var model = MNNDhVaani.Load(Root))
            {
                var reference = MNNReferenceAudio.FromWaveFile(referencePath);
                Assert.Throws<ArgumentException>(() => model.Synthesize("中文", reference, "Paris is the capital of France."));
                Assert.Throws<ArgumentException>(() => model.Synthesize("Paris.", reference, ""));
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Synthesize("Paris.", reference, "Paris is the capital of France.", cancellationToken: cancelled.Token));
                }
                var audio = model.Synthesize("Paris is the capital of France.", reference, "Paris is the capital of France.");
                string path = AdditionalTtsValidation.ValidateAndSave(audio, "dhvaani-en.wav", 24000);
                AdditionalTtsValidation.Transcribe(path, false);
                audio = model.Synthesize("The capital of France is Paris.", reference, "Paris is the capital of France.", seed: 42);
                path = AdditionalTtsValidation.ValidateAndSave(audio, "dhvaani-en-repeat.wav", 24000);
                AdditionalTtsValidation.Transcribe(path, false);
            }
        }
    }
}
