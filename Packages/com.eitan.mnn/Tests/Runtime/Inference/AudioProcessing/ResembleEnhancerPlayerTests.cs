using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("NativeInference"), Category("SpeechModels")]
    public sealed class ResembleEnhancerPlayerTests
    {
        private static string Directory => Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "Resemble-Enhance-44K-MNN");

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void EveryOriginalPcmConditionMatchesInBuiltPlayer(MNNBackendType backend)
        {
            Assert.That(Application.isEditor, Is.False, "Run in a built Player.");
            MNNPlatformSupport.RequireSupported();
            var names = ResembleEnhancerReference.Names;
            Assert.That(names.Length, Is.EqualTo(24));
            using (var model = MNNResembleEnhancer.Load(Directory, backendType: backend))
            {
                foreach (string name in names.Concat(names.Take(1)))
                {
                    float[] input = ResembleEnhancerReference.Read(name + "-input");
                    var original = (float[])input.Clone();
                    ResembleEnhancerReference.Check(name + " Player PCM", model.Enhance(input),
                        ResembleEnhancerReference.Read(name + "-target"));
                    Assert.That(input, Is.EqualTo(original), "The caller owns the PCM input.");
                    ResembleEnhancerReference.CheckBackends(model, backend);
                }
                Assert.Throws<OperationCanceledException>(() => model.Enhance(new float[400], cancellationToken: new CancellationToken(true)));
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Enhance(new float[400]));
            }
        }

        [Test]
        public void AutoRunsItsQualifiedDefaultInBuiltPlayer()
        {
            Assert.That(Application.isEditor, Is.False, "Run in a built Player.");
            using (var model = MNNResembleEnhancer.Load(Directory))
            {
                ResembleEnhancerReference.Check("Auto Player PCM", model.Enhance(ResembleEnhancerReference.Read("minimum-input")),
                    ResembleEnhancerReference.Read("minimum-target"));
                ResembleEnhancerReference.CheckBackends(model,
                    MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU);
            }
        }
    }
}
