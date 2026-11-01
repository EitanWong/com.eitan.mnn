using System;
using System.IO;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Performance")]
    [Ignore("Waveform std::function marshalling is not implemented by the direct C++ ABI; historical speech measurements are not current support evidence.")]
    public class OmniSpeechPerformanceTests
    {
        [Test]
        public void HighMetalCpuMedia_ReportsStageTimingsAndRealtimeFactorByThreadCount()
        {
            if (!MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal))
                Assert.Ignore("Metal is unavailable on this device.");

            string root = MultimodalTestData.Artifacts;
            Directory.CreateDirectory(root);
            int[] threadCounts = {1, 2, 4, 8};
            const int repeats = 3;
            string[] wavs = new string[threadCounts.Length * repeats];
            for (int i = 0; i < threadCounts.Length; ++i)
            {
                int threads = threadCounts[i];
                using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen2.5-Omni-3B-MNN"), threadCount: threads, precisionMode: MNNPrecisionMode.High, backendType: MNNBackendType.Metal))
                {
                    Assert.AreEqual(MNNBackendType.Metal, model.Backend);
                    double totalSeconds = 0, audioSecondsTotal = 0;
                    for (int repeat = 0; repeat < repeats; ++repeat)
                    {
                        var result = model.GenerateMultimodal("Say hello in one short sentence.", maxNewTokens: 32, generateSpeech: true, maxAudioTokens: 192);
                        Assert.AreEqual(MNNBackendType.CPU, model.MediaBackend);
                        Assert.AreEqual(24000, result.SampleRate);
                        Assert.Greater(result.Waveform.Length, 1000);
                        Assert.Greater(result.TextGenerationMicroseconds, 0);
                        Assert.Greater(result.WaveformGenerationMicroseconds, 0);

                        double audioSeconds = (double)result.Waveform.Length / result.SampleRate;
                        double callSeconds = (result.TextGenerationMicroseconds + result.WaveformGenerationMicroseconds) / 1000000.0;
                        totalSeconds += callSeconds;
                        audioSecondsTotal += audioSeconds;
                        int index = i * repeats + repeat;
                        wavs[index] = Path.Combine(root, "omni-thread-" + threads + "-" + repeat + ".wav");
                        File.WriteAllBytes(wavs[index], MNNStudioAudio.EncodeWave(result.Waveform, result.SampleRate));
                        TestContext.WriteLine("threads=" + threads + "; repeat=" + repeat + "; text_ms=" +
                            (result.TextGenerationMicroseconds / 1000.0).ToString("0") + "; waveform_ms=" +
                            (result.WaveformGenerationMicroseconds / 1000.0).ToString("0") + "; total_ms=" +
                            (callSeconds * 1000).ToString("0") + "; audio_s=" + audioSeconds.ToString("0.00") +
                            "; rtf=" + (callSeconds / audioSeconds).ToString("0.00") + "; text=" + result.Text);
                    }

                    TestContext.WriteLine("THREAD SUMMARY threads=" + threads + "; repeats=" + repeats +
                        "; weighted_rtf=" + (totalSeconds / audioSecondsTotal).ToString("0.00") +
                        "; total_ms=" + (totalSeconds * 1000).ToString("0") + "; audio_s=" + audioSecondsTotal.ToString("0.00"));
                }
            }

            using (var recognizer = MNNLlm.Load(MultimodalTestData.Model("LFM2.5-Audio-1.5B-MNN"), backendType: MNNBackendType.CPU))
            {
                for (int i = 0; i < wavs.Length; ++i)
                {
                    var transcript = recognizer.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: wavs[i], maxNewTokens: 96);
                    int threads = threadCounts[i / repeats];
                    TestContext.WriteLine("ASR threads=" + threads + "; repeat=" + (i % repeats) + "; text=" + transcript.Text);
                    StringAssert.Contains("hello", transcript.Text.ToLowerInvariant());
                }
            }
        }
    }
}
