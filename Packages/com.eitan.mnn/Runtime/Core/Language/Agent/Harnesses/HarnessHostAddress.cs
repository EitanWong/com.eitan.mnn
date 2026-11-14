using System;

namespace MNN.Samples.SiriAI.Harnesses
{
    public static class HarnessHostAddress
    {
        public static Uri Parse(string value, bool mobile = false)
        {
            Uri endpoint;
            if (!Uri.TryCreate(value, UriKind.Absolute, out endpoint) || !string.IsNullOrEmpty(endpoint.UserInfo) ||
                !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment))
                throw new ArgumentException("Use an absolute host URL without credentials, query or fragment.", nameof(value));
            if (endpoint.Scheme != "https" && !(endpoint.Scheme == "http" && endpoint.IsLoopback))
                throw new ArgumentException("Remote hosts require HTTPS; loopback HTTP is allowed.", nameof(value));
            if (mobile && endpoint.IsLoopback)
                throw new ArgumentException("Mobile clients require a remote host address.", nameof(value));
            return new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/");
        }
    }
}
