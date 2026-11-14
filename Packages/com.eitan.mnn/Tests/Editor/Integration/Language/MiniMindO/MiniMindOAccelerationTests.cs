using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using MNN.Unity;
using NUnit.Framework;
using UnityEngine;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity.Tests
{
    [Explicit("Benchmarks existing local exports on the current CPU/Metal runtime; never downloads assets.")]
    public sealed class MiniMindOAccelerationTests
    {
        [Test]
        public void MeasureSeededSamplingAndReferenceVoice()
        {
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN/Models", MNNMiniMindO.InstallationName);
            var source = new System.Random(321);
            var logits = Enumerable.Range(0, 8 * 2112).Select(_ => (float)(source.NextDouble() * 20 - 10)).ToArray();
            var random = new System.Random(42);
            var selected = new int[4096];
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < selected.Length; ++i)
                selected[i] = MNNMiniMindOSampling.Sample(logits, i % 8 * 2112, 2112, .2f, 1, 50, random, new[] { 7, 7, 12 });
            double milliseconds = timer.Elapsed.TotalMilliseconds;
            var bytes = new byte[selected.Length * 4]; Buffer.BlockCopy(selected, 0, bytes, 0, bytes.Length);
            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            TestContext.WriteLine("SAMPLER_SHA256=" + hash + "\nSAMPLER_4096_MS=" + milliseconds.ToString("F3", CultureInfo.InvariantCulture));
            Assert.AreEqual("f0daf87c4131121d56bb65a70263eb13a1803b0106bd453cafbdf7817fac3030", hash);
            using (var model = MNNMiniMindO.Load(directory))
            {
                timer.Restart(); double first = -1;
                var result = model.Generate("你好，请用一句话介绍你自己。", 128, 42,
                    update => { if (first < 0 && update.Waveform.Length > 0) first = timer.Elapsed.TotalMilliseconds; });
                double elapsed = timer.Elapsed.TotalMilliseconds;
                var codes = result.AudioCodes.SelectMany(x => x).ToArray(); bytes = new byte[codes.Length * 4]; Buffer.BlockCopy(codes, 0, bytes, 0, bytes.Length);
                using (var sha = System.Security.Cryptography.SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                TestContext.WriteLine("VOICE_CODES_SHA256=" + hash + "\nVOICE_TEXT=" + result.Text + "\nVOICE_FIRST_PCM_MS=" + first.ToString("F3", CultureInfo.InvariantCulture) +
                    "\nVOICE_TOTAL_MS=" + elapsed.ToString("F3", CultureInfo.InvariantCulture) + "\nVOICE_SAMPLES=" + result.Waveform.Length);
                Assert.AreEqual("f868aedb5c4469157453fa33e6567715b8401d39ac885d7bea9452a13fcee4e1", hash);
                Assert.False(result.TokenLimitReached); Assert.IsNotEmpty(result.Waveform);
            }
        }

        [Test]
        public void MeasureCompleteVoiceTurnsOnTwoAndFourThreads()
        {
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN/Models", MNNMiniMindO.InstallationName);
            string artifacts = Environment.GetEnvironmentVariable("SIRIAI_MINIMINDO_ARTIFACTS") ?? Path.GetFullPath("TestArtifacts~/MiniMindOSiriAI");
            Directory.CreateDirectory(artifacts);
            float[] audio;
            using (var seed = MNNMiniMindO.Load(directory))
            {
                var spoken = seed.Generate("请说：你好。", 128);
                audio = MNNAudioResampling.Resample(spoken.Waveform.Select(x => Math.Max(-1, Math.Min(1, x))).ToArray(), 24000, 16000);
            }
            var references = new System.Collections.Generic.Dictionary<string, MNNMiniMindOResult>();
            foreach (int threads in new[] { 4, 2 })
            using (var model = MNNMiniMindO.Load(directory, threads))
            foreach (string prompt in new[] { "请听取这条语音并简短回答。", "请用一句话回答。", "请介绍你自己。" })
            {
                var timer = Stopwatch.StartNew();
                double first = -1;
                var result = model.Generate(new MNNMiniMindORequest { Text = prompt, Audio16Khz = audio },
                    new MNNMiniMindOOptions { MaxNewTokens = 256, Temperature = 0 }, update =>
                    { if (first < 0 && update.Waveform.Length > 0) first = timer.Elapsed.TotalMilliseconds; });
                double elapsed = timer.Elapsed.TotalMilliseconds;
                bool exactText = true, exactCodes = true;
                double relative = 0;
                if (threads == 4) references[prompt] = result;
                else
                {
                    var expected = references[prompt];
                    exactText = expected.Text == result.Text;
                    exactCodes = expected.AudioCodes.SelectMany(x => x).SequenceEqual(result.AudioCodes.SelectMany(x => x));
                    if (result.Waveform.Length != expected.Waveform.Length) relative = double.PositiveInfinity;
                    else
                    {
                        double square = 0, difference = 0;
                        for (int i = 0; i < result.Waveform.Length; ++i)
                        { double value = expected.Waveform[i], delta = result.Waveform[i] - value; square += value * value; difference += delta * delta; }
                        relative = Math.Sqrt(difference / Math.Max(square, 1e-20));
                    }
                }
                string line = string.Join("\t", threads, prompt, first.ToString("F3", CultureInfo.InvariantCulture),
                    elapsed.ToString("F3", CultureInfo.InvariantCulture), result.GeneratedTokens, result.Waveform.Length,
                    result.TokenLimitReached, exactText, exactCodes, relative.ToString("G9", CultureInfo.InvariantCulture), result.Text);
                TestContext.WriteLine(line); File.AppendAllText(Path.Combine(artifacts, "voice-benchmark.tsv"), line + "\n");
                Assert.IsNotEmpty(result.Waveform);
                Assert.False(result.TokenLimitReached);
                Assert.True(exactText, "Thread tuning must preserve reference text.");
                TestContext.WriteLine("THREAD_RECIPE=" + (exactText && exactCodes && relative <= .001 ? "ELIGIBLE" : "REJECTED"));
            }
        }

        [TestCase("prefill")]
        [TestCase("decode")]
        [TestCase("audio_encoder")]
        [TestCase("mimi_decoder")]
        public void MeasureStagePlacementSpeedAndNumerics(string name)
        {
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN/Models", MNNMiniMindO.InstallationName);
            string artifacts = Environment.GetEnvironmentVariable("SIRIAI_MINIMINDO_ARTIFACTS") ?? Path.GetFullPath("TestArtifacts~/MiniMindOSiriAI");
            Directory.CreateDirectory(artifacts);
            var tokens = new[] { 1, 1, 1, 1, 1, 1, 1, 1 };
            Input[] inputs;
            string[] outputs;
            switch (name)
            {
                case "audio_encoder":
                    var speech = Enumerable.Range(0, 32000).Select(i => .1f * (float)Math.Sin(i * 2 * Math.PI * 440 / 16000)).ToArray();
                    var features = MNNSenseVoiceFeatures.Extract(speech, out int frames, default);
                    inputs = new[] { new Input("fbank", features, 1, frames, 560) };
                    outputs = new[] { "features" }; break;
                case "mimi_decoder":
                    inputs = new[] { new Input("codes", Enumerable.Range(0, 32).Select(i => i % 2048).ToArray(), 1, 8, 4) };
                    outputs = new[] { "pcm" }; break;
                default:
                    var list = new System.Collections.Generic.List<Input> { new Input("tokens", tokens, 1, 8),
                        new Input("audio_codes", Enumerable.Repeat(2049, 64).ToArray(), 1, 8, 8) };
                    if (name == "decode") list.Add(new Input("past", new float[12 * 2 * 16 * 4 * 96], 12, 2, 1, 16, 4, 96));
                    inputs = list.ToArray(); outputs = new[] { "text_logits", "audio_logits", "present" }; break;
            }
            float[][] reference = null;
            foreach (var setting in new[] { (MNNBackendType.CPU, 4, MNNPrecisionMode.Low), (MNNBackendType.CPU, 2, MNNPrecisionMode.Low),
                (MNNBackendType.CPU, 8, MNNPrecisionMode.Low), (MNNBackendType.Metal, 4, MNNPrecisionMode.Low), (MNNBackendType.Metal, 4, MNNPrecisionMode.High) })
            {
                var timer = Stopwatch.StartNew();
                try
                {
                    using (var graph = new MNNGenerationGraph(Path.Combine(directory, name + ".mnn"), setting.Item2,
                        backendType: setting.Item1, precisionMode: setting.Item3, memoryMode: MNNMemoryMode.Low,
                        externalWeightsPath: Path.Combine(directory, name + ".mnn.weight")))
                    {
                        var result = graph.RunOutputs(outputs, inputs).Select(x => x.Values).ToArray();
                        double cold = timer.Elapsed.TotalMilliseconds;
                        if (reference == null) reference = result;
                        double relative = 0;
                        for (int n = 0; n < result.Length; ++n)
                        {
                            Assert.AreEqual(reference[n].Length, result[n].Length);
                            double square = 0, difference = 0;
                            for (int i = 0; i < result[n].Length; ++i)
                            { double value = reference[n][i], delta = result[n][i] - value; square += value * value; difference += delta * delta; }
                            relative = Math.Max(relative, Math.Sqrt(difference / Math.Max(square, 1e-20)));
                        }
                        var times = new double[5];
                        for (int i = 0; i < times.Length; ++i) { timer.Restart(); graph.RunOutputs(outputs, inputs); times[i] = timer.Elapsed.TotalMilliseconds; }
                        Array.Sort(times);
                        string line = string.Join("\t", name, setting.Item1, graph.Backend, setting.Item2, setting.Item3,
                            cold.ToString("F3", CultureInfo.InvariantCulture), times[2].ToString("F3", CultureInfo.InvariantCulture),
                            relative.ToString("G9", CultureInfo.InvariantCulture), relative <= .02 && graph.Backend == setting.Item1 ? "ELIGIBLE" : "REJECTED");
                        TestContext.WriteLine(line); File.AppendAllText(Path.Combine(artifacts, "stage-benchmark.tsv"), line + "\n");
                    }
                }
                catch (Exception error)
                {
                    string line = name + "\t" + setting.Item1 + "\tERROR\t" + setting.Item2 + "\t" + setting.Item3 + "\t" + error.GetType().Name + "\t" + error.Message.Replace('\n', ' ');
                    TestContext.WriteLine(line); File.AppendAllText(Path.Combine(artifacts, "stage-benchmark.tsv"), line + "\n");
                    if (setting.Item1 == MNNBackendType.CPU) throw;
                }
            }
        }
    }
}
