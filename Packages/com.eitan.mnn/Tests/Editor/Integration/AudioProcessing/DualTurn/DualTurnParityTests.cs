using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using static MNN.Unity.Tests.DualTurnReference;

namespace MNN.Unity.Tests
{
    public sealed class DualTurnParityTests
    {
        [Serializable] private sealed class Measurement
        {
            public int threads, ticks;
            public double load_ms, first_ms, median_ms, p95_ms, maximum_ms, maximum_probability_error;
        }

        private static int Threads => int.TryParse(Environment.GetEnvironmentVariable("MNN_TEST_DUALTURN_THREADS"), out int value) ? value : MNNDualTurnEndpointing.DefaultThreadCount;

        [TestCase(1), TestCase(2), TestCase(4), TestCase(8)]
        public void MeasureCompleteManagedStreamAcrossThreads(int threads)
        {
            InputRow row = Rows.Single(item => item.@case == "real_stereo");
            var timer = Stopwatch.StartNew();
            using (var model = MNNDualTurnEndpointing.Load(Candidate, threads, MNNBackendType.CPU))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                float[] pcm = ReadFloats(row.file), expected = ReadFloats(row.@case + "-probabilities.f32");
                var user = new float[1920]; var agent = new float[1920];
                var times = new double[row.ticks];
                double maximumError = 0;
                for (int tick = 0; tick < row.ticks; ++tick)
                {
                    CopyTick(pcm, row, tick, user, agent);
                    timer.Restart();
                    var result = model.Push(user, agent);
                    times[tick] = timer.Elapsed.TotalMilliseconds;
                    maximumError = Math.Max(maximumError, CheckPrediction(result, expected, tick, row.@case));
                }
                double first = times[0];
                double[] warm = times.Skip(1).OrderBy(value => value).ToArray();
                var measurement = new Measurement
                {
                    threads = threads, ticks = row.ticks, load_ms = load, first_ms = first,
                    median_ms = warm[warm.Length / 2], p95_ms = warm[(int)Math.Ceiling(warm.Length * .95) - 1],
                    maximum_ms = warm.Last(), maximum_probability_error = maximumError
                };
                string json = JsonUtility.ToJson(measurement, true);
                File.WriteAllText(Path.Combine(Root, (Environment.GetEnvironmentVariable("MNN_TEST_DUALTURN_RUN_LABEL") ?? "managed-performance") + "-thread" + threads + ".json"), json + "\n");
                TestContext.WriteLine(json);
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Auto)]
        public void CompleteAllOriginalStreamsAndResetReplayMatch(MNNBackendType backend)
        {
            Assert.AreEqual(14, Rows.Length);
            using (var model = MNNDualTurnEndpointing.Load(Candidate, Threads, backend))
            {
                foreach (InputRow row in Rows)
                {
                    float[] pcm = ReadFloats(row.file), expected = ReadFloats(row.@case + "-probabilities.f32");
                    var user = new float[1920]; var agent = new float[1920];
                    var first = new float[row.ticks * 11];
                    double maximumError = 0;
                    for (int replay = 0; replay < 2; ++replay)
                    {
                        model.Reset();
                        Assert.AreEqual(0, model.TickCount);
                        int expectedSustain = 0, actualSustain = 0;
                        for (int tick = 0; tick < row.ticks; ++tick)
                        {
                            CopyTick(pcm, row, tick, user, agent);
                            var result = model.Push(user, row.mono ? null : agent);
                            maximumError = Math.Max(maximumError, CheckPrediction(result, expected, tick, row.@case));
                            float[] actual = Values(result);
                            expectedSustain = expected[tick * 11] > .5 ? expectedSustain + 1 : 0;
                            actualSustain = actual[0] > .5 ? actualSustain + 1 : 0;
                            Assert.AreEqual(expectedSustain >= 3, actualSustain >= 3, row.@case + " sustain tick=" + tick);
                            if (replay == 0) Array.Copy(actual, 0, first, tick * 11, 11);
                            else for (int i = 0; i < 11; ++i) Assert.AreEqual(first[tick * 11 + i], actual[i], row.@case + " reset replay tick=" + tick);
                        }
                    }
                    TestContext.WriteLine(row.@case + " ticks=" + row.ticks + " reset_replays=2 maxabs=" + maximumError.ToString("R"));
                }
                Assert.AreEqual(MNNBackendType.CPU, model.Backend);
            }
        }

