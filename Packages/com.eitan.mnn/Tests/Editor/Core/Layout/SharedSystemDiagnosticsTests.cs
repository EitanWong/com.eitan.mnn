using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class SharedSystemDiagnosticsTests
    {
        [Test]
        public void FrameWindowUsesUnscaledTimeAndStopsAfterDisposal()
        {
            using (var diagnostics = new MNNSystemDiagnostics())
            {
                double now = Time.realtimeSinceStartupAsDouble;
                for (int i = 0; i < 100; ++i) diagnostics.Tick(.01f, now + i * .01);
                diagnostics.Tick(.01f, now + 1.1);
                StringAssert.Contains("100.0 FPS", diagnostics.Performance);
                Assert.IsNotEmpty(diagnostics.Memory);
                diagnostics.Dispose();
                Assert.IsFalse(diagnostics.Tick(.01f, now + 3));
            }
        }
    }
}
