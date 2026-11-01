using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class SourceRealFftTests
    {
        // Original torch.fft.rfft, Torch 2.8.0 a1cb3cc05d46d198467bebbb6e8fba50a325d4e7,
        // CPU1. These exact source words complement the independent double DFT.
        private static readonly string[] SourceWords =
        {
            "bf000000,00000000,beff8c0c,bd090af5,befbb3e0,bdd195b8,bee03560,be8a25c0,3f200000,bfc12318,3f200000,bf200000,c04fb46c,3e1bc775,c0500000,00000000",
            "bf700000,00000000,bf700022,bd49623c,bf6fffee,be18efba,bf700011,bebedfe8,bf700000,c01a827a,bf700000,bf800000,bf6ffffe,3e17f919,bf700000,00000000",
            "bfa80000,00000000,bfa804fb,bbfca820,bfa82dad,bcbdc944,bfa8fccd,bd5e3ea8,4003ae66,be945a6f,40280000,bf300000,c04c0d1e,bba06ee0,c04c0000,00000000",
            "bf500000,00000000,bf500328,39c71e30,bf501b83,3a9686a8,bf509a46,3b32b8f0,bf880000,c007322a,40340000,bf600000,bf880560,bbeeb9bd,bf880000,00000000"
        };

        [TestCase(400), TestCase(640), TestCase(1680), TestCase(2048)]
        public void ForwardMatchesIndependentDft(int length)
        {
            var input = Signal(length); var real = new float[length / 2 + 1]; var imaginary = new float[real.Length];
            new MNNSourceRealFft(length).Forward((float[])input.Clone(), real, imaginary);
            double error = 0, energy = 0;
            for (int bin = 0; bin < real.Length; ++bin)
            {
                double expectedReal = 0, expectedImaginary = 0;
                for (int i = 0; i < length; ++i)
                {
                    double phase = -2 * Math.PI * bin * i / length;
                    expectedReal += input[i] * Math.Cos(phase); expectedImaginary += input[i] * Math.Sin(phase);
                }
                double dr = real[bin] - expectedReal, di = imaginary[bin] - expectedImaginary;
                error += dr * dr + di * di; energy += expectedReal * expectedReal + expectedImaginary * expectedImaginary;
            }
            Assert.Less(Math.Sqrt(error / energy), .000001);
        }

        [TestCase(400, 0), TestCase(640, 1), TestCase(1680, 2), TestCase(2048, 3)]
        public void PlanRetainsOriginalRoundingAcrossReuse(int length, int reference)
        {
            var plan = new MNNSourceRealFft(length); var real = new float[length / 2 + 1]; var imaginary = new float[real.Length];
            var other = Signal(length); for (int i = 0; i < length; ++i) other[i] *= -.25f;
            plan.Forward(other, real, imaginary); plan.Forward(Signal(length), real, imaginary);
            int[] bins = { 0, 1, 3, 7, length / 8, length / 4, length / 2 - 1, length / 2 };
            string[] words = SourceWords[reference].Split(',');
            for (int i = 0; i < bins.Length; ++i)
            {
                Assert.AreEqual(Convert.ToUInt32(words[2 * i], 16), BitConverter.ToUInt32(BitConverter.GetBytes(real[bins[i]]), 0), "real " + bins[i]);
                Assert.AreEqual(Convert.ToUInt32(words[2 * i + 1], 16), BitConverter.ToUInt32(BitConverter.GetBytes(imaginary[bins[i]]), 0), "imaginary " + bins[i]);
            }
        }

        [Test]
        public void RejectsUnsupportedLengthAndMismatchedBuffers()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNSourceRealFft(2049));
            var plan = new MNNSourceRealFft(2048);
            Assert.Throws<ArgumentException>(() => plan.Forward(new float[2047], new float[1025], new float[1025]));
            Assert.Throws<ArgumentException>(() => plan.Forward(new float[2048], new float[1024], new float[1025]));
        }

        private static float[] Signal(int length)
        {
            var result = new float[length]; for (int i = 0; i < length; ++i) result[i] = (i * 17 % 31 - 15) / 16f;
            return result;
        }
    }
}
