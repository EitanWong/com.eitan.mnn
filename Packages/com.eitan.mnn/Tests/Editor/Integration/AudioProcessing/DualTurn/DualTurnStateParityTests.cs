using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
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
        [Serializable] private sealed class CaseMetrics
        {
            public string name;
            public int ticks, replays;
            public double maximum_probability_error, maximum_state_nrmse, median_ms, maximum_ms;
        }
        [Serializable] private sealed class Qualification
        {
            public string backend;
            public int threads;
            public bool qualified;
            public List<CaseMetrics> cases = new List<CaseMetrics>();
        }

        [Test]
        public void CompleteGraphStateCarryMatchesOriginalIncludingCacheEvictionBoundaries()
        {
            RunGraphCases(Candidate, MNNBackendType.CPU, 4, 1);
        }

        internal static void RunGraphCases(string directory, MNNBackendType backend, int threads, int replays)
        {
            var cases = JsonUtility.FromJson<References>(File.ReadAllText(Path.Combine(Reference, "cases.json"))).cases;
            var report = new Qualification { backend = backend.ToString(), threads = threads };
            string label = Environment.GetEnvironmentVariable("MNN_TEST_DUALTURN_RUN_LABEL");
            string receipt = Path.Combine(Root, label + "-graph-" + backend + ".json");
            using (var graph = new MNNGenerationGraph(Path.Combine(directory, "model_fp32.mnn"), threads,
                backendType: backend, precisionMode: MNNPrecisionMode.High,
                externalWeightsPath: Path.Combine(directory, "model_fp32.mnn.weight"), gpuTuningMode: MNNGpuTuningMode.None))
            {
                foreach (InputRow row in Rows)
                {
                    float[] pcm = ReadFloats(row.file);
                    float[] expected = ReadFloats(row.@case + "-probabilities.f32");
                    var user = new float[1920]; var agent = new float[1920];
                    var snapshots = cases.Single(item => item.@case == row.@case).snapshots;
                    double worst = 0, maximumProbability = 0;
                    var times = new List<double>();
                    var first = new float[row.ticks * 11];
                    for (int replay = 0; replay < replays; ++replay)
                    {
                    var state = MNNDualTurnEndpointingState.Initial();
                    int expectedSustain = 0, actualSustain = 0;
                    for (int tick = 0; tick < row.ticks; ++tick)
                    {
                        CopyTick(pcm, row, tick, user, agent);
                        var chunk = new float[3840];
                        Array.Copy(user, chunk, 1920);
                        if (!row.mono) Array.Copy(agent, 0, chunk, 1920, 1920);
                        var inputs = new[] { new MNNGenerationGraph.Input("chunk", chunk, 2, 1920) }.Concat(state).ToArray();
                        var timer = Stopwatch.StartNew();
                        var outputs = graph.RunOutputs(MNNDualTurnEndpointingState.OutputNames, inputs);
                        state = MNNDualTurnEndpointingState.Next(outputs, tick + 1);
                        times.Add(timer.Elapsed.TotalMilliseconds);
                        var result = new MNNDualTurnEndpointingResult(((float[])outputs[0].Data)[0],
                            (float[])outputs[1].Data, (float[])outputs[2].Data, tick + 1);
                        maximumProbability = Math.Max(maximumProbability, CheckPrediction(result, expected, tick, row.@case));
                        float[] actual = Values(result);
                        expectedSustain = expected[tick * 11] > .5 ? expectedSustain + 1 : 0;
                        actualSustain = actual[0] > .5 ? actualSustain + 1 : 0;
                        Assert.AreEqual(expectedSustain >= 3, actualSustain >= 3, row.@case + " sustain");
                        Assert.AreEqual(backend, graph.Backend, "Unexpected backend fallback");
                        if (replay == 0) Array.Copy(actual, 0, first, tick * 11, 11);
                        else for (int i = 0; i < 11; ++i) Assert.AreEqual(first[tick * 11 + i], actual[i], row.@case + " repeat");
                        foreach (Snapshot snapshot in snapshots.Where(item => item.tick == tick + 1))
                            worst = Math.Max(worst, Compare(snapshot, outputs[3 + Array.IndexOf(StateNames, snapshot.key)]));
                    }
                    }
                    double[] sorted = times.OrderBy(value => value).ToArray();
                    report.cases.Add(new CaseMetrics { name = row.@case, ticks = row.ticks, replays = replays,
                        maximum_probability_error = maximumProbability, maximum_state_nrmse = worst,
                        median_ms = sorted[sorted.Length / 2], maximum_ms = sorted.Last() });
                    File.WriteAllText(receipt, JsonUtility.ToJson(report, true) + "\n");
                    TestContext.WriteLine(row.@case + " maxabs=" + maximumProbability.ToString("R") + " maximum_state_nrmse=" + worst.ToString("R"));
                }
            }
            report.qualified = true;
            File.WriteAllText(receipt, JsonUtility.ToJson(report, true) + "\n");
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
