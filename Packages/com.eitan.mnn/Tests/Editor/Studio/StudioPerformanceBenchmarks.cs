using System;
using System.Diagnostics;
using System.IO;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class StudioPerformanceBenchmarks
    {
        [Serializable]
        private sealed class Report
        {
            public string unity, machine;
            public int queriesPerSample;
            public double[] milliseconds;
            public long[] allocatedBytes;
            public long snapshotBytes;
            public bool allocationCounterSupported;
        }
        [Test]
        public void MeasureModelQueriesAndSteadyStateSnapshots()
        {
            string root = Path.GetFullPath(Path.Combine("TestArtifacts~", "StudioPerformanceBenchmark"));
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "config.json"), "{\"model_type\":\"piper\"}");
            var model = new MNNStudioModel("benchmark", root, MNNStudioTask.AudioProcessing, 0);
            var report = new Report { unity = Application.unityVersion, machine = SystemInfo.processorType,
                queriesPerSample = 6000, milliseconds = new double[5], allocatedBytes = new long[5] };
            long probeBefore = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[1024 * 1024];
            report.allocationCounterSupported = GC.GetAllocatedBytesForCurrentThread() - probeBefore >= probe.Length;
            GC.KeepAlive(probe);
            for (int i = 0; i < 200; ++i) Query(model);
            for (int trial = 0; trial < 5; ++trial)
            {
                var clock = Stopwatch.StartNew();
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 2000; ++i) Query(model);
                report.allocatedBytes[trial] = report.allocationCounterSupported ? GC.GetAllocatedBytesForCurrentThread() - before : -1;
                report.milliseconds[trial] = clock.Elapsed.TotalMilliseconds;
            }
            // The stream publisher retains its builder; only the UI materializes display strings.
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 512);
            var response = output.BeginResponse();
            var session = new MNNRealtimeSession(new IdleRuntime(), output);
            var ports = new MNNConversationPorts(new EmptyCapture(), () => session);
            var text = new System.Text.StringBuilder("A short reply snapshot.");
            Assert.True(ports.TryPublishAssistantSnapshot(response, text));
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; ++i) ports.TryPublishAssistantSnapshot(response, text);
            report.snapshotBytes = report.allocationCounterSupported ? GC.GetAllocatedBytesForCurrentThread() - start : -1;
            Assert.True(ports.TryRead(out var visible));
            Assert.AreEqual(text.ToString(), visible.Text);
            File.WriteAllText(Path.Combine(root, "measurements.json"), JsonUtility.ToJson(report, true));
            if (report.allocationCounterSupported)
                Assert.AreEqual(0, report.snapshotBytes, "Coalescing a warmed snapshot must not allocate per token.");
        }
        private static void Query(MNNStudioModel model)
        {
            bool value = model.IsPiper;
            value ^= model.IsDualTurn;
            value ^= model.HasAudioProcessingPipeline;
            if (!value) throw new InvalidOperationException("Benchmark fixture changed.");
        }
        private sealed class EmptyCapture : IMNNAudioCaptureSource
        {
            public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
            public int MaximumCaptureSamples => 512;
            public long DroppedCaptureBlocks => 0;
            public bool HasPendingCaptureFrames => false;
            public bool TryReadCapture(float[] target, int offset, out MNNAudioFrameInfo info, out int count)
            { info = default; count = 0; return false; }
        }
        private sealed class IdleRuntime : IMNNRealtimeSessionRuntime
        {
            public System.Threading.Tasks.Task PrepareAsync(System.Threading.CancellationToken token) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task StartListeningAsync(System.Threading.CancellationToken token) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task RequestCancellationAsync(MNNResponseId response) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task WaitForInferenceQuiescenceAsync(MNNResponseId response) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task RestoreContextAsync(MNNResponseId response) => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task StopListeningAsync() => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task WaitForQuiescenceAsync() => System.Threading.Tasks.Task.CompletedTask;
            public System.Threading.Tasks.Task DisposeAsync() => System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
