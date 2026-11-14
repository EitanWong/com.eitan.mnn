using System;
using System.Runtime.Serialization;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessReadinessTests
    {
        [Test] public void VersionEvidenceSurvivesThePublicJsonContract()
        {
            var compatible = new HarnessBackendVersion { Version = "0.21.6", Minimum = "0.21.6", State = "compatible" };
            var readiness = new HarnessReadiness { HarnessId = "qwen", WorkspaceKey = "project", Protocol = "acp",
                State = "protocol_verified", Evidence = "acp_initialize", CheckedUtcTicks = DateTime.UtcNow.Ticks, BackendVersion = compatible };
            var copy = RoundTrip(readiness);
            Assert.AreEqual("0.21.6", copy.BackendVersion.Version);
            Assert.AreEqual("compatible", copy.BackendVersion.State);
            readiness.BackendVersion = null;
            Assert.IsNull(RoundTrip(readiness).BackendVersion, "Old Hosts may omit version evidence.");
            var catalog = RoundTrip(new HarnessHostCatalog { Installations = new[] { new HarnessInstallation { Id = "qwen", MinimumVersion = "0.21.6" } } });
            Assert.AreEqual("0.21.6", catalog.Installations[0].MinimumVersion);
            foreach (var state in new[] { "too_old", "unconfirmed" })
            {
                readiness.State = "unavailable";
                readiness.BackendVersion = new HarnessBackendVersion { Version = state == "too_old" ? "0.21.5" : null, Minimum = "0.21.6", State = state };
                Assert.AreEqual(state, RoundTrip(readiness).BackendVersion.State);
                readiness.State = "protocol_verified";
                Reject(readiness);
            }
        }
        [Test] public void VersionContractRejectsMalformedOrContradictoryEvidence()
        {
            foreach (var invalid in new[]
            {
                new HarnessBackendVersion { Version = "0.21.5", Minimum = "0.21.6", State = "compatible" },
                new HarnessBackendVersion { Version = "1.0.0", Minimum = "0.21.6", State = "too_old" },
                new HarnessBackendVersion { Version = "0.21.6", Minimum = "0.21.6", State = "unconfirmed" },
                new HarnessBackendVersion { Version = "qwen 0.21.6", Minimum = "0.21.6", State = "compatible" },
                new HarnessBackendVersion { Version = "0.21.6", Minimum = "0.21", State = "compatible" },
                new HarnessBackendVersion { Version = "2147483648.0.0", Minimum = "0.21.6", State = "compatible" },
                new HarnessBackendVersion { Version = "-1.0.0", Minimum = "0.21.6", State = "compatible" },
                new HarnessBackendVersion { Version = "0.21.6", Minimum = "0.21.6", State = "unknown" }
            }) Reject(invalid);
        }
        private static void Reject<T>(T value)
        {
            var error = Assert.Catch<Exception>(() => RoundTrip(value), "Invalid evidence must also fail when read independently.");
            Assert.IsInstanceOf<SerializationException>(error.GetBaseException());
        }
        private static T RoundTrip<T>(T value) => TaskHostJson.Read<T>(TaskHostJson.Write(value));
    }
}
