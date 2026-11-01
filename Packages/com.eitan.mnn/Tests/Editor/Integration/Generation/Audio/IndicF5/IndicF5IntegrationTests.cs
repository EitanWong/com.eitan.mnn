using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class IndicF5IntegrationTests
    {
        private static string Root => Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT"), "IndicF5-MNN");

        [Test, Explicit("Compare published mel decoding with the publisher's independent WAV.")]
        public void PublishedVocosMelMatchesReference()
        {
            float[] mel = ReadNpy(Path.Combine(Root, "Reference", "samples", "mel.npy"));
            using (var graph = new MNNGenerationGraph(Path.Combine(Root, "indicf5_vocos_fp32_dynamic.mnn"), 4, backendType: MNNBackendType.CPU))
            {
                float[] actual = graph.Run("wav", new Input("mel", mel, 1, 100, mel.Length / 100));
                var expected = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "samples", "hi_short_mnn_vocos_fp32.wav"));
                Assert.AreEqual(expected.Samples.Length, actual.Length);
                File.WriteAllBytes(Path.Combine(AdditionalTtsValidation.Artifacts, "indicf5-vocos.wav"), MNN.Unity.Editor.MNNStudioAudio.EncodeWave(actual, 24000));
                double rmse = Math.Sqrt(expected.Samples.Zip(actual, (a, b) => Math.Pow(a - b, 2)).Average());
                TestContext.WriteLine("Vocos length=" + actual.Length + ", RMSE=" + rmse);
                double gain = expected.Samples.Zip(actual, (a, b) => (double)a * b).Sum() / actual.Sum(x => (double)x * x);
                double residual = Math.Sqrt(expected.Samples.Zip(actual, (a, b) => Math.Pow(a - b * gain, 2)).Average());
                double referenceRms = Math.Sqrt(expected.Samples.Select(x => (double)x * x).Average());
                TestContext.WriteLine("Publisher relative amplitude=" + gain + ", fitted residual=" + residual);
                Assert.That(gain, Is.InRange(.5, 2)); Assert.Less(residual / referenceRms, .04);
            }
        }

        [Test, Explicit("Inspect the published DiT's actual output contract before implementing flow inference.")]
        public void PublishedDit128Contract()
        {
            const int frames = 128;
            using (var graph = new MNNGenerationGraph(Path.Combine(Root, "indicf5_dit_int8wq.mnn"), 4, backendType: MNNBackendType.CPU))
            {
                var pred = graph.Run("pred", new Input("x", new float[frames * 100], 1, frames, 100),
                    new Input("cond", new float[frames * 100], 1, frames, 100),
                    new Input("text", Enumerable.Repeat(-1, frames).ToArray(), 1, frames), new Input("time", new[]{.5f}, 1));
                Assert.AreEqual(frames * 100, pred.Length);
                Assert.IsTrue(pred.All(x => !float.IsNaN(x) && !float.IsInfinity(x)));
                TestContext.WriteLine("DiT range=" + pred.Min() + "," + pred.Max());
            }
        }

        [Test, Explicit("Validate adapted DiT at original and longer frame lengths.")]
        public void AdaptedDitLengths()
        {
            string path = Path.Combine(Root, "indicf5_dit_int8wq.mnn");
            foreach (int frames in new[]{128, 256})
            using (var graph = new MNNGenerationGraph(path, 4, MNNIndicF5Graph.Prepare(File.ReadAllBytes(path), frames), backendType: MNNBackendType.CPU))
            {
                var pred = graph.Run("pred", new Input("x", new float[frames * 100], 1, frames, 100),
                    new Input("cond", new float[frames * 100], 1, frames, 100),
                    new Input("text", Enumerable.Repeat(-1, frames).ToArray(), 1, frames), new Input("time", new[]{.5f}, 1));
                Assert.AreEqual(frames * 100, pred.Length);
                Assert.IsTrue(pred.All(x => !float.IsNaN(x) && !float.IsInfinity(x)));
                TestContext.WriteLine("Adapted DiT " + frames + " range=" + pred.Min() + "," + pred.Max());
            }
        }

        [Test, Explicit("Determine the content of the publisher's Hindi reference sample.")]
        public void TranscribePublishedHindiReference()
        {
            using (var asr = MNNWhisperRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_TAMIL_ASR_ROOT")))
            {
                typeof(MNNWhisperRecognizer).GetField("_initialTokens", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(asr, new[]{50258, 50276, 50359, 50363});
                string text = asr.RecognizeWaveFile(Path.Combine(Root, "samples", "hi_short_mnn_vocos_fp32.wav")).Text;
                File.WriteAllText(Path.Combine(AdditionalTtsValidation.Artifacts, "indicf5-published-hi.asr.txt"), text);
                TestContext.WriteLine(text);
                Assert.IsNotEmpty(text);
            }
        }

        [Test, Explicit("Full IndicF5 Hindi speech with independent content checks.")]
        public void NativeHindiSpeech()
        {
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "samples", "hi_short_mnn_vocos_fp32.wav"));
            using (var model = MNNIndicF5Tts.Load(Root))
            {
                Assert.Throws<OperationCanceledException>(() => model.Synthesize("नमस्ते", reference, "नमस्ते", cancellationToken: new System.Threading.CancellationToken(true)));
                Assert.Throws<ArgumentNullException>(() => model.Synthesize("नमस्ते", null, "नमस्ते"));
                Assert.Throws<ArgumentException>(() => model.Synthesize("नमस्ते", reference, ""));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize("नमस्ते", reference, "नमस्ते", steps: 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize("नमस्ते", reference, "नमस्ते", speed: float.NaN));
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var audio = model.Synthesize("नमस्ते, यह आवाज़ का परीक्षण है।", reference, "नमस्ते, आप कैसे हैं?");
                string path = AdditionalTtsValidation.ValidateAndSave(audio, "indicf5-native-hi.wav", 24000);
                TestContext.WriteLine("Native elapsed " + watch.Elapsed.TotalSeconds);
                using (var asr = MNNWhisperRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_TAMIL_ASR_ROOT")))
                {
                    typeof(MNNWhisperRecognizer).GetField("_initialTokens", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .SetValue(asr, new[]{50258, 50276, 50359, 50363});
                    string text = asr.Recognize(audio.Waveform, audio.SampleRate).Text;
                    File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), text);
                    TestContext.WriteLine(text);
                    if (!text.Contains("नमस्ते") || !text.Contains("परीक्षण"))
                    using (var second = MNNLlm.Load(Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT"), cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "indicf5-hi-asr-cache"), backendType: MNNBackendType.CPU))
                    {
                        string transcript = second.GenerateMultimodal("Transcribe the Hindi speech exactly in Devanagari. Output only the transcript.", audioPath: path, maxNewTokens: 96).Text;
                        File.WriteAllText(Path.ChangeExtension(path, "independent-asr.txt"), transcript); text += "\n" + transcript;
                    }
                    StringAssert.Contains("नमस्ते", text); StringAssert.Contains("परीक्षण", text);
                }
            }
        }

        [Test]
        public void ManagedFrontendMatchesIndependentNumpy()
        {
            var frontend = new MNNIndicF5Frontend(Path.Combine(Root, "checkpoints", "vocab.txt"));
            foreach (var item in MNNModelData.Array(MNNModelData.Read(Path.Combine(AdditionalTtsValidation.Artifacts, "indicf5-tokenizer-reference.json"))))
            {
                string text = (string)MNNModelData.Get(item, "text");
                int[] expected = MNNModelData.Array(MNNModelData.Get(item, "ids")).Select(Convert.ToInt32).ToArray();
                CollectionAssert.AreEqual(expected, frontend.Encode(text), text);
            }
            var audio = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "samples", "hi_short_mnn_vocos_fp32.wav"));
            float[] actual = frontend.Mel(audio.Samples, out int frames, default);
            byte[] bytes = File.ReadAllBytes(Path.Combine(AdditionalTtsValidation.Artifacts, "indicf5-reference-mel.f32"));
            var reference = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, reference, 0, bytes.Length);
            Assert.AreEqual(reference.Length, actual.Length);
            double max = actual.Zip(reference, (a, b) => Math.Abs(a-b)).Max();
            Assert.Less(max, .0001); TestContext.WriteLine("Frontend frames=" + frames + ", max error=" + max);
        }

        [Test, Explicit("Actual Tamil speech with the publisher's Hindi reference and independent Tamil ASR.")]
        public void NativeTamilSpeech()
        {
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "samples", "hi_short_mnn_vocos_fp32.wav"));
            using (var model = MNNIndicF5Tts.Load(Root))
            {
                Assert.Throws<OperationCanceledException>(() => model.Synthesize("नमस्ते", reference, "नमस्ते", cancellationToken: new System.Threading.CancellationToken(true)));
                Assert.Throws<ArgumentNullException>(() => model.Synthesize("नमस्ते", null, "नमस्ते"));
                Assert.Throws<ArgumentException>(() => model.Synthesize("नमस्ते", reference, ""));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize("नमस्ते", reference, "नमस्ते", steps: 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize("नमस्ते", reference, "नमस्ते", speed: float.NaN));
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var audio = model.Synthesize("வணக்கம், இது ஒரு குரல் சோதனை.", reference, "नमस्ते, आप कैसे हैं?");
                string path = AdditionalTtsValidation.ValidateAndSave(audio, "indicf5-native-ta.wav", 24000);
                TestContext.WriteLine("Tamil elapsed " + watch.Elapsed.TotalSeconds);
                using (var asr = MNNWhisperRecognizer.Load(Environment.GetEnvironmentVariable("MNN_TEST_TAMIL_ASR_ROOT")))
                {
                    asr.Language = "ta";
                    string text = asr.Recognize(audio.Waveform, audio.SampleRate).Text;
                    File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), text); TestContext.WriteLine(text);
                    if (!text.Contains("சோதனை"))
                    using (var second = MNNLlm.Load(Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT"), cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "indicf5-ta-asr-cache"), backendType: MNNBackendType.CPU))
                    {
                        string transcript = second.GenerateMultimodal("Transcribe the Tamil speech exactly in Tamil script. Output only the transcript.", audioPath: path, maxNewTokens: 96).Text;
                        File.WriteAllText(Path.ChangeExtension(path, "independent-asr.txt"), transcript); text += "\n" + transcript;
                    }
                    StringAssert.Contains("வணக்கம்", text); StringAssert.Contains("சோதனை", text);
                }
            }
        }

        [Test, Explicit("Measure real DiT steps for bounded CPU candidates with output equivalence checks.")]
        public void MeasureCpuThreads()
        {
            string path = Path.Combine(Root, "indicf5_dit_int8wq.mnn");
            const int frames = 256;
            var report = new System.Text.StringBuilder("threads,coldSeconds,warmMedianSeconds,maxError\n");
            float[] baseline = null;
            foreach (int threads in new[]{1, 2, 4, 8})
            using (var graph = new MNNGenerationGraph(path, threads, MNNIndicF5Graph.Prepare(File.ReadAllBytes(path), frames), backendType: MNNBackendType.CPU))
            {
                var inputs = new[]{new Input("x", MNNGenerationMath.Gaussian(frames * 100, 42), 1, frames, 100), new Input("cond", new float[frames * 100], 1, frames, 100),
                    new Input("text", Enumerable.Repeat(-1, frames).ToArray(), 1, frames), new Input("time", new[]{.5f}, 1)};
                var watch = System.Diagnostics.Stopwatch.StartNew(); var actual = graph.Run("pred", inputs); double cold = watch.Elapsed.TotalSeconds;
                var timings = new double[3];
                for (int i = 0; i < timings.Length; ++i) { watch.Restart(); actual = graph.Run("pred", inputs); timings[i] = watch.Elapsed.TotalSeconds; }
                double max = baseline == null ? 0 : baseline.Zip(actual, (a, b) => Math.Abs(a-b)).Max();
                Assert.Less(max, .001); baseline = baseline ?? actual; Array.Sort(timings);
                report.Append(threads).Append(',').Append(cold.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                    .Append(timings[1].ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(max.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            }
            File.WriteAllText(Path.Combine(AdditionalTtsValidation.Artifacts, "indicf5-cpu-performance.csv"), report.ToString());
            TestContext.WriteLine(report);
        }

        [Test]
        public void ChangedExportIsRejectedBeforeGraphAdaptation()
        {
            string destination = Path.Combine(AdditionalTtsValidation.Artifacts, "indicf5-invalid-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(destination, "checkpoints"));
                foreach (string file in new[]{"indicf5_dit_int8wq.mnn", "indicf5_vocos_fp32_dynamic.mnn", "checkpoints/vocab.txt"})
                    File.WriteAllBytes(Path.Combine(destination, file), new byte[16]);
                Assert.IsTrue(MNNIndicF5Tts.IsComplete(destination));
                Assert.Throws<InvalidDataException>(() => MNNIndicF5Tts.Load(destination));
            }
            finally { if (Directory.Exists(destination)) Directory.Delete(destination, true); }
        }

        internal static float[] ReadNpy(string path)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                Assert.AreEqual(147, reader.ReadByte()); reader.ReadBytes(5);
                int major = reader.ReadByte(); reader.ReadByte();
                int count = major == 1 ? reader.ReadUInt16() : reader.ReadInt32();
                string header = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(count));
                StringAssert.Contains("'<f4'", header); StringAssert.Contains("False", header);
                byte[] data = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
                var result = new float[data.Length / 4]; Buffer.BlockCopy(data, 0, result, 0, data.Length); return result;
            }
        }
    }
}
