using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("NativeInference"), Category("SpeechModels")]
    public sealed class VoxTurnPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void OriginalPublicDecisionsMatchInBuiltPlayer(MNNBackendType backend)
        {
            Assert.That(Application.isEditor, Is.False, "Run in a built Player.");
            MNNPlatformSupport.RequireSupported();
            string reference = Environment.GetEnvironmentVariable("MNN_TEST_VOXTURN");
            Assert.That(reference, Is.Not.Null.And.Not.Empty);
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "VoxTurn-v1-MNN");
            var cases = VoxTurnReference.ReadAll(reference);
            using (var model = MNNVoxTurn.Load(directory, backendType: backend))
            {
                foreach (var row in cases.Concat(new[] { cases[0] }))
                {
                    VoxTurnReference.Check(row, model.PredictBatch(row.Requests, row.Tasks));
                    var expected = backend == MNNBackendType.Auto
                        ? (row.Requests.Any(request => request.HasAudio) ? MNNBackendType.Metal : MNNBackendType.CPU) : backend;
                    Assert.That(model.AudioBackend, Is.EqualTo(MNNBackendType.CPU));
                    Assert.That(model.FirstLanguageBackend, Is.EqualTo(expected));
                    Assert.That(model.SecondLanguageBackend, Is.EqualTo(expected));
                }
                Assert.Throws<OperationCanceledException>(() => model.Predict(new MNNVoxTurnRequest(), cancellationToken: new CancellationToken(true)));
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Predict(new MNNVoxTurnRequest()));
            }
        }
    }
}
