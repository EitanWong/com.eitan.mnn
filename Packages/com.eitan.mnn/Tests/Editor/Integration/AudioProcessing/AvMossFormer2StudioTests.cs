using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class AvMossFormer2StudioTests
    {
        private static string ModelDirectory => Path.GetFullPath(Path.Combine("Assets", "StreamingAssets", "MNN", "Models", "AV-MossFormer2-TSE-16K-MNN"));
        [Test]
        public void PreparedFaceUsesOfficialCropAndGrayscaleNormalization()
        {
            var pixels = new byte[224 * 224 * 3];
            for (int y = 56; y < 168; ++y)
                for (int x = 56; x < 168; ++x)
                    for (int c = 0; c < 3; ++c) pixels[(y * 224 + x) * 3 + c] = 128;
            var frame = MNNAvMossFormer2TseVisual.PrepareFrame(new MNNRgbImage(pixels, 224, 224));
            Assert.AreEqual(112 * 112, frame.Length);
            float expected = (128f / 255 - .4161f) / .1688f;
            Assert.That(frame.All(value => Math.Abs(value - expected) < 1e-6), Is.True);
            Assert.Throws<ArgumentException>(() => MNNAvMossFormer2TseVisual.PrepareFrame(new MNNRgbImage(new byte[3], 1, 1)));
        }
        [Test]
        [Explicit("Requires the AV-TSE graph and MNN plugin. Checks temporal alignment through the public extraction API.")]
        public void MissingVisualFramesHoldLastFrameWithoutStretchingTime()
        {
            var audio = new float[16000];
            for (int i = 0; i < audio.Length; ++i) audio[i] = .15f * (float)Math.Sin(i * .137);
            const int pixels = 112 * 112;
            var shortVisual = Enumerable.Repeat(-1f, pixels).Concat(Enumerable.Repeat(1f, pixels)).ToArray();
            var explicitPadding = Enumerable.Repeat(1f, 25 * pixels).ToArray();
            Array.Copy(shortVisual, explicitPadding, shortVisual.Length);
            using (var model = MNNAvMossFormer2Tse.Load(ModelDirectory, 2, MNNBackendType.CPU))
            {
                var shortOutput = model.Extract(audio, shortVisual, 2);
                var paddedOutput = model.Extract(audio, explicitPadding, 25);
                Assert.That(shortOutput.Select((value, i) => Math.Abs(value - paddedOutput[i])).Max(), Is.LessThan(1e-7));
            }
        }
        [Test, Explicit("Requires the AV-TSE graph and MNN plugin; checks the public resampling contract.")]
        public void ExtractionPreservesNonAlignedInputSampleCount()
        {
            var audio = new float[22087];
            for (int i = 0; i < audio.Length; ++i) audio[i] = .1f * (float)Math.Sin(i * .137);
            var visual = new float[25 * 112 * 112];
            using (var model = MNNAvMossFormer2Tse.Load(ModelDirectory, 2, MNNBackendType.CPU))
                Assert.AreEqual(audio.Length, model.Extract(audio, visual, 25, 22050).Length);
        }
        [Test, Explicit("Requires the source-PyTorch audio/video oracle and AV-TSE graph.")]
        public void ExtractedWaveformMatchesOriginalPyTorchOnCpu() => VerifyPyTorchParity(MNNBackendType.CPU);
        [Test, Explicit("Requires the AV-TSE graph and MNN plugin; checks extraction at the chunk boundaries.")]
        public void ExtractionKeepsDecoderSamplesAtBothChunkEdges()
        {
            var audio = Enumerable.Repeat(.1f, 48000).ToArray();
            var visual = new float[75 * 112 * 112];
            float[] frames;
            using (var graph = new MNNGenerationGraph(Path.Combine(ModelDirectory, MNNAvMossFormer2TseConfig.Read(ModelDirectory).graph_fp32), 2,
                backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High))
                frames = graph.Run("target_frames", new MNNGenerationGraph.Input("mixture", audio, 1, 48000),
                    new MNNGenerationGraph.Input("visual", visual, 1, 75, 112, 112));
            using (var model = MNNAvMossFormer2Tse.Load(ModelDirectory, 2, MNNBackendType.CPU))
            {
                var actual = model.Extract(audio, visual, 75);
                for (int i = 0; i < 8; ++i)
                {
                    Assert.AreEqual(frames[i], actual[i], 1e-6, "First decoder frame sample " + i);
                    Assert.AreEqual(frames[frames.Length - 1 - i], actual[actual.Length - 1 - i], 1e-6,
                        "Last decoder frame sample " + i);
                }
            }
        }
        [Test, Explicit("Requires the source-PyTorch audio/video oracle and Metal-capable MNN plugin.")]
        public void ExtractedWaveformMatchesOriginalPyTorchOnMetal() => VerifyPyTorchParity(MNNBackendType.Metal);
        private static void VerifyPyTorchParity(MNNBackendType backend)
        {
            string reference = Environment.GetEnvironmentVariable("MNN_TEST_AV_REFERENCE");
            if (string.IsNullOrWhiteSpace(reference)) Assert.Ignore("Set MNN_TEST_AV_REFERENCE to the generated original-PyTorch oracle.");
            if (backend == MNNBackendType.Metal && !MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore("Metal unavailable.");
            var audio = ReadFloats(Path.Combine(reference, "input.f32"));
            var visual = ReadFloats(Path.Combine(reference, "visual.f32"));
            var expected = ReadFloats(Path.Combine(reference, "enhanced.f32"));
            var loading = System.Diagnostics.Stopwatch.StartNew();
            using (var model = MNNAvMossFormer2Tse.Load(ModelDirectory, 2, backend))
            {
                loading.Stop();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var actual = model.Extract(audio, visual, 75);
                timer.Stop();
                Assert.AreEqual(backend, model.Backend);
                Assert.AreEqual(expected.Length, actual.Length);
                double maximum = expected.Select((value, i) => Math.Abs((double)value - actual[i])).Max();
                double relative = Math.Sqrt(expected.Select((value, i) => Math.Pow((double)value - actual[i], 2)).Sum() / expected.Sum(value => (double)value * value));
                TestContext.WriteLine("Maximum error=" + maximum + "; normalized RMSE=" + relative + "; first ms=" + timer.Elapsed.TotalMilliseconds);
                Assert.That(relative, Is.LessThan(backend == MNNBackendType.CPU ? .0005 : .005));
                Assert.That(maximum, Is.LessThan(.02));
                double edge = Enumerable.Range(0, 16).SelectMany(i => new[] {i, expected.Length - i - 1}).Max(i => Math.Abs((double)expected[i] - actual[i]));
                Assert.That(edge, Is.LessThan(.002), "Chunk edges must contain extracted speech, including the first/last decoder frames.");
                var warm = new double[3];
                for (int run = 0; run < warm.Length; ++run)
                {
                    timer.Restart();
                    var repeated = model.Extract(audio, visual, 75);
                    warm[run] = timer.Elapsed.TotalMilliseconds;
                    Assert.That(repeated.Select((value, i) => Math.Abs((double)value - actual[i])).Max(), Is.LessThan(1e-5),
                        "Repeated extraction must refill audio and visual inputs.");
                    Assert.AreEqual(backend, model.Backend);
                }
                Array.Sort(warm);
                TestContext.WriteLine("Load ms=" + loading.Elapsed.TotalMilliseconds + "; median warm ms=" + warm[1]);
            }
        }
        private static float[] ReadFloats(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var values = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
        [Test]
        [Explicit("Requires the AV-TSE graph and MNN plugin; decodes real frame files and executes a Studio request.")]
        public void StudioLoadsOrderedFaceFramesAndExtractsAudio()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "av-studio-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                WriteFace(Path.Combine(directory, "000001.png"), 255);
                WriteFace(Path.Combine(directory, "000000.png"), 0);
                var visual = MNNStudioAvFrames.Read(directory);
                const int pixels = 112 * 112;
                Assert.AreEqual(2 * pixels, visual.Length);
                Assert.AreEqual(-.4161f / .1688f, visual[0], 1e-6);
                Assert.AreEqual((1f - .4161f) / .1688f, visual[pixels], 1e-6);
                var audio = new float[16000];
                for (int i = 0; i < audio.Length; ++i) audio[i] = .1f * (float)Math.Sin(i * .13);
                string wave = Path.Combine(directory, "mixture.wav");
                File.WriteAllBytes(wave, MNNWaveAudio.EncodePcm16(audio, 16000));
                var model = MNNStudioModel.Read(ModelDirectory);
                Assert.True(model.IsAvMossFormer2Tse);
                Assert.True(model.HasAudioProcessingPipeline);
                using (var backend = MNNStudioNativeBackend.Load(model, 2, false))
                {
                    Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest{Audio = wave}));
                    var result = backend.Run(new MNNStudioRequest{Audio = wave, VisualFrames = visual});
                    Assert.AreEqual(16000, result.SampleRate);
                    Assert.AreEqual(audio.Length, result.Waveform.Length);
                    Assert.That(result.Waveform.All(value => !float.IsNaN(value) && !float.IsInfinity(value)), Is.True);
                    Assert.That(result.BackendDescription, Does.StartWith("AV-MossFormer2 TSE"));
                }
            }
            finally { Directory.Delete(directory, true); }
        }
        private static void WriteFace(string path, byte gray)
        {
            var texture = new UnityEngine.Texture2D(112, 112, UnityEngine.TextureFormat.RGB24, false);
            try
            {
                texture.SetPixels32(Enumerable.Repeat(new UnityEngine.Color32(gray, gray, gray, 255), 112 * 112).ToArray());
                texture.Apply();
                File.WriteAllBytes(path, UnityEngine.ImageConversion.EncodeToPNG(texture));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
