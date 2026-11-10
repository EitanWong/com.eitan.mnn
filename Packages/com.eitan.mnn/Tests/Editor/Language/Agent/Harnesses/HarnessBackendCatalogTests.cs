using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
namespace MNN.Samples.SiriAI.Harnesses.Tests
{
    public sealed class HarnessBackendCatalogTests
    {
        [Test] public void RemoteDefaultsKeepSavedChoicesAndHandleRemovedOptions()
        {
            var catalog = new HarnessHostCatalog
            {
                Configured = new[] { new ConfiguredHarness { Id = "text", Backend = "llm" }, new ConfiguredHarness { Id = "work", Backend = "codex" } },
                Workspaces = new[] { "project", "notes" }
            };
            var backend = typeof(HarnessHostCatalog).GetMethod("ChooseBackend");
            var workspace = typeof(HarnessHostCatalog).GetMethod("ChooseWorkspace");
            Assert.NotNull(backend); Assert.NotNull(workspace);
            Assert.AreEqual("work", backend.Invoke(catalog, new object[] { null }));
            Assert.AreEqual("text", backend.Invoke(catalog, new object[] { "text" }));
            Assert.AreEqual("work", backend.Invoke(catalog, new object[] { "removed" }));
            Assert.AreEqual("notes", workspace.Invoke(catalog, new object[] { "notes" }));
            Assert.AreEqual("project", workspace.Invoke(catalog, new object[] { "removed" }));
            Assert.IsNull(backend.Invoke(new HarnessHostCatalog(), new object[] { null }));
            Assert.IsNull(workspace.Invoke(new HarnessHostCatalog(), new object[] { null }));
        }
        [Test] public void ProjectDetectionUsesTheFirstExistingDirectoryWithoutCreatingAnything()
        {
            var find = typeof(HarnessDesktopPaths).GetMethod("FindWorkspace"); Assert.NotNull(find);
            string directory = Path.GetFullPath("TestArtifacts~/WorkspaceDefaults/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                Assert.AreEqual(directory, find.Invoke(null, new object[] { new[] { directory + "/missing", directory, Path.GetTempPath() } }));
                Assert.IsNull(find.Invoke(null, new object[] { new[] { directory + "/missing" } }));
                Assert.False(Directory.Exists(directory + "/missing"));
            }
            finally { Directory.Delete(directory); }
        }
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
