using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("BairongTurnSense")]
    public sealed class BairongTurnSenseParityTests
    {
        [Serializable] private sealed class Reference { public int frames; public float[] probabilities; }
        private static string Root => Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT") ?? throw new InvalidOperationException("Set MNN_TURN_ACCEPTANCE_ROOT.");
        private static float[] Floats(string path)
        {
            byte[] bytes = File.ReadAllBytes(path); var result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length); return result;
        }
        [TestCase("1.0"), TestCase("1.1")]
        public void ManagedFrontendMatchesOfficialKaldiFbankAndCmvn(string version)
        {
            string source = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "Bairong-TurnSense-Source", "TurnSense" + version);
            var frontend = new MNNBairongTurnSenseFeatures(Path.Combine(source, "am.mvn"));
            string fixtures = Path.Combine(Root, "Bairong", "fixtures-" + version);
            foreach (string metadata in Directory.GetFiles(fixtures, "*.json"))
            {
                string prefix = metadata.Substring(0, metadata.Length - 5);
                var expected = Floats(prefix + "-features.f32");
                var actual = frontend.Extract(Floats(prefix + "-input.f32"), out int frames, default);
                Assert.AreEqual(JsonUtility.FromJson<Reference>(File.ReadAllText(metadata)).frames, frames);
                Assert.AreEqual(expected.Length, actual.Length);
                float maximum = actual.Select((v, i) => Math.Abs(v - expected[i])).Max();
                TestContext.WriteLine(Path.GetFileName(prefix) + ": feature error=" + maximum);
                // Kaldi uses float FFT/filter sums; the managed kernel accumulates in double.
                Assert.Less(maximum, .0002f);
            }
        }
        [TestCase("1.0"), TestCase("1.1")]
        public void SelectedMnnGraphMatchesOfficialProbabilitiesAndThreeWayDecisions(string version)
        {
            string directory = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "Bairong-TurnSense-" + version + "-MNN");
            string fixtures = Path.Combine(Root, "Bairong", "fixtures-" + version);
            using (var model = MNNBairongTurnSense.Load(directory))
            using (var adapter = MNNTurnDetector.Load(directory))
                foreach (string metadata in Directory.GetFiles(fixtures, "*.json"))
                {
                    var expected = JsonUtility.FromJson<Reference>(File.ReadAllText(metadata)).probabilities;
                    var pcm = Floats(metadata.Substring(0, metadata.Length - 5) + "-input.f32");
                    var result = model.Predict(pcm, 16000);
                    float maximum = result.Probabilities.Select((v, i) => Math.Abs(v - expected[i])).Max();
                    TestContext.WriteLine(Path.GetFileName(metadata) + ": probability error=" + maximum + "; label=" + result.Label);
                    Assert.Less(maximum, .02f);
                    Assert.AreEqual(Array.IndexOf(expected, expected.Max()), result.ClassIndex);
                    Assert.AreEqual(result.ClassIndex == 0, adapter.IsComplete(pcm, 16000));
                }
        }
    }
}
