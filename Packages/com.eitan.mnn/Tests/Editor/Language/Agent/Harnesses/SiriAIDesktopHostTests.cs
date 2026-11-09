using System;
using NUnit.Framework;
using MNN.Samples.SiriAI.Harnesses;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIDesktopHostTests
    {
        [Test]
        public void MobileCannotLaunchAHostAndCommandsAreClosed()
        {
            Assert.Throws<PlatformNotSupportedException>(() => new DesktopHostServiceClient("runtime", "host", "registration", true));
            var client = new DesktopHostServiceClient("runtime", "host", "registration", false);
            Assert.Throws<ArgumentException>(() => client.ExecuteAsync("run").GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(() => client.ExecuteAsync("--config").GetAwaiter().GetResult());
        }
    }
}
