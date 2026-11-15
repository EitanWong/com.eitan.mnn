using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the local Hojo MNN export and HOJO_TEST_ARTIFACTS reference outputs."), Category("Hojo")]
    public sealed class HojoTtsNativeTests
    {
        private static string Artifacts => Environment.GetEnvironmentVariable("HOJO_TEST_ARTIFACTS") ?? throw new InvalidOperationException("Set HOJO_TEST_ARTIFACTS.");
        private static string Model => Path.Combine(Application.streamingAssetsPath, "MNN/Models/Hojo-TTS-Light-40M-V2-MNN");
        [Test]
        public void ManagedTokenizerMatchesTheOfficialTokenizer()
        {
            var type = typeof(MNNHojoTts).Assembly.GetType("MNN.Unity.MNNHojoTokenizer");
            var tokenizer = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { Path.Combine(Model, "tokenizer.json") }, null);
            var encode = type.GetMethod("Encode", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (string line in File.ReadAllLines(Path.Combine(Artifacts, "tokenizer-cases.tsv")))
            {
                string[] parts = line.Split('\t'); string text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                int[] expected = parts[1].Split(',').Select(int.Parse).ToArray();
                CollectionAssert.AreEqual(expected, (int[])encode.Invoke(tokenizer, new object[] { text }), text);
            }
        }
        [Test] public void CpuPipelineMatchesOfficialGreedySpeech() => NativePipelineMatchesOfficialGreedySpeech(MNNBackendType.CPU);
        [Test] public void MetalPipelineMatchesOfficialGreedySpeech() => NativePipelineMatchesOfficialGreedySpeech(MNNBackendType.Metal);
        private static void NativePipelineMatchesOfficialGreedySpeech(MNNBackendType backend)
        {
            using (var runner = MNNHojoTts.Load(Model, 4, backend))
            {
                for (int iteration = 0; iteration < 2; ++iteration)
                {
                    var clock = Stopwatch.StartNew();
                    var audio = runner.Synthesize("Hello, this is a test.", temperature: 0, maximumTokens: 512);
                    clock.Stop();
                    byte[] bytes = File.ReadAllBytes(Path.Combine(Artifacts, "reference-en.bin"));
                    var reference = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, reference, 0, bytes.Length);
                    Assert.AreEqual(reference.Length, audio.Waveform.Length);
                    double signal = 0, error = 0;
                    for (int i = 0; i < reference.Length; ++i)
                    {
                        Assert.False(float.IsNaN(audio.Waveform[i]) || float.IsInfinity(audio.Waveform[i]));
                        signal += reference[i] * (double)reference[i];
                        double delta = reference[i] - audio.Waveform[i]; error += delta * delta;
                    }
                    double snr = 10 * Math.Log10(signal / Math.Max(error, 1e-30));
                    var stages = string.Join(",", runner.StageBackends.Select(pair => pair.Key + "=" + pair.Value));
                    TestContext.WriteLine("BACKEND=" + backend + ";ITERATION=" + iteration + ";SAMPLES=" + audio.Waveform.Length +
                        ";SECONDS=" + clock.Elapsed.TotalSeconds + ";RTF=" + clock.Elapsed.TotalSeconds / (audio.Waveform.Length / 24000d) + ";SNR_DB=" + snr + ";STAGES=" + stages);
                    Assert.Greater(snr, 35, "Waveform parity threshold");
                    if (backend == MNNBackendType.Metal) Assert.True(runner.StageBackends.Values.Any(value => value == MNNBackendType.Metal), "No stage actually selected Metal.");
                    var pcm = new byte[audio.Waveform.Length * 4]; Buffer.BlockCopy(audio.Waveform, 0, pcm, 0, pcm.Length);
                    File.WriteAllBytes(Path.Combine(Artifacts, "unity-" + backend + ".bin"), pcm);
                }
            }
        }
        [Test]
        public void SharedAdapterLoadsTheDedicatedModel()
        {
            Assert.AreEqual("Hojo", MNNSpeechSynthesizer.DetectModelKind(Model).ToString());
            Assert.True(MNNSpeechSynthesizer.HasProfile(Model));
            Assert.AreEqual(17, MNNSpeechSynthesizer.InstalledVoices(Model).Length);
            using (var runner = MNNSpeechSynthesizer.Load(Model, new MNNSpeechSynthesisOptions { Voice = "hojo_zh_f_01" }))
            {
                var audio = runner.Synthesize("你好，欢迎使用语音合成。");
                Assert.AreEqual(24000, audio.SampleRate); Assert.Greater(audio.Waveform.Length, 2400);
                Assert.True(audio.Waveform.All(value => !float.IsNaN(value) && !float.IsInfinity(value)));
            }
        }

        [Test]
        public void CompleteInstallationRejectsMissingStagesAndMalformedMetadata()
        {
            var model = MNNModelCatalog.CreateDefault().SearchModels("HojoAI").Single();
            var registration = MNNModelRegistry.Find(model.provider, model.repositoryId);
            Assert.True(registration.IsComplete(Model));
            Assert.True(MNN.Unity.Editor.MNNModelRepository.GetDefaultRepository().SearchModels("HojoAI").Single().isInstalled);
            string copy = Path.Combine(Artifacts, "incomplete-install-MNN");
            Directory.CreateDirectory(copy);
            try
            {
                foreach (var file in registration.Files()) File.Copy(Path.Combine(Model, file.Path), Path.Combine(copy, file.Path), true);
                Assert.True(MNNSpeechSynthesizer.HasProfile(copy));
                foreach (string graph in model.mnnFiles)
                {
                    string path = Path.Combine(copy, graph), held = Path.Combine(Artifacts, "held-stage.bin");
                    File.Move(path, held);
                    Assert.False(registration.IsComplete(copy), graph);
                    Assert.False(MNNSpeechSynthesizer.HasProfile(copy), graph);
                    File.Move(held, path);
                }
                File.WriteAllText(Path.Combine(copy, "mnn_hojo_tts.json"), "{}");
                Assert.False(registration.IsComplete(copy));
            }
            finally { Directory.Delete(copy, true); }
        }

        [Test]
        public void BilingualVoicesMatchOfficialGreedySpeech()
        {
            using (var runner = MNNHojoTts.Load(Model, 4, MNNBackendType.CPU))
                foreach (string line in File.ReadAllLines(Path.Combine(Artifacts, "speech-cases.tsv")))
                {
                    string[] fields = line.Split('\t');
                    string text = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(fields[1]));
                    var watch = Stopwatch.StartNew();
                    var audio = runner.Synthesize(text, fields[2], temperature: 0, maximumTokens: 1024);
                    watch.Stop();
                    byte[] bytes = File.ReadAllBytes(Path.Combine(Artifacts, "reference-" + fields[0] + ".bin"));
                    var reference = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, reference, 0, bytes.Length);
                    Assert.AreEqual(reference.Length, audio.Waveform.Length, fields[0]);
                    double signal = 0, error = 0;
                    for (int i = 0; i < reference.Length; ++i)
                    {
                        signal += reference[i] * (double)reference[i];
                        double delta = reference[i] - audio.Waveform[i]; error += delta * delta;
                    }
                    double snr = 10 * Math.Log10(signal / Math.Max(error, 1e-30));
                    TestContext.WriteLine("CASE=" + fields[0] + ";SAMPLES=" + audio.Waveform.Length + ";SECONDS=" + watch.Elapsed.TotalSeconds +
                        ";RTF=" + watch.Elapsed.TotalSeconds / (audio.Waveform.Length / 24000d) + ";SNR_DB=" + snr);
                    Assert.Greater(snr, 35, fields[0]);
                    var output = new byte[audio.Waveform.Length * 4]; Buffer.BlockCopy(audio.Waveform, 0, output, 0, output.Length);
                    File.WriteAllBytes(Path.Combine(Artifacts, "unity-" + fields[0] + ".bin"), output);
                }
        }

        [Test, Category("Network")]
        public void PublishedInstallerDownloadsAndLoadsThePinnedExport()
        {
            var model = MNNModelCatalog.CreateDefault().SearchModels("HojoAI").Single();
            Assert.AreEqual(40, model.revision.Length, "Publish and pin the release first.");
            string target = Path.Combine(Application.streamingAssetsPath, "MNN/Models/Hojo-TTS-Light-40M-V2-Verified-MNN");
            using (var downloader = new MNNModelDownloader())
            {
                string installed = downloader.DownloadAsync(model, target).GetAwaiter().GetResult();
                Assert.True(MNNModelRegistry.Find(model.provider, model.repositoryId).IsComplete(installed));
                using (var runner = MNNSpeechSynthesizer.Load(installed))
                {
                    var audio = runner.Synthesize("Hello, this is a test.");
                    Assert.AreEqual(24000, audio.SampleRate);
                    Assert.Greater(audio.Waveform.Length, 2400);
                    Assert.True(audio.Waveform.All(value => !float.IsNaN(value) && !float.IsInfinity(value)));
                    TestContext.WriteLine("DOWNLOADED=" + installed + ";REVISION=" + model.revision + ";SAMPLES=" + audio.Waveform.Length);
                }
            }
        }

        [Test]
        public void LongerSampledSpeechCompletesAndTokenBudgetNeverReturnsTruncatedAudio()
        {
            using (var runner = MNNHojoTts.Load(Model))
            {
                Assert.Throws<InvalidDataException>(() => runner.Synthesize("Hello, this is a test.", maximumTokens: 1));
                var audio = runner.Synthesize("Welcome to our voice system. It can read longer sentences in a clear and natural voice.");
                Assert.Greater(audio.Waveform.Length, 24000);
                Assert.True(audio.Waveform.All(value => !float.IsNaN(value) && !float.IsInfinity(value)));
                TestContext.WriteLine("LONG_SAMPLED_SAMPLES=" + audio.Waveform.Length);
            }
        }

        [TestCase("Hello, this is a test.", "hojo_en_m_02", 73440, "13cc351ebab8fe54229198250901ba954b33ff642763207535bf3d4e2d5fb81c")]
        [TestCase("你好，欢迎使用语音合成。", "hojo_zh_f_01", 72960, "caa03ab95427d078b5054fda6aeffeb5c389e18bd09c7d3f38de7bb512e56db6")]
        [TestCase("The quick brown fox jumps over the lazy dog.", "hojo_en_f_01", 85920, "007132693ca0aaf9b9e0ff6bf4e70ca52ee7a49b26f8b04fde5cbd9b90faf138")]
        [TestCase("Welcome to our voice system. It can read longer sentences in a clear and natural voice.", "hojo_en_m_02", 147360, "42e3f1e2c1053a0e8081f764cd5820282a2ffc60da800b9e0aff3d9ea226aa89")]
        public void SampledSpeechPreservesPriorSeededWaveforms(string text, string voice, int samples, string expectedHash)
        {
            // Exact fixtures were captured before the sampler optimization using
            // the package's official 3.6.2 macOS arm64 Mono CPU contract.
            using (var runner = MNNHojoTts.Load(Model, 4, MNNBackendType.CPU))
            {
                var audio = runner.Synthesize(text, voice, seed: 42, temperature: .8f);
                Assert.AreEqual(samples, audio.Waveform.Length);
                var bytes = new byte[audio.Waveform.Length * 4];
                Buffer.BlockCopy(audio.Waveform, 0, bytes, 0, bytes.Length);
                using (var hash = SHA256.Create())
                    Assert.AreEqual(expectedHash, BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant());
            }
        }
    }
}
