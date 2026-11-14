using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.Serialization;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

// The private test CA is scoped to this client; no system trust or production callback is changed.
string mode = args[0];
var endpoint = new Uri(args[1]);
string token = Environment.GetEnvironmentVariable("MNN_HTTPS_TEST_TOKEN")
    ?? throw new InvalidOperationException("Test token is missing.");
using var authority = X509Certificate2.CreateFromPem(File.ReadAllText(args[2]));
var cancellation = CancellationToken.None;

HttpClientHandler TrustedHandler(X509Certificate2 root)
{
    return new HttpClientHandler
    {
        AllowAutoRedirect = false,
        ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
        {
            if (certificate == null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0)
                return false;
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(certificate);
        }
    };
}

void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

async Task RejectTls(RemoteTaskClient client, string boundary)
{
    using (client)
    {
        try { await client.GetHealthAsync(cancellation); }
        catch (HttpRequestException) { Console.WriteLine("PASS " + boundary); return; }
    }
    throw new InvalidOperationException("TLS boundary was accepted: " + boundary);
}

using var remote = new RemoteTaskClient(endpoint, token, TrustedHandler(authority));
switch (mode)
{
    case "boundaries":
        await RejectTls(new RemoteTaskClient(endpoint, token), "untrusted certificate rejected by the default production client");
        using (var other = X509Certificate2.CreateFromPem(File.ReadAllText(args[3])))
            await RejectTls(new RemoteTaskClient(endpoint, token, TrustedHandler(other)), "wrong CA rejected");
        var wrongName = new UriBuilder(endpoint) { Host = "127.0.0.1" }.Uri;
        await RejectTls(new RemoteTaskClient(wrongName, token, TrustedHandler(authority)), "wrong DNS identity rejected");
        using (var denied = new RemoteTaskClient(endpoint, new string('x', 64), TrustedHandler(authority)))
        {
            bool unauthorized = false;
            try { await denied.GetHealthAsync(cancellation); }
            catch (TaskHostRequestException error) when (error.StatusCode == 401) { unauthorized = true; }
            Require(unauthorized, "TLS must not bypass Host authentication.");
        }
        var health = await remote.GetHealthAsync(cancellation);
        var catalog = await remote.GetCatalogAsync(cancellation);
        Require(health.ProtocolVersion == 1 && catalog.Configured.Any(harness => harness.Id == "acp"),
            "TLS did not reach the actual authenticated package Host.");
        Console.WriteLine("PASS valid TLS reaches the real Host and retains bearer authentication");
        foreach (var error in new[] { (401, ""), (502, "<html>upstream unavailable</html>"),
                     (400, "{\"Code\":\"invalid\",\"Message\":\"Rejected objective\"}") })
        {
            using var failed = new RemoteTaskClient(endpoint, token, new ReplyHandler(error.Item1, error.Item2));
            bool rejected = false;
            try { await failed.GetHealthAsync(cancellation); }
            catch (TaskHostRequestException exception) when (exception.StatusCode == error.Item1)
            {
                Require(error.Item1 != 400 || exception.Message.Contains("Rejected objective"),
                    "A valid protocol error lost its message.");
                rejected = true;
            }
            Require(rejected, "An HTTP failure lost its original status.");
        }
        using (var malformed = new RemoteTaskClient(endpoint, token, new ReplyHandler(200, "not-json")))
        {
            bool rejected = false;
            try { await malformed.GetHealthAsync(cancellation); }
            catch (SerializationException) { rejected = true; }
            Require(rejected, "Malformed successful protocol data must still fail validation.");
        }
        Console.WriteLine("PASS empty and proxy error responses retain HTTP status; successful payloads remain strict");
        foreach (int status in new[] { 401, 502 })
        {
            string directory = Path.Combine(Path.GetDirectoryName(args[2])!, "journal-" + status);
            using (var journal = new SubmissionJournal(directory))
            using (var failed = new RemoteTaskClient(endpoint, token,
                       new ReplyHandler(status, status == 401 ? "" : "<html>proxy failure</html>")))
            {
                bool rejected = false;
                try
                {
                    await journal.SubmitAsync(new SubmitTaskRequest
                    {
                        ConversationId = "https-errors", SubmissionKey = "same-invocation", HarnessId = "acp",
                        WorkspaceKey = "test", Objective = "must retain uncertain admission"
                    }, failed.SubmitAsync, cancellation);
                }
                catch (TaskHostRequestException error) when (status == 401 && error.StatusCode == 401) { rejected = true; }
                catch (PendingSubmissionException) when (status == 502) { rejected = true; }
                Require(rejected, "The submission journal lost the observed HTTP outcome.");
            }
            using var reopened = new SubmissionJournal(directory);
            Require(reopened.PendingCount == (status == 502 ? 1 : 0),
                "Uncertain proxy admission must survive; explicit unauthorized admission must not replay.");
        }
        Console.WriteLine("PASS submission journal preserves uncertain proxy admission and removes explicit unauthorized rejection");
        break;
    case "submit":
        var task = await remote.SubmitAsync(new SubmitTaskRequest
        {
            ConversationId = "https-phone", SubmissionKey = "https-stable", HarnessId = "acp",
            WorkspaceKey = "test", Objective = "slow"
        }, cancellation);
        Require(task.State == HarnessTaskState.Queued || task.State == HarnessTaskState.Running,
            "Independent work must still be pending when the foreground exits.");
        Console.WriteLine(JsonSerializer.Serialize(new { task.Id, State = task.State.ToString() }));
        break;
    case "present":
    case "empty":
        var result = await remote.GetAsync(args[3], cancellation);
        Require(result.State == HarnessTaskState.Completed && result.Result == "fixture result: slow",
            "Durable exact backend result was lost across HTTPS/restart.");
        var request = new ResultClaimRequest
        {
            ConversationId = "https-phone", ClientId = "reconnected-https-phone", LeaseSeconds = 60
        };
        var claim = await remote.ClaimAsync(request, cancellation);
        if (mode == "present")
        {
            Require(claim != null && claim.Id == result.Id && claim.Result == result.Result,
                "The reconnected C# foreground did not receive the exact result.");
            await remote.AcknowledgeAsync(claim.Id, new ResultReceiptRequest
            {
                LeaseId = claim.LeaseId, Receipt = ResultReceipt.TextDisplayed
            }, cancellation);
            Require(await remote.ClaimAsync(request, cancellation) == null, "Text receipt did not consume the delivered result.");
            Console.WriteLine("PASS HTTPS reconnect receives the exact completed result and persists a text receipt");
        }
        else
        {
            Require(claim == null, "The displayed result was redelivered after Host restart.");
            Console.WriteLine("PASS HTTPS Host restart retains both the result and its presentation receipt");
        }
        break;
    default:
        throw new ArgumentException("Unknown HTTPS consumer mode.");
}

sealed class ReplyHandler : HttpMessageHandler
{
    private readonly int _status;
    private readonly string _body;
    internal ReplyHandler(int status, string body) { _status = status; _body = body; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)_status) { Content = new StringContent(_body) });
    }
}
