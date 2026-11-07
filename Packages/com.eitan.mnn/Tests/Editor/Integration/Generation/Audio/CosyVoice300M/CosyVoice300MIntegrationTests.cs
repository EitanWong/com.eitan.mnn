using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class CosyVoice300MIntegrationTests
    {
        private static string Root => Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
        private static void RequireAssets()
        {
            if (string.IsNullOrEmpty(Root)) Assert.Ignore("Set MNN_TEST_COSYVOICE300M_ROOT to the local converted export; no downloads.");
            Assert.IsTrue(MNNCosyVoice300MModel.IsComplete(Root));
        }

        [Test]
        public void WhisperTokenizerMatchesIndependentReference()
        {
            RequireAssets();
            var tokenizer = new MNNCosyVoice300MTokenizer(Path.Combine(Root, "common"));
            CollectionAssert.AreEqual(new[]{26410,171,120,234,28566,17699,22982,9254,5233,255,18034,14245,11336,1543}, tokenizer.Encode("你好，欢迎使用语音合成。"));
            CollectionAssert.AreEqual(new[]{2278,23831,5214,307,2108,264,4838,11,958,281,264,2183,3398,13}, tokenizer.Encode("The nearest station is across the street, next to the post office."));
            CollectionAssert.AreEqual(new[]{40,478,1919,0,10930,2131,171,120,234,13217,34466,13,198,31002,1622,13}, tokenizer.Encode("I'm ready! 你好，world 123.\nNext line."));
            CollectionAssert.AreEqual(new[]{50257}, tokenizer.Encode("<|endoftext|>"));
        }

        [TestCase(MNNBackendType.CPU, false)]
        [TestCase(MNNBackendType.Auto, false)]
        [TestCase(MNNBackendType.Auto, true)]
        public void ActualWeightsSynthesizeAndReuseOnWorker(MNNBackendType backend, bool quantized)
        {
            RequireAssets();
            if (quantized && !File.Exists(Path.Combine(Root, MNNCosyVoice300MModel.QuantizedGraph)))
            {
                Assert.Throws<FileNotFoundException>(() => MNNCosyVoice300M.Load(Root, acousticBackend: backend, experimentalQuantizedDecoder: true));
                return; // The optimized distribution intentionally excludes rejected quantization.
            }
            Directory.CreateDirectory(AdditionalTtsValidation.Artifacts);
            string csv = Path.Combine(AdditionalTtsValidation.Artifacts, "unity-timings.csv");
            if (!File.Exists(csv)) File.WriteAllText(csv, "requested,quantized,flow,vocoder,phrase,run,load_seconds,token_seconds,flow_seconds,vocoder_seconds,seconds,audio_seconds,rtf,tokens\n");
            Task.Run(() =>
            {
                var clock = Stopwatch.StartNew();
                var model = MNNCosyVoice300M.Load(Root, acousticBackend: backend, experimentalQuantizedDecoder: quantized);
                double loading = clock.Elapsed.TotalSeconds;
                try
                {
                    using (var cancelled = new CancellationTokenSource())
                    {
                        cancelled.Cancel();
                        Assert.Throws<OperationCanceledException>(() => model.Synthesize("你好。", cancellationToken: cancelled.Token));
                    }
                    string[] phrases = {"你好，欢迎使用语音合成。", "The nearest station is across the street, next to the post office."};
                    for (int phrase = 0; phrase < phrases.Length; ++phrase)
                    for (int run = 0; run < 2; ++run)
                    {
                        var audio = model.Synthesize(phrases[phrase]);
                        string tag = backend + (quantized ? "-q4" : "-fp16") + "-" + phrase + "-" + run;
                        AdditionalTtsValidation.ValidateAndSave(audio, "unity-" + tag + ".wav", 22050);
                        string row = string.Join(",", backend, quantized, model.FlowBackend, model.VocoderBackend, phrase, run,
                            loading.ToString("R", CultureInfo.InvariantCulture), model.LastTokenSeconds.ToString("R", CultureInfo.InvariantCulture),
                            model.LastFlowSeconds.ToString("R", CultureInfo.InvariantCulture), model.LastVocoderSeconds.ToString("R", CultureInfo.InvariantCulture),
                            model.LastSynthesisSeconds.ToString("R", CultureInfo.InvariantCulture), (audio.Waveform.Length / 22050d).ToString("R", CultureInfo.InvariantCulture),
                            model.LastRealTimeFactor.ToString("R", CultureInfo.InvariantCulture), model.LastSpeechTokens);
                        File.AppendAllText(csv, row + "\n"); TestContext.WriteLine(row);
                    }
                }
                finally { model.Dispose(); }
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Synthesize("你好。"));
            }).GetAwaiter().GetResult();
        }

        [Test]
        public void StudioDiscoversLoadsAndGeneratesActualAudio()
        {
            RequireAssets();
            var model = MNNStudioModel.Read(Root);
            Assert.IsTrue(model.IsCosyVoice300M);
            Assert.IsFalse(model.IsCosyVoice);
            Assert.IsTrue(MNNStudioTasks.Get((int)MNNStudioTask.SpeechSynthesis).CanRun(model));
            var loader = MNNStudioNativeBackend.CreateLoader(model, 4, false);
            var result = Task.Run(() =>
            {
                using (var backend = loader())
                    return backend.Run(new MNNStudioRequest {Prompt = "你好，欢迎使用语音合成。", Seed = 42});
            }).GetAwaiter().GetResult();
            Assert.AreEqual(22050, result.SampleRate);
            StringAssert.Contains("CosyVoice-300M-MNN", result.BackendDescription);
            StringAssert.Contains("Text/AR ", result.BackendDescription);
            StringAssert.Contains("HiFT/DSP ", result.BackendDescription);
            AdditionalTtsValidation.ValidateAndSave(new MNNGeneratedAudio(result.Waveform, result.SampleRate, result.Text), "unity-studio.wav", 22050);
        }
    }
}
