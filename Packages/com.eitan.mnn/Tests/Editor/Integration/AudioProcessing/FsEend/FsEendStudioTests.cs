using System;
using System.IO;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class FsEendStudioTests
    {
        [Test]
        public void StudioDiscoversAndExecutesOriginalRecordingWithExactRttm()
        {
            CheckStudio(FsEendReference.Candidate, FsEendReference.Directory, "FS-EEND-Simu-8K-MNN");
        }
        [Test]
        public void CallhomeStudioExecutesItsOriginalRecording()
        {
            CheckStudio(FsEendReference.CallhomeCandidate, FsEendReference.CallhomeDirectory, "FS-EEND-CALLHOME-8K-MNN");
        }
        private static void CheckStudio(string directory, string reference, string identity)
        {
            var model = MNNStudioModel.Read(directory); Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
            Assert.True(model.HasAudioProcessingPipeline); Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            Assert.AreEqual(MNNModelCatalogCategory.SpeakerDiarization.ToString(), MNNModelCatalog.Classify("example/" + identity));
            using (var backend = MNNStudioNativeBackend.Load(model, 8, false))
            {
                string wav = Path.Combine(reference, "real.wav"); var result = backend.Run(new MNNStudioRequest { Audio = wav });
                StringAssert.Contains("FS-EEND", result.Text); StringAssert.Contains("High", result.BackendDescription); StringAssert.Contains(identity, result.BackendDescription);
                var expected = File.ReadAllText(Path.Combine(reference, "real.rttm")).Replace("source", "real");
                if (expected.Length == 0) StringAssert.Contains("No active segments.", result.Text);
                else StringAssert.EndsWith(expected, result.Text);
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = wav, Cancellation = new CancellationToken(true) }));
            }
        }
    }
}
