using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class MNNModelLoadingTests
    {
        private sealed class Model : IDisposable
        {
            internal bool Disposed;
            public void Dispose() { Disposed = true; }
        }

        [Test]
        public void LoadingUsesWorkerAndTransfersOwnershipToCaller()
        {
            int caller = Thread.CurrentThread.ManagedThreadId;
            int worker = caller;
            var model = MNNModelLoading.LoadAsync(() =>
            {
                worker = Thread.CurrentThread.ManagedThreadId;
                return new Model();
            }).GetAwaiter().GetResult();
            Assert.AreNotEqual(caller, worker);
            Assert.IsFalse(model.Disposed);
            model.Dispose();
        }

        [Test]
        public void CancellationDuringNativeLoadDisposesUnpublishedModel()
        {
            using (var source = new CancellationTokenSource())
            {
                var model = new Model();
                Assert.Throws<OperationCanceledException>(() => MNNModelLoading.LoadAsync(() =>
                {
                    source.Cancel(); // Native loading completed after the request was cancelled.
                    return model;
                }, source.Token).GetAwaiter().GetResult());
                Assert.IsTrue(model.Disposed);
            }
        }

        [Test]
        public void CancelledRequestDoesNotStartNativeLoading()
        {
            bool invoked = false;
            Assert.Throws<TaskCanceledException>(() => MNNModelLoading.LoadAsync(() =>
            {
                invoked = true;
                return new Model();
            }, new CancellationToken(true)).GetAwaiter().GetResult());
            Assert.IsFalse(invoked);
        }
    }
}
