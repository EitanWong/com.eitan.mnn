using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class Qwen3ExplicitTtsIntegrationTests
    {
        private static string Root => Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_QWEN3_TTS_ROOT"), "Qwen3-TTS-Explicit");

        [Test, Explicit("Measures real CP shape reuse and thread candidates; each candidate must preserve speech content.")]
        public void MeasureWarmSynthesisAndCodePredictorShapeCache()
        {
            var report = new System.Text.StringBuilder("["); float[] baseline = null;
            foreach (var candidate in new[]{Tuple.Create(4, false), Tuple.Create(4, true), Tuple.Create(1, true), Tuple.Create(2, true), Tuple.Create(8, true)})
            {
                GC.Collect(); GC.WaitForPendingFinalizers();
                using (var model = MNNQwen3ExplicitTts.Load(Root, candidate.Item1, candidate.Item2))
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var audio = model.Synthesize("你好，这是语音合成测试。", maxFrames: 160);
                    double cold = watch.Elapsed.TotalSeconds;
                    var samples = new double[3];
                    for (int i = 0; i < samples.Length; ++i)
                    {
                        watch.Restart(); audio = model.Synthesize("你好，这是语音合成测试。", maxFrames: 160);
                        samples[i] = watch.Elapsed.TotalSeconds;
                    }
                    long resident = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
                    if (baseline == null) baseline = (float[])audio.Waveform.Clone();
                    Assert.AreEqual(baseline.Length, audio.Waveform.Length);
                    double difference = baseline.Zip(audio.Waveform, (a, b) => Math.Abs(a - b)).Max();
                    Assert.Less(difference, .02);
                    double median = samples.OrderBy(n => n).ElementAt(1), seconds = (double)audio.Waveform.Length / audio.SampleRate;
                    string label = "qwen3-cpu-" + candidate.Item1 + (candidate.Item2 ? "-cached" : "-resized");
                    string path = AdditionalTtsValidation.ValidateAndSave(audio, label + ".wav", 24000);
                    AdditionalTtsValidation.Transcribe(path, true);
                    if (report.Length > 1) report.Append(',');
                    report.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                        "{{\"threads\":{0},\"cache\":{1},\"coldSeconds\":{2:R},\"medianSeconds\":{3:R},\"audioSeconds\":{4:R},\"rtf\":{5:R},\"residentBytes\":{6}",
                        candidate.Item1, candidate.Item2 ? "true" : "false", cold, median, seconds, median / seconds, resident);
                    report.Append('}');
                    File.WriteAllText(Path.Combine(AdditionalTtsValidation.Artifacts, "qwen3-explicit-performance.json"), report + "]");
                    TestContext.WriteLine(label + ": cold=" + cold + ", warm median=" + median + ", RTF=" + median / seconds + ", max difference=" + difference + ", residentBytes=" + resident);
                }
            }
        }

        [Test]
        public void LinearModuleAndDiskEmbeddingsMatchIndependentReference()
        {
            using (var model = MNNQwen3ExplicitTts.Load(Root))
            {
                string directory = Path.Combine(AdditionalTtsValidation.Artifacts, "Sources", "Qwen3Explicit");
                var tokenizer = new MNNSpeechTokenizer(Path.Combine(Root, "tokenizer"));
                foreach (string text in new[]{"你好，这是语音合成测试。", "Paris is the capital of France.", "こんにちは、音声合成のテストです。", "I'm testing 123!\n안녕하세요"})
                {
                    string[] line = File.ReadAllLines(Path.Combine(directory, "tokenizer-reference.tsv")).First(row => row.StartsWith(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text)) + "\t", StringComparison.Ordinal)).Split('\t');
                    CollectionAssert.AreEqual(line[1].Split(',').Select(int.Parse).ToArray(), tokenizer.EncodeBare(text));
                }
                object[] args = {"你好，这是语音合成测试。", 2055, null};
                var rows = (float[][])typeof(MNNQwen3ExplicitTts).GetMethod("Prefill", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(model, args);
                byte[] bytes = File.ReadAllBytes(Path.Combine(directory, "prefill-reference.bin"));
                var expected = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
                float[] actual = rows.SelectMany(row => row).ToArray();
                Assert.AreEqual(expected.Length, actual.Length);
                double max = expected.Zip(actual, (a, b) => Math.Abs(a - b)).Max();
                TestContext.WriteLine("Independent Qwen prefill max error: " + max);
                Assert.Less(max, 1e-4);
            }
        }

        [Test]
        public void ExplicitKvSynthesizesChineseAndEnglishWithoutTruncation()
        {
            using (var model = MNNQwen3ExplicitTts.Load(Root))
            {
                Assert.Throws<ArgumentException>(() => model.Synthesize("test", voice: "../demo"));
                Assert.Throws<ArgumentException>(() => model.Synthesize("test", language: "auto"));
                Assert.Throws<OperationCanceledException>(() => model.Synthesize("你好", cancellationToken: new CancellationToken(true)));
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var audio = model.Synthesize("你好，这是语音合成测试。", "zh", maxFrames: 160);
                TestContext.WriteLine("Chinese generation seconds: " + watch.Elapsed.TotalSeconds);
                string path = AdditionalTtsValidation.ValidateAndSave(audio, "qwen3-explicit-zh.wav", 24000);
                AdditionalTtsValidation.Transcribe(path, true);
                Assert.Throws<InvalidDataException>(() => model.Synthesize("你好，这是语音合成测试。", maxFrames: 2));
                watch.Restart();
                audio = model.SynthesizeAsync("Paris is the capital of France.", "en", maxFrames: 160, voice: "demo").GetAwaiter().GetResult();
                TestContext.WriteLine("English generation seconds: " + watch.Elapsed.TotalSeconds);
                path = AdditionalTtsValidation.ValidateAndSave(audio, "qwen3-explicit-en.wav", 24000);
                AdditionalTtsValidation.Transcribe(path, false);
            }
        }
    }
}
