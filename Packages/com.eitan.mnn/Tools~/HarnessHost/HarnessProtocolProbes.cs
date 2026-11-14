using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal static class HarnessProtocolProbes
{
    internal static async Task<HarnessReadiness> CheckAsync(HarnessConfiguration configuration, IHarnessAdapter adapter,
        string workspace, CancellationToken cancellation)
    {
        switch (configuration.Protocol)
        {
            case "acp":
                await using (var probe = new AcpSession(configuration))
                    return new HarnessReadiness { Evidence = "acp_initialize", AcpCapabilities = await probe.ProbeProtocolAsync(workspace, cancellation) };
            case "msp":
                await using (var probe = new MspConnection(configuration, workspace)) await probe.InitializeAsync(cancellation);
                return new HarnessReadiness { Evidence = "msp_initialize" };
            case "a2a":
                await ((A2aHarnessAdapter)adapter).ProbeProtocolAsync(cancellation);
                return new HarnessReadiness { Evidence = "a2a_agent_card" };
            case "http":
                await ((HttpHarnessAdapter)adapter).ProbeProtocolAsync(cancellation);
                return new HarnessReadiness { Evidence = "remote_host_contract" };
            default:
                // LLM/provider and in-process libraries have no universal non-execution readiness contract.
                return new HarnessReadiness { State = "unsupported", Evidence = "no_non_execution_probe" };
        }
    }
}
