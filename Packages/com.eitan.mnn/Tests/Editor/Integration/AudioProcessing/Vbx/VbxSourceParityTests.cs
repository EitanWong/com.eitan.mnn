using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SpeakerEmbedding")]
    public sealed class VbxSourceParityTests
    {
        [Test]
        public void CompleteFrontendMatchesOriginalDitherFbankAndSlidingCmn()
        {
            var features = new MNNVbxFeatures(MNNVbxConfig.Read(VbxReference.Candidate));
            foreach (string name in VbxReference.Cases.Concat(new[] { VbxReference.Cases[0] }))
            {
                var input = VbxReference.Read(name + "-input"); var actual = features.Extract(input, CancellationToken.None);
                var expected = VbxReference.Read(name + "-features"); Assert.AreEqual(expected.Length, actual.Length);
                double maximum = actual.Select((v, i) => Math.Abs((double)v - expected[i])).Max();
                TestContext.WriteLine(name + ";feature_maximum_absolute=" + maximum.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Assert.Less(maximum, .0001, name); CollectionAssert.AreEqual(actual, features.Extract(input, CancellationToken.None));
            }
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void FullPcmMatchesOriginalRawEmbeddingAcrossShapesAndRepeats(MNNBackendType backend)
        {
            using (var model = MNNVbxEmbedding.Load(VbxReference.Candidate, backendType: backend))
            {
                foreach (string name in VbxReference.Cases.Concat(new[] { VbxReference.Cases[0] }))
                {
                    var input = VbxReference.Read(name + "-input"); var actual = model.Encode(input);
                    VbxReference.Check(VbxReference.Read(name + "-embedding"), actual, backend + ";" + name);
                    CollectionAssert.AreEqual(actual, model.Encode(input), name);
                    Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.Backend);
                }
                Assert.AreEqual("VBx-ResNet101-16K-MNN", model.ModelName);
                Assert.AreEqual(8, model.ThreadCount);
            }
        }
        [Test]
        public void InvalidPcmBackendCancellationAndDisposalAreExplicit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNVbxEmbedding.Load(VbxReference.Candidate, 0));
            Assert.Throws<NotSupportedException>(() => MNNVbxEmbedding.Load(VbxReference.Candidate, backendType: MNNBackendType.NN));
            var model = MNNVbxEmbedding.Load(VbxReference.Candidate, 1, MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(() => model.Encode(null));
                Assert.Throws<ArgumentException>(() => model.Encode(new float[1519]));
                Assert.Throws<ArgumentException>(() => model.Encode(new float[160001]));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Encode(new float[16000], 8000));
                foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue })
                {
                    var input = new float[1600]; input[777] = value; Assert.Throws<ArgumentException>(() => model.Encode(input));
                }
                Assert.Throws<OperationCanceledException>(() => model.Encode(new float[1600], cancellationToken: new CancellationToken(true)));
                Assert.Throws<OperationCanceledException>(() => model.EncodeWaveFile("unused.wav", new CancellationToken(true)));
            }
            finally { model.Dispose(); model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Encode(new float[1600]));
        }
        [Test]
        public void AsyncOwnsInputAndWaveFileMatchesOriginal()
        {
            using (var model = MNNVbxEmbedding.Load(VbxReference.Candidate, 1, MNNBackendType.CPU))
            {
                var input = VbxReference.Read("real-input"); var pending = model.EncodeAsync(input); Array.Clear(input, 0, input.Length);
                VbxReference.Check(VbxReference.Read("real-embedding"), pending.GetAwaiter().GetResult(), "async-owned-input");
                VbxReference.Check(VbxReference.Read("real-embedding"), model.EncodeWaveFile(Path.Combine(VbxReference.Directory, "real.wav")), "WAV");
            }
        }
        [Test]
        public void ModifiedProfileCoefficientsAndGraphAreRejectedBeforeInference()
        {
            string temporary = Path.Combine(VbxReference.Root, "tamper-v348"); Assert.False(System.IO.Directory.Exists(temporary)); System.IO.Directory.CreateDirectory(temporary);
            try
            {
                foreach (string name in new[] { MNNVbxConfig.FileName, MNNVbxConfig.CoefficientsFile, MNNVbxModel.Graph })
                    File.Copy(Path.Combine(VbxReference.Candidate, name), Path.Combine(temporary, name));
                Assert.True(MNNVbxModel.IsComplete(temporary));
                string path = Path.Combine(temporary, MNNVbxConfig.FileName), text = File.ReadAllText(path);
                foreach (var pair in new[] { new[] { "whole_utterance_raw_embedding", "normalized_embedding" }, new[] { "f550bf3d", "0550bf3d" }, new[] { "\"dither_seed\": 3", "\"dither_seed\": 4" }, new[] { "\"cmn_right\": 149", "\"cmn_right\": 150" } })
                {
                    string changed = text.Replace(pair[0], pair[1]); Assert.AreNotEqual(text, changed); File.WriteAllText(path, changed);
                    Assert.Throws<InvalidDataException>(() => MNNVbxEmbedding.Load(temporary)); File.WriteAllText(path, text);
                }
                string coefficients = Path.Combine(temporary, MNNVbxConfig.CoefficientsFile); var bytes = File.ReadAllBytes(coefficients); bytes[111] ^= 1; File.WriteAllBytes(coefficients, bytes);
                Assert.Throws<InvalidDataException>(() => MNNVbxEmbedding.Load(temporary)); File.Copy(Path.Combine(VbxReference.Candidate, MNNVbxConfig.CoefficientsFile), coefficients, true);
                using (var stream = new FileStream(Path.Combine(temporary, MNNVbxModel.Graph), FileMode.Open, FileAccess.ReadWrite)) { stream.Position = 500; int value = stream.ReadByte(); stream.Position = 500; stream.WriteByte((byte)(value ^ 1)); }
                Assert.Throws<InvalidDataException>(() => MNNVbxEmbedding.Load(temporary));
            }
            finally { System.IO.Directory.Delete(temporary, true); }
        }
    }
}
