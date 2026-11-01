using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("FsEend")]
    public sealed class FsEendCallhomeSourceTests
    {
        [Test]
        public void AuthorCallhomeCheckpointIsRecognizedIndependently()
        {
            var config = MNNFsEendConfig.Read(FsEendReference.CallhomeCandidate);
            Assert.AreEqual("eda37490029b9112d5a40849e2e825404ec55e12671dd982d07b81780339c949", config.checkpoint_sha256);
            Assert.True(MNNFsEendModel.IsComplete(FsEendReference.CallhomeCandidate), "Recognize original CALLHOME checkpoint and its own graph.");
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void OriginalCallhomePcmAndRttmMatch(MNNBackendType backend)
        {
            using (var model = MNNFsEendDiarization.Load(FsEendReference.CallhomeCandidate, backendType: backend))
            foreach (string name in FsEendReference.Cases.Concat(new[] { FsEendReference.Cases[0] }))
            {
                var pcm = FsEendReference.ReadFrom(FsEendReference.CallhomeDirectory, name + "-input");
                var actual = model.Analyze(pcm);
                FsEendReference.Check(FsEendReference.ReadFrom(FsEendReference.CallhomeDirectory, name + "-posterior"), actual.Probabilities, backend + ";CALLHOME;" + name);
                CollectionAssert.AreEqual(actual.Probabilities, model.Analyze(pcm).Probabilities, name + " repeat");
                Assert.AreEqual(File.ReadAllText(Path.Combine(FsEendReference.CallhomeDirectory, name + ".rttm")), actual.ToRttm("source"));
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend, model.Backend);
            }
        }
    }
}
