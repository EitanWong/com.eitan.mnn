using System.ComponentModel;
using System.Text.Json;
using DotCraft.Plugins;
using DotCraft.Tools;
using Microsoft.Extensions.AI;

namespace SiriAI.PluginFixture;

public sealed class Plugin : IDotCraftPlugin
{
    public ValueTask ActivateAsync(IPluginActivationContext context, CancellationToken cancellationToken)
    {
        var tools = new ProofTools(context);
        context.Lifetime.Own(tools);
        context.Contributions.Add<IToolSource>(new ProofSource(tools));
        return ValueTask.CompletedTask;
    }
}

internal sealed class ProofSource(ProofTools tools) : AIFunctionToolSource, IToolSource
{
    public override string SourceId => "siriai-plugin-proof";
    async ValueTask<IReadOnlyList<ToolRegistration>> IToolSource.GetRegistrationsAsync(ToolPlanningContext context, CancellationToken cancellationToken)
    {
        try { return await base.GetRegistrationsAsync(context, cancellationToken); }
        catch (Exception error) { tools.RecordError(error); throw; }
    }
    protected override IEnumerable<AIFunction> CreateFunctions(ToolPlanningContext context) =>
    [
        DotCraft.GeneratedTools.DotCraftPlugin.GeneratedToolFunctions.ProofTools_Echo(tools),
        DotCraft.GeneratedTools.DotCraftPlugin.GeneratedToolFunctions.ProofTools_Mutate(tools),
        DotCraft.GeneratedTools.DotCraftPlugin.GeneratedToolFunctions.ProofTools_Hold(tools)
    ];
    protected override string? GetNamespace(AIFunction function, ToolPlanningContext context) => "SiriAIPlugin";
    protected override ToolPolicyHints GetPolicyHints(AIFunction function, ToolPlanningContext context) =>
        new(ReadOnly: function.Name != "Mutate", RequiresApproval: function.Name == "Mutate");
    protected override ToolPresentationDescriptor? GetPresentation(AIFunction function, ToolPlanningContext context) => null;
}

internal sealed class ProofTools : IDisposable
{
    private readonly IPluginActivationContext _context;
    private int _count;
    public ProofTools(IPluginActivationContext context) { _context = context; Record("activated"); }
    private void Record(string action) => File.AppendAllText(Path.Combine(_context.DataRoot, "events.jsonl"),
        JsonSerializer.Serialize(new { action, _context.Plugin.GenerationId, _context.WorkspaceRoot }) + "\n");
    internal void RecordError(Exception error) => File.WriteAllText(Path.Combine(_context.DataRoot, "error.txt"), error.ToString());
    private ProofResponse Result(string text, ToolInvocationContext invocation) => new(
        "SIRIAI_PLUGIN_OK:" + text, Interlocked.Increment(ref _count),
        _context.DataRoot, _context.Plugin.GenerationId, invocation.ThreadId, invocation.TurnId,
        invocation.CallId, invocation.WorkspacePath);
    [GeneratedTool(Name = "Echo")]
    [Description("Read the isolated fixture counter and return the exact input and official invocation identity.")]
    public ProofResponse Echo([Description("Exact input text.")] string text, ToolInvocationContext invocation) => Result(text, invocation);
    [GeneratedTool(Name = "Mutate")]
    [Description("Make a recorded mutation; requires the ordinary native tool permission flow.")]
    public ProofResponse Mutate([Description("Exact input text.")] string text, ToolInvocationContext invocation)
    {
        Record("mutated:" + text);
        return Result(text, invocation);
    }
    [GeneratedTool(Name = "Hold")]
    [Description("Wait until explicitly cancelled, recording actual plugin invocation and cancellation.")]
    public async Task<ProofResponse> Hold([Description("Exact input text.")] string text, ToolInvocationContext invocation, CancellationToken cancellationToken)
    {
        Record("hold:" + text);
        try { await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken); }
        catch (OperationCanceledException) { Record("cancelled:" + text); throw; }
        return Result(text, invocation);
    }
    public void Dispose() => Record("disposed");
}

internal sealed record ProofResponse(string Value, int Count, string DataRoot, string GenerationId, string ThreadId, string? TurnId, string CallId, string? WorkspacePath);
