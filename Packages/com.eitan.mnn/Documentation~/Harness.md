# Harness task execution

The reusable Harness implementation is part of `com.eitan.mnn`. Applications
provide conversation, credentials, UI and audio; the package owns task execution,
persistence, remote connections and result delivery.

## Package entry points

| Location | Responsibility |
| --- | --- |
| `Runtime/Core/Language/Agent/Harnesses` | Task contracts, `PersistentTaskRuntime`, `FileTaskStore`, `SubmissionJournal`, `RemoteTaskClient` and host profiles |
| Same Core directory | `HarnessTaskTool` for Submit/Status/Cancel/Update, `HarnessTaskObserver` for reconciliation, `HarnessResultFeedback` for result leases and presentation receipts |
| `Tools~/HarnessCore/MNN.HarnessCore.csproj` | Standalone .NET Standard 2.1 build of the same managed sources |
| `Tools~/HarnessHost` | Independent .NET 8 HTTP/HTTPS host, backend discovery/adapters, readiness, coordinator and service tooling |
| `Tools~/DotCraftHost` | .NET 10 host using the official DotCraft SDK |
| `Tests/Editor/Language/Agent/Harnesses` and `Tools~/HarnessChecks` | Behavior tests, protocol fixtures and package-only consumers |

The public namespace and assembly remain `MNN.Samples.SiriAI.Harnesses` for
compatibility. These names do not imply an application dependency: the Core
assembly has no Unity engine references and does not depend on imported SiriAI
sources. Keep its existing Unity GUIDs and reference this assembly rather than
copying the implementation into an application.

## Connect an application

The following uses the public package API. Supply the host URL, credential,
configured Harness ID and workspace key from application settings. Persist the
submission key before sending and reuse it after an ambiguous network failure.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;

public static class BackgroundTasks
{
    public static async Task<HarnessTask> SubmitAsync(
        string host, string token, bool mobile, string conversationId,
        string submissionKey, string harnessId, string workspaceKey,
        string objective, CancellationToken cancellation)
    {
        Uri address = HarnessHostAddress.Parse(host, mobile);
        using (var client = new RemoteTaskClient(address, token))
        {
            return await client.SubmitAsync(new SubmitTaskRequest
            {
                ConversationId = conversationId,
                SubmissionKey = submissionKey,
                HarnessId = harnessId,
                WorkspaceKey = workspaceKey,
                Objective = objective
            }, cancellation);
        }
    }
}
```

Submission returns the accepted task; completion arrives later. Retain a client
for ongoing observation and result delivery in a real application. Inject the
application's conversation/audio presentation through `IHarnessResultPresenter`
and `IHarnessResultReply`. A completed generation or queued audio is insufficient
to acknowledge presentation: report the actual observed presentation outcome.

Remote hosts require HTTPS. Desktop loopback may use HTTP; mobile clients reject
loopback addresses. Cancelling a local request or disconnecting the foreground
does not cancel accepted Host work. Use the explicit task cancellation operation.
After reconnecting, reconcile the submission journal and claim pending results.
The independent Host must remain running for work to continue.

## Backend and host references

- [Package ownership and application boundary](ProjectStructure.md#harness-核心所有权)
- [A2A transport and optional user context](HarnessA2a.md)
- [Backend version and readiness checks](HarnessBackendVersions.md)
- [Durable skill operations and startup synchronization](HarnessSkills.md)

Codex, Claude Code, OpenClaw, Hermes and DeepSeek Harness profiles require their
corresponding installed CLI or configured remote service. A profile or protocol
fixture is not evidence that a particular vendor has executed a real task.
DotCraftHost additionally requires its SDK and .NET 10 environment.

## Reproduce the managed checks

Run from the repository root with installed Mono, .NET 8 and the development
project's cached NUnit reference:

```bash
python3 Packages/com.eitan.mnn/Tools~/HarnessChecks/run_checks.py
python3 Packages/com.eitan.mnn/Tools~/HarnessChecks/check_package_host.py \
  --source-root . \
  --dotnet /absolute/path/to/dotnet \
  --output TestArtifacts~/SiriAIHarness/PackageHost
python3 Packages/com.eitan.mnn/Tools~/Validation/validate_package.py
```

The package-only Host check builds without SiriAI application sources, executes
an independent ACP fixture after foreground disconnect, restarts the Host and
checks result delivery and the persisted receipt. It verifies the managed task
lifecycle. It does not validate vendor CLIs, physical speech playback, a phone
service, native MNN inference, model quality or performance.

Package inclusion establishes source ownership and reusable managed APIs. Full
device integration, vendor availability and performance/quality acceptance remain
separate application checks.
