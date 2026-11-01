using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("NativeInference"), Category("SpeechModels")]
    public sealed class FsEendPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void OriginalFullPcmAndRttmMatchInBuiltPlayer(MNNBackendType backend)
        {
            Assert.False(Application.isEditor, "Run in a built Player.");
            MNNPlatformSupport.RequireSupported();
            string root = Environment.GetEnvironmentVariable("MNN_TEST_FS_EEND");
            Assert.That(root, Is.Not.Null.And.Not.Empty);
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "FS-EEND-Simu-8K-MNN");
            string reference = Path.Combine(root, "source-graph-v293", "reference");
            var cases = new[] { "minimum", "short", "real", "quiet", "gain2", "silence", "constant", "hop_before", "hop_at", "hop_after",
                "subsample_before", "subsample_at", "subsample_after", "delay_before", "delay_at", "delay_after",
                "context_before", "context_at", "context_after", "maximum" };
            using (var model = MNNFsEendDiarization.Load(directory, backendType: backend))
            {
                foreach (string name in cases.Concat(new[] { cases[0] }))
                {
                    var pcm = EendReference.Read(Path.Combine(reference, name + "-input.f32"));
                    var expected = EendReference.Read(Path.Combine(reference, name + "-posterior.f32"));
                    var actual = model.Analyze(pcm);
                    EendReference.Check(name, expected, actual.Probabilities);
                    CollectionAssert.AreEqual(actual.Probabilities, model.Analyze(pcm).Probabilities, name + " repeat");
                    Assert.AreEqual((pcm.Length + 799) / 800, actual.FrameCount);
                    Assert.AreEqual(File.ReadAllText(Path.Combine(reference, name + ".rttm")), actual.ToRttm("source"));
                }
                var selected = backend == MNNBackendType.Auto ? MNNBackendType.CPU : backend;
                Assert.AreEqual(selected, model.Backend);
                Assert.Throws<OperationCanceledException>(() => model.Analyze(new float[1], cancellationToken: new CancellationToken(true)));
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Analyze(new float[1]));
            }
        }
    }
}
