using System.Net.Sockets;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpHarnessAdapter
{
    internal static void ValidateStartupWait(HarnessConfiguration configuration)
    {
        int wait = configuration.OpenClawStartupWaitMilliseconds;
        if (wait < 0 || wait > 60000) throw new ArgumentException("OpenClawStartupWaitMilliseconds must be between 0 and 60000.");
        if (wait == 0) return;
        if (configuration.Backend != "openclaw" || configuration.Protocol != "acp" ||
            !Uri.TryCreate(configuration.Endpoint ?? "http://127.0.0.1:18789", UriKind.Absolute, out var endpoint) ||
            !endpoint.IsLoopback || endpoint.Scheme is not ("http" or "https" or "ws" or "wss"))
            throw new ArgumentException("Startup waiting requires an explicitly supervised loopback OpenClaw Gateway. External Gateways connect directly by default.");
    }
    private bool ValidStartupState(AcpCheckpoint state) => !state.WaitingForBackend ||
        _configuration.OpenClawStartupWaitMilliseconds > 0 && state.SessionId == null &&
        state.StartupDeadlineUtcTicks >= 0 && state.StartupDeadlineUtcTicks <= DateTime.MaxValue.Ticks;
    private AcpExecution Execution(HarnessTask task) => _executions.GetOrAdd(task.Id,
        _ => new AcpExecution(_configuration.OpenClawStartupWaitMilliseconds > 0 &&
            (task.BackendState == null || ReadStartupState(task).WaitingForBackend)));
    public string CreateAdmissionCheckpoint(TaskSubmission request) => _configuration.OpenClawStartupWaitMilliseconds == 0 ? null :
        JsonSerializer.Serialize(new AcpCheckpoint
        {
            Fingerprint = _fingerprint,
            ContextKey = MspWire.Hash(new { request.OwnerId, request.ConversationId, request.WorkspaceKey }),
            WaitingForBackend = true
        });
    private AcpCheckpoint ReadStartupState(HarnessTask task)
    {
        var state = JsonSerializer.Deserialize<AcpCheckpoint>(task.BackendState) ?? throw new IOException("Missing ACP checkpoint.");
        if (state.Fingerprint != _fingerprint || state.ContextKey != ContextKey(task) || !ValidStartupState(state))
            throw new IOException("ACP startup checkpoint configuration changed.");
        return state;
    }
    private async Task<HarnessOutcome> AwaitStartupAsync(HarnessTask task, string key, AcpExecution execution,
        Action<HarnessActivity> activity, Action<string> checkpoint, CancellationToken cancellation)
    {
        var state = task.BackendState == null ? new AcpCheckpoint
            { Fingerprint = _fingerprint, ContextKey = key, WaitingForBackend = true } : ReadStartupState(task);
        if (task.State == HarnessTaskState.Cancelling) execution.CancelStartup();
        cancellation.ThrowIfCancellationRequested();
        bool connected = _contexts.TryGetValue(key, out var existing) && existing.IsOpen && existing.SessionId != null;
        if (!connected)
        {
            if (state.StartupDeadlineUtcTicks == 0)
            {
                state.StartupDeadlineUtcTicks = DateTime.UtcNow.AddMilliseconds(_configuration.OpenClawStartupWaitMilliseconds).Ticks;
                checkpoint(JsonSerializer.Serialize(state));
            }
            activity(new HarnessActivity(HarnessWaitingFor.None, null,
                "Waiting for OpenClaw Gateway startup. TCP reachability does not verify the native connection."));
            var endpoint = new Uri(_configuration.Endpoint ?? "http://127.0.0.1:18789");
            if (!await WaitForEndpointAsync(endpoint, new DateTime(state.StartupDeadlineUtcTicks, DateTimeKind.Utc), cancellation))
                return HarnessOutcome.Failed("OpenClaw Gateway did not become reachable within the configured startup wait; no ACP process, native session or prompt was started.");
        }
        if (!execution.BeginNative(() => checkpoint(JsonSerializer.Serialize(new AcpCheckpoint { Fingerprint = _fingerprint, ContextKey = key }))))
            cancellation.ThrowIfCancellationRequested();
        activity(new HarnessActivity(HarnessWaitingFor.None, null, connected
            ? "Continuing the established native ACP conversation."
            : "OpenClaw endpoint is reachable; establishing the native ACP connection."));
        return null;
    }
    private static async Task<bool> WaitForEndpointAsync(Uri endpoint, DateTime deadline, CancellationToken cancellation)
    {
        while (DateTime.UtcNow < deadline)
        {
            cancellation.ThrowIfCancellationRequested();
            using var client = new TcpClient();
            using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            probe.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, Math.Min(300, (deadline - DateTime.UtcNow).TotalMilliseconds))));
            try
            {
                await client.ConnectAsync(endpoint.Host.Trim('[', ']'), endpoint.Port, probe.Token);
                cancellation.ThrowIfCancellationRequested();
                return DateTime.UtcNow < deadline;
            }
            catch (SocketException) { }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { }
            double remaining = (deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining > 0) await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(250, remaining)), cancellation);
        }
        cancellation.ThrowIfCancellationRequested();
        return false;
    }
}