        [Test]
        public void InvalidInputCancellationAndDisposalDoNotConsumeNextTick()
        {
            Assert.Throws<ArgumentNullException>(() => MNNDualTurnEndpointing.Load(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNDualTurnEndpointing.Load(Candidate, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNDualTurnEndpointing.Load(Candidate, 65));
            Assert.Throws<NotSupportedException>(() => MNNDualTurnEndpointing.Load(Candidate, 1, MNNBackendType.Metal));
            Assert.Throws<NotSupportedException>(() => MNNDualTurnEndpointing.Load(Candidate, 1, MNNBackendType.NN));
            Assert.Throws<InvalidDataException>(() => MNNDualTurnEndpointing.Load(Root));
            InputRow row = Rows.Single(item => item.@case == "real_stereo");
            float[] pcm = ReadFloats(row.file), expected = ReadFloats(row.@case + "-probabilities.f32");
            var user = new float[1920]; var agent = new float[1920];
            var model = MNNDualTurnEndpointing.Load(Candidate);
            try
            {
                CopyTick(pcm, row, 0, user, agent);
                CheckPrediction(model.Push(user, agent), expected, 0, row.@case);
                Assert.Throws<ArgumentNullException>(() => model.Push(null));
                Assert.Throws<ArgumentException>(() => model.Push(new float[1919]));
                Assert.Throws<ArgumentException>(() => model.Push(user, new float[1921]));
                foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    var invalid = new float[1920]; invalid[1919] = value;
                    Assert.Throws<ArgumentException>(() => model.Push(invalid));
                    Assert.Throws<ArgumentException>(() => model.Push(user, invalid));
                }
                Assert.Throws<OperationCanceledException>(() => model.Push(user, agent, new CancellationToken(true)));
                Assert.AreEqual(1, model.TickCount);
                CopyTick(pcm, row, 1, user, agent);
                CheckPrediction(model.Push(user, agent), expected, 1, row.@case);
            }
            finally { model.Dispose(); }
            model.Dispose();
            Assert.Throws<ObjectDisposedException>(() => model.Push(user, agent));
            Assert.Throws<ObjectDisposedException>(() => model.Reset());
        }

        [Test]
        public void InstancesOwnIndependentStatesAndReturnReadOnlyPredictions()
        {
            InputRow row = Rows.Single(item => item.@case == "real_stereo");
            float[] pcm = ReadFloats(row.file), expected = ReadFloats(row.@case + "-probabilities.f32");
            float[] silence = ReadFloats("silence-probabilities.f32");
            var user = new float[1920]; var agent = new float[1920];
            using (var first = MNNDualTurnEndpointing.Load(Candidate))
            using (var second = MNNDualTurnEndpointing.Load(Candidate))
            {
                CopyTick(pcm, row, 0, user, agent);
                var prediction = first.Push(user, agent);
                CheckPrediction(prediction, expected, 0, row.@case);
                var list = prediction.UserFutureVoiceProbabilities as IList<float>;
                Assert.IsNotNull(list);
                Assert.Throws<NotSupportedException>(() => list[0] = 1);
                CheckPrediction(second.Push(new float[1920]), silence, 0, "silence");
                second.Reset();
                CopyTick(pcm, row, 1, user, agent);
                CheckPrediction(first.Push(user, agent), expected, 1, row.@case);
                CopyTick(pcm, row, 0, user, agent);
                CheckPrediction(second.Push(user, agent), expected, 0, row.@case);
                CheckPrediction(prediction, expected, 0, row.@case);
                Assert.AreEqual(2, first.TickCount);
                Assert.AreEqual(1, second.TickCount);
            }
        }

    }
}
