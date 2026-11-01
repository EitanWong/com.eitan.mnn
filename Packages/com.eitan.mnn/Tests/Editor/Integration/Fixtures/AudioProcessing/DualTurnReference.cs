using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    internal static class DualTurnReference
    {
        [Serializable] private sealed class Contract { public InputRow[] inputs; }
        [Serializable] internal sealed class InputRow { public string @case, file; public int[] shape; public int ticks; public bool mono; }
        internal static string Root
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("MNN_TEST_DUALTURN");
                if (string.IsNullOrEmpty(path)) Assert.Ignore("Set MNN_TEST_DUALTURN to the frozen DualTurn references.");
                return path;
            }
        }

        internal static string Candidate => Path.Combine(Root, "candidate-v460");
        internal static string Reference => Path.Combine(Root, "reference-v444");
        internal static InputRow[] Rows => JsonUtility.FromJson<Contract>(File.ReadAllText(Path.Combine(Root, "qualification-contract-v443.json"))).inputs;
        internal static double CheckPrediction(MNNDualTurnEndpointingResult result, float[] expected, int tick, string name)
        {
            float[] actual = Values(result);
            Assert.AreEqual(11, actual.Length);
            double error = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                Assert.That(actual[i], Is.InRange(0f, 1f), name);
                error = Math.Max(error, Math.Abs((double)actual[i] - expected[tick * 11 + i]));
                Assert.AreEqual(expected[tick * 11 + i] > .5, actual[i] > .5, name + " tick=" + (tick + 1) + " output=" + i);
            }
            Assert.Less(error, .001, name + " tick=" + (tick + 1));
            Assert.AreEqual(tick + 1, result.TickCount);
            Assert.AreEqual((tick + 1) * .08, result.ProcessedSeconds);
            return error;
        }

        internal static float[] Values(MNNDualTurnEndpointingResult result)
        {
            Assert.AreEqual(4, result.UserFutureVoiceProbabilities.Count);
            Assert.AreEqual(4, result.AgentFutureVoiceProbabilities.Count);
            return new[] { result.EndOfTurnProbability, result.UserVoiceProbability, result.AgentVoiceProbability }
                .Concat(result.UserFutureVoiceProbabilities).Concat(result.AgentFutureVoiceProbabilities).ToArray();
        }

        internal static void CopyTick(float[] pcm, InputRow row, int tick, float[] user, float[] agent)
        {
            Assert.AreEqual(row.ticks * 1920, row.shape[1]);
            Array.Copy(pcm, tick * 1920, user, 0, 1920);
            if (!row.mono) Array.Copy(pcm, row.shape[1] + tick * 1920, agent, 0, 1920);
        }

        internal static float[] ReadFloats(string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(Reference, name));
            Assert.AreEqual(0, bytes.Length % 4);
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
