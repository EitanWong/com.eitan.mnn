using System.Runtime.Serialization;
using MNN.Samples.SiriAI.Harnesses;
using SiriAI.HarnessHost;

#if DOTCRAFT
if (args.Length > 0 && args[0] == "--plugins") return await DotCraftRegistryCli.ExecuteAsync(args);
if (args.Length > 0 && args[0] == "script-worker")
{
    if (args.Length != 2 || args[1] != "code-mode")
    {
        Console.Error.WriteLine("Usage: SiriAI.DotCraftHost script-worker code-mode");
        return 2;
    }
    return await DotCraft.CodeMode.CodeModeWorkerRunner.RunAsync(
        Console.OpenStandardInput(), Console.OpenStandardOutput(), Console.OpenStandardError());
}
#endif

if (args.Length > 0 && args[0] == "--service")
{
    if (args.Length > 1 && args[1] == "run")
    {
        try { args = HostServiceCli.RunArguments(args); }
        catch (Exception error) { Console.Error.WriteLine("Service error: " + error.Message); return 2; }
    }
    else return await HostServiceCli.ExecuteAsync(args);
}
if (args.Length == 1 && args[0] == "--backend-profiles")
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(BackendProfiles.All, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return 0;
}
if (args.Length == 1 && args[0] == "--discover")
{
    Console.WriteLine(System.Text.Encoding.UTF8.GetString(TaskHostJson.Write(BackendDiscovery.Scan())));
    return 0;
}
if (args.Length != 2 || args[0] != "--config")
{
    Console.Error.WriteLine("Usage: SiriAI.HarnessHost --config PATH");
    return 2;
}
string instanceId = Guid.NewGuid().ToString("N");
long startedUtcTicks = DateTime.UtcNow.Ticks;
var configuration = HostConfiguration.Load(Path.GetFullPath(args[1]));
var coordinatorRuns = new CoordinatorMcpRuns(configuration);
var adapters = configuration.Harnesses.Select<HarnessConfiguration, IHarnessAdapter>(item => item.Protocol switch
{
    "acp" => item.CoordinatorScopeId != null ? new CoordinatorHarnessAdapter(item, configuration, coordinatorRuns) :
        item.Backend == "openclaw" ? new OpenClawHarnessAdapter(item, configuration.DataDirectory) : new AcpHarnessAdapter(item, configuration.DataDirectory),
    "http" => new HttpHarnessAdapter(item),
    "a2a" => new A2aHarnessAdapter(item),
    "llm" => new LlmHarnessAdapter(item),
    "msp" => new MspHarnessAdapter(item, configuration.DataDirectory),
#if DOTCRAFT
    "dotcraft" => new DotCraftHarnessAdapter(item, configuration.DataDirectory),
#endif
    _ => throw new NotSupportedException("Backend protocol is not implemented: " + item.Protocol)
}).ToArray();
using var store = new FileTaskStore(configuration.DataDirectory);
using var runtime = new PersistentTaskRuntime(store, adapters, configuration.MaximumConcurrent);
var builder = WebApplication.CreateSlimBuilder();
builder.WebHost.UseKestrelHttpsConfiguration();
builder.WebHost.UseUrls(configuration.Listen);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024;
    options.Limits.MaxRequestLineSize = 65536; // Bounded escaped native pagination cursors.
});
var app = builder.Build();
var readiness = new HarnessReadinessService(configuration, adapters);
using var skills = new SkillLibraryService(configuration);
app.Lifetime.ApplicationStarted.Register(() => coordinatorRuns.Attach(runtime));
var coordinator = new CoordinatorMcpServer(configuration, runtime, coordinatorRuns);
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/mcp/coordinator") { await coordinator.HandleAsync(context); return; }
    string owner = configuration.Authenticate(context.Request.Headers.Authorization.ToString());
    if (owner == null) { context.Response.StatusCode = 401; return; }
    context.Items["owner"] = owner;
    try { await next(context); }
    catch (Exception error) when (!context.Response.HasStarted)
    {
        int status = error is UnauthorizedAccessException ? 404 : error is ArgumentException || error is SerializationException ? 400 :
            error is NotSupportedException ? 422 : error is InvalidOperationException ? 409 : 500;
        context.Response.StatusCode = status;
        await WriteAsync(context, new TaskHostError { Code = "task_operation_failed", Message = status == 500 ? "The task host could not complete the operation." : error.Message });
    }
});
app.MapGet("/v1/health", async context => await WriteAsync(context, new HarnessHostHealth
{
    ProtocolVersion = 1, HostId = configuration.HostId, InstanceId = instanceId, ServiceLabel = HostServiceCli.ActiveLabel,
    ProcessId = Environment.ProcessId, StartedUtcTicks = startedUtcTicks, OwnerId = Owner(context)
}));
app.MapGet("/v1/skills/catalog", async context => await WriteAsync(context, skills.Catalog(Owner(context))));
app.MapGet("/v1/skills/operations", async context => await WriteAsync(context, skills.List(Owner(context))));
app.MapPost("/v1/skills/operations", async context =>
{
    var request = await ReadAsync<HarnessSkillRequest>(context);
    var operation = skills.Submit(Owner(context), request);
    context.Response.StatusCode = 202; await WriteAsync(context, operation);
});
app.MapGet("/v1/skills/operations/{id}", async context => await WriteAsync(context, skills.Get(Owner(context), Id(context))));
app.MapPost("/v1/skills/operations/{id}/cancel", async context => await WriteAsync(context, await skills.CancelAsync(Owner(context), Id(context), context.RequestAborted)));
app.MapGet("/v1/tasks", async context => await WriteAsync(context, runtime.List(Owner(context))));
app.MapGet("/v1/harnesses", async context => await WriteAsync(context, new HarnessHostCatalog
{
    Configured = adapters.Select(adapter => new ConfiguredHarness { Id = adapter.Id, Capabilities = adapter.Capabilities,
        Backend = configuration.Harnesses.First(h => h.Id == adapter.Id).Backend,
        PermissionMode = configuration.Harnesses.First(h => h.Id == adapter.Id).PermissionMode,
        CancellationScope = (adapter as ILocalCancellationHarnessAdapter)?.CancellationScope ??
            (adapter as IHarnessCancellationOutcomeAdapter)?.CancellationScope }).ToArray(),
    Installations = BackendDiscovery.Scan(),
    Workspaces = configuration.Workspaces.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray()
}));
app.MapGet("/v1/harnesses/{harness}/readiness", async context =>
    await WriteAsync(context, readiness.Read(Owner(context), context.Request.RouteValues["harness"]?.ToString(), context.Request.Query["workspace"].ToString())));
