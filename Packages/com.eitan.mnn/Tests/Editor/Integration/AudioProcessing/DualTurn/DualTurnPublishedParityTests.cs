using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class DualTurnPublishedParityTests
    {
        [Serializable] private sealed class Probabilities { public float[] values; }
        [Serializable] private sealed class Row { public string name; public int samples; public Probabilities[] probabilities; }
        [Serializable] private sealed class Rows { public Row[] rows; }
        [Test]
        public void PublishedStudioPreservesUserAndAgentChannelsAndFrameResults()
        {
            string root = Path.Combine(Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT"), "DualTurn");
            var row = JsonUtility.FromJson<Rows>(File.ReadAllText(Path.Combine(root, "managed-reference.json"))).rows.First(r => r.name != "silence");
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, row.name + ".f32"));
            var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
            int count = Math.Min(row.samples, 1920 * 2 + 24);
            var channels = new[] { new float[count], new float[count] };
            Array.Copy(pcm, channels[0], count); Array.Copy(pcm, row.samples, channels[1], 0, count);
            string wave = Path.Combine(root, "published-stereo-regression.wav");
            DualTurnWaveTests.Write(wave, channels, 24000);
            channels = MNNWaveAudio.ReadChannels(wave, out _, false);
            string directory = Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "DualTurn-Endpointing-MNN");
            using (var backend = MNNStudioNativeBackend.Load(MNNStudioModel.Read(directory), 1, false))
            using (var direct = MNNDualTurnEndpointing.Load(directory, 1))
            {
                var result = backend.Run(new MNNStudioRequest { Audio = wave });
                Assert.IsNotNull(result.DualTurnFrames, "Published registration must preserve the dual-channel Studio pipeline.");
                Assert.AreEqual(3, result.DualTurnFrames.Count);
                StringAssert.Contains("CH0: user · CH1: agent", result.Text);
                for (int tick = 0; tick < 3; ++tick)
                {
                    var user = new float[1920]; var agent = new float[1920];
                    int offset = tick * 1920, valid = Math.Min(1920, count - offset);
                    Array.Copy(channels[0], offset, user, 0, valid); Array.Copy(channels[1], offset, agent, 0, valid);
                    var expected = direct.Push(user, agent);
                    Assert.AreEqual(expected.EndOfTurnProbability, result.DualTurnFrames[tick].EndOfTurnProbability, 1e-5);
                    Assert.AreEqual(expected.UserVoiceProbability, result.DualTurnFrames[tick].UserVoiceProbability, 1e-5);
                    Assert.AreEqual(expected.AgentVoiceProbability, result.DualTurnFrames[tick].AgentVoiceProbability, 1e-5);
                }
            }
        }
        [Test]
        public void StreamingStartupHistoryAndResetMatchOfficialOnnx()
        {
            string root = Path.Combine(Environment.GetEnvironmentVariable("MNN_TURN_ACCEPTANCE_ROOT"), "DualTurn");
            var rows = JsonUtility.FromJson<Rows>(File.ReadAllText(Path.Combine(root, "managed-reference.json"))).rows;
            using (var model = MNNDualTurnEndpointing.Load(Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_MODELS_ROOT"), "DualTurn-Endpointing-MNN"), 1, MNNBackendType.CPU))
            foreach (var row in rows)
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(root, row.name + ".f32"));
                var pcm = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, pcm, 0, bytes.Length);
                model.Reset(); double maxError = 0;
                for (int tick = 0; tick < row.probabilities.Length; ++tick)
                {
                    var user = new float[1920]; var agent = new float[1920];
                    int offset = tick * 1920, count = Math.Min(1920, row.samples - offset);
                    Array.Copy(pcm, offset, user, 0, count); Array.Copy(pcm, row.samples + offset, agent, 0, count);
                    var result = model.Push(user, agent);
                    var actual = new[] { result.EndOfTurnProbability, result.UserVoiceProbability, result.AgentVoiceProbability }
                        .Concat(result.UserFutureVoiceProbabilities).Concat(result.AgentFutureVoiceProbabilities).ToArray();
                    for (int i = 0; i < actual.Length; ++i)
                    {
                        maxError = Math.Max(maxError, Math.Abs(actual[i] - row.probabilities[tick].values[i]));
                        Assert.AreEqual(row.probabilities[tick].values[i] >= .5f, actual[i] >= .5f, row.name + " tick=" + tick + " output=" + i);
                    }
                    Assert.AreEqual(tick + 1, model.TickCount);
                }
                Assert.Less(maxError, .01, row.name);
                TestContext.WriteLine(row.name + ": ticks=" + row.probabilities.Length + "; max_error=" + maxError);
            }
        }
    }
}
