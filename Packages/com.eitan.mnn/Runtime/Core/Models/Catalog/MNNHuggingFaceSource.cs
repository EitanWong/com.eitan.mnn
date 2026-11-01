using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed class MNNHuggingFaceSource
    {
        public string Name { get; }

        public string ApiRoot { get; }

        public string FileRoot { get; }

        public bool SupportsApi { get; }

        public MNNHuggingFaceSource(string name, string apiRoot, string fileRoot = null, bool supportsApi = true)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A mirror name is required.", nameof(name));
            Name = name.Trim();
            ApiRoot = NormalizeRoot(apiRoot);
            FileRoot = NormalizeRoot(fileRoot ?? apiRoot);
            SupportsApi = supportsApi;
        }

        public string GetFileUrl(string repositoryId, string revision, string path)
        {
            return FileRoot + "/" + Uri.EscapeDataString(repositoryId).Replace("%2F", "/") + "/resolve/" + Uri.EscapeDataString(string.IsNullOrWhiteSpace(revision) ? "main" : revision) + "/" + string.Join("/", (path ?? string.Empty).Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
        }

        private static string NormalizeRoot(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException("Mirror URLs must be absolute HTTPS URLs without credentials.", nameof(value));
            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }
    }
}
