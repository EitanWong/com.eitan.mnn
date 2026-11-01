using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class RealtimeSessionTests
    {
        private static readonly MNNPcmFormat Format = new MNNPcmFormat(16000, 1);
        private sealed class SingleBlockCaptureSource : IMNNAudioCaptureSource
        {
            private readonly MNNAudioCaptureBuffer _buffer = new MNNAudioCaptureBuffer(2, 80);
            public MNNPcmFormat CaptureFormat => Format;
            public int MaximumCaptureSamples => 80;
            public long DroppedCaptureBlocks => _buffer.DroppedBlocks;
            public bool HasPendingCaptureFrames => _buffer.HasPendingFrames;
            internal SingleBlockCaptureSource()
            {
                _buffer.TryWrite(new float[80], 0, 80, new MNNAudioFrameInfo(1, 1, 0, 0, 0, Format, false));
            }

            public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count) => _buffer.TryRead(destination, offset, out info, out count);
        }

        private static void AwaitStopped(Task task)
        {
            Assert.NotNull(task);
            Assert.True(SpinWait.SpinUntil(() => task.IsCompleted, TimeSpan.FromSeconds(3)), "Session operation did not settle within the test bound.");
        }

        private static void AwaitCount(ref int count)
        {
            // ref parameters cannot be captured. Poll a bounded number of reads with a monotonic clock.
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            var spin = new SpinWait();
            while (Volatile.Read(ref count) == 0 && timeout.Elapsed < TimeSpan.FromSeconds(3))
                spin.SpinOnce();
            Assert.Greater(Volatile.Read(ref count), 0, "Runtime did not reach the expected operation.");
        }

        private static MNNRealtimeOutputPipeline NewOutput() => new MNNRealtimeOutputPipeline(Format, framesPerBlock: 160);
        private static TaskCompletionSource<bool> PendingTask() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private sealed class FakeRuntime : IMNNRealtimeSessionRuntime
        {
            private TaskCompletionSource<bool> _startListening;
            private TaskCompletionSource<bool> _quiescence;
            internal readonly List<string> Calls = new List<string>();
            internal TaskCompletionSource<bool> StartListening { get => _startListening; set => _startListening = value; }

            internal Task StopListening { get; set; }

            internal Task InferenceTask { get; set; }

            internal TaskCompletionSource<bool> Quiescence { get => _quiescence; set => _quiescence = value; }

            internal Exception StartListeningError;
            internal Exception RestoreError;
            internal Exception StopError;
            internal Exception QuiescenceError;
            internal bool CancelPendingStart;
            internal Action<CancellationToken> OnPrepare;
            internal Action OnCancel;
            internal Action OnStop;
            internal Action OnDispose;
            internal int CancelCount;
            internal int ListenCount;
            internal int InferenceQuiescenceCount;
            internal int StopCount;
            internal int QuiescenceCount;
            internal int DisposeCount;
            public Task PrepareAsync(CancellationToken cancellationToken)
            {
                Calls.Add("prepare");
                OnPrepare?.Invoke(cancellationToken);
                return Task.CompletedTask;
            }

            public Task StartListeningAsync(CancellationToken cancellationToken)
            {
                Calls.Add("listen");
                if (StartListeningError != null)
                    throw StartListeningError;
                if (CancelPendingStart)
                    cancellationToken.Register(() => StartListening?.TrySetCanceled());
                Interlocked.Increment(ref ListenCount);
                return StartListening?.Task ?? Task.CompletedTask;
            }

            public Task RequestCancellationAsync(MNNResponseId response)
            {
                Calls.Add("cancel");
                OnCancel?.Invoke();
                Interlocked.Increment(ref CancelCount);
                return Task.CompletedTask;
            }

            public Task WaitForInferenceQuiescenceAsync(MNNResponseId response)
            {
                Calls.Add("quiesce");
                Interlocked.Increment(ref InferenceQuiescenceCount);
                return InferenceTask ?? Quiescence?.Task ?? Task.CompletedTask;
            }

            public Task RestoreContextAsync(MNNResponseId interruptedResponse)
            {
                Calls.Add("restore");
                if (RestoreError != null)
                    throw RestoreError;
                return Task.CompletedTask;
            }

            public Task StopListeningAsync()
            {
                Calls.Add("stop");
                OnStop?.Invoke();
                Interlocked.Increment(ref StopCount);
                if (StopError != null)
                    throw StopError;
                return StopListening ?? Task.CompletedTask;
            }

            public Task WaitForQuiescenceAsync()
            {
                Calls.Add("join");
                ++QuiescenceCount;
                if (QuiescenceError != null)
                    throw QuiescenceError;
                return Task.CompletedTask;
            }

            public Task DisposeAsync()
            {
                Calls.Add("dispose");
                OnDispose?.Invoke();
                ++DisposeCount;
                return Task.CompletedTask;
            }

            internal void CompleteStartListening() => StartListening?.TrySetResult(true);
            internal void CompleteQuiescence() => Quiescence?.TrySetResult(true);
        }
    }
}
