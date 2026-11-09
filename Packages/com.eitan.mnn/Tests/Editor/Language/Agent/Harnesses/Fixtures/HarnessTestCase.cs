using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests.Fixtures
{
    internal static class HarnessTestCase
    {
        // Unity 2021's bundled NUnit does not accept Task-returning [Test] methods.
        // These cases use no Unity APIs; a worker also avoids blocking Unity's synchronization context.
        internal static void Run(Func<Task> behavior) => Task.Run(behavior).GetAwaiter().GetResult();
        internal static string NewDirectory() => Path.Combine(
            Environment.GetEnvironmentVariable("SIRIAI_HARNESS_TEST_ARTIFACTS") ??
            Path.Combine(TestContext.CurrentContext.WorkDirectory ?? Directory.GetCurrentDirectory(), "TestArtifacts~", "HarnessChecks"),
            Guid.NewGuid().ToString("N"));
    }
}
