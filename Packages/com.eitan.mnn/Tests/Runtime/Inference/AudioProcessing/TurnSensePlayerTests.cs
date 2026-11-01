using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("NativeInference"), Category("SpeechModels")]
    public sealed class TurnSensePlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void EveryOriginalTranscriptAndRepeatMatchesInBuiltPlayer(MNNBackendType backend)
        {
            Assert.False(Application.isEditor, "Run in a built Player.");
            MNNPlatformSupport.RequireSupported();
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "TurnSense-SmolLM2-135M-MNN");
            var timer = Stopwatch.StartNew();
            using (var model = MNNTurnSense.Load(directory, backendType: backend))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                var times = new double[TurnSenseReference.Cases.Length];
                for (int i = 0; i < times.Length; ++i)
                {
                    var row = TurnSenseReference.Cases[i];
                    var actual = model.Predict(row.Text);
                    double error = Math.Max(Math.Abs(actual.NonEndOfUtteranceProbability - row.Incomplete), Math.Abs(actual.EndOfUtteranceProbability - row.Complete));
                    Assert.Less(error, .001, row.Name);
                    Assert.AreEqual(row.Decision == 1, actual.IsComplete, row.Name);
                    timer.Restart();
                    var repeated = model.Predict(row.Text);
                    times[i] = timer.Elapsed.TotalMilliseconds;
                    Assert.AreEqual(actual.NonEndOfUtteranceProbability, repeated.NonEndOfUtteranceProbability, row.Name);
                    Assert.AreEqual(actual.EndOfUtteranceProbability, repeated.EndOfUtteranceProbability, row.Name);
                    TestContext.WriteLine(row.Name + ";maxabs=" + error.ToString("R", CultureInfo.InvariantCulture));
                }
                Assert.AreEqual(backend == MNNBackendType.Auto ? MNNBackendType.Metal : backend, model.Backend);
                Array.Sort(times);
                TestContext.WriteLine("PERF backend=" + model.Backend + " load_ms=" + load + " warm_median_ms=" + times[times.Length / 2] + " warm_max_ms=" + times.Last());
                Assert.Throws<ArgumentException>(() => model.Predict(TurnSenseReference.Overlong));
                Assert.Throws<OperationCanceledException>(() => model.Predict("Hello", new CancellationToken(true)));
                model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Predict("Hello"));
            }
        }
    }
}
