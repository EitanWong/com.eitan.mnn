using System;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Tests;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 5 && args[0] == "--service-client")
            {
                var client = new MNN.Samples.SiriAI.Harnesses.DesktopHostServiceClient(args[1], args[2], args[3], false);
                if (args[4] == "discover") Console.WriteLine(System.Text.Encoding.UTF8.GetString(MNN.Samples.SiriAI.Harnesses.TaskHostJson.Write(client.DiscoverAsync().GetAwaiter().GetResult())));
                else Console.WriteLine((args[4] == "prepare" ? client.PrepareAsync("desktop-test", args[5]) : client.ExecuteAsync(args[4])).GetAwaiter().GetResult());
            }
            else if (args.Length == 2 && args[0] == "--remote") CheckRemoteAsync(args[1]).GetAwaiter().GetResult();
            else RunAsync().GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static async Task CheckRemoteAsync(string address)
    {
        using (var client = new MNN.Samples.SiriAI.Harnesses.RemoteTaskClient(new Uri(address),
            Environment.GetEnvironmentVariable("SIRIAI_HOST_CHECK_TOKEN")))
        {
            var tasks = await client.ListAsync(System.Threading.CancellationToken.None);
            if (tasks.Length == 0) throw new InvalidOperationException("Expected a persisted remote task.");
            var item = await client.GetAsync(tasks[0].Id, System.Threading.CancellationToken.None);
            if (item.Id != tasks[0].Id) throw new InvalidOperationException("Remote task identity mismatch.");
            var catalog = await client.GetCatalogAsync(System.Threading.CancellationToken.None);
            if (catalog.Configured == null || catalog.Configured.Length == 0 || catalog.Installations == null || catalog.Workspaces == null)
                throw new InvalidOperationException("Host discovery DTO did not round-trip.");
            Console.WriteLine("PASS Unity-compatible C# remote client round-trip");
        }
    }
    private static async Task RunAsync()
    {
        var skills = new SiriAIHarnessSkillsTests();
        skills.SkillRequestsRequireExplicitNamesAndClosedOperations();
        Console.WriteLine("PASS explicit closed skill management requests preserve literal source arguments");
        await skills.VerifyAsync();
        Console.WriteLine("PASS durable skill admission journal reopens and replays the exact management route and identity");
        var readiness = new SiriAIHarnessReadinessTests();
        readiness.VersionEvidenceSurvivesThePublicJsonContract();
        Console.WriteLine("PASS typed backend version evidence, optional old-Host fields and readiness consistency");
        readiness.VersionContractRejectsMalformedOrContradictoryEvidence();
        Console.WriteLine("PASS standalone backend version contract rejects malformed and contradictory evidence");
        new SiriAIDesktopHostTests().MobileCannotLaunchAHostAndCommandsAreClosed();
        Console.WriteLine("PASS mobile refuses local process launch and lifecycle commands are closed");
        var content = new SiriAIHarnessContentUITests();
        content.DraftCompletionCannotClearNewEditsOrCrossHostScopes();
        Console.WriteLine("PASS attachment draft snapshots and host/revision fences");
        content.ExplicitFileImportAndExportAreBoundedAndNeverEscapeOrOverwrite();
        Console.WriteLine("PASS bounded explicit file import and isolated artifact export");
        content.FeedbackIncludesArtifactTextAndMetadataWithoutBinaryPayloads();
        Console.WriteLine("PASS bounded artifact feedback without binary payloads");
        await new SiriAIHarnessExecutionTests().VerifyAsync();
        Console.WriteLine("PASS ordered tool execution details survive serialization and host restart");
        await new SiriAIHarnessAttachmentTests().VerifyAsync();
        Console.WriteLine("PASS bounded input attachments retain admission identity and survive journal/store restart");
        await new SiriAIHarnessRecoveryTests().ConfirmedRemoteHandleSurvivesHostRestart();
        Console.WriteLine("PASS confirmed remote handle resumes after file-store restart");
        await new SiriAIHarnessRecoveryTests().ChangedBackendCannotReplayPreviousWork();
        Console.WriteLine("PASS changed backend interrupts without replaying previous work");
        await new SiriAIHarnessRecoveryTests().QueuedWorkWithRemovedHarnessDoesNotPreventRestart();
        Console.WriteLine("PASS removed queued harness interrupts without preventing host restart");
        await new SiriAIHarnessCollectionTests().VerifyAsync();
        Console.WriteLine("PASS atomic artifact collection, failed writes, exact provenance, ownership, content hash and recovery");
        await new SiriAIHarnessArtifactTests().VerifyAsync();
        Console.WriteLine("PASS typed artifacts persist and snapshot arrays remain isolated");
        await new SiriAIHarnessDashboardTests().ExternalInputPersistsAndFencesReplies();
        Console.WriteLine("PASS immutable external input persists, accepts empty replies and fences URL handoff by exact request and host scope");
        await new SiriAIHarnessNativeToolStateTests().VerifyAsync();
        Console.WriteLine("PASS native tool state remains detached, bounded, durable and separate from task result, permissions and stale dashboards");
        await new SiriAIHarnessSessionConfigurationTests().VerifyAsync();
        Console.WriteLine("PASS native session configuration snapshots retain exact values, detached arrays, typed booleans and durable permission/task ownership");
        await new SiriAIHarnessStoreBudgetTests().OversizedWritePreservesCommittedResultAndRestart();
        Console.WriteLine("PASS oversized durable store write preserves the prior committed result and restart");
        var tests = new SiriAITaskRuntimeTests();
        var dashboard = new SiriAIHarnessDashboardTests();
        await dashboard.StructuredInputPreservesTypesAndSnapshotOwnership();
        Console.WriteLine("PASS structured input retains typed values, optional absence, persistent detached snapshots and exact request ownership");
        dashboard.HostProfilesAndSelectionSurviveReopenWithoutCredentialFields();
        Console.WriteLine("PASS persistent host profiles and remote-only mobile addresses");
        await dashboard.ProfileGenerationAndExactPromptIdentityFenceOldResponses();
        Console.WriteLine("PASS profile generation and exact permission request identity");
        await tests.ActiveResultDeliveryCanRenewOnlyItsMatchingLease();
        Console.WriteLine("PASS matching delivery lease renewal without false completion");
        await tests.SubmissionReturnsBeforeWorkAndDisconnectDoesNotCancelIt();
        Console.WriteLine("PASS nonblocking admission and independent task lifetime");
        await tests.DuplicateAdmissionIsIdempotentAndOwnerIsEnforced();
        Console.WriteLine("PASS admission idempotency and owner isolation");
        await tests.CompletionIsDurableAndDeliveryRequiresMatchingLease();
        Console.WriteLine("PASS durable result and exclusive delivery lease");
        await tests.CancellationIsNotReportedUntilBackendConfirms();
        Console.WriteLine("PASS backend-confirmed cancellation");
        await tests.LocalCancellationWaitsForObservedOutcomeAndCannotOverwriteCompletion();
        Console.WriteLine("PASS local abort waits for observed result, preserves completion races and survives reopen");
        await tests.AdmissionFailureNeverExecutesAndUnknownBackendIsRejected();
        Console.WriteLine("PASS no execution before durable admission");
        await tests.DifferentLanesCanRunWhileSameWorkspaceRemainsSerial();
        Console.WriteLine("PASS independent lanes and shared-workspace exclusion");
        await tests.FileStoreHasExclusiveWriterAndSurvivesReopen();
        Console.WriteLine("PASS exclusive file store and restart persistence");
        await tests.WaitingForInputUsesExactRequestAndDoesNotFinishWork();
        Console.WriteLine("PASS correlated backend input request");
    }
}
