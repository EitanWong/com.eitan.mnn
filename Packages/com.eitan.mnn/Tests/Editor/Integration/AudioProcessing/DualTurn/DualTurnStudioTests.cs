using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using static MNN.Unity.Tests.DualTurnReference;

namespace MNN.Unity.Tests
{
    public sealed class DualTurnStudioTests
    {
        [Test]
        public void CompleteDualTurnProfileIsDiscoveredAsRunnableAudioPipeline()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_DUALTURN");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Set MNN_TEST_DUALTURN to the frozen DualTurn references.");
            var model = MNNStudioModel.Read(Path.Combine(root, "candidate-v460"));
            Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task, "Missing DualTurn Studio discovery.");
            Assert.True(model.HasAudioProcessingPipeline);
            Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
            Assert.AreEqual(MNNModelCatalogCategory.AudioProcessing.ToString(), MNNModelCatalog.Classify("example/DualTurn-Endpointing-80ms-MNN"));
        }

        [Test]
        public void EveryOriginalWavStreamPreservesSpeakerChannelsAndResetsPerRequest()
        {
            var model = MNNStudioModel.Read(Candidate);
            using (var backend = MNNStudioNativeBackend.Load(model, MNNDualTurnEndpointing.DefaultThreadCount, false))
            {
                foreach (InputRow row in Rows)
                {
                    float[] pcm = ReadFloats(row.file), expected = ReadFloats(row.@case + "-probabilities.f32");
                    float[][] channels = Enumerable.Range(0, row.shape[0])
                        .Select(index => pcm.Skip(index * row.shape[1]).Take(row.shape[1]).ToArray()).ToArray();
                    string wave = Path.Combine(Root, row.@case + "-studio-v487.wav");
                    DualTurnWaveTests.Write(wave, channels, 24000);
                    float first = 0;
                    for (int replay = 0; replay < 2; ++replay)
                    {
                        var result = backend.Run(new MNNStudioRequest { Audio = wave });
                        Assert.AreEqual(row.ticks, result.DualTurnFrames.Count);
                        double error = 0;
                        for (int tick = 0; tick < row.ticks; ++tick)
                            error = Math.Max(error, CheckPrediction(result.DualTurnFrames[tick], expected, tick, row.@case));
                        Assert.AreEqual(result.DualTurnFrames.Last().EndOfTurnProbability, result.TurnDetection.CompletionProbability);
                        StringAssert.Contains(row.mono ? "Mono: user" : "CH0: user · CH1: agent", result.Text);
                        StringAssert.Contains("silence padding: 0 samples", result.Text);
                        StringAssert.Contains("640–1200", result.Text);
                        StringAssert.Contains("DualTurn", result.BackendDescription);
                        if (replay == 0) first = result.DualTurnFrames[0].EndOfTurnProbability;
                        else Assert.AreEqual(first, result.DualTurnFrames[0].EndOfTurnProbability);
                        TestContext.WriteLine(row.@case + " replay=" + replay + " maxabs=" + error.ToString("R"));
                    }
                    Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest { Audio = wave, Cancellation = new CancellationToken(true) }));
                }
            }
        }

        [Test]
        public void ResamplingAndFinalPaddingAreExplicitAndNextRequestStartsFresh()
        {
            string wave = Path.Combine(Root, "padded-mono-16k-v487.wav");
            DualTurnWaveTests.Write(wave, new[] { new float[1296] }, 16000);
            float[] expected = ReadFloats("silence-probabilities.f32");
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(Candidate), 4, false))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = wave });
                Assert.AreEqual(2, result.DualTurnFrames.Count);
                CheckPrediction(result.DualTurnFrames[0], expected, 0, "resampled zero");
                CheckPrediction(result.DualTurnFrames[1], expected, 1, "padded zero");
                StringAssert.Contains("silence padding: 1896 samples", result.Text);
                Assert.Throws<ArgumentException>(() => backend.Run(new MNNStudioRequest()));
                var next = backend.Run(new MNNStudioRequest { Audio = wave });
                CollectionAssert.AreEqual(Values(result.DualTurnFrames[0]), Values(next.DualTurnFrames[0]));
            }
        }
    }
}
