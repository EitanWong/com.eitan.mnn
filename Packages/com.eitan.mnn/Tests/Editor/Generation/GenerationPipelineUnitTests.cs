using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class GenerationPipelineUnitTests
    {
        [Test]
        public void SupertonicNormalizationAndUnsupportedCharacters()
        {
            Assert.AreEqual("Hello at home.", MNNSupertonic.Normalize("  Hello @ home 😀  "));
            Assert.Throws<ArgumentException>(() => MNNSupertonic.Normalize("😀"));
            Assert.Throws<NotSupportedException>(() => MNNSupertonic.Encode("中", Enumerable.Repeat(-1, 65536).ToArray()));
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
