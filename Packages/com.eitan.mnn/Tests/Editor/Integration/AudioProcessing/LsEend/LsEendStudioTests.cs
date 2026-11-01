using System;
using System.IO;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class LsEendStudioTests
    {
        private static string Root => Environment.GetEnvironmentVariable("MNN_TEST_LS_EEND");
        [Test]
        public void StudioDiscoversAndExecutesTheOriginalWaveAsRttm()
        {
            var model = MNNStudioModel.Read(Path.Combine(Root, "candidate-v216"));
            Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
            Assert.True(model.HasAudioProcessingPipeline);
            Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            Assert.AreEqual(MNNModelCatalogCategory.AudioProcessing.ToString(), MNNModelCatalog.Classify("example/LS-EEND-Simu-8K-MNN"));
            using (var backend = MNNStudioNativeBackend.Load(model, 8, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = Path.Combine(Root, "source-graphs-v208/reference/real.wav") });
                string expected = File.ReadAllText(Path.Combine(Root, "managed-reference-v216/real.rttm"));
                StringAssert.Contains(expected.TrimEnd(), result.Text);
                StringAssert.Contains("LS-EEND", result.Text);
                StringAssert.Contains("CPU", result.BackendDescription);
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest
                {
                    Audio = Path.Combine(Root, "source-graphs-v208/reference/real.wav"), Cancellation = new CancellationToken(true)
                }));
            }
        }
    }
}
