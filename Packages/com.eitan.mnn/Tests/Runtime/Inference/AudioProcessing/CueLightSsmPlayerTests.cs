using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("NativeInference"), Category("SpeechModels")]
    public sealed class CueLightSsmPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Auto)]
        public void OriginalSsmPcmSixOutputsReplayAndEndpointsMatchBuiltPlayer(MNNBackendType backend)
        {
            Assert.False(Application.isEditor, "Run in a built Player."); MNNPlatformSupport.RequireSupported();
            string root = Environment.GetEnvironmentVariable("MNN_TEST_CUELIGHT_SSM"); Assert.That(root, Is.Not.Null.And.Not.Empty);
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "CueLight-Nano-SSM-MNN");
            string reference = Path.Combine(root, "source-graph-v267", "reference");
            CueLightReference.Verify(directory, reference, backend);
            Assert.Throws<NotSupportedException>(() => MNNCueLightTurnDetector.Load(directory, backendType: MNNBackendType.Metal));
        }
    }
}
