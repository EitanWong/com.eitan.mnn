using System.Net;
using System.Net.Http;

namespace MNN.Unity
{
    /// <summary>Status-only failure; provider bodies and credentials are never copied to diagnostics.</summary>
    public sealed class MNNOpenAIHttpException : HttpRequestException
    {
        public HttpStatusCode Status { get; }
        public MNNOpenAIHttpException(HttpStatusCode status)
            : base("OpenAI-compatible provider returned HTTP " + (int)status + ".") { Status = status; }
    }
}
