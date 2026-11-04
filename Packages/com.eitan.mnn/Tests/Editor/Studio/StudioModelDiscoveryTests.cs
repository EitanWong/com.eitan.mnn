using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class StudioModelDiscoveryTests
    {
        [Test]
        public void RefreshReturnsWhileWorkerIsBlockedAndOwnsItsInputRoots()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                int caller = Thread.CurrentThread.ManagedThreadId, worker = caller;
                using (var discovery = new MNNStudioModelDiscovery((roots, token) =>
                {
                    worker = Thread.CurrentThread.ManagedThreadId;
                    entered.Set();
                    if (!release.Wait(5000)) throw new TimeoutException();
                    return Snapshot(roots[0]);
                }))
                {
                    try
                    {
                        var roots = new[] { "original" };
                        var timer = Stopwatch.StartNew();
                        discovery.Refresh(roots);
                        Assert.Less(timer.ElapsedMilliseconds, 200);
                        Assert.True(entered.Wait(3000));
                        Assert.AreNotEqual(caller, worker);
                        roots[0] = "changed";
                        Assert.False(discovery.TryTake(out _));
                        release.Set();
                        Assert.AreEqual("original", Take(discovery).Models[0].Name);
                        Assert.False(discovery.IsRunning);
                    }
                    finally { release.Set(); }
                }
            }
        }

        [Test]
        public void ReplacedRequestCannotPublishAfterTheLatestSnapshot()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var finished = new ManualResetEventSlim())
            {
                bool cancelled = false;
                using (var discovery = new MNNStudioModelDiscovery((roots, token) =>
                {
                    if (roots[0] == "old")
                    {
                        entered.Set();
                        if (!release.Wait(5000)) throw new TimeoutException();
                        cancelled = token.IsCancellationRequested;
                        finished.Set();
                    }
                    return Snapshot(roots[0]);
                }))
                {
                    try
                    {
                        discovery.Refresh(new[] { "old" });
                        Assert.True(entered.Wait(3000));
                        discovery.Refresh(new[] { "new" });
                        Assert.AreEqual("new", Take(discovery).Models[0].Name);
                        release.Set();
                        Assert.True(finished.Wait(3000));
                        Assert.True(cancelled);
                        Assert.False(discovery.TryTake(out _));
                    }
                    finally { release.Set(); }
                }
            }
        }

        [Test]
        public void DisposeCancelsWithoutWaitingAndSuppressesLateFailure()
        {
            using (var entered = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            using (var finished = new ManualResetEventSlim())
            {
                bool cancelled = false;
                var discovery = new MNNStudioModelDiscovery((roots, token) =>
                {
                    entered.Set();
                    if (!release.Wait(5000)) throw new TimeoutException();
                    cancelled = token.IsCancellationRequested;
                    finished.Set();
                    throw new InvalidOperationException("late disk failure");
                });
                try
                {
                    discovery.Refresh(new[] { "closing" });
                    Assert.True(entered.Wait(3000));
                    var timer = Stopwatch.StartNew();
                    discovery.Dispose();
                    Assert.Less(timer.ElapsedMilliseconds, 200);
                    Assert.False(discovery.IsRunning);
                    release.Set();
                    Assert.True(finished.Wait(3000));
                    Assert.True(cancelled);
                    Assert.False(discovery.TryTake(out _));
                    Assert.Throws<ObjectDisposedException>(() => discovery.Refresh(new[] { "closed" }));
                    discovery.Dispose();
                }
                finally { release.Set(); discovery.Dispose(); }
            }
        }

        [Test]
        public void FailureIsDeliveredAndTheNextRefreshCanRecover()
        {
            using (var discovery = new MNNStudioModelDiscovery((roots, token) =>
            {
                if (roots[0] == "broken") throw new System.IO.IOException("disk unavailable");
                return Snapshot(roots[0]);
            }))
            {
                discovery.Refresh(new[] { "broken" });
                var failed = Take(discovery);
                Assert.IsNull(failed.Models);
                StringAssert.Contains("disk unavailable", failed.Error);
                discovery.Refresh(new[] { "recovered" });
                Assert.AreEqual("recovered", Take(discovery).Models[0].Name);
            }
        }

        private static MNNStudioModelDiscovery.Snapshot Snapshot(string name) =>
            new MNNStudioModelDiscovery.Snapshot(new List<MNNStudioModel>
                { new MNNStudioModel(name, "missing-model", MNNStudioTask.Chat, MNNModelCapabilities.Text) });

        private static MNNStudioModelDiscovery.Snapshot Take(MNNStudioModelDiscovery discovery)
        {
            MNNStudioModelDiscovery.Snapshot snapshot = null;
            Assert.True(SpinWait.SpinUntil(() => discovery.TryTake(out snapshot), 3000), "Worker did not complete.");
            Assert.NotNull(snapshot);
            return snapshot;
        }
    }
}
