using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class GenerationPipelineUnitTests
    {
        private static readonly int[] NonFiniteBits =
        {
            0x7f800000, unchecked((int)0xff800000), // Infinities.
            0x7fc00000, 0x7f800001, unchecked((int)0xffc12345) // NaN signs and payloads.
        };

        [Test]
        public void GraphValidationRetainsFiniteBoundaryBitsAndRejectsEmptyOutput()
        {
            var bits = new[]{0, int.MinValue, 1, unchecked((int)0x80000001),
                0x007fffff, 0x00800000, 0x7f7fffff, unchecked((int)0xff7fffff)};
            var values = bits.Select(value => BitConverter.ToSingle(BitConverter.GetBytes(value), 0)).ToArray();
            Assert.AreSame(values, MNNGenerationGraph.Validate(values, "finite"));
            CollectionAssert.AreEqual(bits, values.Select(value => BitConverter.ToInt32(BitConverter.GetBytes(value), 0)));
            var error = Assert.Throws<InvalidDataException>(() => MNNGenerationGraph.Validate(Array.Empty<float>(), "empty"));
            StringAssert.Contains("empty", error.Message);
        }

        [TestCase(0), TestCase(512), TestCase(1024)]
        public void GraphValidationRejectsEveryNonFiniteEncodingAtAnyPosition(int position)
        {
            foreach (int bits in NonFiniteBits)
            {
                var values = new float[1025];
                values[position] = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
                var error = Assert.Throws<InvalidDataException>(() => MNNGenerationGraph.Validate(values, "decoder"));
                StringAssert.Contains("decoder", error.Message);
            }
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        public void PocketCacheValidationScansBothHalvesOfEveryPopulatedLayer(int layer)
        {
            const int capacity = 3, steps = 2;
            var state = new List<MNNGenerationGraph.Input>();
            var outputs = new MNNGenerationGraph.Output[8];
            for (int i = 0; i < 6; ++i)
            {
                var values = Enumerable.Repeat(float.NaN, 2 * capacity * 1024).ToArray();
                for (int half = 0; half < 2; ++half)
                for (int index = 0; index < steps * 1024; ++index)
                    values[half * capacity * 1024 + index] = index % 2 == 0 ? float.MaxValue : -float.Epsilon;
                outputs[2 + i] = new MNNGenerationGraph.Output(values, new[]{2, 1, capacity, 16, 64});
                state.Add(new MNNGenerationGraph.Input("state_" + i * 3, new float[values.Length], 2, 1, capacity, 16, 64));
                state.Add(new MNNGenerationGraph.Input("unused", new float[0], 0));
                state.Add(new MNNGenerationGraph.Input("step", new[]{0}, 1));
            }
            // Prefix outputs are intentionally absent: the selected offset must be honored.
            MNNPocketTts.Update(state, outputs, 2, steps);
            for (int i = 0; i < 6; ++i)
            {
                Assert.AreSame(outputs[2 + i].Data, state[i * 3].Values);
                Assert.AreEqual(steps, ((int[])state[i * 3 + 2].Values)[0]);
            }
            var cache = outputs[2 + layer].Values;
            foreach (int half in new[]{0, 1})
            foreach (int position in new[]{0, 1023, steps * 1024 - 1})
            foreach (int bits in NonFiniteBits)
            {
                int index = half * capacity * 1024 + position;
                float original = cache[index];
                cache[index] = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
                Assert.Throws<InvalidDataException>(() => MNNPocketTts.Update(state, outputs, 2, steps),
                    "Layer=" + layer + ", half=" + half + ", position=" + position + ", bits=" + bits);
                cache[index] = original;
            }
            MNNPocketTts.Update(state, outputs, 2, steps); // Future NaN rows remain legal.
            Assert.Throws<InvalidDataException>(() => MNNPocketTts.Update(state, outputs, 2, capacity));
        }

        [Test]
        public void PocketRejectsInvalidExecutionOptionsBeforeAccessingModelsOrNativeCode()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNPocketTts.Load(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNPocketTts.Load(null, 17));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNPocketTts.Load(null, 4, MNNPrecisionMode.High));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNPocketTts.Load(null, 4, MNNPrecisionMode.LowBF16));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNPocketTts.Load(null, 4, (MNNPrecisionMode)(-1)));
        }

        [TestCase(1, 0), TestCase(1, 1), TestCase(3, 2), TestCase(5, 2), TestCase(1000, 3)]
        public void PocketCacheResizePreservesBothPopulatedHalvesAndIsolatesVoiceSnapshot(int capacity, int populatedRows)
        {
            var source = new float[2 * 3 * 1024];
            for (int half = 0; half < 2; ++half)
            for (int i = 0; i < 3 * 1024; ++i)
                source[half * 3 * 1024 + i] = (half + 1) * 10 + i / 1024;
            var cache = new MNNGenerationGraph.Input("state_0", source, 2, 1, 3, 16, 64);
            var resized = MNNPocketTts.ResizeCache(cache, capacity, populatedRows);
            Assert.AreEqual(cache.Name, resized.Name);
            CollectionAssert.AreEqual(new[]{2, 1, capacity, 16, 64}, resized.Shape);
            var actual = (float[])resized.Values;
            Assert.AreEqual(2 * capacity * 1024, actual.Length);
            for (int half = 0; half < 2; ++half)
            for (int i = 0; i < capacity * 1024; ++i)
            {
                float value = actual[half * capacity * 1024 + i];
                if (i < populatedRows * 1024)
                    Assert.AreEqual((half + 1) * 10 + i / 1024, value);
                else if (!float.IsNaN(value))
                    Assert.Fail("Unused rows must start as NaN, including previous request's populated slots.");
            }
            actual[0] = -99;
            Assert.AreEqual(10, source[0], "A request must not modify the cached reference voice.");
            CollectionAssert.AreEqual(new[]{2, 1, 3, 16, 64}, cache.Shape);
        }

        [Test]
        public void PocketCacheResizeRejectsInvalidCapacityStateAndRowBounds()
        {
            var cache = new MNNGenerationGraph.Input("state_0", new float[2 * 3 * 1024], 2, 1, 3, 16, 64);
            Assert.Throws<ArgumentNullException>(() => MNNPocketTts.ResizeCache(null, 3, 0));
            foreach (int capacity in new[]{-1, 0, 1001, int.MaxValue})
                Assert.Throws<ArgumentOutOfRangeException>(() => MNNPocketTts.ResizeCache(cache, capacity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNPocketTts.ResizeCache(cache, 3, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNPocketTts.ResizeCache(cache, 3, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNPocketTts.ResizeCache(cache, 1, 2));
            var wrongShape = new MNNGenerationGraph.Input("state_0", new float[2 * 3 * 1024], 2, 3, 16, 64);
            Assert.Throws<InvalidDataException>(() => MNNPocketTts.ResizeCache(wrongShape, 3, 0));
            var wrongType = new MNNGenerationGraph.Input("state_0", new int[2 * 3 * 1024], 2, 1, 3, 16, 64);
            Assert.Throws<InvalidDataException>(() => MNNPocketTts.ResizeCache(wrongType, 3, 0));
        }

        [Test]
        public void HalfTableReadsBoundaryValuesAndRejectsInvalidAssets()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT") ?? Path.GetFullPath("TestArtifacts~/GenerationUnitTests");
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "half-table-" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                ushort[] bits = {0, 0x8000, 1, 0x03ff, 0x0400, 0x3c00, 0xc000, 0x7bff};
                var bytes = new byte[bits.Length * 2]; Buffer.BlockCopy(bits, 0, bytes, 0, bytes.Length);
                File.WriteAllBytes(path, bytes);
                CollectionAssert.AreEqual(new[]{0f, -0f, (float)Math.Pow(2, -24), (float)(1023 * Math.Pow(2, -24)), (float)Math.Pow(2, -14), 1f, -2f, 65504f}, MNNHalfTable.Load(path, 2, 4));
                using (var table = new MNNHalfTable(path, 2, 4))
                {
                    CollectionAssert.AreEqual(new[]{(float)Math.Pow(2, -14), 1f, -2f, 65504f}, table.Row(1));
                    Assert.Throws<InvalidDataException>(() => table.Row(2));
                }
                Assert.Throws<InvalidDataException>(() => MNNHalfTable.Load(path, 2, 5));
                File.WriteAllBytes(path, new byte[]{0, 0x7c});
                Assert.Throws<InvalidDataException>(() => MNNHalfTable.Load(path, 1, 1));
                File.WriteAllBytes(path, new byte[]{1, 0x7c});
                Assert.Throws<InvalidDataException>(() => MNNHalfTable.Load(path, 1, 1));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void SupertonicNormalizationAndUnsupportedCharacters()
        {
            Assert.AreEqual("Hello at home.", MNNSupertonic.Normalize("  Hello @ home 😀  "));
            Assert.Throws<ArgumentException>(() => MNNSupertonic.Normalize("😀"));
            Assert.Throws<NotSupportedException>(() => MNNSupertonic.Encode("中", Enumerable.Repeat(-1, 65536).ToArray()));
        }

        [TestCase("...?! ;: -- () \"\" ** ++ $$ %% &&")]
        [TestCase("😀🎉👨‍👩‍👧‍👦")]
        [TestCase("\u200b\ufeff\0\u0001")]
        public void SupertonicRejectsInputWithoutSpeech(string text)
        {
            Assert.Throws<ArgumentException>(() => MNNSupertonic.Normalize(text));
        }

        [Test]
        public void SupertonicNormalizesInvisibleCharactersAndChecksWholeVocabulary()
        {
            Assert.AreEqual("Hello world. End.", MNNSupertonic.Normalize("Hello\u200bworld.\ufeff\0\u0001\r\n\tEnd. 👨‍👩‍👧‍👦"));
            Assert.AreEqual("The price is twelve point five zero dollars, or twenty five percent off.", MNNSupertonic.Normalize("The price is $12.50, or 25% off."));
            Assert.AreEqual("zero zero seven, one two three four five six seven eight nine zero, three point one four.", MNNSupertonic.Normalize("007, 1234567890, 3.14"));
            Assert.Throws<ArgumentException>(() => MNNSupertonic.Normalize("Hello\ud800"));
            int[] indexer = Enumerable.Repeat(-1, 65536).ToArray();
            indexer['a'] = 1;
            Assert.Throws<NotSupportedException>(() => MNNSupertonic.Encode("a中", indexer));
        }

        [Test]
        public void SupertonicRejectsCorruptAudioRatherThanClippingIt()
        {
            Assert.Throws<InvalidDataException>(() => MNNSupertonic.PrepareWaveform(new[]{0f, 4.8f, -.1f}, 3));
            Assert.Throws<InvalidDataException>(() => MNNSupertonic.PrepareWaveform(new[]{float.NaN}, 1));
            Assert.Throws<InvalidDataException>(() => MNNSupertonic.PrepareWaveform(new[]{float.PositiveInfinity}, 1));
            Assert.Throws<InvalidDataException>(() => MNNSupertonic.PrepareWaveform(new[]{0f}, 3072));
            var wave = new[]{0f, 1.2f, -.6f};
            MNNSupertonic.PrepareWaveform(wave, wave.Length);
            Assert.That(wave[1], Is.EqualTo(1).Within(.00001));
            Assert.That(wave[2], Is.EqualTo(-.5).Within(.00001));
        }

        [Test]
        public void SupertonicSplitsLongTextAtSentencesAndWordsAndJoinsWithoutClicks()
        {
            string sentence = "The price is $12.50, or 25% off. The capital of France is Paris.";
            string normalized = MNNSupertonic.Normalize(string.Join(" ", Enumerable.Repeat(sentence, 7)));
            string[] parts = MNNSupertonic.SplitText(normalized);
            Assert.Greater(parts.Length, 1);
            Assert.True(parts.All(p => p.Length <= 161));
            Assert.AreEqual(normalized, string.Join(" ", parts));
            string noStops = string.Join(" ", Enumerable.Repeat("mountains", 45));
            string[] words = MNNSupertonic.SplitText(noStops + ".");
            Assert.True(words.All(p => p.Length <= 161 && !p.Contains("mountai.")));
            Assert.Throws<ArgumentException>(() => MNNSupertonic.SplitText(new string('a', 490) + "."));
            var first = Enumerable.Repeat(.5f, 2000).ToArray();
            var second = Enumerable.Repeat(-.5f, 2000).ToArray();
            var joined = MNNSupertonic.JoinWaveforms(new[]{first, second}, 44100);
            Assert.AreEqual(4000 + 3528, joined.Length);
            Assert.AreEqual(0, joined[0]);
            Assert.AreEqual(0, joined[1999]);
            Assert.AreEqual(0, joined[2000 + 3528]);
            Assert.AreEqual(0, joined[joined.Length - 1]);
            Assert.That(joined.Skip(2000).Take(3528), Is.All.Zero);
            Assert.AreEqual(.5f, joined[500]);
            Assert.AreEqual(-.5f, joined[2000 + 3528 + 500]);
            Assert.AreEqual(.5f, first[0], "Joining must not modify owned segment buffers.");
        }

        [Test]
        public void SeededNoiseIsRepeatableAndNonDegenerate()
        {
            var a = MNNGenerationMath.Gaussian(10000, 42);
            CollectionAssert.AreEqual(a, MNNGenerationMath.Gaussian(10000, 42));
            Assert.That(a.Average(), Is.InRange(-.05, .05));
            Assert.That(a.Select(v => v * v).Average(), Is.InRange(.95, 1.05));
        }

        [TestCase("01"), TestCase("1."), TestCase("+2"), TestCase("1e"), TestCase("{\"x\":1,}"), TestCase("[1,]")]
        public void ModelMetadataRejectsMalformedNumbersAndStructures(string json)
        {
            Assert.Throws<InvalidDataException>(() => MNNModelData.Parse(json));
        }

        [Test]
        public void MetadataRejectsFractionalShape()
        {
            Assert.Throws<InvalidDataException>(() => MNNModelData.Shape(MNNModelData.Parse("[1,2.5]")));
        }

        [Test]
        public void PlmsZeroNoisePreservesAnalyticalScalingAndRejectsOutOfOrder()
        {
            var scheduler = new MNNPlmsScheduler(20);
            Assert.AreEqual(951, scheduler.Timesteps[0]);
            Assert.AreEqual(1, scheduler.Timesteps[19]);
            Assert.Throws<ArgumentException>(() => scheduler.Step(new[]{1f}, new[]{0f}, 1));
            var first = scheduler.Step(new[]{1f}, new[]{0f}, 0);
            Assert.Greater(first[0], 1);
            var corrected = scheduler.Step(first, new[]{0f}, 1);
            Assert.That(corrected[0], Is.EqualTo(first[0]).Within(.00001));
        }

        [Test]
        public void GeneratedImageTexturePreservesTopLeftOrientation()
        {
            var image = new MNNGeneratedImage(new byte[]{255, 0, 0, 0, 0, 255}, 1, 2, "test", 42);
            var texture = MNNStudioImage.CreateTexture(image);
            try
            {
                Assert.AreEqual(Color.blue, texture.GetPixel(0, 0));
                Assert.AreEqual(Color.red, texture.GetPixel(0, 1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void GraphInputRejectsShapeMismatch()
        {
            Assert.Throws<ArgumentException>(() => new MNNGenerationGraph.Input("input", new float[2], 1, 3));
        }
    }
}
