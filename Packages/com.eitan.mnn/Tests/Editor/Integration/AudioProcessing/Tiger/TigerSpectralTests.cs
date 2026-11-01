using System;
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("Tiger")]
    public sealed class TigerSpectralTests
    {
        [Serializable] private sealed class Case { public string @case = ""; public int samples = 0; public int[] shape = null; }
        [Serializable] private sealed class Cases { public Case[] cases = null; }

        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_TIGER_REFERENCE")
            ?? throw new InvalidOperationException("Set the original TIGER source reference directory.");

        private static float[] Read(string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        private static void Check(float[] actual, float[] expected, double tolerance, string context)
        {
            Assert.AreEqual(expected.Length, actual.Length, context);
            double error = 0, energy = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.False(float.IsNaN(actual[i]) || float.IsInfinity(actual[i]), context);
                double delta = (double)actual[i] - expected[i];
                error += delta * delta; energy += (double)expected[i] * expected[i];
            }
            double nrmse = Math.Sqrt(error / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(context + ";nrmse=" + nrmse);
            Assert.Less(nrmse, tolerance, context);
        }

        [Test]
        public void SourceOrdered640PointPlanIsAvailable()
        {
            Assert.DoesNotThrow(() => new MNNSourceRealFft(640));
        }

        [Test]
        public void LowEnergySpectralBinsPreserveOriginalFloatCancellation()
        {
            var spectral = new MNNTigerSpectral(Read("window"));
            var cases = JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases;
            Assert.AreEqual(25, cases.Length);
            foreach (var item in cases)
            {
                string name = item.@case;
                var actual = spectral.Analyze(Read(name + "-input"), default, out int frames);
                CollectionAssert.AreEqual(Read(name + "-features"), actual, name + " original float spectral bins");
            }
        }

        [Test]
        public void OriginalFeaturesAndIndependentSourceInverseMatch()
        {
            var type = typeof(MNNBackendType).Assembly.GetType("MNN.Unity.MNNTigerSpectral");
            Assert.NotNull(type, "The original TIGER PCM spectral frontend is missing.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object spectral = Activator.CreateInstance(type, flags, null, new object[] { Read("window") }, null);
            MethodInfo analyze = type.GetMethod("Analyze", flags), synthesize = type.GetMethod("Synthesize", flags);
            var cases = JsonUtility.FromJson<Cases>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases;
            Assert.AreEqual(25, cases.Length);
            foreach (var item in cases)
            {
                object[] args = { Read(item.@case + "-input"), CancellationToken.None, 0 };
                var actual = (float[])analyze.Invoke(spectral, args);
                int frames = (int)args[2];
                Assert.AreEqual(item.shape[3], frames, item.@case);
                Check(actual, Read(item.@case + "-features"), 2e-6, item.@case + " analysis");
                var source = Read(item.@case + "-spectrum");
                var target = Read(item.@case + "-target");
                int sourceSize = 2 * 321 * frames;
                for (int speaker = 0; speaker < 2; ++speaker)
                {
                    var spectrum = new float[sourceSize];
                    var expected = new float[item.samples];
                    Array.Copy(source, speaker * sourceSize, spectrum, 0, sourceSize);
                    Array.Copy(target, speaker * item.samples, expected, 0, item.samples);
                    var waveform = (float[])synthesize.Invoke(spectral, new object[] { spectrum, frames, item.samples, CancellationToken.None });
                    Check(waveform, expected, 1e-5, item.@case + " inverse " + speaker);
                }
            }
        }
    }
}
