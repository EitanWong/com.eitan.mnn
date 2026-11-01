using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MNNGenerationGraphTests
    {
        [TestCase("float")]
        [TestCase("int")]
        [TestCase("long")]
        [TestCase("byte")]
        public void RecurrentStateRoundTripPreservesNativeTypeAndExtremeValues(string kind)
        {
            Array data = kind == "float" ? (Array)new[] { float.MinValue, 0f, float.MaxValue }
                : kind == "int" ? new[] { int.MinValue, 16777217, int.MaxValue }
                : kind == "long" ? (Array)new[] { long.MinValue, 9007199254740993L, long.MaxValue }
                : new byte[] { 0, 1, byte.MaxValue };
            var output = new MNNGenerationGraph.Output(data, new[] { 1, 3 });
            var input = output.AsInput("next_state");

            Assert.AreEqual("next_state", input.Name);
            Assert.AreEqual(data.GetType(), input.Values.GetType());
            CollectionAssert.AreEqual(data, input.Values);
            CollectionAssert.AreEqual(new[] { 1, 3 }, input.Shape);
            Assert.False(input.ShapeOnly);
        }

        [Test]
        public void FloatConversionPreservesExactBoundaryAndByteValues()
        {
            CollectionAssert.AreEqual(new[] { -16777216f, 0f, 16777216f },
                new MNNGenerationGraph.Output(new[] { -16777216, 0, 16777216 }, new[] { 3 }).Values);
            CollectionAssert.AreEqual(new[] { -16777216f, 0f, 16777216f },
                new MNNGenerationGraph.Output(new[] { -16777216L, 0L, 16777216L }, new[] { 3 }).Values);
            CollectionAssert.AreEqual(new[] { 0f, 255f },
                new MNNGenerationGraph.Output(new byte[] { 0, 255 }, new[] { 2 }).Values);
        }

        [TestCase(int.MinValue)]
        [TestCase(-16777217)]
        [TestCase(16777217)]
        [TestCase(int.MaxValue)]
        public void ExplicitFloatConversionRejectsUnsafeIntegerRange(int value)
        {
            var output = new MNNGenerationGraph.Output(new[] { value }, new[] { 1 });
            Assert.Throws<InvalidDataException>(() => { var unused = output.Values; });
            Assert.AreEqual(value, ((int[])output.AsInput("state").Values)[0]);
        }

        [TestCase(long.MinValue)]
        [TestCase(long.MaxValue)]
        public void ExtremeInt64StateNeverPassesThroughFloat(long value)
        {
            var output = new MNNGenerationGraph.Output(new[] { value }, Array.Empty<int>());
            Assert.Throws<InvalidDataException>(() => { var unused = output.Values; });
            Assert.AreEqual(value, ((long[])output.AsInput("state").Values)[0]);
        }

        [Test]
        public void EmptyRecurrentDimensionAndRankZeroScalarRemainDistinct()
        {
            var empty = new MNNGenerationGraph.Output(Array.Empty<float>(), new[] { 1, 128, 0 });
            Assert.IsEmpty(empty.Values);
            var input = empty.AsInput("empty_state");
            CollectionAssert.AreEqual(new[] { 1, 128, 0 }, input.Shape);
            Assert.IsEmpty(input.Values);
            Assert.False(input.ShapeOnly);

            var scalar = new MNNGenerationGraph.Output(new[] { 17 }, Array.Empty<int>()).AsInput("scalar_state");
            Assert.IsEmpty(scalar.Shape);
            CollectionAssert.AreEqual(new[] { 17 }, scalar.Values);
            Assert.Throws<ArgumentException>(() => new MNNGenerationGraph.Output(Array.Empty<int>(), Array.Empty<int>()));
        }

        [Test]
        public void InvalidOutputMetadataIsRejectedBeforeStateReuse()
        {
            Assert.Throws<ArgumentException>(() => new MNNGenerationGraph.Output(null, new[] { 1 }));
            Assert.Throws<ArgumentException>(() => new MNNGenerationGraph.Output(new float[1], null));
            Assert.Throws<ArgumentException>(() => new MNNGenerationGraph.Output(new float[1], new[] { -1 }));
            Assert.Throws<ArgumentException>(() => new MNNGenerationGraph.Output(new float[2], new[] { 1, 3 }));
            Assert.Throws<NotSupportedException>(() => new MNNGenerationGraph.Output(new double[1], new[] { 1 }));
            Assert.Throws<OverflowException>(() => new MNNGenerationGraph.Output(Array.Empty<float>(), new[] { int.MaxValue, 2 }));
        }
    }
}
