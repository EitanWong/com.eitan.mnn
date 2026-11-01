using System;
using System.IO;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class CosyVoiceIntegrationTests
    {
        [Test, Explicit("Requires the complete local CosyVoice release; never downloads models.")]
        public void CompleteArchiveInstallsAndSynthesizesOnWorker()
        {
            string staging = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE_ROOT");
            Assert.That(staging, Is.Not.Null.And.Not.Empty);
            if (!MNNCosyVoiceModel.IsComplete(staging))
                MNNCosyVoiceInstaller.Install(staging, CancellationToken.None);
            Assert.IsTrue(MNNCosyVoiceModel.IsComplete(staging));
            using (var model = MNNCosyVoice.Load(staging, backendType: MNNBackendType.CPU))
            {
                var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => model.Synthesize("你好。", cancellationToken: cancellation.Token));
                string phrase = "你好，欢迎使用语音合成。";
                var audio = Task.Run(() => model.Synthesize(phrase)).GetAwaiter().GetResult();
                string wav = AdditionalTtsValidation.ValidateAndSave(audio, "cosyvoice-chinese.wav", 24000);
                TestContext.WriteLine("Audio seconds: " + audio.Waveform.Length / 24000d);
                using (var inFlight = new CancellationTokenSource())
                {
                    inFlight.CancelAfter(100);
                    Assert.Throws<OperationCanceledException>(() => Task.Run(() => model.Synthesize(phrase, cancellationToken: inFlight.Token)).GetAwaiter().GetResult());
                }

                var repeat = Task.Run(() => model.Synthesize(phrase)).GetAwaiter().GetResult();
                Assert.AreEqual(audio.Waveform.Length, repeat.Waveform.Length, "Cancellation must reset LLM state.");
                Assert.Less(audio.Waveform.Zip(repeat.Waveform, (a, b) => Math.Abs(a - b)).Max(), 1e-4);
                string other = AdditionalTtsValidation.ValidateAndSave(Task.Run(() => model.Synthesize("今天天气很好，我们一起去公园散步。", seed: 7)).GetAwaiter().GetResult(), "cosyvoice-second.wav", 24000);
                VerifySpeech(wav, "你好", "语音合成");
                VerifySpeech(other, "天气", "公园", "散步");
                model.Dispose();
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase));
            }
        }

        [Test, Explicit("Requires local CosyVoice/ASR weights and Metal; never downloads models.")]
        public void CpuAndMetalLlmProduceSpeechAndReportSynchronizedRtf()
        {
            Assert.IsTrue(MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal), "Metal is required for this comparison.");
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE_ROOT");
            Assert.IsTrue(MNNCosyVoiceModel.IsComplete(directory));
            string[] phrases = {"你好，欢迎使用语音合成。", "今天天气很好，我们一起去公园散步。"};
            string metrics = Path.Combine(AdditionalTtsValidation.Artifacts, "cosyvoice-cpu-metal.csv");
            File.WriteAllText(metrics, "backend,phrase,run,generation_seconds,audio_seconds,rtf\n");
            foreach (var backend in new[]{MNNBackendType.CPU, MNNBackendType.Metal})
            using (var model = MNNCosyVoice.Load(directory, backendType: backend))
            {
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrases[0], cancellationToken: cancelled.Token));
                }
                for (int phrase = 0; phrase < phrases.Length; ++phrase)
                for (int run = 0; run < 2; ++run)
                {
                    // PCM must be read before stopping: asynchronous GPU submission
                    // by itself is not a completed synthesis benchmark.
                    var audio = Task.Run(() =>
                    {
                        var timer = Stopwatch.StartNew();
                        var generated = model.Synthesize(phrases[phrase]);
                        double seconds = timer.Elapsed.TotalSeconds;
                        double duration = (double)generated.Waveform.Length / generated.SampleRate;
                        string row = string.Join(",", model.Backend, phrase, run,
                            seconds.ToString("R", CultureInfo.InvariantCulture), duration.ToString("R", CultureInfo.InvariantCulture),
                            (seconds / duration).ToString("R", CultureInfo.InvariantCulture));
                        File.AppendAllText(metrics, row + "\n");
                        TestContext.WriteLine(row);
                        return generated;
                    }).GetAwaiter().GetResult();
                    Assert.AreEqual(backend, model.Backend, "A managed CPU fallback is not evidence of Metal execution.");
                    string wav = AdditionalTtsValidation.ValidateAndSave(audio, "cosyvoice-" + backend + "-" + phrase + ".wav", 24000);
                    if (backend == MNNBackendType.Metal && run == 1)
                    {
                        if (phrase == 0)
                            VerifySpeech(wav, "你好", "语音合成");
                        else
                            VerifySpeech(wav, "天气", "公园", "散步");
                    }
                }
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires local CosyVoice and Chinese ASR weights; never downloads models.")]
        public void MultipleSentencesIncludeTheFinalReminder(MNNBackendType backend)
        {
            const string phrase = "今天我们先去图书馆看书，然后去公园散步，最后回家准备晚饭。明天早上还要按时出发，请记得带好雨伞。";
            Assert.Greater(MNNCosyVoice.SplitText(phrase).Length, 1);
            using (var model = MNNCosyVoice.Load(Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE_ROOT"), backendType: backend))
            {
                var audio = Task.Run(() => model.Synthesize(phrase, seed: 7)).GetAwaiter().GetResult();
                string wav = AdditionalTtsValidation.ValidateAndSave(audio, "cosyvoice-multiple-sentences-" + backend + ".wav", 24000);
                VerifySpeech(wav, "图书馆", "晚饭", "明天", "雨伞");
                // Each segment resets the LLM; cancellation/reuse must retain
                // deterministic output across a complete multi-segment request.
                var repeat = Task.Run(() => model.Synthesize(phrase, seed: 7)).GetAwaiter().GetResult();
                Assert.AreEqual(audio.Waveform.Length, repeat.Waveform.Length);
                Assert.Less(audio.Waveform.Zip(repeat.Waveform, (a, b) => Math.Abs(a - b)).Max(), 1e-4);
            }
        }

        private static void VerifySpeech(string wav, params string[] expected)
        {
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT");
            Assert.That(directory, Is.Not.Null.And.Not.Empty, "Independent ASR is required.");
            using (var asr = MNNLlm.Load(directory, cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "cosyvoice-asr-cache"), backendType: MNNBackendType.CPU))
            {
                string text = asr.GenerateMultimodal("请逐字转写音频中的中文内容，只输出原文。", audioPath: wav, maxNewTokens: 96).Text;
                File.WriteAllText(Path.ChangeExtension(wav, "asr.txt"), text);
                foreach (string word in expected)
                    StringAssert.Contains(word, text);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Requires local CosyVoice and LFM2.5-Audio ASR weights; never downloads models.")]
        public void EnglishSentenceCompletesAtReceiverThird(MNNBackendType backend)
        {
            const string phrase = "Every standard statement in English places the doer first, the action second, and the receiver third";
            string directory = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE_ROOT");
            var recordings = new System.Collections.Generic.List<string>();
            using (var model = MNNCosyVoice.Load(directory, backendType: backend))
            {
                float[] first = null;
                for (int run = 0; run < 4; ++run)
                {
                    int seed = run < 2 ? 42 : run == 2 ? 7 : 123;
                    var timer = Stopwatch.StartNew();
                    var audio = Task.Run(() => model.Synthesize(phrase, seed: seed)).GetAwaiter().GetResult();
                    double elapsed = timer.Elapsed.TotalSeconds, duration = (double)audio.Waveform.Length / audio.SampleRate;
                    Assert.AreEqual(backend, model.Backend, "Do not count CPU fallback as GPU evidence.");
                    string name = "cosyvoice-english-" + backend + "-" + run;
                    string wav = AdditionalTtsValidation.ValidateAndSave(audio, name + ".wav", 24000);
                    string metrics = string.Join(",", backend, run, elapsed.ToString("R", CultureInfo.InvariantCulture), duration.ToString("R", CultureInfo.InvariantCulture),
                        (elapsed / duration).ToString("R", CultureInfo.InvariantCulture), model.LastTokenSeconds.ToString("R", CultureInfo.InvariantCulture),
                        model.LastFlowSeconds.ToString("R", CultureInfo.InvariantCulture), model.LastVocoderSeconds.ToString("R", CultureInfo.InvariantCulture));
                    File.WriteAllText(Path.Combine(AdditionalTtsValidation.Artifacts, name + ".csv"), "backend,run,seconds,audio_seconds,rtf,tokens_seconds,flow_seconds,vocoder_seconds\n" + metrics + "\n");
                    TestContext.WriteLine(metrics);
                    if (run == 0)
                    {
                        first = audio.Waveform;
                        continue;
                    }
                    if (run == 1)
                    {
                        Assert.AreEqual(first.Length, audio.Waveform.Length);
                        Assert.Less(first.Zip(audio.Waveform, (x, y) => Math.Abs(x - y)).Max(), 1e-4);
                    }
                    recordings.Add(wav);
                }
            }
            // Use the ASR model's documented system task with audio-only user
            // content. Appended user instructions can make it echo instructions
            // or answer conversationally instead of transcribing the recording.
            string asrDirectory = Environment.GetEnvironmentVariable("MNN_TEST_ASR_ROOT");
            Assert.That(asrDirectory, Is.Not.Null.And.Not.Empty);
            using (var asr = MNNLlm.Load(asrDirectory, backendType: MNNBackendType.CPU,
                cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "english-asr-cache")))
            foreach (string wav in recordings)
            {
                var result = asr.GenerateConversation(new[]{new MNNChatMessage(MNNChatRole.System, "Perform ASR."),
                    new MNNChatMessage(MNNChatRole.User, "<audio>" + wav + "</audio>")}, maxNewTokens: 128);
                Assert.Greater(result.AudioMicroseconds, 0);
                Assert.Greater(result.InputAudioSeconds, 0);
                Assert.IsFalse(result.ReachedTokenLimit);
                string transcript = result.Text.ToLowerInvariant();
                File.WriteAllText(Path.ChangeExtension(wav, "asr.txt"), transcript);
                TestContext.WriteLine(transcript);
                string recognized = System.Text.RegularExpressions.Regex.Replace(transcript, "[^a-z]+", " ").Trim();
                string expected = System.Text.RegularExpressions.Regex.Replace(phrase.ToLowerInvariant(), "[^a-z]+", " ").Trim();
                Assert.AreEqual(expected, recognized, "Verify every word and the final receiver third, without an invented tail.");
            }
        }
    }
}
