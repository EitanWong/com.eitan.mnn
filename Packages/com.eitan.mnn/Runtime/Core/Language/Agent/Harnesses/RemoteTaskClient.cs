using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Connection lifetime never controls host work. Reuse submission keys after ambiguous failures.</summary>
    public sealed class RemoteTaskClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly Uri _endpoint;
        private readonly string _token;
        public RemoteTaskClient(Uri endpoint, string token, HttpMessageHandler handler = null)
        {
            _endpoint = HarnessHostAddress.Parse(endpoint?.OriginalString);
            _token = HarnessText.Required(token, 4096);
            _http = handler == null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) : new HttpClient(handler, true);
            _http.Timeout = TimeSpan.FromSeconds(30);
        }
        public Task<HarnessTask> SubmitAsync(SubmitTaskRequest request, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, "v1/tasks", request, cancellation);
        public Task<HarnessTask[]> ListAsync(CancellationToken cancellation) => SendAsync<HarnessTask[]>(HttpMethod.Get, "v1/tasks", null, cancellation);
        public Task<HarnessHostHealth> GetHealthAsync(CancellationToken cancellation) => SendAsync<HarnessHostHealth>(HttpMethod.Get, "v1/health", null, cancellation);
        public Task<HarnessReadiness> GetReadinessAsync(string harnessId, string workspaceKey, CancellationToken cancellation) =>
            SendAsync<HarnessReadiness>(HttpMethod.Get, ReadinessPath(harnessId) + "?workspace=" + Uri.EscapeDataString(HarnessText.Required(workspaceKey, 1024)), null, cancellation);
        public Task<HarnessReadiness> ProbeReadinessAsync(string harnessId, string workspaceKey, CancellationToken cancellation) =>
            SendAsync<HarnessReadiness>(HttpMethod.Post, ReadinessPath(harnessId),
                new HarnessReadinessRequest { WorkspaceKey = HarnessText.Required(workspaceKey, 1024) }, cancellation);
        private static string ReadinessPath(string harnessId) => "v1/harnesses/" + Uri.EscapeDataString(HarnessText.Required(harnessId, 128)) + "/readiness";
        public Task<HarnessSkillCatalog> GetSkillCatalogAsync(CancellationToken cancellation) => SendAsync<HarnessSkillCatalog>(HttpMethod.Get, "v1/skills/catalog", null, cancellation);
        public Task<HarnessTask[]> ListSkillOperationsAsync(CancellationToken cancellation) => SendAsync<HarnessTask[]>(HttpMethod.Get, "v1/skills/operations", null, cancellation);
        public Task<HarnessTask> SubmitSkillOperationAsync(HarnessSkillRequest request, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, "v1/skills/operations", request, cancellation);
        public Task<HarnessTask> GetSkillOperationAsync(string id, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Get, SkillPath(id), null, cancellation);
        public Task<HarnessTask> CancelSkillOperationAsync(string id, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, SkillPath(id) + "/cancel", null, cancellation);
        private static string SkillPath(string id) => "v1/skills/operations/" + Uri.EscapeDataString(HarnessText.Required(id, 128));
        public Task<HarnessHostCatalog> GetCatalogAsync(CancellationToken cancellation) => SendAsync<HarnessHostCatalog>(HttpMethod.Get, "v1/harnesses", null, cancellation);
        public Task<HarnessTask> GetAsync(string taskId, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Get, TaskPath(taskId), null, cancellation);
        public Task<HarnessNativeSessionPage> ListNativeSessionsAsync(string taskId, string cursor, CancellationToken cancellation) =>
            SendAsync<HarnessNativeSessionPage>(HttpMethod.Get, TaskPath(taskId) + "/native-sessions" +
                (cursor == null ? "" : "?cursor=" + Uri.EscapeDataString(HarnessText.Required(cursor, 16384))), null, cancellation);
        public Task<HarnessNativeHistory> LoadNativeHistoryAsync(string taskId, CancellationToken cancellation) =>
            SendAsync<HarnessNativeHistory>(HttpMethod.Post, TaskPath(taskId) + "/native-history/load", null, cancellation);
        public Task<HarnessTask> PauseAsync(string taskId, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, TaskPath(taskId) + "/pause", null, cancellation);
        public Task<HarnessTask> ResumeAsync(string taskId, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, TaskPath(taskId) + "/resume", null, cancellation);
        public Task<HarnessTask> RequestCancelAsync(string taskId, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, TaskPath(taskId) + "/cancel-request", null, cancellation);
        public Task<HarnessTask> CancelAsync(string taskId, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, TaskPath(taskId) + "/cancel", null, cancellation);
        public Task<HarnessTask> RespondAsync(string taskId, TaskControlRequest request, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, TaskPath(taskId) + "/respond", request, cancellation);
        public Task<HarnessTask> UpdateAsync(string taskId, TaskControlRequest request, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, TaskPath(taskId) + "/update", request, cancellation);
        public Task<HarnessTask> ClaimAsync(ResultClaimRequest request, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, "v1/results/claim", request, cancellation);
        public Task<HarnessTask> AcknowledgeAsync(string taskId, ResultReceiptRequest request, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, TaskPath(taskId) + "/receipt", request, cancellation);
        public Task<HarnessTask> ReleaseAsync(string taskId, string leaseId, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, TaskPath(taskId) + "/release", new ResultReceiptRequest { LeaseId = leaseId }, cancellation);
        public Task<HarnessTask> RenewAsync(string taskId, string leaseId, int seconds, CancellationToken cancellation) => SendAsync<HarnessTask>(HttpMethod.Post, TaskPath(taskId) + "/renew", new ResultReceiptRequest { LeaseId = leaseId, LeaseSeconds = seconds }, cancellation);
        private static string TaskPath(string taskId) => "v1/tasks/" + Uri.EscapeDataString(HarnessText.Required(taskId, 128));
        private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, CancellationToken cancellation)
        {
            using (var request = new HttpRequestMessage(method, new Uri(_endpoint, path)))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
                if (body is HarnessSkillRequest skill) request.Content = Content(skill);
                else if (body is HarnessReadinessRequest probe) request.Content = Content(probe);
                else if (body is SubmitTaskRequest submit) request.Content = Content(submit);
                else if (body is TaskControlRequest control) request.Content = Content(control);
                else if (body is ResultClaimRequest claim) request.Content = Content(claim);
                else if (body is ResultReceiptRequest receipt) request.Content = Content(receipt);
                using (var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.NoContent) return default(T);
                    byte[] bytes = await ReadBoundedAsync(response.Content, cancellation).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new TaskHostRequestException((int)response.StatusCode, ReadErrorMessage(bytes));
                    }
                    return TaskHostJson.Read<T>(bytes);
                }
            }
        }
        private static string ReadErrorMessage(byte[] bytes)
        {
            if (bytes.Length != 0)
            {
                try { return TaskHostJson.Read<TaskHostError>(bytes)?.Message ?? "Host request failed."; }
                catch (SerializationException) { return "Host returned a non-protocol error response."; }
            }
            return "Host request failed.";
        }
        private static HttpContent Content<T>(T value)
        {
            var content = new ByteArrayContent(TaskHostJson.Write(value));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return content;
        }
        private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellation)
        {
            if (content.Headers.ContentLength > 1024 * 1024) throw new IOException("Host response exceeds its budget.");
            using (var input = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192];
                int count;
                while ((count = await input.ReadAsync(buffer, 0, buffer.Length, cancellation).ConfigureAwait(false)) != 0)
                {
                    if (output.Length + count > 1024 * 1024) throw new IOException("Host response exceeds its budget.");
                    output.Write(buffer, 0, count);
                }
                return output.ToArray();
            }
        }
        public void Dispose() => _http.Dispose();
    }
}
