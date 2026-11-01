using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class Vbx8kSourceTests
    {
        [Test]
        public void Original8kMinimumPcmMatchesAuthorVector()
        {
            using (var model = MNNVbxEmbedding.Load(VbxReference.Candidate8, 1, MNNBackendType.CPU))
                VbxReference.Check(VbxReference.ReadFrom(VbxReference.Directory8, "minimum-embedding"),
                    model.Encode(VbxReference.ReadFrom(VbxReference.Directory8, "minimum-input"), 8000), "original8k-minimum");
        }
        [Test]
        public void Original8kFrontendMatchesAcrossEveryBoundary()
        {
            var features = new MNNVbxFeatures(MNNVbxConfig.Read(VbxReference.Candidate8));
            foreach (string name in VbxReference.Cases.Concat(new[] { VbxReference.Cases[0] }))
            {
                var pcm = VbxReference.ReadFrom(VbxReference.Directory8, name + "-input");
                var actual = features.Extract(pcm, CancellationToken.None); var expected = VbxReference.ReadFrom(VbxReference.Directory8, name + "-features");
                Assert.AreEqual(expected.Length, actual.Length); double maximum = actual.Select((v, i) => Math.Abs((double)v - expected[i])).Max();
                TestContext.WriteLine(name + ";feature_maximum_absolute=" + maximum.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Assert.Less(maximum, .0001, name); CollectionAssert.AreEqual(actual, features.Extract(pcm, CancellationToken.None));
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void Original8kFullPcmVectorsAndRepeatsMatchAcrossShapes(MNNBackendType backend)
        {
            using (var model = MNNVbxEmbedding.Load(VbxReference.Candidate8, backendType: backend))
            {
                Assert.AreEqual("VBx-ResNet101-8K-MNN", model.ModelName); Assert.AreEqual(8000, model.InputSampleRate); Assert.AreEqual(4, model.ThreadCount);
                foreach (string name in VbxReference.Cases.Concat(new[] { VbxReference.Cases[0] }))
                {
                    var pcm = VbxReference.ReadFrom(VbxReference.Directory8, name + "-input"); var actual = model.Encode(pcm);
                    VbxReference.Check(VbxReference.ReadFrom(VbxReference.Directory8, name + "-embedding"), actual, backend + ";" + name);
                    CollectionAssert.AreEqual(actual, model.Encode(pcm, 8000));
                    Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.Backend);
                }
            }
        }
        [Test]
        public void Original8kPcmRateBoundariesCancellationAndDisposalAreStrict()
        {
            var model = MNNVbxEmbedding.Load(VbxReference.Candidate8, 1, MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(() => model.Encode(new float[759]));
                Assert.Throws<ArgumentException>(() => model.Encode(new float[80001]));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Encode(new float[8000], 16000));
                var bad = new float[8000]; bad[100] = float.NaN; Assert.Throws<ArgumentException>(() => model.Encode(bad));
                Assert.Throws<OperationCanceledException>(() => model.Encode(new float[800], cancellationToken: new CancellationToken(true)));
            }
            finally { model.Dispose(); model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Encode(new float[800]));
        }
        [Test]
        public void Original8kWaveFileAndAsyncOwnedInputMatch()
        {
            using (var model = MNNVbxEmbedding.Load(VbxReference.Candidate8, 1, MNNBackendType.CPU))
            {
                var pcm = VbxReference.ReadFrom(VbxReference.Directory8, "real-input"); var pending = model.EncodeAsync(pcm); Array.Clear(pcm, 0, pcm.Length);
                VbxReference.Check(VbxReference.ReadFrom(VbxReference.Directory8, "real-embedding"), pending.GetAwaiter().GetResult(), "async8k");
                VbxReference.Check(VbxReference.ReadFrom(VbxReference.Directory8, "real-embedding"), model.EncodeWaveFile(Path.Combine(VbxReference.Directory8, "real.wav")), "WAV8k");
            }
        }
        [Test]
        public void CrossedCheckpointGraphAndCoefficientIdentitiesAreRejected()
        {
            string temp = Path.Combine(VbxReference.Root8, "identity-tamper-v366"); Assert.False(System.IO.Directory.Exists(temp)); System.IO.Directory.CreateDirectory(temp);
            try
            {
                foreach (string name in new[] { MNNVbxConfig.FileName, MNNVbxConfig.CoefficientsFile, MNNVbxModel.Graph }) File.Copy(Path.Combine(VbxReference.Candidate8, name), Path.Combine(temp, name));
                Assert.True(MNNVbxModel.IsComplete(temp));
                string config = Path.Combine(temp, MNNVbxConfig.FileName); var text = File.ReadAllText(config);
                File.WriteAllText(config, text.Replace("\"sample_rate\": 8000", "\"sample_rate\": 16000")); Assert.Throws<InvalidDataException>(() => MNNVbxEmbedding.Load(temp)); File.WriteAllText(config, text);
                File.Copy(Path.Combine(VbxReference.Candidate, MNNVbxModel.Graph), Path.Combine(temp, MNNVbxModel.Graph), true); Assert.Throws<InvalidDataException>(() => MNNVbxEmbedding.Load(temp));
                File.Copy(Path.Combine(VbxReference.Candidate8, MNNVbxModel.Graph), Path.Combine(temp, MNNVbxModel.Graph), true);
                File.Copy(Path.Combine(VbxReference.Candidate, MNNVbxConfig.CoefficientsFile), Path.Combine(temp, MNNVbxConfig.CoefficientsFile), true); Assert.Throws<InvalidDataException>(() => MNNVbxEmbedding.Load(temp));
            }
            finally { System.IO.Directory.Delete(temp, true); }
        }
    }
}
