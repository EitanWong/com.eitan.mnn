using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("FsEend")]
    public sealed class FsEendSourceTests
    {
        [Test]
        public void ManagedLogmelWithoutCumulativeMeanMatchesOriginalPcmFeatures()
        {
            var c = MNNFsEendConfig.Read(FsEendReference.Candidate); var frontend = new MNNEendFeatures(c.analysis_window, c.mel_filters, false);
            foreach (string name in FsEendReference.Cases)
                FsEendReference.Check(FsEendReference.Read(name + "-features"), frontend.Extract(FsEendReference.Read(name + "-input"), default), name + ";features");
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void OriginalStreamingPosteriorsAndRttmMatchCompletePcm(MNNBackendType backend)
        {
            using (var model = MNNFsEendDiarization.Load(FsEendReference.Candidate, backendType: backend))
            foreach (string name in FsEendReference.Cases.Concat(new[] { FsEendReference.Cases[0] }))
            {
                var pcm = FsEendReference.Read(name + "-input"); var actual = model.Analyze(pcm); var repeat = model.Analyze(pcm);
                FsEendReference.Check(FsEendReference.Read(name + "-posterior"), actual.Probabilities, backend + ";" + name);
                CollectionAssert.AreEqual(actual.Probabilities, repeat.Probabilities, name + " repeat");
                Assert.AreEqual((pcm.Length + 799) / 800, actual.FrameCount); Assert.AreEqual(pcm.Length / 8000.0, actual.DurationSeconds);
                Assert.AreEqual(File.ReadAllText(Path.Combine(FsEendReference.Directory, name + ".rttm")), actual.ToRttm("source"), name + " RTTM");
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend, model.Backend);
            }
        }
        [Test]
        public void PcmBoundsCancellationAndLifecycleAreEnforced()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNFsEendDiarization.Load(FsEendReference.Candidate, 0));
            Assert.Throws<NotSupportedException>(() => MNNFsEendDiarization.Load(FsEendReference.Candidate, backendType: MNNBackendType.NN));
            using (var model = MNNFsEendDiarization.Load(FsEendReference.Candidate))
            {
                Assert.Throws<ArgumentException>(() => model.Analyze(null)); Assert.Throws<ArgumentException>(() => model.Analyze(Array.Empty<float>()));
                Assert.Throws<ArgumentException>(() => model.Analyze(new float[480001])); Assert.Throws<ArgumentException>(() => model.Analyze(new[] { float.NaN }));
                Assert.Throws<ArgumentException>(() => model.Analyze(new[] { float.PositiveInfinity })); Assert.Throws<ArgumentOutOfRangeException>(() => model.Analyze(new float[1], 16000));
                Assert.Throws<OperationCanceledException>(() => model.Analyze(new float[1], cancellationToken: new CancellationToken(true)));
                Assert.AreEqual(1, model.Analyze(new[] { 1.2f }).FrameCount); model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Analyze(new float[1]));
            }
        }
        [Test]
        public void RuntimeIdentityTransformCoefficientsAndGraphRejectTampering()
        {
            string dir = Path.Combine(FsEendReference.Root, "tamper-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try
            {
                foreach (string path in Directory.GetFiles(FsEendReference.Candidate)) File.Copy(path, Path.Combine(dir, Path.GetFileName(path)));
                Assert.True(MNNFsEendModel.IsComplete(dir)); string pathConfig = Path.Combine(dir, MNNFsEendConfig.FileName); string original = File.ReadAllText(pathConfig);
                foreach (string text in new[] { original.Replace("logmel23", "logmel23_cummn"), original.Replace("172fa6ee", "072fa6ee"), original.Replace("\"output_columns\": 5", "\"output_columns\": 9") })
                {
                    Assert.AreNotEqual(original, text); File.WriteAllText(pathConfig, text); Assert.False(MNNFsEendModel.IsComplete(dir));
                    Assert.Throws<InvalidDataException>(() => MNNFsEendDiarization.Load(dir));
                }
                File.WriteAllText(pathConfig, original);
                foreach (string name in new[] { MNNFsEendModel.Graph, MNNFsEendConfig.CoefficientsFile })
                {
                    string path = Path.Combine(dir, name); var bytes = File.ReadAllBytes(path); bytes[1000] ^= 1; File.WriteAllBytes(path, bytes);
                    Assert.False(MNNFsEendModel.IsComplete(dir)); Assert.Throws<InvalidDataException>(() => MNNFsEendDiarization.Load(dir));
                    File.Copy(Path.Combine(FsEendReference.Candidate, name), path, true);
                }
            }
            finally { Directory.Delete(dir, true); }
        }
        [Test]
        public void OriginalStrictThresholdAndZeroPaddedMedianPreserveFiveColumns()
        {
            var p = new float[15 * 5]; for (int t = 0; t < 15; ++t) p[t * 5 + 1] = .5f;
            for (int t = 2; t < 13; ++t) p[t * 5] = 1;
            var activity = new MNNFsEendActivity(p, 1.5);
            Assert.AreEqual("SPEAKER record 1    0.20    1.10 <NA> <NA> record_0 <NA>\n", activity.ToRttm("record"));
            Assert.Throws<ArgumentException>(() => activity.ToRttm("invalid id")); Assert.Throws<ArgumentException>(() => new MNNFsEendActivity(new[] { float.NaN, 0, 0, 0, 0 }, .1));
            Assert.AreEqual("", new MNNFsEendActivity(new float[] { 1, 1, 1, 1, 1 }, .0001).ToRttm("short"));
        }
    }
}
