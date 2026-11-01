using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("DtlnAec")]
    public sealed class DtlnAecIntegrationTests
    {
        [TestCase(128), TestCase(256), TestCase(512)]
        public void ContinuousStreamMatchesIndependentPythonReference(int units)
        {
            var mic = DtlnAecTestData.Read("mic.bin"); var reference = DtlnAecTestData.Read("ref.bin");
            var actual = new float[mic.Length]; var micHop = new float[128]; var refHop = new float[128]; var output = new float[128];
            using (var model = MNNDtlnAec.Load(DtlnAecTestData.ModelDirectory(units)))
            {
                Assert.AreEqual(units, model.Units); Assert.AreEqual(MNNBackendType.CPU, model.Backend);
                model.ProcessHop(micHop, refHop, output);
                model.Reset();
                var timer = Stopwatch.StartNew();
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < mic.Length; i += 128)
                {
                    Array.Copy(mic, i, micHop, 0, 128); Array.Copy(reference, i, refHop, 0, 128);
                    model.ProcessHop(micHop, refHop, output); Array.Copy(output, 0, actual, i, 128);
                }
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                timer.Stop();
                TestContext.WriteLine("DTLN " + units + " complete streaming ms/hop=" + timer.Elapsed.TotalMilliseconds / (mic.Length / 128) + " managed allocated=" + allocated);
                Assert.Zero(allocated, "Prepared streaming inference must not allocate managed memory.");
            }
            DtlnAecTestData.Compare(DtlnAecTestData.Read("expected-" + units + ".bin"), actual);
        }
        [TestCase(128), TestCase(256), TestCase(512)]
        public void OfflineScenariosAndNonAlignedTailMatchIndependentReference(int units)
        {
            using (var model = MNNDtlnAec.Load(DtlnAecTestData.ModelDirectory(units)))
                foreach (string scenario in new[] {"doubletalk", "farend_singletalk", "nearend_singletalk"})
                {
                    var mic = DtlnAecTestData.Read(scenario + "-mic.bin"); var reference = DtlnAecTestData.Read(scenario + "-ref.bin");
                    var actual = model.Process(mic, reference);
                    TestContext.WriteLine(scenario); DtlnAecTestData.Compare(DtlnAecTestData.Read(scenario + "-expected-" + units + ".bin"), actual);
                    DtlnAecTestData.Compare(actual, model.Process(mic, reference));
                }
        }
        [Test]
        public void RejectsInvalidInputAndResetCancellationDisposalKeepStatePredictable()
        {
            var mic = new float[128]; var reference = new float[128]; var output = new float[128]; mic[0] = .25f;
            var model = MNNDtlnAec.Load(DtlnAecTestData.ModelDirectory(128));
            try
            {
                model.ProcessHop(mic, reference, output); var first = (float[])output.Clone();
                model.Reset(); model.ProcessHop(mic, reference, output); CollectionAssert.AreEqual(first, output);
                Assert.Throws<ArgumentException>(() => model.ProcessHop(new float[127], reference, output));
                Assert.Throws<ArgumentException>(() => model.ProcessHop(mic, reference, new float[127]));
                var bad = (float[])mic.Clone(); bad[3] = float.NaN;
                Assert.Throws<ArgumentException>(() => model.ProcessHop(bad, reference, output));
                Assert.Throws<ArgumentException>(() => model.Process(mic, new float[256]));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Process(mic, reference, 48000));
                Assert.Throws<OperationCanceledException>(() => model.ProcessHop(mic, reference, output, new CancellationToken(true)));
                Assert.Throws<OperationCanceledException>(() => model.Process(mic, reference, cancellationToken: new CancellationToken(true)));
                var expected = model.Process(mic, reference);
                var pending = model.ProcessAsync(mic, reference); Array.Clear(mic, 0, mic.Length);
                DtlnAecTestData.Compare(expected, pending.GetAwaiter().GetResult());
                Assert.Throws<NotSupportedException>(() => MNNDtlnAec.Load(DtlnAecTestData.ModelDirectory(128), backendType: MNNBackendType.NN));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => model.ProcessHop(mic, reference, output));
        }
        [UnityTest]
        public IEnumerator StudioWindowRequiresBothInputsAndReturnsPlayableAudio()
        {
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            object Invoke(string name) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            var model = MNNStudioModel.Read(DtlnAecTestData.ModelDirectory(128));
            for (int stage = 1; stage <= 2; ++stage)
            {
                LogAssert.Expect(LogType.Log, "[MNN] Loaded model from: " + Path.Combine(model.Directory, MNNDtlnAecModel.Graph(128, stage)));
                LogAssert.Expect(LogType.Log, "[MNN] Created session: requested=CPU, actual=CPU, threads=1");
            }
            var session = new MNNStudioSession(MNNStudioNativeBackend.Load(model, 4, false));
            try
            {
                Assert.AreEqual(MNNStudioTask.EchoCancellation, model.Task); Assert.True(model.HasEchoCancellationPipeline);
                Field("_available").SetValue(window, new List<MNNStudioModel> {model}); Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.EchoCancellation); Field("_query").SetValue(window, "");
                Field("_session").SetValue(window, session); Field("_autoTitle").SetValue(window, false);
                Assert.False((bool)Invoke("CanRunTask"));
                Field("_audioPath").SetValue(window, Path.Combine(DtlnAecTestData.Root, "Reference/doubletalk-mic.wav"));
                Assert.False((bool)Invoke("CanRunTask"));
                Field("_aecReferencePath").SetValue(window, Path.Combine(DtlnAecTestData.Root, "Reference/doubletalk-ref.wav"));
                Assert.True((bool)Invoke("CanRunTask")); session.Completed += () => Invoke("OnCompleted"); Invoke("RunTask");
                var deadline = DateTime.UtcNow.AddSeconds(60);
                while (session.Busy && DateTime.UtcNow < deadline) { MNNStudioJobs.Update(); yield return null; }
                Assert.False(session.Busy); Assert.IsNull(session.Error);
                var result = (MNNStudioResult)Field("_retrieval").GetValue(window);
                Assert.AreEqual(16000, result.SampleRate);
                DtlnAecTestData.Compare(DtlnAecTestData.Read("doubletalk-expected-128.bin"), result.Waveform);
                byte[] wave = MNNStudioAudio.EncodeWave(result.Waveform, result.SampleRate);
                File.WriteAllBytes(Path.Combine(DtlnAecTestData.Root, MNNFftPlan.IsBurstAvailable ? "studio-burst.wav" : "studio-managed.wav"), wave);
                Assert.Greater(wave.Length, 44);
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(window); session.Dispose(); }
        }
    }
}
