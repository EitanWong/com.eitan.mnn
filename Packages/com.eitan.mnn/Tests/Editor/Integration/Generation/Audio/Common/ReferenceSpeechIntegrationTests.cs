using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class ReferenceSpeechIntegrationTests
    {
        // Expected IDs were produced by upstream Python tiktoken / Hugging Face
        // tokenizers, not by this managed tokenizer. No inference or downloads.
        [Test, Explicit("Requires only the local release tokenizer files.")]
        public void TokenizersMatchUpstreamGoldenIds()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_SPEECH_ROOT");
            var index = new MNNSpeechTokenizer(Path.Combine(root, "IndexTTS-2.5-mnn"), true);
            var fire = new MNNSpeechTokenizer(Path.Combine(root, "fireredtts3-mnn"), false);
            CollectionAssert.AreEqual(new[]{58839, 220, 48934, 50371, 11, 52581, 56980, 48951, 53719, 56466, 57967, 49592, 51404, 13}, index.Encode("\u4f60\u597d\uff0c\u6b22\u8fce\u4f7f\u7528\u8bed\u97f3\u5408\u6210\u3002", "zh"));
            CollectionAssert.AreEqual(new[]{151876, 151676, 108386, 3837, 100437, 37029, 105761, 106726, 1773, 151677}, fire.Encode("\u4f60\u597d\uff0c\u6b22\u8fce\u4f7f\u7528\u8bed\u97f3\u5408\u6210\u3002", "zh"));
            CollectionAssert.AreEqual(new[]{58838, 309, 311, 257, 19903, 786, 11, 1939, 380, 309, 30}, index.Encode("It's a sunny day, isn't it?", "en"));
            CollectionAssert.AreEqual(new[]{151877, 151676, 2132, 594, 264, 39698, 1899, 11, 4436, 944, 432, 30, 151677}, fire.Encode("It's a sunny day, isn't it?", "en"));
            CollectionAssert.AreEqual(new[]{58838, 472, 922, 20020, 6, 1260, 1045, 12, 22626}, index.Encode("One (two)\u2026 three\u2014four!", "en"));
            CollectionAssert.AreEqual(new[]{151877, 151676, 3966, 320, 19789, 8, 1940, 2326, 2293, 34024, 0, 151677}, fire.Encode("One (two)\u2026 three\u2014four!", "en"));
            CollectionAssert.AreEqual(new[]{58839, 922, 48934, 50371, 6, 1260, 6, 52581, 56980, 6019, 52879, 56443, 11, 49572, 48857, 49607, 30}, index.Encode("\u3010\u4f60\u597d\u3011\u2026\u2026\u201c\u6b22\u8fce\u201d\uff1b\u6d4b\u8bd5\uff1a\u53ef\u4ee5\u5417\uff1f", "zh"));
            CollectionAssert.AreEqual(new[]{151876, 151676, 10904, 108386, 10958, 14053, 2073, 100437, 854, 24968, 81705, 5122, 73670, 101037, 11319, 151677}, fire.Encode("\u3010\u4f60\u597d\u3011\u2026\u2026\u201c\u6b22\u8fce\u201d\uff1b\u6d4b\u8bd5\uff1a\u53ef\u4ee5\u5417\uff1f", "zh"));
            CollectionAssert.AreEqual(new[]{58838, 17345, 31749, 33350, 8324, 13, 2081}, index.Encode("Cafe\u0301 12345.\nYes!", "en"));
            CollectionAssert.AreEqual(new[]{151877, 151676, 34, 2577, 963, 220, 16, 17, 18, 19, 20, 624, 9454, 0, 151677}, fire.Encode("Cafe\u0301 12345.\nYes!", "en"));
            CollectionAssert.AreEqual(new[]{58838, 257, 12849, 222, 65, 40788, 240, 250, 10725, 253, 247}, index.Encode("A\ud83d\ude00B \ud835\udc9c\ud835\udfd9", "en"));
            CollectionAssert.AreEqual(new[]{151877, 151676, 32, 141334, 33, 81250, 240, 250, 56252, 253, 247, 151677}, fire.Encode("A\ud83d\ude00B \ud835\udc9c\ud835\udfd9", "en"));
            Assert.Throws<ArgumentException>(() => index.Encode("你好", "ja"));
            Assert.Throws<ArgumentException>(() => fire.Encode(new string('中', 600), "zh"));
        }

        [TestCase(true), TestCase(false), Explicit("Requires pinned local models and a reference WAV; never downloads weights.")]
        public void SuppliedReferenceGeneratesIntelligibleChinese(bool index)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_SPEECH_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            var reference = MNNReferenceAudio.FromWaveFile(Environment.GetEnvironmentVariable("MNN_TEST_REFERENCE_WAV"));
            string referenceText = "你好，欢迎使用语音合成。";
            string phrase = "你好，今天我们测试本地语音合成。";
            string wav;
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                if (index)
                {
                    using (var model = MNNIndexTts25.Load(Path.Combine(root, "IndexTTS-2.5-mnn")))
                    {
                        Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, reference, cancellationToken: cancel.Token));
                        using (var runningCancel = new CancellationTokenSource(200))
                            Assert.Throws<OperationCanceledException>(() => Task.Run(() => model.Synthesize(phrase, reference, cancellationToken: runningCancel.Token)).GetAwaiter().GetResult());
                        var audio = Task.Run(() => model.Synthesize(phrase, reference)).GetAwaiter().GetResult();
                        var repeat = Task.Run(() => model.Synthesize(phrase, reference)).GetAwaiter().GetResult();
                        CollectionAssert.AreEqual(audio.Waveform, repeat.Waveform, "Same seed must reproduce speech after cancellation/retry.");
                        wav = AdditionalTtsValidation.ValidateAndSave(audio, "indextts-chinese.wav", 22050);
                        model.Dispose(); model.Dispose();
                        Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase, reference));
                    }
                }
                else
                {
                    using (var model = MNNFireRedTts3.Load(Path.Combine(root, "fireredtts3-mnn")))
                    {
                        Assert.Throws<OperationCanceledException>(() => model.Synthesize(phrase, reference, referenceText, cancellationToken: cancel.Token));
                        using (var runningCancel = new CancellationTokenSource(200))
                            Assert.Throws<OperationCanceledException>(() => Task.Run(() => model.Synthesize(phrase, reference, referenceText, cancellationToken: runningCancel.Token)).GetAwaiter().GetResult());
                        var audio = Task.Run(() => model.Synthesize(phrase, reference, referenceText)).GetAwaiter().GetResult();
                        var repeat = Task.Run(() => model.Synthesize(phrase, reference, referenceText)).GetAwaiter().GetResult();
                        CollectionAssert.AreEqual(audio.Waveform, repeat.Waveform, "Same seed must reproduce speech after cancellation/retry.");
                        wav = AdditionalTtsValidation.ValidateAndSave(audio, "fireredtts-chinese.wav", 24000);
                        model.Dispose(); model.Dispose();
                        Assert.Throws<ObjectDisposedException>(() => model.Synthesize(phrase, reference, referenceText));
                    }
                }
            }
            string asrRoot = Environment.GetEnvironmentVariable("MNN_TEST_CHINESE_ASR_ROOT");
            if (string.IsNullOrWhiteSpace(asrRoot)) Assert.Fail("Independent ASR is required.");
            using (var asr = MNNLlm.Load(asrRoot, backendType: MNNBackendType.CPU, cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "reference-speech-asr-cache")))
            {
                string transcript = asr.GenerateMultimodal("请逐字转写音频中的中文内容，只输出原文。", audioPath: wav, maxNewTokens: 96).Text;
                File.WriteAllText(Path.ChangeExtension(wav, "asr.txt"), transcript);
                // The independent generative ASR can paraphrase a greeting. Check
                // distinctive target words, which cannot pass by echoing the reference.
                StringAssert.Contains("语音合成", transcript);
                StringAssert.Contains("今天", transcript); StringAssert.Contains("本地", transcript);
            }
        }
    }
}
