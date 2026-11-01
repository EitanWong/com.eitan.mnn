using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static MNN.Unity.Tests.DualTurnReference;

namespace MNN.Unity.Tests
{
    public sealed class DualTurnStateParityTests
    {
        [Serializable] private sealed class References { public Case[] cases; }
        [Serializable] private sealed class Case { public string @case; public Snapshot[] snapshots; }
        [Serializable] private sealed class Snapshot { public string key, file, dtype; public int tick; public int[] shape; }
        private static readonly string[] StateNames = { "audio_ctx", "pk", "pv", "tf_hist", "h", "c", "pos" };

        [Test]
        public void CompleteGraphStateCarryMatchesOriginalIncludingCacheEvictionBoundaries()
        {
            var cases = JsonUtility.FromJson<References>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases;
            using (var graph = new MNNGenerationGraph(Path.Combine(Candidate, MNNDualTurnEndpointingModel.Graph), 4,
                backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High,
                externalWeightsPath: Path.Combine(Candidate, MNNDualTurnEndpointingModel.Weights)))
            {
                foreach (InputRow row in Rows)
                {
                    float[] pcm = ReadFloats(row.file);
                    var user = new float[1920]; var agent = new float[1920];
                    var state = MNNDualTurnEndpointingState.Initial();
                    var snapshots = cases.Single(item => item.@case == row.@case).snapshots;
                    double worst = 0;
                    for (int tick = 0; tick < row.ticks; ++tick)
                    {
                        CopyTick(pcm, row, tick, user, agent);
                        var chunk = new float[3840];
                        Array.Copy(user, chunk, 1920);
                        if (!row.mono) Array.Copy(agent, 0, chunk, 1920, 1920);
                        var inputs = new[] { new MNNGenerationGraph.Input("chunk", chunk, 2, 1920) }.Concat(state).ToArray();
                        var outputs = graph.RunOutputs(MNNDualTurnEndpointingState.OutputNames, inputs);
                        state = MNNDualTurnEndpointingState.Next(outputs, tick + 1);
                        foreach (Snapshot snapshot in snapshots.Where(item => item.tick == tick + 1))
                            worst = Math.Max(worst, Compare(snapshot, outputs[3 + Array.IndexOf(StateNames, snapshot.key)]));
                    }
                    TestContext.WriteLine(row.@case + " maximum_state_nrmse=" + worst.ToString("R"));
                }
            }
        }

        private static double Compare(Snapshot snapshot, MNNGenerationGraph.Output output)
        {
            CollectionAssert.AreEqual(snapshot.shape, output.Shape, snapshot.key);
            if (snapshot.key == "pos")
            {
                Assert.True(output.Data is int[] || output.Data is long[]);
                using (var reader = new BinaryReader(File.OpenRead(Path.Combine(Reference, snapshot.file))))
                    Assert.AreEqual(reader.ReadInt64(), Convert.ToInt64(output.Data.GetValue(0)));
                return 0;
            }
            var actual = (float[])output.Data;
            float[] expected = ReadFloats(snapshot.file);
            Assert.AreEqual(expected.Length, actual.Length);
            double energy = 0, difference = 0, maximum = 0;
            for (int i = 0; i < actual.Length; ++i)
            {
                double delta = (double)actual[i] - expected[i];
                energy += (double)expected[i] * expected[i];
                difference += delta * delta;
                maximum = Math.Max(maximum, Math.Abs(delta));
            }
            if (snapshot.key == "audio_ctx") Assert.AreEqual(0, maximum);
            double nrmse = Math.Sqrt(difference / Math.Max(energy, 1e-30));
            Assert.Less(energy > 0 ? nrmse : maximum, .001, snapshot.key + " tick=" + snapshot.tick);
            return nrmse;
        }
    }
}
