using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class SharedFreeRegionSolverTests
    {
        [Test]
        public void RepeatedLayoutSolvesDoNotAllocateManagedMemory()
        {
            long probeStart = System.GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[4096]; System.GC.KeepAlive(probe);
            if (System.GC.GetAllocatedBytesForCurrentThread() - probeStart < probe.Length)
                Assert.Ignore("Runtime allocation counter unavailable.");
            var solver = new MNNScreenSpaceFreeRegionSolver();
            var viewport = new Rect(0, 0, 1200, 800);
            var panel = new Rect(650, 20, 530, 760);
            for (int i = 0; i < 10; ++i) { solver.Reset(viewport); solver.Exclude(panel); }
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; ++i) { solver.Reset(viewport); solver.Exclude(panel); }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated, "Steady-state region solving must reuse its buffers.");
        }

        [Test]
        public void BottomAndSidePanelsLeaveBalancedUsableRegions()
        {
            var solver = new MNNScreenSpaceFreeRegionSolver();
            solver.Reset(new Rect(0, 0, 1000, 800));
            solver.Exclude(new Rect(0, 0, 1000, 300), 10);
            Assert.AreEqual(new Rect(0, 310, 1000, 490), solver.LargestRegion);
            solver.Exclude(new Rect(600, 310, 400, 490), 10);
            Assert.AreEqual(new Rect(0, 310, 590, 490), solver.LargestRegion);
            Assert.AreEqual(new Vector2(295, 555), solver.LargestRegion.center);
        }

        [Test]
        public void InvalidEmptyAndOutsideObstaclesDoNotConsumeSpace()
        {
            var solver = new MNNScreenSpaceFreeRegionSolver();
            var viewport = new Rect(20, 30, 100, 200);
            solver.Reset(viewport);
            foreach (var obstacle in new[] { Rect.zero, new Rect(0, 0, -10, 100), new Rect(float.NaN, 0, 100, 100),
                new Rect(0, 0, float.PositiveInfinity, 20), new Rect(300, 400, 50, 50) }) solver.Exclude(obstacle);
            Assert.AreEqual(viewport, solver.LargestRegion);
            solver.Exclude(new Rect(0, 0, 1000, 1000));
            Assert.AreEqual(Vector2.zero, solver.LargestRegion.size);
            Assert.AreEqual(viewport.center, solver.LargestRegion.center);
            solver.Reset(new Rect(float.NaN, 0, 1, 1));
            Assert.AreEqual(Rect.zero, solver.LargestRegion);
            solver.Reset(new Rect(30, 20, 0, 0));
            Assert.AreEqual(new Vector2(30, 20), solver.LargestRegion.center);
        }

        [Test]
        public void OverlappingDuplicateAndEdgeTouchingRegionsRemainStable()
        {
            var solver = new MNNScreenSpaceFreeRegionSolver();
            var viewport = new Rect(10, 10, 1000, 800);
            solver.Reset(viewport);
            solver.Exclude(new Rect(1010, 10, 100, 800));
            Assert.AreEqual(viewport, solver.LargestRegion);
            var obstacle = new Rect(10, 10, 500, 800);
            solver.Exclude(obstacle);
            Rect expected = solver.LargestRegion;
            for (int i = 0; i < 100; ++i) solver.Exclude(obstacle);
            Assert.AreEqual(expected, solver.LargestRegion);
            solver.Exclude(new Rect(0, 0, 200, 200));
            Assert.AreEqual(expected, solver.LargestRegion);
            solver.Reset(viewport);
            Assert.AreEqual(viewport, solver.LargestRegion, "Reset must release previous reservations.");
        }

        [Test]
        public void FragmentedScreensNeverReturnAnOccupiedOrOutOfBoundsRegion()
        {
            var random = new System.Random(731);
            var solver = new MNNScreenSpaceFreeRegionSolver();
            var viewport = new Rect(10, 30, 1000, 800);
            var obstacles = new Rect[160];
            for (int iteration = 0; iteration < 20; ++iteration)
            {
                solver.Reset(viewport);
                for (int i = 0; i < obstacles.Length; ++i)
                {
                    obstacles[i] = new Rect(random.Next(-100, 1100), random.Next(-100, 900), random.Next(1, 100), random.Next(1, 100));
                    solver.Exclude(obstacles[i]);
                }
                Rect free = solver.LargestRegion;
                Assert.GreaterOrEqual(free.xMin, viewport.xMin);
                Assert.GreaterOrEqual(free.yMin, viewport.yMin);
                Assert.LessOrEqual(free.xMax, viewport.xMax);
                Assert.LessOrEqual(free.yMax, viewport.yMax);
                if (free.width > 0 && free.height > 0)
                    foreach (Rect occupied in obstacles) Assert.IsFalse(free.Overlaps(occupied));
            }
        }
    }
}
