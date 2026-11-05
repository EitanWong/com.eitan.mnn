using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class AgentLifetimeTests
    {
        private sealed class ReleaseProbe : IDisposable
        {
            internal int Failures, Attempts, Successes;
            public void Dispose()
            {
                ++Attempts;
                if (Failures-- > 0) throw new InvalidOperationException("Fixture release failed.");
                ++Successes;
            }
        }

        private sealed class Model : IMNNConversationTextProvider
        {
            internal readonly ReleaseProbe Release = new ReleaseProbe();
            public MNNRealtimeCapabilities Capabilities => new MNNRealtimeCapabilities(
                MNNRealtimeFeature.TextInput, "Managed lifetime fixture", "No native inference.", execution: MNNRealtimeExecution.Managed);
            public Task PrepareAsync(CancellationToken token) => Task.CompletedTask;
            public Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int rate,
                Func<string, Task> delta, CancellationToken token) => delta("fixture reply");
            public void Dispose() => Release.Dispose();
        }

        private static void LoadCloud(MNNAgentProvider provider)
        {
            provider.GenerateAsync(new[] { new MNNOpenAIMessage(MNNChatRole.User, "Give a rigorous proof.") },
                null, 16000, _ => Task.CompletedTask, CancellationToken.None).GetAwaiter().GetResult();
        }

        [TestCase("local")]
        [TestCase("cloud")]
        [TestCase("transport")]
        public void FailedReleaseCanRetryWithoutReleasingSuccessfulChildrenAgain(string failedChild)
        {
            var local = new Model();
            var cloud = new Model();
            var transport = new ReleaseProbe();
            var failure = failedChild == "local" ? local.Release : failedChild == "cloud" ? cloud.Release : transport;
            failure.Failures = 1;
            var provider = new MNNAgentProvider(local, new MNNAgentOptions { AllowCloud = true, Strategy = MNNAgentRoutingStrategy.Policy },
                () => cloud, transport: transport, policy: _ => MNNAgentPlan.Cloud());
            LoadCloud(provider);
            Assert.Catch(provider.Dispose);
            Assert.AreEqual(1, local.Release.Attempts);
            Assert.AreEqual(1, cloud.Release.Attempts);
            Assert.AreEqual(1, transport.Attempts);
            provider.Dispose();
            Assert.AreEqual(2, failure.Attempts, "The failed owner must survive for a later release attempt.");
            foreach (var resource in new[] { local.Release, cloud.Release, transport })
            {
                Assert.AreEqual(1, resource.Successes);
                Assert.AreEqual(ReferenceEquals(resource, failure) ? 2 : 1, resource.Attempts);
            }
            provider.Dispose();
            Assert.AreEqual(2, failure.Attempts, "Completed release remains idempotent.");
        }

        [Test]
        public void EveryReleaseFailureIsReportedAndFailedChildrenRemainOwned()
        {
            var local = new Model();
            var cloud = new Model();
            var transport = new ReleaseProbe { Failures = 1 };
            local.Release.Failures = cloud.Release.Failures = 1;
            var provider = new MNNAgentProvider(local, new MNNAgentOptions { AllowCloud = true, Strategy = MNNAgentRoutingStrategy.Policy },
                () => cloud, transport: transport, policy: _ => MNNAgentPlan.Cloud());
            LoadCloud(provider);
            var failure = Assert.Throws<AggregateException>(provider.Dispose);
            Assert.AreEqual(3, failure.InnerExceptions.Count);
            Assert.Catch<ObjectDisposedException>(() => LoadCloud(provider), "A partially released provider cannot accept another request.");
            Assert.Throws<ObjectDisposedException>(() => provider.PrepareAsync(CancellationToken.None));
            Assert.Throws<ObjectDisposedException>(() => provider.ClassifyVoiceIntentAsync(
                Array.Empty<MNNOpenAIMessage>(), _ => Task.CompletedTask, CancellationToken.None));
            provider.Dispose();
            provider.Dispose();
            foreach (var resource in new[] { local.Release, cloud.Release, transport })
            {
                Assert.AreEqual(2, resource.Attempts);
                Assert.AreEqual(1, resource.Successes);
            }
        }

        [Test]
        public void AliasedOwnedResourcesAreReleasedOnceEvenAfterAReleaseFailure()
        {
            var model = new Model();
            model.Release.Failures = 1;
            var provider = new MNNAgentProvider(model, new MNNAgentOptions { AllowCloud = true, Strategy = MNNAgentRoutingStrategy.Policy },
                () => model, transport: model, policy: _ => MNNAgentPlan.Cloud());
            LoadCloud(provider);
            Assert.Catch(provider.Dispose);
            Assert.AreEqual(1, model.Release.Attempts, "One owner cannot be retried through another role in the same cleanup pass.");
            provider.Dispose();
            provider.Dispose();
            Assert.AreEqual(2, model.Release.Attempts);
            Assert.AreEqual(1, model.Release.Successes);
        }
    }
}
