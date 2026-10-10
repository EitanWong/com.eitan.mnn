using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class AccelerationIntegrationTests
    {
        private static string Affine => Path.Combine(PackageInfo.FindForAssembly(typeof(MNNInterpreter).Assembly).resolvedPath, "Tests/Fixtures/affine.mnn");
        [Test]
        public void AutoSessionReportsActualDeviceAndProducesKnownValues()
        {
            var preferred = MNNAcceleration.PreferredBackend;
            TestContext.WriteLine("Preferred backend=" + preferred);
            using (var interpreter = MNNInterpreter.CreateFromFile(Affine))
            using (var session = interpreter.CreateSession(new MNNSessionConfig()))
            {
                Assert.AreEqual(MNNBackendType.Auto, session.RequestedBackend);
                Assert.AreEqual(preferred, session.ActualBackend);
                session.GetInput().CopyFromArray(new[]{1f, 2f, 3f, 4f});
                session.Run();
                CollectionAssert.AreEqual(new[]{3f, 5f, 7f, 9f}, session.GetOutput().CopyToArray<float>());
                // Returned configuration must not allow callers to falsify diagnostics.
                session.Config.BackendType = MNNBackendType.CUDA;
                Assert.AreEqual(preferred, session.ActualBackend);
            }
        }

        [Test]
        public void UnavailableDeviceFallsBackToCpuAndStillComputes()
        {
            if (MNNAcceleration.IsBackendAvailable(MNNBackendType.CUDA))
                Assert.Ignore("CUDA is available on this device.");
            using (var interpreter = MNNInterpreter.CreateFromFile(Affine))
            using (var session = interpreter.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.CUDA}))
            {
                Assert.AreEqual(MNNBackendType.CPU, session.ActualBackend);
                session.GetInput().CopyFromArray(new[]{-1f, 0f, 1f, 2f});
                session.Run();
                CollectionAssert.AreEqual(new[]{-1f, 1f, 3f, 5f}, session.GetOutput().CopyToArray<float>());
            }
        }

        [Test]
        public void ExpressUsesSelectedDeviceInsteadOfForcingCpu()
        {
            using (var graph = new MNNGenerationGraph(Affine, 2, express: true))
            {
                Assert.AreEqual(MNNAcceleration.PreferredBackend, graph.Backend);
                CollectionAssert.AreEqual(new[]{3f, 5f, 7f, 9f}, graph.Run("output", new MNNGenerationGraph.Input("input", new[]{1f, 2f, 3f, 4f}, 1, 4)));
            }
        }

        [Test]
        public void LlmDefaultPrefersGpuAndAnswersArithmetic()
        {
            using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen3.5-0.8B-MNN")))
            {
                Assert.AreEqual(MNNAcceleration.PreferredBackend, model.Backend);
                var result = model.Generate("What is 1 + 1? Answer with just the number.", 16);
                TestContext.WriteLine("Configured backend=" + model.Backend + "; output=" + result.Text);
                StringAssert.Contains("2", result.Text);
            }
        }

        [Test]
        public void RequestedGpuEmbeddingPreservesRetrievalOrderThroughCompatibilityPolicy()
        {
            using (var model = MNNEmbedding.Load(MultimodalTestData.Model("Qwen3-Embedding-0.6B-MNN"), precisionMode: MNNPrecisionMode.High, backendType: MNNBackendType.Metal))
            {
                var query = model.Encode("Instruct: Given a web search query, retrieve relevant passages that answer the query\nQuery: What is the capital of France?");
                var good = model.Encode("Paris is the capital city of France.");
                var bad = model.Encode("Bananas are yellow tropical fruit.");
                double Cosine(float[] values) => query.Zip(values, (a, b) => (double)a * b).Sum() / Math.Sqrt(query.Sum(a => (double)a * a) * values.Sum(a => (double)a * a));
                TestContext.WriteLine($"Backend={model.Backend}; relevant={Cosine(good)}; unrelated={Cosine(bad)}");
                Assert.Greater(Cosine(good), Cosine(bad) + .05);
            }
        }

        [Test]
        public void DefaultOmniSpeechUsesCompatibleBackendAndIsRecognizedByCpuAsr()
        {
            string wav = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT"), "omni-hybrid-gpu.wav");
            using (var model = MNNLlm.Load(MultimodalTestData.Model("Qwen2.5-Omni-3B-MNN")))
            {
                var result = model.GenerateMultimodal("Say hello in one short sentence.", maxNewTokens: 32, generateSpeech: true, maxAudioTokens: 192);
                Assert.AreEqual(MNNAcceleration.PreferredBackend, model.Backend);
                Assert.AreEqual(MNNBackendType.CPU, model.MediaBackend);
                TestContext.WriteLine("Omni backend=" + model.Backend + "; text=" + result.Text);
                File.WriteAllBytes(wav, MNN.Unity.Editor.MNNStudioAudio.EncodeWave(result.Waveform, result.SampleRate));
            }

            using (var recognizer = MNNLlm.Load(MultimodalTestData.Model("LFM2.5-Audio-1.5B-MNN"), backendType: MNNBackendType.CPU))
            {
                string text = recognizer.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: wav, maxNewTokens: 96).Text;
                TestContext.WriteLine("CPU recognizer=" + text);
                StringAssert.Contains("hello", text.ToLowerInvariant());
            }
        }

        [Test]
        public void LargeFiniteInputRemainsFiniteWithAutomaticPrecisionOrCpuRetry()
        {
            using (var graph = new MNNGenerationGraph(Affine, 2))
            {
                var input = new[]{40000f, 50000f, -40000f, -50000f};
                var result = graph.Run("output", new MNNGenerationGraph.Input("input", input, 1, 4));
                TestContext.WriteLine("Backend after range retry=" + graph.Backend);
                for (int i = 0; i < input.Length; ++i)
                    Assert.That(result[i], Is.EqualTo(input[i] * 2 + 1).Within(2));
            }
        }

        [Test]
        public void PiperAcceleratedSpeechIsRecognizedByCpuAsr()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            string wav = Path.Combine(root, "piper-gpu-cpu-recognizer.wav");
            using (var speaker = MNNPiper.Load(GenerationTestData.Model("piper-voices-MNN")))
            {
                var audio = speaker.Synthesize("The capital of France is Paris.");
                TestContext.WriteLine("Piper backend=" + speaker.Backend);
                File.WriteAllBytes(wav, MNN.Unity.Editor.MNNStudioAudio.EncodeWave(audio.Waveform, audio.SampleRate));
            }

            using (var recognizer = MNNLlm.Load(MultimodalTestData.Model("LFM2.5-Audio-1.5B-MNN"), backendType: MNNBackendType.CPU))
            {
                string text = recognizer.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: wav, maxNewTokens: 96).Text;
                TestContext.WriteLine("CPU recognizer=" + text);
                StringAssert.Contains("paris", text.ToLowerInvariant());
                StringAssert.Contains("france", text.ToLowerInvariant());
            }
        }

        [Test]
        public void StableDiffusionCpuAndAutoComparison()
        {
            foreach (var backend in new[]{MNNBackendType.Auto, MNNBackendType.CPU})
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                using (var model = MNNStableDiffusion.Load(GenerationTestData.Model("stable-diffusion-v1-5-mnn"), backendType: backend))
                {
                    long load = timer.ElapsedMilliseconds;
                    timer.Restart();
                    var image = model.Generate("A red bicycle beside a white wall, photograph", steps: 2);
                    long first = timer.ElapsedMilliseconds;
                    timer.Restart();
                    var repeat = model.Generate("A red bicycle beside a white wall, photograph", steps: 2);
                    long warm = timer.ElapsedMilliseconds;
                    TestContext.WriteLine($"SD 2 steps requested={backend}; stages={string.Join(",", model.StageBackends.Select(pair => pair.Key + "=" + pair.Value))}; load_ms={load}; first_ms={first}; warm_ms={warm}");
                    Assert.AreEqual(512 * 512 * 3, image.Rgb.Length);
                    Assert.Greater(image.Rgb.Distinct().Count(), 200);
                    Assert.LessOrEqual(image.Rgb.Zip(repeat.Rgb, (a, b) => Math.Abs(a - b)).Max(), 1);
                }
            }
        }

        [Test]
        public void StableDiffusionDefaultUsesGpuStagesAndProducesImage()
        {
            using (var model = MNNStableDiffusion.Load(GenerationTestData.Model("stable-diffusion-v1-5-mnn")))
            {
                foreach (var pair in model.StageBackends)
                {
                    TestContext.WriteLine(pair.Key + "=" + pair.Value);
                    Assert.AreEqual(MNNAcceleration.PreferredBackend, pair.Value);
                }

                var timer = System.Diagnostics.Stopwatch.StartNew();
                var image = model.Generate("A red bicycle beside a white wall, photograph", steps: 2);
                TestContext.WriteLine("Generate ms=" + timer.ElapsedMilliseconds);
                Assert.AreEqual(512 * 512 * 3, image.Rgb.Length);
                Assert.Greater(image.Rgb.Distinct().Count(), 200);
                Assert.That(image.Rgb.Average(value => (double)value), Is.InRange(10, 245));
                var root = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
                if (!string.IsNullOrEmpty(root))
                {
                    Directory.CreateDirectory(root);
                    var texture = MNN.Unity.Editor.MNNStudioImage.CreateTexture(image);
                    try
                    {
                        File.WriteAllBytes(Path.Combine(root, "sd15-auto.png"), texture.EncodeToPNG());
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
            }
        }
    }
}
