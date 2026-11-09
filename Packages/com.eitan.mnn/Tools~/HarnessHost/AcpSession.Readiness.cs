using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpSession
{
    internal async Task<HarnessAcpCapabilities> ProbeProtocolAsync(string workspace, CancellationToken cancellation)
    {
        await StartAsync(workspace, cancellation);
        AcpMcpConfiguration.RequireCapabilities(_configuration, _agentCapabilities);
        if (!IsOpen) throw new IOException("ACP initialization connection closed.");
        return new HarnessAcpCapabilities
        {
            LoadSession = MspWire.Field(_agentCapabilities, "loadSession").ValueKind == JsonValueKind.True,
            ResumeSession = _canResume, ListSessions = _canList,
            Images = MspWire.Field(_promptCapabilities, "image").ValueKind == JsonValueKind.True,
            Audio = MspWire.Field(_promptCapabilities, "audio").ValueKind == JsonValueKind.True,
            EmbeddedContext = MspWire.Field(_promptCapabilities, "embeddedContext").ValueKind == JsonValueKind.True,
            McpHttp = MspWire.Field(MspWire.Field(_agentCapabilities, "mcpCapabilities"), "http").ValueKind == JsonValueKind.True,
            McpSse = MspWire.Field(MspWire.Field(_agentCapabilities, "mcpCapabilities"), "sse").ValueKind == JsonValueKind.True,
            Steering = _steeringExtension
        };
    }
}