app.MapPost("/v1/harnesses/{harness}/readiness", async context =>
{
    var request = await ReadAsync<HarnessReadinessRequest>(context);
    await WriteAsync(context, await readiness.ProbeAsync(Owner(context), context.Request.RouteValues["harness"]?.ToString(), request.WorkspaceKey, context.RequestAborted));
});
app.MapPost("/v1/tasks", async context =>
{
    var request = await ReadAsync<SubmitTaskRequest>(context);
    if (!configuration.Workspaces.TryGetValue(request.WorkspaceKey ?? "", out var workspace)) throw new ArgumentException("Workspace is not configured.");
    var task = runtime.Submit(new TaskSubmission(Owner(context), request.ConversationId, request.SubmissionKey,
        request.HarnessId, workspace, request.Objective, request.Attachments, isolatedContext: request.IsolatedContext));
    context.Response.StatusCode = 202;
    await WriteAsync(context, task);
});
app.MapGet("/v1/tasks/{id}", async context => await WriteAsync(context, runtime.Get(Id(context), Owner(context))));
app.MapGet("/v1/tasks/{id}/native-sessions", async context =>
{
    var task = runtime.Get(Id(context), Owner(context));
    var configured = adapters.FirstOrDefault(item => item.Id == task.HarnessId);
    var adapter = configured is OpenClawHarnessAdapter openClaw ? openClaw.NativeAdapter : configured as AcpHarnessAdapter;
    if (adapter == null) throw new NotSupportedException("This harness does not expose native ACP session listing.");
    string cursor = context.Request.Query.ContainsKey("cursor") ? context.Request.Query["cursor"].ToString() : null;
    await WriteAsync(context, await adapter.ListOwnedSessionsAsync(task, cursor, context.RequestAborted));
});
app.MapPost("/v1/tasks/{id}/native-history/load", async context =>
{
    var task = runtime.Get(Id(context), Owner(context));
    var configured = adapters.FirstOrDefault(item => item.Id == task.HarnessId);
    var adapter = configured is OpenClawHarnessAdapter openClaw ? openClaw.NativeAdapter : configured as AcpHarnessAdapter;
    if (adapter == null) throw new NotSupportedException("This harness does not expose native ACP history loading.");
    await WriteAsync(context, await adapter.LoadOwnedHistoryAsync(task, context.RequestAborted));
});
app.MapPost("/v1/tasks/{id}/cancel", async context =>
{
    await runtime.CancelAsync(Id(context), Owner(context), context.RequestAborted);
    await WriteAsync(context, runtime.Get(Id(context), Owner(context)));
});
app.MapPost("/v1/tasks/{id}/respond", async context =>
{
    var input = await ReadAsync<TaskControlRequest>(context);
    await runtime.RespondAsync(Id(context), Owner(context), input.RequestId, input.Text, input.Allow, context.RequestAborted, input.InputAction);
    await WriteAsync(context, runtime.Get(Id(context), Owner(context)));
});
app.MapPost("/v1/tasks/{id}/update", async context =>
{
    var input = await ReadAsync<TaskControlRequest>(context);
    await runtime.UpdateContentAsync(Id(context), Owner(context), input.Text, input.Attachments, context.RequestAborted, input.RequestId, !input.AdmitOnly);
    await WriteAsync(context, runtime.Get(Id(context), Owner(context)));
});
app.MapPost("/v1/results/claim", async context =>
{
    var input = await ReadAsync<ResultClaimRequest>(context);
    var task = runtime.ClaimResult(Owner(context), input.ConversationId, input.ClientId, TimeSpan.FromSeconds(input.LeaseSeconds));
    if (task == null) { context.Response.StatusCode = 204; return; }
    await WriteAsync(context, task);
});
app.MapPost("/v1/tasks/{id}/receipt", async context =>
{
    var input = await ReadAsync<ResultReceiptRequest>(context);
    runtime.AcknowledgeResult(Id(context), Owner(context), input.LeaseId, input.Receipt);
    await WriteAsync(context, runtime.Get(Id(context), Owner(context)));
});
app.MapPost("/v1/tasks/{id}/release", async context =>
{
    var input = await ReadAsync<ResultReceiptRequest>(context);
    runtime.ReleaseResult(Id(context), Owner(context), input.LeaseId);
    await WriteAsync(context, runtime.Get(Id(context), Owner(context)));
});
app.MapPost("/v1/tasks/{id}/renew", async context =>
{
    var input = await ReadAsync<ResultReceiptRequest>(context);
    runtime.RenewResult(Id(context), Owner(context), input.LeaseId, TimeSpan.FromSeconds(input.LeaseSeconds));
    await WriteAsync(context, runtime.Get(Id(context), Owner(context)));
});
try { await app.RunAsync(); }
finally
{
    runtime.Dispose();
    foreach (var adapter in adapters) if (adapter is IAsyncDisposable disposable) await disposable.DisposeAsync();
}
return 0;

static string Owner(HttpContext context) => (string)context.Items["owner"];
static string Id(HttpContext context) => (string)context.Request.RouteValues["id"];
static async Task<T> ReadAsync<T>(HttpContext context)
{
    using var output = new MemoryStream();
    var buffer = new byte[8192];
    int count;
    while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
    {
        if (output.Length + count > 1024 * 1024) throw new ArgumentException("Request body exceeds its budget.");
        output.Write(buffer, 0, count);
    }
    return TaskHostJson.Read<T>(output.ToArray()) ?? throw new ArgumentException("Request body is required.");
}
static async Task WriteAsync<T>(HttpContext context, T value)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    await context.Response.Body.WriteAsync(TaskHostJson.Write(value), context.RequestAborted);
}
