using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class CosyVoice300MUnitTests
    {
        [Test]
        public void RejectsUndefinedOrConflictingDecoderModesBeforeLoading()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNCosyVoice300M.Load(_root,
                decoderMode: (MNNCosyVoice300MDecoderMode)999));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNCosyVoice300M.Load(_root, metalShapeCacheCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNCosyVoice300M.Load(_root, metalShapeCacheCapacity: 5));
            Assert.Throws<ArgumentException>(() => MNNCosyVoice300M.Load(_root,
                experimentalQuantizedDecoder: true, decoderMode: MNNCosyVoice300MDecoderMode.ExperimentalInt8));
        }

        [Test]
        public void PartialTopKMatchesFullSortIncludingRasEosAndTies()
        {
            for (int seed = 0; seed < 6; ++seed)
            {
                var optimized = new MNNCosyVoice300MSampler(seed);
                var referenceRandom = new Random(seed);
                var inputRandom = new Random(1234 + seed);
                var history = new List<int>();
                for (int step = 0; step < 64; ++step)
                {
                    var logits = new float[4097];
                    for (int i = 0; i < logits.Length; ++i)
                        logits[i] = step % 4 == 0 ? 0 : step % 4 == 1 ? (i % 31) - 15 :
                            step % 4 == 2 ? (float)(inputRandom.NextDouble() * 20 - 10) : (i == step ? 1000 : -1000);
                    int expected = FullSortSample(logits, history, 20, referenceRandom);
                    Assert.AreEqual(expected, optimized.Sample(logits, history, 20), $"seed={seed}, step={step}");
                    // Include repeated winners in the last ten tokens to exercise RAS redraws.
                    history.Add(expected);
                    if (step % 7 == 0) history.Add(step + 1);
                    Assert.AreEqual(referenceRandom.NextDouble(), optimized.Uniform(), "Random draws must match exactly.");
                }
            }
        }

        private static int FullSortSample(float[] logits, List<int> history, int minimum, Random random)
        {
            var probability = new double[4097];
            var order = Enumerable.Range(0, 4097).ToArray();
            double max = logits.Where((_, i) => i != 4096 || history.Count >= minimum).Max();
            double total = 0;
            for (int i = 0; i < 4097; ++i)
            {
                probability[i] = i == 4096 && history.Count < minimum ? 0 : Math.Exp(logits[i] - max);
                total += probability[i];
            }
            for (int i = 0; i < 4097; ++i) probability[i] /= total;
            Array.Sort(order, (a, b) => probability[b] != probability[a] ? probability[b].CompareTo(probability[a]) : a.CompareTo(b));
            double mass = 0; int count = 0;
            do { mass += probability[order[count++]]; } while (count < 25 && mass < .8);
            double pick = random.NextDouble() * mass, cumulative = 0;
            int selected = order[count - 1];
            for (int i = 0; i < count; ++i)
            {
                cumulative += probability[order[i]];
                if (pick < cumulative) { selected = order[i]; break; }
            }
            if (history.Skip(Math.Max(0, history.Count - 10)).Contains(selected))
            {
                pick = random.NextDouble(); cumulative = 0;
                for (int i = 0; i < 4097; ++i)
                {
                    cumulative += probability[i];
                    if (pick < cumulative) return i;
                }
                return Enumerable.Range(0, 4097).Last(i => probability[i] > 0);
            }
            return selected;
        }

        [Test]
        public void AcousticRandomRetryPreservesUniformAndSpareGaussianState()
        {
            var original = new MNNCosyVoice300MSampler(42);
            for (int i = 0; i < 29; ++i) original.Uniform();
            original.Normal(); // Leave the second Box-Muller sample pending.
            var snapshot = original.Fork();
            var second = snapshot.Fork();
            for (int i = 0; i < 100; ++i)
            {
                Assert.AreEqual(original.Normal(), second.Normal());
                Assert.AreEqual(original.Uniform(), second.Uniform());
            }
            var replay = snapshot.Fork();
            var replayAgain = snapshot.Fork();
            for (int i = 0; i < 100; ++i) Assert.AreEqual(replay.Normal(), replayAgain.Normal());
        }

        [Test]
        public void RejectsSaturatedWaveformWithoutRejectingBriefPeaks()
        {
            var pcm = new float[10000];
            pcm[0] = .99f;
            Assert.IsFalse(MNNCosyVoice300M.IsSaturated(pcm));
            for (int i = 0; i < 100; ++i) pcm[i] = i % 2 == 0 ? .99f : -.99f;
            Assert.IsTrue(MNNCosyVoice300M.IsSaturated(pcm));
        }

        private string _root;
        [SetUp]
        public void Setup()
        {
            _root = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? Path.GetFullPath("TestArtifacts~/CosyVoice300M"), "temporary-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }
        [TearDown]
        public void Cleanup() => Directory.Delete(_root, true);

        [Test]
        public void FirstGenerationMarkerRoutesToItsOwnPipelineAndRejectsIncompleteAssets()
        {
            File.WriteAllText(Path.Combine(_root, MNNCosyVoice300MModel.ConfigName), "{\"model\":\"iic/CosyVoice-300M\",\"schema_version\":1,\"sample_rate\":22050}");
            var model = MNNStudioModel.Read(_root);
            Assert.AreEqual(MNNStudioTask.SpeechSynthesis, model.Task);
            Assert.AreEqual(MNNTtsModelProfile.CosyVoice300M, model.TtsProfile);
            Assert.IsTrue(model.IsCosyVoice300M);
            Assert.IsFalse(model.IsCosyVoice);
            Assert.IsFalse(model.HasGenerationPipeline);
            CollectionAssert.AreEqual(new[]{"Default"}, model.TtsVoices);
            foreach (string stage in MNNCosyVoice300MModel.Stages)
            foreach (string suffix in new[]{".mnn", ".mnn.weight"})
            {
                string path = Path.Combine(_root, "fp16", stage + suffix);
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, "fixture");
            }
            Directory.CreateDirectory(Path.Combine(_root, "common"));
            foreach (string name in MNNCosyVoice300MModel.Common) File.WriteAllText(Path.Combine(_root, "common", name), "fixture");
            Assert.IsTrue(model.HasGenerationPipeline, "Layout check is separate from loading graph contents.");
            string configPath = Path.Combine(_root, MNNCosyVoice300MModel.ConfigName);
            File.WriteAllText(configPath, "{\"model\":\"iic/CosyVoice-300M\",\"schema_version\":1,\"sample_rate\":22050,\"decoder_graph\":\"fp16/llm_decode_compact.mnn\"}");
            Assert.IsFalse(model.HasGenerationPipeline, "Final export requires the compact graph and its external weights.");
            string compactPath = Path.Combine(_root, MNNCosyVoice300MModel.CompactGraph);
            File.WriteAllText(compactPath, "fixture");
            Assert.IsFalse(model.HasGenerationPipeline);
            File.WriteAllText(compactPath + ".weight", "fixture");
            Assert.IsTrue(model.HasGenerationPipeline);
            File.WriteAllText(configPath, "{}");
            Assert.IsFalse(model.HasGenerationPipeline, "Missing required metadata must reject the directory without throwing.");
            File.WriteAllText(Path.Combine(_root, MNNCosyVoice300MModel.ConfigName), "{\"model\":\"iic/CosyVoice-300M\",\"schema_version\":1,\"sample_rate\":24000}");
            Assert.IsFalse(model.HasGenerationPipeline);
        }

        [Test]
        public void LegacyCosyVoiceProfileKeepsItsOwnRoute()
        {
            File.WriteAllText(Path.Combine(_root, "config-cpu-cosyvoice-ras.json"), "{}");
            Assert.AreEqual(MNNTtsModelProfile.CosyVoice, MNNTtsModelProfiles.Detect(_root));
        }

        [Test]
        public void FlowInterpolationUsesHalfPixelCentersAndClampsEdges()
        {
            // Independent align_corners=False example: [0, 10] -> [0, 2.5, 7.5, 10].
            var data = Enumerable.Range(0, 160).Select(i => i < 80 ? 0f : 10f).ToArray();
            float[] actual = MNNCosyVoice300M.Interpolate(data, 0, 2, 4);
            float[] expected = {0, 2.5f, 7.5f, 10};
            for (int frame = 0; frame < 4; ++frame)
                for (int c = 0; c < 80; ++c) Assert.AreEqual(expected[frame], actual[frame * 80 + c], 1e-6);
            Assert.Throws<InvalidDataException>(() => MNNCosyVoice300M.Interpolate(data, 1, 2, 4));
        }

        [Test]
        public void SamplerForbidsEarlyEosEvenWhenItHasTheLargestLogit()
        {
            var logits = Enumerable.Repeat(-1000f, 4097).ToArray();
            logits[7] = 1; logits[4096] = 1000;
            var random = new MNNCosyVoice300MSampler(42);
            Assert.AreEqual(7, random.Sample(logits, new List<int>(), 2));
            Assert.AreEqual(7, random.Sample(logits, new List<int>{7}, 2));
            Assert.AreEqual(4096, random.Sample(logits, new List<int>{7, 7}, 2));
            logits[7] = float.NaN;
            Assert.Throws<InvalidDataException>(() => random.Sample(logits, new List<int>(), 2));
        }

        [Test]
        public void FortranNumpyIsConvertedToRowMajorWithoutChangingValues()
        {
            string path = Path.Combine(_root, "fortran.npy");
            string header = "{'descr': '<f4', 'fortran_order': True, 'shape': (2, 3), }\n";
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(new byte[]{147, 78, 85, 77, 80, 89, 1, 0});
                writer.Write((ushort)header.Length); writer.Write(Encoding.ASCII.GetBytes(header));
                foreach (float value in new[]{1f, 4f, 2f, 5f, 3f, 6f}) writer.Write(value);
            }
            CollectionAssert.AreEqual(new[]{1f, 2f, 3f, 4f, 5f, 6f}, MNNNumpy.Floats(path, 2, 3));
        }

        [Test]
        public void NumpyReaderRejectsShapeTypeAndTruncatedData()
        {
            string path = Path.Combine(_root, "values.npy");
            string header = "{'descr': '<f4', 'fortran_order': False, 'shape': (2,), }\n";
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(new byte[]{147, 78, 85, 77, 80, 89, 1, 0});
                writer.Write((ushort)header.Length); writer.Write(Encoding.ASCII.GetBytes(header));
                writer.Write(1.25f); writer.Write(-2.5f);
            }
            CollectionAssert.AreEqual(new[]{1.25f, -2.5f}, MNNNumpy.Floats(path, 2));
            Assert.Throws<InvalidDataException>(() => MNNNumpy.Floats(path, 1, 2));
            byte[] bytes = File.ReadAllBytes(path); Array.Resize(ref bytes, bytes.Length - 1); File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => MNNNumpy.Floats(path, 2));
            File.WriteAllBytes(path, Encoding.ASCII.GetBytes("not a NumPy file"));
            Assert.Throws<InvalidDataException>(() => MNNNumpy.Floats(path, 2));
        }
    }
}
