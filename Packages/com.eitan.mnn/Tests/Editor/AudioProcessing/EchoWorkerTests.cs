using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class EchoWorkerTests
    {
        [Test]
        public void StopReturnsImmediatelyAndWaitsForModelWorkBeforeRelease()
        {
            var source = new Source();
            var model = new Model();
            var worker = new MNNEchoCancellationWorker(source, model, 16000, new MNNEchoCancellationOptions { automaticDelay = false, referenceDelayMilliseconds = 0 }, 128);
            try
            {
                source.Submit();
                Assert.True(model.Entered.Wait(2000));
                Task stop = worker.StopAsync();
                Assert.False(stop.IsCompleted);
                Assert.Zero(model.Disposals);
                model.Proceed.Set();
                Assert.True(stop.Wait(3000));
                Assert.AreEqual(1, model.Disposals);
                worker.StopAsync().GetAwaiter().GetResult();
                Assert.AreEqual(1, model.Disposals);
            }
            finally { model.Proceed.Set(); worker.Dispose(); }
        }

        [Test]
        public void FailedWorkerReleaseCanBeRetriedWithoutRunningTheModelAgain()
        {
            var model = new Model { FailDispose = true };
            model.Proceed.Set();
            var worker = new MNNEchoCancellationWorker(new Source(), model, 16000, new MNNEchoCancellationOptions(), 128);
            Assert.Throws<InvalidOperationException>(() => worker.StopAsync().GetAwaiter().GetResult());
            model.FailDispose = false;
            worker.StopAsync().GetAwaiter().GetResult();
            Assert.AreEqual(2, model.Disposals);
        }

        private sealed class Source : IMNNAudioCaptureSource
        {
            private readonly MNNAudioCaptureBuffer _queue = new MNNAudioCaptureBuffer(8, 128);
            public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
            public int MaximumCaptureSamples => 128;
            public long DroppedCaptureBlocks => _queue.DroppedBlocks;
            public bool HasPendingCaptureFrames => _queue.HasPendingFrames;
            internal void Submit() => _queue.TryWrite(new float[128], 0, 128,
                new MNNAudioFrameInfo(1, 1, 0, 0, Stopwatch.GetTimestamp(), CaptureFormat, true));
            public bool TryReadCapture(float[] output, int offset, out MNNAudioFrameInfo info, out int count) => _queue.TryRead(output, offset, out info, out count);
        }
        private sealed class Model : IMNNEchoHopProcessor
        {
            internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
            internal readonly ManualResetEventSlim Proceed = new ManualResetEventSlim();
            internal bool FailDispose;
            internal int Disposals;
            public void Reset() { }
            public void ProcessHop(float[] microphone, float[] reference, float[] output)
            { Entered.Set(); Proceed.Wait(); Array.Copy(microphone, output, 128); }
            public void Dispose() { ++Disposals; if (FailDispose) throw new InvalidOperationException("release failed"); }
        }
    }
}
