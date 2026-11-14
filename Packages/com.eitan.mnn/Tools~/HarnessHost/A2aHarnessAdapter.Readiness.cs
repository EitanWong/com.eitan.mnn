namespace SiriAI.HarnessHost;

internal sealed partial class A2aHarnessAdapter
{
    internal async Task ProbeProtocolAsync(CancellationToken cancellation)
    {
        var card = await RequestAsync(HttpMethod.Get, new Uri(Endpoint, Configuration.AgentCardPath), null, cancellation, "1.0");
        SelectInterface(card, new RemoteCheckpoint());
    }
}
