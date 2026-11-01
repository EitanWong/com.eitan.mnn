using System;
using System.IO;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ResembleEnhancerStudioTests
    {
        [Test]
        public void DiscoveryConnectsTheCompleteEnhancerPipeline()
        {
            var model = MNNStudioModel.Read(ResembleEnhancerReference.Model);
            Assert.That(model.Task, Is.EqualTo(MNNStudioTask.AudioProcessing));
            Assert.That(model.HasAudioProcessingPipeline, Is.True);
            Assert.That(MNNStudioTasks.Get((int)model.Task).CanRun(model), Is.True);
            Assert.That(MNNModelCatalog.Classify("example/Resemble-Enhance-44K-MNN"),
                Is.EqualTo(MNNModelCatalogCategory.AudioProcessing.ToString()));
        }

        [Test]
        public void StudioEnhancesTheOriginalFloatWavAndReportsActualBackends()
        {
            string path = Path.Combine(ResembleEnhancerReference.Root, "public-api-fixtures-v80", "hop_at.wav");
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(ResembleEnhancerReference.Model), 8, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = path });
                Assert.That(result.SampleRate, Is.EqualTo(44100));
                ResembleEnhancerReference.Check("Studio hop PCM", result.Waveform, ResembleEnhancerReference.Read("hop_at-target"));
                var expected = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
                foreach (string stage in new[] { "denoiser", "encoder", "velocity", "decoder", "vocoder" })
                    StringAssert.Contains(stage + " " + expected, result.BackendDescription);
                StringAssert.Contains("32 NFE", result.BackendDescription);
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = path, Cancellation = new CancellationToken(true) }));
            }
        }
    }
}
