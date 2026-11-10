using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
namespace MNN.Samples.SiriAI.Harnesses.Tests
{
    public sealed class HarnessBackendCatalogTests
    {
        [Test] public void CatalogContainsEverySupportedOnboardingChoiceExactlyOnce()
        {
            CollectionAssert.AreEquivalent(new[] { "codex", "claude", "deepseek", "pi", "hermes", "openclaw", "qwen", "opencode", "qoder", "minimax", "kimi", "codebuddy", "muse", "llm" }, HarnessBackendCatalog.All.Select(x => x.Id));
            Assert.AreEqual(14, HarnessBackendCatalog.All.Select(x => x.Id).Distinct().Count());
        }
        [Test] public void AutomaticSelectionRequiresBothTheAgentAndItsAdapter()
        {
            var inventory = new[] { new HarnessInstallation { Id = "codex", Installed = true }, new HarnessInstallation { Id = "claude", Installed = true, ProtocolCandidate = true } };
            Assert.AreEqual("claude", HarnessBackendCatalog.ChooseLocal(inventory, "codex"));
            Assert.AreEqual("claude", HarnessBackendCatalog.ChooseLocal(inventory, "claude"));
            Assert.IsNull(HarnessBackendCatalog.ChooseLocal(Array.Empty<HarnessInstallation>()));
        }
        [Test] public void InstallationPreservesAnExistingCliAndOnlyAddsTheMissingAdapter()
        {
            var codex = HarnessBackendCatalog.Get("codex");
            var missing = codex.MissingComponents(new HarnessInstallation { Installed = true });
            Assert.AreEqual(1, missing.Length); Assert.True(missing[0].Adapter);
            Assert.IsEmpty(codex.MissingComponents(new HarnessInstallation { Installed = true, AdapterExecutable = "adapter" }));
        }
        [Test] public void LocalHostScopesAreStableAndIndependentAcrossProjectsAndAgents()
        {
            string project = Path.GetFullPath("TestArtifacts~/ProjectA");
            string first = HarnessLocalSetup.ScopeFor("codex", project);
            Assert.AreEqual(first, HarnessLocalSetup.ScopeFor("codex", Path.Combine(project, ".")));
            Assert.AreNotEqual(first, HarnessLocalSetup.ScopeFor("claude", project));
            Assert.AreNotEqual(first, HarnessLocalSetup.ScopeFor("codex", project + "B"));
        }
        [Test] public void ManagedDiscoveryWorksBeforeAnyHostOrRuntimeIsConfigured()
        {
            string directory = Path.GetFullPath("TestArtifacts~/HarnessDiscovery/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory); string previous = Environment.GetEnvironmentVariable("QWEN_CODE_BIN");
            try
            {
                string command = Path.Combine(directory, "qwen"); File.WriteAllText(command, "fixture executable presence");
                Environment.SetEnvironmentVariable("QWEN_CODE_BIN", command);
                var item = HarnessLocalDiscovery.Scan(new[] { directory }).Single(x => x.Id == "qwen");
                Assert.True(item.Installed); Assert.True(item.ProtocolCandidate);
                Assert.False(item.InteroperabilityVerified); Assert.True(item.RequiresExplicitConfiguration);
                Assert.AreEqual(command, item.Executable);
            }
            finally { Environment.SetEnvironmentVariable("QWEN_CODE_BIN", previous); Directory.Delete(directory, true); }
        }
        [Test] public void UnknownBackendIsRejectedBeforeAnyInstallerCanRun()
        { Assert.Throws<ArgumentException>(() => HarnessBackendCatalog.Get("unknown")); }
    }
}
