using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class VbxStudioTests
    {
        [TestCase(false), TestCase(true)]
        public void DiscoveredSpeakerModelExecutesOriginalRawVectorAndCancellation(bool telephone)
        {
            string directory = telephone ? VbxReference.Candidate8 : VbxReference.Candidate;
            string reference = telephone ? VbxReference.Directory8 : VbxReference.Directory;
            string profile = telephone ? "VBx-ResNet101-8K-MNN" : "VBx-ResNet101-16K-MNN";
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.SpeakerRecognition, model.Task);
            Assert.True(model.HasSpeakerRecognitionPipeline);
            Assert.AreEqual(MNNModelCatalogCategory.SpeakerRecognition.ToString(), MNNModelCatalog.Classify(profile));
            using (var backend = MNNStudioNativeBackend.Load(model, 1, false))
            {
                string wav = Path.Combine(reference, "real.wav");
                var result = backend.Run(new MNNStudioRequest { Audio = wav });
                StringAssert.Contains("raw", result.Text); StringAssert.Contains("256 dimensions", result.Text);
                StringAssert.Contains(profile, result.BackendDescription);
                var vector = result.Text.Split('\n')[1].Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                VbxReference.Check(VbxReference.ReadFrom(reference, "real-embedding"), vector, "Studio");
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = wav, Cancellation = new CancellationToken(true) }));
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
            }
        }
    }
}
