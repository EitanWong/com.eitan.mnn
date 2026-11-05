using System;
using System.Globalization;
using System.IO;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class CueLightStudioTests
    {
        [Test]
        public void StudioDiscoversAndExecutesOriginalLongCallWithEndpointTimeline()
        {
            var model = MNNStudioModel.Read(CueLightSourceParityTests.Candidate);
            Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
            Assert.True(model.HasAudioProcessingPipeline);
            Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            Assert.AreEqual(MNNModelCatalogCategory.TurnDetection.ToString(), MNNModelCatalog.Classify("example/CueLight-Nano-MNN"));
            using (var backend = MNNStudioNativeBackend.Load(model, 8, false))
            {
                string wav = Path.Combine(CueLightSourceParityTests.Root, "studio-reference-v252/long_call.wav");
                var result = backend.Run(new MNNStudioRequest { Audio = wav });
                StringAssert.Contains("CueLight", result.Text); StringAssert.Contains("CPU", result.BackendDescription);
                var lines = result.Text.Substring(result.Text.IndexOf("time_seconds", StringComparison.Ordinal)).TrimEnd().Split('\n');
                var expected = CueLightSourceParityTests.Read("long_call-events"); Assert.AreEqual(expected.Length / 5 + 1, lines.Length);
                for (int i = 1; i < lines.Length; ++i)
                {
                    var columns = lines[i].Split('\t'); Assert.AreEqual(6, columns.Length);
                    Assert.AreEqual(i * .02, double.Parse(columns[0], CultureInfo.InvariantCulture), 1e-10);
                    Assert.Less(Math.Abs(float.Parse(columns[1], CultureInfo.InvariantCulture) - expected[(i - 1) * 5]), .0001);
                    Assert.Less(Math.Abs(float.Parse(columns[2], CultureInfo.InvariantCulture) - expected[(i - 1) * 5 + 1]), .0001);
                    for (int j = 0; j < 3; ++j) Assert.AreEqual((int)expected[(i - 1) * 5 + j + 2], int.Parse(columns[j + 3]));
                }
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = wav, Cancellation = new CancellationToken(true) }));
            }
        }
    }
}
