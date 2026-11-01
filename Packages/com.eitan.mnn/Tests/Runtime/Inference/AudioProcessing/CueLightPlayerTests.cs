using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("NativeInference"), Category("SpeechModels")]
    public sealed class CueLightPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void OriginalPcmSixOutputsReplayAndEndpointsMatchBuiltPlayer(MNNBackendType backend)
        {
            Assert.False(Application.isEditor, "Run in a built Player."); MNNPlatformSupport.RequireSupported();
            string root = Environment.GetEnvironmentVariable("MNN_TEST_CUELIGHT"); Assert.That(root, Is.Not.Null.And.Not.Empty);
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "CueLight-Nano-MNN");
            string reference = Path.Combine(root, "source-graph-v237", "reference");
            CueLightReference.Verify(directory, reference, backend);
        }
    }
}
