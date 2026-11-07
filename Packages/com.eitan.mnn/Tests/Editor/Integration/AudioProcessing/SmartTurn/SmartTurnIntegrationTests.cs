using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MNN.Unity.Editor;
using UnityEngine.TestTools;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SmartTurn")]
    public class SmartTurnIntegrationTests
    {
        [Serializable] private class Manifest { public Row[] rows; }
        [Serializable] private class Row { public string name; public float probability; public bool complete, label; }
        private static string Root
        {
            get
            {
                string path = Environment.GetEnvironmentVariable("MNN_TEST_SMART_TURN_ROOT");
                if (string.IsNullOrWhiteSpace(path)) Assert.Ignore("Set MNN_TEST_SMART_TURN_ROOT with pinned local graph and independent ONNX references; no downloads.");
                return path;
            }
        }
        private static float[] Read(string file)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root, "Reference", file));
            var result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }
        [Test]
        public void FrontendAndNativeProbabilityMatchIndependentReference()
        {
            MNNSmartTurnModel.Prepare(Path.Combine(Root, "Models"));
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Root, "Reference/manifest.json")));
            using (var model = MNNSmartTurn.Load(Path.Combine(Root, "Models")))
            {
                foreach (var row in manifest.rows)
                {
                    var pcm = Read(row.name + "-pcm.f32");
                    var expected = Read(row.name + "-features.f32");
                    var actual = MNNSmartTurnFeatures.Extract(pcm, default);
                    var errors = actual.Select((p, i) => Math.Abs(p - expected[i])).ToArray();
                    TestContext.WriteLine(row.name + " feature max=" + errors.Max() + " mean=" + errors.Average());
                    Assert.Less(errors.Max(), .00002, "Independent transformers frontend: " + row.name);
                    Assert.Less(errors.Average(), .000001);
                    var result = model.Predict(pcm, 16000);
                    TestContext.WriteLine(row.name + " MNN=" + result.CompletionProbability + " ONNX=" + row.probability);
                    File.WriteAllText(Path.Combine(Root, row.name + "-mnn.txt"), result.CompletionProbability.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    Assert.AreEqual(row.probability, result.CompletionProbability, .04, row.name);
                    Assert.AreEqual(row.complete, result.IsComplete, row.name);
                    if (row.name.StartsWith("dataset-", StringComparison.Ordinal)) Assert.AreEqual(row.label, result.IsComplete, "Published endpoint label: " + row.name);
                    Assert.AreEqual((double)pcm.Length / 16000, result.DurationSeconds);
                }
                Assert.Throws<OperationCanceledException>(() => model.Predict(new float[16000], 16000, cancellationToken: new CancellationToken(true)));
            }
        }
        [UnityTest]
        public IEnumerator StudioWindowRunsAudioWorkspaceAndDisplaysNativeTurnProbability()
        {
            string directory = Path.Combine(Root, "Models");
            MNNSmartTurnModel.Prepare(directory);
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            object Invoke(string name) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            var model = MNNStudioModel.Read(directory);
            var session = new MNNStudioSession(MNNStudioNativeBackend.Load(model, 1, false));
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel> {model});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.AudioProcessing);
                Field("_query").SetValue(window, "");
                Field("_session").SetValue(window, session);
                Field("_autoTitle").SetValue(window, false);
                Assert.False((bool)Invoke("CanRunTask"), "Audio workspace requires a recording.");
                Field("_audioPath").SetValue(window, Path.Combine(Root, "../../ASR/Zipformer/Audio/en-0.wav"));
                session.Completed += () => Invoke("OnCompleted");
                Assert.True((bool)Invoke("CanRunTask"));
                Invoke("RunTask");
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (session.Busy && DateTime.UtcNow < deadline) { MNNStudioJobs.Update(); yield return null; }
                Assert.False(session.Busy);
                Assert.IsNull(session.Error);
                var result = (MNNStudioResult)Field("_retrieval").GetValue(window);
                Assert.IsNotNull(result);
                Assert.True(result.TurnDetection.IsComplete);
                Assert.That(result.Text, Does.Contain("Turn complete.").And.Contain("Completion probability:"));
                Assert.IsEmpty((List<MNNStudioMessage>)Field("_messages").GetValue(window));
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(window); session.Dispose(); }
        }

        [Test]
        public void InputValidationAsyncOwnershipResamplingAndDisposal()
        {
            var pcm = Read("en-pcm.f32");
            var model = MNNSmartTurn.Load(Path.Combine(Root, "Models"));
            try
            {
                var reference = model.Predict(pcm, 16000);
                Assert.Throws<ArgumentException>(() => model.Predict(Array.Empty<float>(), 16000));
                Assert.Throws<ArgumentException>(() => model.Predict(new[] {float.PositiveInfinity}, 16000));
                Assert.Throws<ArgumentException>(() => model.Predict(new[] {1.01f}, 16000));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Predict(pcm, 7999));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Predict(pcm, 16000, float.NaN));
                Assert.Throws<OperationCanceledException>(() => model.Predict(pcm, 16000, cancellationToken: new CancellationToken(true)));
                var pending = model.PredictAsync(pcm, 16000);
                Array.Clear(pcm, 0, pcm.Length);
                Assert.AreEqual(reference.CompletionProbability, pending.GetAwaiter().GetResult().CompletionProbability);
                pcm = Read("en-pcm.f32");
                var resampled = MNNSpeechDsp.Resample(pcm, 16000, 44100, default);
                var result = model.Predict(resampled, 44100);
                Assert.AreEqual(reference.CompletionProbability, result.CompletionProbability, .01);
                Assert.AreEqual((double)resampled.Length / 44100, result.DurationSeconds);
                Assert.True(result.IsComplete);
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.Predict(pcm, 16000));
        }
    }
}
